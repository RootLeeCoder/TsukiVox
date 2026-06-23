#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <memory>
#include <mutex>

#include <android/log.h>
#include <oboe/Oboe.h>

namespace
{
    constexpr int32_t TargetSampleRate = 48000;
    constexpr int32_t InputChannelCount = 1;
    constexpr int32_t OutputChannelCount = 2;
    constexpr float MaximumGain = 1.4f;
    constexpr float LimiterCeiling = 0.95f;
    constexpr int32_t NativeSuccess = 1;
    constexpr int32_t NativeFailure = 0;

    struct TsukiVoxNativeStats
    {
        int32_t running;
        int32_t sampleRate;
        int32_t framesPerBurst;
        int32_t inputBufferFrames;
        int32_t outputBufferFrames;
        int32_t inputXRunCount;
        int32_t outputXRunCount;
        int32_t sharingMode;
        float inputLevel;
        float outputLevel;
        uint64_t callbackCount;
    };

    static_assert(sizeof(TsukiVoxNativeStats) == 48, "Stats layout must match C# struct packing.");

    constexpr int32_t MaximumCallbackFrames = 4096;
    std::mutex lastErrorMutex;
    std::array<char, 256> lastError{};

    void StoreLastError(const char* message)
    {
        std::lock_guard<std::mutex> lock(lastErrorMutex);
        std::snprintf(lastError.data(), lastError.size(), "%s", message == nullptr ? "" : message);
    }

    const char* LoadLastError()
    {
        std::lock_guard<std::mutex> lock(lastErrorMutex);
        return lastError.data();
    }

    float ClampGain(float gain)
    {
        if (!std::isfinite(gain))
        {
            return 0.0f;
        }

        return std::clamp(gain, 0.0f, MaximumGain);
    }

    float SoftLimit(float value)
    {
        const auto sign = value < 0.0f ? -1.0f : 1.0f;
        const auto absolute = std::fabs(value);
        if (absolute <= LimiterCeiling)
        {
            return value;
        }

        constexpr auto headroom = 0.999f - LimiterCeiling;
        const auto limited = LimiterCeiling + headroom * std::tanh((absolute - LimiterCeiling) / headroom);
        return sign * std::min(0.999f, limited);
    }

    int32_t ValueOrZero(const oboe::ResultWithValue<int32_t>& result)
    {
        return result ? result.value() : 0;
    }

    class OboeMonitor final : public oboe::AudioStreamDataCallback, public oboe::AudioStreamErrorCallback
    {
    public:
        bool Start(float initialGain, bool initialMuted)
        {
            Stop();

            gain_.store(ClampGain(initialGain), std::memory_order_relaxed);
            muted_.store(initialMuted, std::memory_order_relaxed);
            callbackCount_.store(0, std::memory_order_relaxed);
            inputLevel_.store(0.0f, std::memory_order_relaxed);
            outputLevel_.store(0.0f, std::memory_order_relaxed);

            auto result = OpenStreams(oboe::SharingMode::Exclusive);
            if (result != oboe::Result::OK)
            {
                __android_log_print(ANDROID_LOG_WARN, "TsukiVoxOboe", "Exclusive stream open failed: %s", oboe::convertToText(result));
                CloseStreams();
                result = OpenStreams(oboe::SharingMode::Shared);
            }

            if (result != oboe::Result::OK)
            {
                StoreLastError(oboe::convertToText(result));
                CloseStreams();
                return false;
            }

            result = inputStream_->requestStart();
            if (result != oboe::Result::OK)
            {
                StoreLastError(oboe::convertToText(result));
                CloseStreams();
                return false;
            }

            result = outputStream_->requestStart();
            if (result != oboe::Result::OK)
            {
                StoreLastError(oboe::convertToText(result));
                CloseStreams();
                return false;
            }

            running_.store(true, std::memory_order_release);
            StoreLastError("");
            return true;
        }

        void Stop()
        {
            running_.store(false, std::memory_order_release);
            CloseStreams();
        }

        void SetGain(float gain)
        {
            gain_.store(ClampGain(gain), std::memory_order_relaxed);
        }

