#include "audio_abi.h"
#include "vocal_dsp.h"
#include <algorithm>
#include <android/log.h>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <memory>
#include <oboe/FullDuplexStream.h>
#include <oboe/Oboe.h>
#include <oboe/OboeExtensions.h>

namespace
{
using namespace tsukivox;
constexpr int32_t InputChannels = 1;
constexpr int32_t OutputChannels = 2;
constexpr int32_t MaxCallbackFrames = 4096;
constexpr int NativeSuccess = 1;
constexpr int NativeFailure = 0;
static_assert(std::atomic<float>::is_always_lock_free && std::atomic<uint64_t>::is_always_lock_free, "Audio atomics must be lock-free");
constexpr const char* LogTag = "TsukiVoxNativeAudio";

enum NativeMonitorProfile : int32_t
{
    Production = 0,
    BaselineGenericCushion1 = 1,
    VoiceRecognitionCushion0 = 2,
    VoicePerformanceCushion0 = 3,
    VoicePerformanceCushion1 = 4,
    VoicePerformanceCushion0Output1 = 5,
};

enum NativeFallbackStage : int32_t
{
    NoFallback = 0,
    SafeExclusive = 1,
    SafeShared = 2,
    GenericSafeShared = 3,
    RuntimeSafe = 4,
    ExperimentShared = 5,
    SharedFallback = 6,
    GenericInputFallback = 7,
};

struct ProfileConfig
{
    int32_t resolvedProfile;
    oboe::InputPreset inputPreset;
    int32_t inputBurstsCushion;
    int32_t inputBufferBursts;
    int32_t outputBufferBursts;
};

template <typename T> T ClampFinite(T v, T lo, T hi, T fallback)
{ return std::isfinite(v) ? std::clamp(v, lo, hi) : fallback; }
int32_t ValueOrZero(const oboe::ResultWithValue<int32_t>& r) { return r ? r.value() : 0; }

int32_t ResolveInputPreset(int32_t mode)
{
    switch (mode)
    {
        case 1: case 6: case 9: case 10: return mode;
        default: return 9; // Natural
    }
}

int32_t NormalizeProfile(int32_t profile)
{
    return profile >= Production && profile <= VoicePerformanceCushion0Output1
        ? profile
        : Production;
}

ProfileConfig ResolveProfile(int32_t profile)
{
    switch (NormalizeProfile(profile))
    {
        case BaselineGenericCushion1:
            return {BaselineGenericCushion1, oboe::InputPreset::Generic, 1, 2, 2};
        case VoiceRecognitionCushion0:
            return {VoiceRecognitionCushion0, oboe::InputPreset::VoiceRecognition, 0, 2, 2};
        case VoicePerformanceCushion1:
            return {VoicePerformanceCushion1, oboe::InputPreset::VoicePerformance, 1, 2, 2};
        case VoicePerformanceCushion0Output1:
            return {VoicePerformanceCushion0Output1, oboe::InputPreset::VoicePerformance, 0, 2, 1};
        case Production:
            return {Production, oboe::InputPreset::Unprocessed, 0, 2, 2};
        case VoicePerformanceCushion0:
        default:
            return {VoicePerformanceCushion0, oboe::InputPreset::VoicePerformance, 0, 2, 2};
    }
}

ProfileConfig MakeSafeConfig(const ProfileConfig& source)
{
    auto safe = source;
    safe.inputBurstsCushion = 1;
    safe.inputBufferBursts = 2;
    safe.outputBufferBursts = 2;
    if (safe.inputPreset == oboe::InputPreset::VoicePerformance)
    {
        safe.resolvedProfile = VoicePerformanceCushion1;
    }
    else if (safe.inputPreset == oboe::InputPreset::Generic)
    {
        safe.resolvedProfile = BaselineGenericCushion1;
    }
    return safe;
}

const char* ProfileName(int32_t profile)
{
    switch (profile)
    {
        case Production: return "natural-unprocessed-c0-o2";
        case BaselineGenericCushion1: return "generic-c1-o2";
        case VoiceRecognitionCushion0: return "recognition-c0-o2";
        case VoicePerformanceCushion0: return "performance-c0-o2";
        case VoicePerformanceCushion1: return "performance-c1-o2";
        case VoicePerformanceCushion0Output1: return "performance-c0-o1";
        default: return "unknown";
    }
}

const char* SharingName(oboe::SharingMode sharing)
{
    return sharing == oboe::SharingMode::Exclusive ? "exclusive" : "shared";
}

int64_t NowNanos()
{
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}

class OboeMonitor final : public oboe::FullDuplexStream, public oboe::AudioStreamErrorCallback
{
public:
    bool Start(const TsukiVoxNativeParameters& p)
    {
        Stop(); requestedProfile_ = NormalizeProfile(p.requestedProfile);
        forceSafeConfiguration_ = p.forceSafeConfiguration != 0;
        SetDspParameters(p); ResetStats();
        auto requestedConfig = ResolveProfile(requestedProfile_);
        requestedConfig.inputPreset = static_cast<oboe::InputPreset>(ResolveInputPreset(p.inputProcessingMode));
        const auto config = forceSafeConfiguration_ ? MakeSafeConfig(requestedConfig) : requestedConfig;
        oboe::Result result = TryOpenStreams(config, oboe::SharingMode::Exclusive,
            forceSafeConfiguration_ ? RuntimeSafe : NoFallback);
        if (result != oboe::Result::OK)
            result = TryOpenStreams(config, oboe::SharingMode::Shared,
                forceSafeConfiguration_ ? RuntimeSafe : SharedFallback);
        // Unsupported Natural/other input source: keep the same buffers when trying Generic.
        if (result != oboe::Result::OK && config.inputPreset != oboe::InputPreset::Generic)
        {
            auto generic = config;
            generic.inputPreset = oboe::InputPreset::Generic;
            result = TryOpenStreams(generic, oboe::SharingMode::Exclusive, GenericInputFallback);
            if (result != oboe::Result::OK)
                result = TryOpenStreams(generic, oboe::SharingMode::Shared, GenericInputFallback);
        }
        if (result != oboe::Result::OK && !forceSafeConfiguration_)
        {
            const auto safeConfig = MakeSafeConfig(config);
            result = TryOpenStreams(safeConfig, oboe::SharingMode::Exclusive, SafeExclusive);
            if (result != oboe::Result::OK)
                result = TryOpenStreams(safeConfig, oboe::SharingMode::Shared, SafeShared);
            if (result != oboe::Result::OK)
            {
                auto generic = safeConfig;
                generic.inputPreset = oboe::InputPreset::Generic;
                result = TryOpenStreams(generic, oboe::SharingMode::Shared, GenericSafeShared);
            }
        }

        if (result != oboe::Result::OK) { running_.store(false); SetError(result); CloseOwnedStreams(); return false; }
        dsp_.Prepare(sampleRate_, ReadDspParameters());
        streamStartNanos_ = NowNanos();
        running_.store(true, std::memory_order_release);
        result = oboe::FullDuplexStream::start();
        if (result != oboe::Result::OK) { running_.store(false); SetError(result); CloseOwnedStreams(); return false; }
        __android_log_print(
            ANDROID_LOG_INFO,
            LogTag,
            "event=stream_started requested=%s resolved=%s fallback=%d api=%d preset=%d sharing=%s/%s "
            "mmap=%d/%d burst=%d/%d buffer=%d/%d cushion=%d",
            ProfileName(requestedProfile_), ProfileName(resolvedProfile_), fallbackStage_,
            static_cast<int32_t>(outputStream_->getAudioApi()), inputPreset_,
            SharingName(inputStream_->getSharingMode()), SharingName(outputStream_->getSharingMode()),
            IsMMapUsed(inputStream_.get()) ? 1 : 0, IsMMapUsed(outputStream_.get()) ? 1 : 0,
            inputFramesPerBurst_, outputFramesPerBurst_, inputStream_->getBufferSizeInFrames(),
            outputStream_->getBufferSizeInFrames(), inputBurstsCushion_);
        return running_.load(std::memory_order_acquire);
    }
    void Stop()
    {
        running_.store(false, std::memory_order_release);
        oboe::FullDuplexStream::stop(); CloseOwnedStreams(); dsp_.Reset();
    }
    bool SetParameters(const TsukiVoxNativeParameters& p)
    {
        if (NormalizeProfile(p.requestedProfile) != requestedProfile_ ||
            (p.forceSafeConfiguration != 0) != forceSafeConfiguration_ ||
            p.inputProcessingMode != inputProcessingMode_.load()) return false;
        SetDspParameters(p); return true;
    }
    void SetDspParameters(const TsukiVoxNativeParameters& p)
    {
        // This path reaches the Quest speakers without Unity distance attenuation.
        gain_.store(ClampFinite(p.gain, 0.0f, 1.0f, 0.65f), std::memory_order_relaxed);
        inputDrive_.store(ClampFinite(p.inputDrive, 1.0f, 6.0f, 1.0f), std::memory_order_relaxed);
        distanceGain_.store(ClampFinite(p.distanceGain, 0.0f, 1.0f, 1.0f), std::memory_order_relaxed);
        safetyGain_.store(ClampFinite(p.safetyGain, 0.0f, 1.0f, 1.0f), std::memory_order_relaxed);
        ambience_.store(ClampFinite(p.ambience, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        echo_.store(ClampFinite(p.echo, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        dynamics_.store(ClampFinite(p.dynamics, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        inputProcessingMode_.store(p.inputProcessingMode, std::memory_order_relaxed);
        dspPreset_.store(std::clamp(p.dspPreset, 0, 3), std::memory_order_relaxed);
        dryGain_.store(ClampFinite(p.dryGain, 0.0f, 1.25f, 1.0f), std::memory_order_relaxed);
        reverbSend_.store(ClampFinite(p.reverbSend, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        reverbPreDelayMs_.store(ClampRoomPreDelay(p.reverbPreDelayMs), std::memory_order_relaxed);
        reverbDecay_.store(ClampRoomDecay(p.reverbDecay), std::memory_order_relaxed);
        reverbWidth_.store(ClampFinite(p.reverbWidth, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        echoDelayMs_.store(ClampFinite(p.echoDelayMs, 45.0f, 120.0f, 48.0f), std::memory_order_relaxed);
        echoFeedback_.store(ClampFinite(p.echoFeedback, 0.0f, 0.6f, 0.0f), std::memory_order_relaxed);
        echoWet_.store(ClampFinite(p.echoWet, 0.0f, 0.4f, 0.0f), std::memory_order_relaxed);
        doublingAmount_.store(ClampFinite(p.doublingAmount, 0.0f, 0.25f, 0.0f), std::memory_order_relaxed);
        highPassHz_.store(ClampFinite(p.highPassHz, 70.0f, 100.0f, 80.0f), std::memory_order_relaxed);
        muted_.store(p.muted != 0, std::memory_order_relaxed);
    }
    TsukiVoxNativeStats GetStats()
    {
        TsukiVoxNativeStats s{}; s.size = sizeof(s); s.version = ApiVersion;
        s.running = running_.load(std::memory_order_acquire) ? 1 : 0;
        s.requestedProfile = requestedProfile_; s.resolvedProfile = resolvedProfile_; s.fallbackStage = fallbackStage_;
        s.sampleRate = sampleRate_;
        s.audioApi = outputStream_ ? static_cast<int32_t>(outputStream_->getAudioApi()) : 0;
        s.inputPerformanceMode = inputStream_ ? static_cast<int32_t>(inputStream_->getPerformanceMode()) : 0;
        s.outputPerformanceMode = outputStream_ ? static_cast<int32_t>(outputStream_->getPerformanceMode()) : 0;
        s.inputSharingMode = inputStream_ && inputStream_->getSharingMode() == oboe::SharingMode::Exclusive ? 1 : 0;
        s.outputSharingMode = outputStream_ && outputStream_->getSharingMode() == oboe::SharingMode::Exclusive ? 1 : 0;
        s.inputMMapUsed = IsMMapUsed(inputStream_.get()) ? 1 : 0;
        s.outputMMapUsed = IsMMapUsed(outputStream_.get()) ? 1 : 0;
        s.inputPreset = inputPreset_;
        s.inputFramesPerBurst = inputFramesPerBurst_; s.outputFramesPerBurst = outputFramesPerBurst_;
        s.inputChannelCount = inputStream_ ? inputStream_->getChannelCount() : 0;
        s.outputChannelCount = outputStream_ ? outputStream_->getChannelCount() : 0;
        s.inputFormat = inputStream_ ? static_cast<int32_t>(inputStream_->getFormat()) : 0;
        s.outputFormat = outputStream_ ? static_cast<int32_t>(outputStream_->getFormat()) : 0;
        s.inputCapacityFrames = inputStream_ ? inputStream_->getBufferCapacityInFrames() : inputCapacityFrames_;
        s.outputCapacityFrames = outputStream_ ? outputStream_->getBufferCapacityInFrames() : outputCapacityFrames_;
        s.requestedInputBufferFrames = requestedInputBufferFrames_; s.requestedOutputBufferFrames = requestedOutputBufferFrames_;
        s.inputBufferFrames = inputStream_ ? inputStream_->getBufferSizeInFrames() : 0;
        s.outputBufferFrames = outputStream_ ? outputStream_->getBufferSizeInFrames() : 0;
        s.inputBurstsCushion = inputBurstsCushion_;
        s.lastCallbackFrames = lastCallbackFrames_.load();
        const auto minimum = minCallbackFrames_.load();
        s.minCallbackFrames = minimum == MaxCallbackFrames ? 0 : minimum;
        s.maxCallbackFrames = maxCallbackFrames_.load();
        s.inputXRunSupported = inputStream_ && inputStream_->isXRunCountSupported() ? 1 : 0;
        s.outputXRunSupported = outputStream_ && outputStream_->isXRunCountSupported() ? 1 : 0;
        s.inputXRunCount = inputStream_ ? ValueOrZero(inputStream_->getXRunCount()) : 0;
        s.outputXRunCount = outputStream_ ? ValueOrZero(outputStream_->getXRunCount()) : 0;
        s.lastStreamError = lastStreamError_.load(std::memory_order_acquire);
        s.latencyErrorCount = latencyErrorCount_.load(); s.fallbackReason = fallbackStage_;
        s.inputProcessingMode = inputProcessingMode_.load();
        s.dspPreset = dspPreset_.load();
        s.dspVersion = VocalDsp::Version;
        s.callbackOverrunCount = callbackOverrunCount_.load();
        s.actualGain = actualGain_.load(); s.actualInputDrive = actualInputDrive_.load();
        s.actualDistanceGain = actualDistanceGain_.load(); s.actualSafetyGain = actualSafetyGain_.load();
        s.preDspLevel = preDspLevel_.load(); s.postDspLevel = postDspLevel_.load(); s.outputLevel = outputLevel_.load();
        s.compressorReductionDb = compressorReductionDb_.load(); s.limiterReductionDb = limiterReductionDb_.load();
        s.dspAlgorithmLatencyMs = VocalDsp::AlgorithmLatencyMs;
        s.callbackCpuLoad = callbackCpuLoad_.load();
        s.callbackMaxMs = callbackMaxMs_.load();
        s.callbackAverageMs = callbackAverageMs_.load();
        s.dryGain = actualDryGain_.load();
        s.wetGain = actualWetGain_.load();
        s.callbackCount = callbackCount_.load(); s.shortReadCount = shortReadCount_.load(); s.frameMismatchCount = frameMismatchCount_.load();
        s.requestedInputFrameCount = requestedInputFrameCount_.load(); s.receivedInputFrameCount = receivedInputFrameCount_.load();
        s.startToFirstInputMs = startToFirstInputMs_.load();
        UpdateLatencyEstimate(s);
        return s;
    }

    int32_t GetLastStreamError() const { return lastStreamError_.load(std::memory_order_acquire); }

    oboe::DataCallbackResult onAudioReady(oboe::AudioStream* stream, void* data, int32_t frames) override
    {
        const auto started = NowNanos();
        const auto result = oboe::FullDuplexStream::onAudioReady(stream, data, frames);
        if (result == oboe::DataCallbackResult::Stop)
        {
            if (lastStreamError_.load() == 0)
                lastStreamError_.store(lastReadError_ != 0 ? lastReadError_ : static_cast<int32_t>(oboe::Result::ErrorInternal));
            running_.store(false);
        }
        RecordCallbackTiming(started, frames);
        return result;
    }

    oboe::ResultWithValue<int32_t> readInput(int32_t frames) override
    {
        const auto result = oboe::FullDuplexStream::readInput(frames);
        lastReadError_ = result ? 0 : static_cast<int32_t>(result.error());
        return result;
    }

    oboe::DataCallbackResult onBothStreamsReady(const void* inputData, int32_t inFrames, void* outputData, int32_t outFrames) override
    {
        auto* out = static_cast<float*>(outputData);
        const auto* in = static_cast<const float*>(inputData);
        if (!out || outFrames <= 0) return oboe::DataCallbackResult::Continue;
        lastCallbackFrames_.store(outFrames);
        UpdateMinimum(minCallbackFrames_, outFrames); UpdateMaximum(maxCallbackFrames_, outFrames);
        callbackCount_.fetch_add(1); requestedInputFrameCount_.fetch_add(static_cast<uint64_t>(outFrames));
        const int available = in ? std::clamp(inFrames, 0, outFrames) : 0;
        receivedInputFrameCount_.fetch_add(static_cast<uint64_t>(available));
        if (available < outFrames) shortReadCount_.fetch_add(1);
        if (inFrames != outFrames) frameMismatchCount_.fetch_add(1);
        if (available > 0 && startToFirstInputMs_.load() < 0)
            startToFirstInputMs_.store(static_cast<float>((NowNanos() - streamStartNanos_) / 1000000.0));
        const auto meters = dsp_.Process(in, available, out, outFrames, ReadDspParameters());
        SmoothMeter(preDspLevel_, meters.pre); SmoothMeter(postDspLevel_, meters.post);
        SmoothMeter(outputLevel_, meters.output);
        compressorReductionDb_.store(meters.compressionDb); limiterReductionDb_.store(meters.limiterDb);
        const auto& smooth = dsp_.Smoothed();
        actualGain_.store(smooth.gain); actualInputDrive_.store(smooth.inputDrive);
        actualDistanceGain_.store(smooth.distanceGain); actualSafetyGain_.store(smooth.safetyGain);
        actualDryGain_.store(dsp_.DryGain()); actualWetGain_.store(dsp_.WetGain());
        return oboe::DataCallbackResult::Continue;
    }

    bool onError(oboe::AudioStream*, oboe::Result error) override
    {
        // Unity's main thread owns stop/close/reopen; Oboe must not close underneath GetStats.
        SetError(error); running_.store(false); return true;
    }

private:
    static bool IsMMapUsed(oboe::AudioStream* stream)
    {
        return stream != nullptr && stream->getAudioApi() == oboe::AudioApi::AAudio &&
               oboe::OboeExtensions::isMMapUsed(stream);
    }

    static void UpdateMinimum(std::atomic<int32_t>& target, int32_t value)
    {
        auto current = target.load(std::memory_order_relaxed);
        while (value < current && !target.compare_exchange_weak(current, value, std::memory_order_relaxed)) {}
    }

    static void UpdateMaximum(std::atomic<int32_t>& target, int32_t value)
    {
        auto current = target.load(std::memory_order_relaxed);
        while (value > current && !target.compare_exchange_weak(current, value, std::memory_order_relaxed)) {}
    }

    void RecordCallbackTiming(int64_t callbackStartNanos, int32_t frames)
    {
        if (callbackStartNanos <= 0 || frames <= 0 || sampleRate_ <= 0)
        {
            return;
        }
        const float elapsedMs = static_cast<float>((NowNanos() - callbackStartNanos) / 1000000.0);
        const float budgetMs = static_cast<float>(frames) * 1000.0f / static_cast<float>(sampleRate_);
        const float load = budgetMs > 0.0f ? elapsedMs / budgetMs : 0.0f;
        callbackCpuLoad_.store(callbackCpuLoad_.load(std::memory_order_relaxed) * 0.9f + load * 0.1f, std::memory_order_relaxed);
        auto previousMax = callbackMaxMs_.load(std::memory_order_relaxed);
        while (elapsedMs > previousMax && !callbackMaxMs_.compare_exchange_weak(previousMax, elapsedMs, std::memory_order_relaxed)) {}
        timingCount_ += 1;
        timingTotalMs_ += elapsedMs;
        callbackAverageMs_.store(static_cast<float>(timingTotalMs_ / timingCount_));
        if (load > 1.0f)
        {
            callbackOverrunCount_.fetch_add(1, std::memory_order_relaxed);
        }
    }

    void UpdateLatencyEstimate(TsukiVoxNativeStats& stats)
    {
        stats.inputLatencyMs = -1.0f; stats.outputLatencyMs = -1.0f; stats.roundTripLatencyMs = -1.0f;
        if (!inputStream_ || !outputStream_ || !running_.load(std::memory_order_acquire)) return;
        const auto inputLatency = inputStream_->calculateLatencyMillis();
        const auto outputLatency = outputStream_->calculateLatencyMillis();
        if (!inputLatency || !outputLatency || !std::isfinite(inputLatency.value()) || !std::isfinite(outputLatency.value()) ||
            inputLatency.value() < 0.0 || outputLatency.value() < 0.0)
        {
            latencyErrorCount_.fetch_add(1); stats.latencyErrorCount = latencyErrorCount_.load(); return;
        }
        stats.inputLatencyMs = static_cast<float>(inputLatency.value());
        stats.outputLatencyMs = static_cast<float>(outputLatency.value());
        stats.roundTripLatencyMs = stats.inputLatencyMs + stats.outputLatencyMs;
        stats.latencyValid = 1;
    }

    oboe::Result TryOpenStreams(const ProfileConfig& config, oboe::SharingMode sharing, int32_t fallbackStage)
    {
        CloseOwnedStreams();
        __android_log_print(
            ANDROID_LOG_INFO,
            LogTag,
            "event=stream_open_attempt requested=%s resolved=%s fallback=%d preset=%d sharing=%s "
            "inputBursts=%d outputBursts=%d cushion=%d",
            ProfileName(requestedProfile_), ProfileName(config.resolvedProfile), fallbackStage,
            static_cast<int32_t>(config.inputPreset), SharingName(sharing), config.inputBufferBursts,
            config.outputBufferBursts, config.inputBurstsCushion);
        const auto result = OpenStreams(config, sharing);
        if (result == oboe::Result::OK)
        {
            resolvedProfile_ = config.resolvedProfile;
            fallbackStage_ = inputPreset_ != static_cast<int32_t>(config.inputPreset) ? GenericInputFallback : fallbackStage;
        }
        else
        {
            __android_log_print(
                ANDROID_LOG_WARN,
                LogTag,
                "event=stream_open_failed requested=%s resolved=%s fallback=%d sharing=%s error=%d text=%s",
                ProfileName(requestedProfile_), ProfileName(config.resolvedProfile), fallbackStage,
                SharingName(sharing), static_cast<int32_t>(result), oboe::convertToText(result));
        }
        return result;
    }

    oboe::Result OpenStreams(const ProfileConfig& config, oboe::SharingMode sharing)
    {
        oboe::AudioStreamBuilder output;
        output.setDirection(oboe::Direction::Output)->setPerformanceMode(oboe::PerformanceMode::LowLatency)->setSharingMode(sharing)
            ->setAudioApi(oboe::AudioApi::AAudio)->setSampleRate(48000)
            ->setFormat(oboe::AudioFormat::Float)->setChannelCount(OutputChannels)->setUsage(oboe::Usage::Media)
            ->setContentType(oboe::ContentType::Music)->setDataCallback(this)->setErrorCallback(this);
        auto result = output.openStream(outputStream_); if (result != oboe::Result::OK) return result;
        if (outputStream_->getSampleRate() != 48000 || outputStream_->getFormat() != oboe::AudioFormat::Float ||
            outputStream_->getChannelCount() != OutputChannels) return oboe::Result::ErrorInvalidFormat;
        sampleRate_ = outputStream_->getSampleRate(); outputFramesPerBurst_ = std::max(1, outputStream_->getFramesPerBurst());
        outputCapacityFrames_ = outputStream_->getBufferCapacityInFrames();
        requestedOutputBufferFrames_ = outputFramesPerBurst_ * std::max(1, config.outputBufferBursts);
        const auto outputSize = outputStream_->setBufferSizeInFrames(requestedOutputBufferFrames_);
        if (!outputSize)
        {
            __android_log_print(ANDROID_LOG_WARN, LogTag, "event=output_buffer_request_failed requested=%d error=%d",
                requestedOutputBufferFrames_, static_cast<int32_t>(outputSize.error()));
        }
        const int requestedInputCapacity = std::max(outputFramesPerBurst_ * std::max(2, config.inputBufferBursts), outputCapacityFrames_ * 2);
        oboe::AudioStreamBuilder input;
        input.setDirection(oboe::Direction::Input)->setPerformanceMode(oboe::PerformanceMode::LowLatency)->setSharingMode(sharing)
            ->setAudioApi(oboe::AudioApi::AAudio)
            ->setFormat(oboe::AudioFormat::Float)->setChannelCount(InputChannels)->setSampleRate(sampleRate_)
            ->setBufferCapacityInFrames(requestedInputCapacity)->setInputPreset(config.inputPreset);
        result = input.openStream(inputStream_); if (result != oboe::Result::OK) return result;
        if (inputStream_->getSampleRate() != sampleRate_ || inputStream_->getFormat() != oboe::AudioFormat::Float ||
            inputStream_->getChannelCount() != InputChannels) return oboe::Result::ErrorInvalidFormat;
        inputPreset_ = static_cast<int32_t>(inputStream_->getInputPreset());
        inputFramesPerBurst_ = std::max(1, inputStream_->getFramesPerBurst());
        inputCapacityFrames_ = inputStream_->getBufferCapacityInFrames();
        requestedInputBufferFrames_ = inputFramesPerBurst_ * std::max(1, config.inputBufferBursts);
        const auto inputSize = inputStream_->setBufferSizeInFrames(requestedInputBufferFrames_);
        if (!inputSize)
        {
            __android_log_print(ANDROID_LOG_WARN, LogTag, "event=input_buffer_request_failed requested=%d error=%d",
                requestedInputBufferFrames_, static_cast<int32_t>(inputSize.error()));
        }
        inputBurstsCushion_ = std::max(0, config.inputBurstsCushion);
        setInputStream(inputStream_.get()); setOutputStream(outputStream_.get());
        setNumInputBurstsCushion(inputBurstsCushion_); return oboe::Result::OK;
    }
    TsukiVoxNativeParameters ReadDspParameters() const
    {
        TsukiVoxNativeParameters p{};
        p.gain = gain_.load(std::memory_order_relaxed);
        p.inputDrive = inputDrive_.load(std::memory_order_relaxed);
        p.distanceGain = distanceGain_.load(std::memory_order_relaxed);
        p.safetyGain = safetyGain_.load(std::memory_order_relaxed);
        p.ambience = ambience_.load(std::memory_order_relaxed);
        p.echo = echo_.load(std::memory_order_relaxed);
        p.dynamics = dynamics_.load(std::memory_order_relaxed);
        p.highPassHz = highPassHz_.load(std::memory_order_relaxed);
        p.dryGain = dryGain_.load(std::memory_order_relaxed);
        p.reverbSend = reverbSend_.load(std::memory_order_relaxed);
        p.reverbPreDelayMs = reverbPreDelayMs_.load(std::memory_order_relaxed);
        p.reverbDecay = reverbDecay_.load(std::memory_order_relaxed);
        p.reverbWidth = reverbWidth_.load(std::memory_order_relaxed);
        p.echoDelayMs = echoDelayMs_.load(std::memory_order_relaxed);
        p.echoFeedback = echoFeedback_.load(std::memory_order_relaxed);
        p.echoWet = echoWet_.load(std::memory_order_relaxed);
        p.doublingAmount = doublingAmount_.load(std::memory_order_relaxed);
        p.dspPreset = dspPreset_.load(std::memory_order_relaxed);
        p.inputProcessingMode = inputProcessingMode_.load(std::memory_order_relaxed);
        p.muted = muted_.load(std::memory_order_relaxed);
        return p;
    }
    static void SmoothMeter(std::atomic<float>& meter, float peak)
    {
        meter.store(meter.load() * 0.82f + std::min(8.0f, peak) * 0.18f);
    }
    void ResetStats()
    {
        callbackCount_.store(0); shortReadCount_.store(0); frameMismatchCount_.store(0); requestedInputFrameCount_.store(0); receivedInputFrameCount_.store(0);
        preDspLevel_.store(0); postDspLevel_.store(0); outputLevel_.store(0); compressorReductionDb_.store(0); limiterReductionDb_.store(0); lastStreamError_.store(0);
        lastCallbackFrames_.store(0); minCallbackFrames_.store(MaxCallbackFrames); maxCallbackFrames_.store(0);
        lastReadError_ = 0;
        latencyErrorCount_.store(0); startToFirstInputMs_.store(-1.0f); streamStartNanos_ = 0;
        callbackOverrunCount_.store(0); callbackCpuLoad_.store(0.0f); callbackMaxMs_.store(0.0f); callbackAverageMs_.store(0.0f); timingCount_ = 0; timingTotalMs_ = 0; actualDryGain_.store(1.0f); actualWetGain_.store(0.0f);
    }
    void CloseOwnedStreams()
    {
        // Close/join output callbacks before releasing the input or DSP state.
        if (outputStream_) { outputStream_->requestStop(); outputStream_->close(); outputStream_.reset(); }
        if (inputStream_) { inputStream_->requestStop(); inputStream_->close(); inputStream_.reset(); }
        setInputStream(nullptr); setOutputStream(nullptr);
    }

    void SetError(oboe::Result r) { lastStreamError_.store(static_cast<int32_t>(r), std::memory_order_release); }

    std::shared_ptr<oboe::AudioStream> inputStream_, outputStream_;
    int32_t requestedProfile_ = Production, resolvedProfile_ = VoicePerformanceCushion0, fallbackStage_ = NoFallback;
    bool forceSafeConfiguration_ = false;
    int32_t sampleRate_ = 48000, inputFramesPerBurst_ = 0, outputFramesPerBurst_ = 0, inputPreset_ = 0;
    int32_t inputCapacityFrames_ = 0, outputCapacityFrames_ = 0, inputBurstsCushion_ = 0;
    int32_t requestedInputBufferFrames_ = 0, requestedOutputBufferFrames_ = 0;
    int64_t streamStartNanos_ = 0;
    VocalDsp dsp_;
    int32_t lastReadError_ = 0;
    uint64_t timingCount_ = 0;
    double timingTotalMs_ = 0;
    std::atomic<float> gain_{1}, inputDrive_{1}, distanceGain_{1}, safetyGain_{1}, ambience_{0}, echo_{0}, dynamics_{0}, highPassHz_{90};
    std::atomic<float> dryGain_{1}, reverbSend_{0}, reverbPreDelayMs_{0}, reverbDecay_{0.9f}, reverbWidth_{0}, echoDelayMs_{48}, echoFeedback_{0}, echoWet_{0}, doublingAmount_{0};
    std::atomic<int32_t> inputProcessingMode_{9}, dspPreset_{1};
    std::atomic<bool> muted_{false}, running_{false};
    std::atomic<float> actualGain_{1}, actualInputDrive_{1}, actualDistanceGain_{1}, actualSafetyGain_{1}, preDspLevel_{0}, postDspLevel_{0}, outputLevel_{0}, compressorReductionDb_{0}, limiterReductionDb_{0};
    std::atomic<float> actualDryGain_{1}, actualWetGain_{0}, callbackAverageMs_{0}, callbackCpuLoad_{0}, callbackMaxMs_{0};
    std::atomic<float> startToFirstInputMs_{-1.0f};
    std::atomic<int32_t> lastStreamError_{0}, latencyErrorCount_{0}, callbackOverrunCount_{0};
    std::atomic<int32_t> lastCallbackFrames_{0}, minCallbackFrames_{MaxCallbackFrames}, maxCallbackFrames_{0};
    std::atomic<uint64_t> callbackCount_{0}, shortReadCount_{0}, frameMismatchCount_{0}, requestedInputFrameCount_{0}, receivedInputFrameCount_{0};
};

OboeMonitor& Monitor() { static OboeMonitor monitor; return monitor; }
bool CopyParameters(const TsukiVoxNativeParameters* source, uint32_t size, TsukiVoxNativeParameters& destination)
{
    if (!ValidHeader(source, size, sizeof(TsukiVoxNativeParameters))) return false;
    destination = {}; std::memcpy(&destination, source, sizeof(destination)); destination.size = sizeof(destination); return true;
}
}

extern "C"
{
int TsukiVoxAudio_GetApiVersion() { return ApiVersion; }
int TsukiVoxAudio_StartV5(const TsukiVoxNativeParameters* p, uint32_t size) { TsukiVoxNativeParameters safe{}; return CopyParameters(p, size, safe) && Monitor().Start(safe) ? NativeSuccess : NativeFailure; }
int TsukiVoxAudio_SetParametersV5(const TsukiVoxNativeParameters* p, uint32_t size) { TsukiVoxNativeParameters safe{}; return CopyParameters(p, size, safe) && Monitor().SetParameters(safe) ? NativeSuccess : NativeFailure; }
void TsukiVoxAudio_Stop() { Monitor().Stop(); }
int TsukiVoxAudio_GetStatsV5(void* destination, uint32_t size)
{
    if (!ValidHeader(destination, size, sizeof(TsukiVoxNativeStats))) return NativeFailure;
    const auto stats = Monitor().GetStats();
    std::memcpy(destination, &stats, sizeof(stats)); return NativeSuccess;
}
const char* TsukiVoxAudio_GetLastError()
{
    static thread_local std::array<char, 96> text{}; const auto code = Monitor().GetLastStreamError();
    const char* message = code == 0 ? "" : oboe::convertToText(static_cast<oboe::Result>(code)); std::strncpy(text.data(), message, text.size() - 1); text.back() = '\0'; return text.data();
}
}