        void SetMuted(bool muted)
        {
            muted_.store(muted, std::memory_order_relaxed);
        }

        TsukiVoxNativeStats GetStats() const
        {
            TsukiVoxNativeStats stats{};
            stats.running = running_.load(std::memory_order_acquire) ? 1 : 0;
            stats.sampleRate = outputStream_ == nullptr ? TargetSampleRate : outputStream_->getSampleRate();
            stats.framesPerBurst = outputStream_ == nullptr ? 0 : outputStream_->getFramesPerBurst();
            stats.inputBufferFrames = inputStream_ == nullptr ? 0 : inputStream_->getBufferSizeInFrames();
            stats.outputBufferFrames = outputStream_ == nullptr ? 0 : outputStream_->getBufferSizeInFrames();
            stats.inputXRunCount = inputStream_ == nullptr ? 0 : ValueOrZero(inputStream_->getXRunCount());
            stats.outputXRunCount = outputStream_ == nullptr ? 0 : ValueOrZero(outputStream_->getXRunCount());
            stats.sharingMode = outputStream_ != nullptr && outputStream_->getSharingMode() == oboe::SharingMode::Exclusive ? 1 : 0;
            stats.inputLevel = inputLevel_.load(std::memory_order_relaxed);
            stats.outputLevel = outputLevel_.load(std::memory_order_relaxed);
            stats.callbackCount = callbackCount_.load(std::memory_order_relaxed);
            return stats;
        }

        oboe::DataCallbackResult onAudioReady(
            oboe::AudioStream* audioStream,
            void* audioData,
            int32_t numFrames) override
        {
            (void)audioStream;

            if (audioData == nullptr || numFrames <= 0)
            {
                return oboe::DataCallbackResult::Continue;
            }

            auto* output = static_cast<float*>(audioData);
            std::fill(output, output + (numFrames * OutputChannelCount), 0.0f);

            auto* inputStream = inputStreamRaw_.load(std::memory_order_acquire);
            if (inputStream == nullptr || muted_.load(std::memory_order_relaxed))
            {
                callbackCount_.fetch_add(1, std::memory_order_relaxed);
                inputLevel_.store(inputLevel_.load(std::memory_order_relaxed) * 0.92f, std::memory_order_relaxed);
                outputLevel_.store(outputLevel_.load(std::memory_order_relaxed) * 0.92f, std::memory_order_relaxed);
                return oboe::DataCallbackResult::Continue;
            }

            const auto framesToRead = std::min(numFrames, MaximumCallbackFrames);
            auto readResult = inputStream->read(inputBuffer_.data(), framesToRead, 0);
            if (!readResult)
            {
                callbackCount_.fetch_add(1, std::memory_order_relaxed);
                inputLevel_.store(inputLevel_.load(std::memory_order_relaxed) * 0.92f, std::memory_order_relaxed);
                outputLevel_.store(outputLevel_.load(std::memory_order_relaxed) * 0.92f, std::memory_order_relaxed);
                return oboe::DataCallbackResult::Continue;
            }

            const auto framesRead = readResult.value();
            const auto currentGain = gain_.load(std::memory_order_relaxed);
            auto peakInput = 0.0f;
            auto peakOutput = 0.0f;

            for (int32_t frame = 0; frame < framesRead; frame += 1)
            {
                const auto input = inputBuffer_[frame];
                const auto limited = SoftLimit(input * currentGain);

                peakInput = std::max(peakInput, std::fabs(input));
                peakOutput = std::max(peakOutput, std::fabs(limited));

                const auto outputIndex = frame * OutputChannelCount;
                output[outputIndex] = limited;
                output[outputIndex + 1] = limited;
            }

            const auto previousInput = inputLevel_.load(std::memory_order_relaxed);
            const auto previousOutput = outputLevel_.load(std::memory_order_relaxed);
            inputLevel_.store((previousInput * 0.82f) + (std::min(1.0f, peakInput) * 0.18f), std::memory_order_relaxed);
            outputLevel_.store((previousOutput * 0.82f) + (std::min(1.0f, peakOutput) * 0.18f), std::memory_order_relaxed);
            callbackCount_.fetch_add(1, std::memory_order_relaxed);

            return oboe::DataCallbackResult::Continue;
        }

        bool onError(
            oboe::AudioStream*,
            oboe::Result error) override
        {
            StoreLastError(oboe::convertToText(error));
            running_.store(false, std::memory_order_release);
            return false;
        }

    private:
        oboe::Result OpenStreams(oboe::SharingMode sharingMode)
        {
            oboe::AudioStreamBuilder inputBuilder;
            inputBuilder.setDirection(oboe::Direction::Input)
                ->setPerformanceMode(oboe::PerformanceMode::LowLatency)
                ->setSharingMode(sharingMode)
                ->setFormat(oboe::AudioFormat::Float)
                ->setChannelCount(InputChannelCount)
                ->setSampleRate(TargetSampleRate)
                ->setInputPreset(oboe::InputPreset::VoiceRecognition);

            auto inputResult = inputBuilder.openStream(inputStream_);
            if (inputResult != oboe::Result::OK)
            {
                return inputResult;
            }

            oboe::AudioStreamBuilder outputBuilder;
            outputBuilder.setDirection(oboe::Direction::Output)
                ->setPerformanceMode(oboe::PerformanceMode::LowLatency)
                ->setSharingMode(sharingMode)
                ->setFormat(oboe::AudioFormat::Float)
                ->setChannelCount(OutputChannelCount)
                ->setSampleRate(TargetSampleRate)
                ->setDataCallback(this)
                ->setErrorCallback(this);

            auto outputResult = outputBuilder.openStream(outputStream_);
            if (outputResult != oboe::Result::OK)
            {
                return outputResult;
            }

            const auto inputBurst = std::max(1, inputStream_->getFramesPerBurst());
            const auto outputBurst = std::max(1, outputStream_->getFramesPerBurst());

            inputStream_->setBufferSizeInFrames(inputBurst * 2);
            outputStream_->setBufferSizeInFrames(outputBurst * 2);
            inputStreamRaw_.store(inputStream_.get(), std::memory_order_release);
            return oboe::Result::OK;
        }

        void CloseStreams()
        {
            inputStreamRaw_.store(nullptr, std::memory_order_release);

            if (outputStream_ != nullptr)
            {
                outputStream_->requestStop();
                outputStream_->close();
                outputStream_.reset();
            }

            if (inputStream_ != nullptr)
            {
                inputStream_->requestStop();
                inputStream_->close();
                inputStream_.reset();
            }
        }

        std::shared_ptr<oboe::AudioStream> inputStream_;
        std::shared_ptr<oboe::AudioStream> outputStream_;
        std::atomic<oboe::AudioStream*> inputStreamRaw_{nullptr};
        std::array<float, MaximumCallbackFrames> inputBuffer_{};
        std::atomic<bool> running_{false};
        std::atomic<float> gain_{0.0f};
        std::atomic<bool> muted_{false};
        std::atomic<float> inputLevel_{0.0f};
        std::atomic<float> outputLevel_{0.0f};
        std::atomic<uint64_t> callbackCount_{0};
    };

    OboeMonitor& Monitor()
    {
        static OboeMonitor monitor;
        return monitor;
    }
}

extern "C"
{
    int TsukiVoxAudio_Start(float gain, int muted)
    {
        return Monitor().Start(gain, muted != 0) ? NativeSuccess : NativeFailure;
    }

    void TsukiVoxAudio_Stop()
    {
        Monitor().Stop();
    }

    void TsukiVoxAudio_SetGain(float gain)
    {
        Monitor().SetGain(gain);
    }

    void TsukiVoxAudio_SetMuted(int muted)
    {
        Monitor().SetMuted(muted != 0);
    }

    int TsukiVoxAudio_GetStats(TsukiVoxNativeStats* stats)
    {
        if (stats == nullptr)
        {
            StoreLastError("Stats pointer was null.");
            return NativeFailure;
        }

        *stats = Monitor().GetStats();
        return NativeSuccess;
    }

    const char* TsukiVoxAudio_GetLastError()
    {
        return LoadLastError();
    }
}
