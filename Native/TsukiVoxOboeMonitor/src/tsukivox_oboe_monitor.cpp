#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <memory>
#include <oboe/FullDuplexStream.h>
#include <oboe/Oboe.h>

namespace
{
constexpr int32_t ApiVersion = 3;
constexpr int32_t InputChannels = 1;
constexpr int32_t OutputChannels = 2;
constexpr int32_t MaxCallbackFrames = 4096;
constexpr float Pi = 3.14159265358979323846f;
constexpr float Epsilon = 1.0e-12f;
constexpr int NativeSuccess = 1;
constexpr int NativeFailure = 0;

struct TsukiVoxNativeParameters
{
    uint32_t size; uint32_t version;
    float gain; float inputDrive; float distanceGain; float safetyGain;
    float ambience; float echo; float dynamics;
    int32_t muted; float highPassHz;
};

struct TsukiVoxNativeStats
{
    uint32_t size; uint32_t version;
    int32_t running; int32_t sampleRate; int32_t audioApi;
    int32_t inputSharingMode; int32_t outputSharingMode; int32_t inputPreset;
    int32_t framesPerBurst; int32_t inputCapacityFrames; int32_t outputCapacityFrames;
    int32_t requestedInputBufferFrames; int32_t requestedOutputBufferFrames;
    int32_t inputBufferFrames; int32_t outputBufferFrames;
    int32_t inputXRunCount; int32_t outputXRunCount; int32_t lastStreamError;
    float actualGain; float actualInputDrive; float actualDistanceGain; float actualSafetyGain;
    float preDspLevel; float postDspLevel; float outputLevel;
    float compressorReductionDb; float limiterReductionDb;
    uint64_t callbackCount; uint64_t shortReadCount; uint64_t frameMismatchCount;
    uint64_t requestedInputFrameCount; uint64_t receivedInputFrameCount;
};
static_assert(sizeof(TsukiVoxNativeParameters) == 44, "Parameter ABI changed");
static_assert(sizeof(TsukiVoxNativeStats) == 152, "Stats ABI changed");

template <typename T> T ClampFinite(T v, T lo, T hi, T fallback)
{ return std::isfinite(v) ? std::clamp(v, lo, hi) : fallback; }
inline float Lerp(float a, float b, float t) { return a + (b - a) * t; }
inline float Sanitize(float v) { return std::isfinite(v) && std::fabs(v) > Epsilon ? v : 0.0f; }
int32_t ValueOrZero(const oboe::ResultWithValue<int32_t>& r) { return r ? r.value() : 0; }

template <size_t Capacity> class Delay
{
public:
    void Reset() { data_.fill(0.0f); write_ = 0; filter_ = 0.0f; }
    float Read(int frames) const
    {
        const int d = std::clamp(frames, 1, static_cast<int>(Capacity) - 1);
        return data_[(write_ + Capacity - static_cast<size_t>(d)) % Capacity];
    }
    void Write(float value) { data_[write_] = Sanitize(value); write_ = (write_ + 1) % Capacity; }
    float ProcessComb(float input, int frames, float feedback, float damping)
    {
        const float delayed = Read(frames);
        filter_ += (delayed - filter_) * (1.0f - damping);
        filter_ = Sanitize(filter_);
        Write(input + filter_ * feedback);
        return delayed;
    }
    float ProcessAllpass(float input, int frames, float feedback)
    {
        const float delayed = Read(frames);
        const float output = delayed - input;
        Write(input + delayed * feedback);
        return output;
    }
private:
    std::array<float, Capacity> data_{};
    size_t write_ = 0;
    float filter_ = 0.0f;
};

class OboeMonitor final : public oboe::FullDuplexStream, public oboe::AudioStreamErrorCallback
{
public:
    bool Start(const TsukiVoxNativeParameters& p)
    {
        Stop(); SetParameters(p); ResetDsp(); ResetStats();
        auto result = OpenStreams(oboe::SharingMode::Exclusive);
        if (result != oboe::Result::OK) { CloseOwnedStreams(); result = OpenStreams(oboe::SharingMode::Shared); }
        if (result != oboe::Result::OK) { SetError(result); CloseOwnedStreams(); return false; }
        result = oboe::FullDuplexStream::start();
        if (result != oboe::Result::OK) { SetError(result); CloseOwnedStreams(); return false; }
        running_.store(true, std::memory_order_release); return true;
    }
    void Stop()
    {
        running_.store(false, std::memory_order_release);
        oboe::FullDuplexStream::stop(); CloseOwnedStreams();
    }
    void SetParameters(const TsukiVoxNativeParameters& p)
    {
        gain_.store(ClampFinite(p.gain, 0.0f, 3.0f, 1.0f), std::memory_order_relaxed);
        inputDrive_.store(ClampFinite(p.inputDrive, 1.0f, 7.2f, 1.0f), std::memory_order_relaxed);
        distanceGain_.store(ClampFinite(p.distanceGain, 0.0f, 1.0f, 1.0f), std::memory_order_relaxed);
        safetyGain_.store(ClampFinite(p.safetyGain, 0.0f, 1.0f, 1.0f), std::memory_order_relaxed);
        ambience_.store(ClampFinite(p.ambience, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        echo_.store(ClampFinite(p.echo, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        dynamics_.store(ClampFinite(p.dynamics, 0.0f, 1.0f, 0.0f), std::memory_order_relaxed);
        highPassHz_.store(ClampFinite(p.highPassHz, 50.0f, 180.0f, 90.0f), std::memory_order_relaxed);
        muted_.store(p.muted != 0, std::memory_order_relaxed);
    }
    TsukiVoxNativeStats GetStats() const
    {
        TsukiVoxNativeStats s{}; s.size = sizeof(s); s.version = ApiVersion;
        s.running = running_.load(std::memory_order_acquire) ? 1 : 0; s.sampleRate = sampleRate_;
        s.audioApi = outputStream_ ? static_cast<int32_t>(outputStream_->getAudioApi()) : 0;
        s.inputSharingMode = inputStream_ && inputStream_->getSharingMode() == oboe::SharingMode::Exclusive ? 1 : 0;
        s.outputSharingMode = outputStream_ && outputStream_->getSharingMode() == oboe::SharingMode::Exclusive ? 1 : 0;
        s.inputPreset = inputPreset_; s.framesPerBurst = framesPerBurst_;
        s.inputCapacityFrames = inputStream_ ? inputStream_->getBufferCapacityInFrames() : inputCapacityFrames_;
        s.outputCapacityFrames = outputStream_ ? outputStream_->getBufferCapacityInFrames() : outputCapacityFrames_;
        s.requestedInputBufferFrames = requestedInputBufferFrames_; s.requestedOutputBufferFrames = requestedOutputBufferFrames_;
        s.inputBufferFrames = inputStream_ ? inputStream_->getBufferSizeInFrames() : 0;
        s.outputBufferFrames = outputStream_ ? outputStream_->getBufferSizeInFrames() : 0;
        s.inputXRunCount = inputStream_ ? ValueOrZero(inputStream_->getXRunCount()) : 0;
        s.outputXRunCount = outputStream_ ? ValueOrZero(outputStream_->getXRunCount()) : 0;
        s.lastStreamError = lastStreamError_.load(std::memory_order_acquire);
        s.actualGain = actualGain_.load(); s.actualInputDrive = actualInputDrive_.load();
        s.actualDistanceGain = actualDistanceGain_.load(); s.actualSafetyGain = actualSafetyGain_.load();
        s.preDspLevel = preDspLevel_.load(); s.postDspLevel = postDspLevel_.load(); s.outputLevel = outputLevel_.load();
        s.compressorReductionDb = compressorReductionDb_.load(); s.limiterReductionDb = limiterReductionDb_.load();
        s.callbackCount = callbackCount_.load(); s.shortReadCount = shortReadCount_.load(); s.frameMismatchCount = frameMismatchCount_.load();
        s.requestedInputFrameCount = requestedInputFrameCount_.load(); s.receivedInputFrameCount = receivedInputFrameCount_.load();
        return s;
    }

    oboe::DataCallbackResult onBothStreamsReady(const void* inputData, int32_t inFrames, void* outputData, int32_t outFrames) override
    {
        auto* out = static_cast<float*>(outputData); const auto* in = static_cast<const float*>(inputData);
        if (!out || outFrames <= 0) return oboe::DataCallbackResult::Continue;
        std::fill(out, out + static_cast<size_t>(outFrames) * OutputChannels, 0.0f);
        callbackCount_.fetch_add(1); requestedInputFrameCount_.fetch_add(static_cast<uint64_t>(outFrames));
        const int available = std::max(0, inFrames); receivedInputFrameCount_.fetch_add(static_cast<uint64_t>(std::min(available, outFrames)));
        if (inFrames < outFrames) shortReadCount_.fetch_add(1);
        if (inFrames != outFrames) frameMismatchCount_.fetch_add(1);
        if (!in || muted_.load()) { DecayMeters(); return oboe::DataCallbackResult::Continue; }
        const int count = std::min({available, outFrames, MaxCallbackFrames});
        const bool shortRead = count < outFrames;
        const float parameterSmooth = 1.0f - std::exp(-1.0f / (0.025f * std::max(1, sampleRate_)));
        float prePeak = 0.0f, postPeak = 0.0f, outputPeak = 0.0f, compReduction = 0.0f, limitReduction = 0.0f;
        for (int i = 0; i < count; ++i)
        {
            SmoothParameters(parameterSmooth);
            const float source = Sanitize(in[i]);
            const float cutoff = highPassHz_.load(); const float dt = 1.0f / std::max(1, sampleRate_);
            const float rc = 1.0f / (2.0f * Pi * cutoff); const float alpha = rc / (rc + dt);
            const float hp = Sanitize(alpha * (hpOutput_ + source - hpInput_)); hpInput_ = source; hpOutput_ = hp;
            prePeak = std::max(prePeak, std::fabs(hp));
            const float driven = Sanitize(hp * smoothGain_ * smoothInputDrive_);
            const float absDriven = std::fabs(driven);
            const float attack = 1.0f - std::exp(-1.0f / (0.006f * sampleRate_));
            const float release = 1.0f - std::exp(-1.0f / (0.120f * sampleRate_));
            envelope_ += (absDriven - envelope_) * (absDriven > envelope_ ? attack : release); envelope_ = Sanitize(envelope_);
            const float thresholdDb = Lerp(-10.0f, -31.0f, smoothDynamics_);
            const float ratio = Lerp(1.1f, 6.2f, smoothDynamics_);
            const float envDb = 20.0f * std::log10(std::max(envelope_, 1.0e-7f));
            const float over = envDb - thresholdDb; const float knee = 6.0f; float reductionDb = 0.0f;
            if (over > knee * 0.5f) reductionDb = over * (1.0f - 1.0f / ratio);
            else if (over > -knee * 0.5f) { const float x = over + knee * 0.5f; reductionDb = (1.0f - 1.0f / ratio) * x * x / (2.0f * knee); }
            const float compGain = std::pow(10.0f, -reductionDb / 20.0f);
            const float makeup = Lerp(1.0f, 3.8f, smoothDynamics_);
            const float dry = Sanitize(driven * compGain * makeup); postPeak = std::max(postPeak, std::fabs(dry)); compReduction = std::max(compReduction, reductionDb);
            float wetL, wetR; ProcessReverb(dry, wetL, wetR);
            const float postGain = smoothDistanceGain_ * smoothSafetyGain_;
            float mixedL = Sanitize((dry + wetL) * postGain); float mixedR = Sanitize((dry + wetR) * postGain);
            if (shortRead && count - i <= 32) { const float fade = static_cast<float>(count - i) / std::min(32, count); mixedL *= fade; mixedR *= fade; }
            const float ceiling = Lerp(0.96f, 0.90f, smoothDynamics_);
            const float limitedL = SoftLimit(mixedL, ceiling); const float limitedR = SoftLimit(mixedR, ceiling);
            const float mixedPeak = std::max(std::fabs(mixedL), std::fabs(mixedR)); const float limitedPeak = std::max(std::fabs(limitedL), std::fabs(limitedR));
            if (mixedPeak > 1.0e-7f) limitReduction = std::max(limitReduction, -20.0f * std::log10(std::max(limitedPeak / mixedPeak, 1.0e-7f)));
            outputPeak = std::max(outputPeak, limitedPeak); out[i * 2] = limitedL; out[i * 2 + 1] = limitedR;
        }
        SmoothMeter(preDspLevel_, prePeak); SmoothMeter(postDspLevel_, postPeak); SmoothMeter(outputLevel_, outputPeak);
        compressorReductionDb_.store(compReduction); limiterReductionDb_.store(limitReduction);
        actualGain_.store(smoothGain_); actualInputDrive_.store(smoothInputDrive_); actualDistanceGain_.store(smoothDistanceGain_); actualSafetyGain_.store(smoothSafetyGain_);
        return oboe::DataCallbackResult::Continue;
    }
    bool onError(oboe::AudioStream*, oboe::Result error) override { SetError(error); running_.store(false); return false; }

private:
    oboe::Result OpenStreams(oboe::SharingMode sharing)
    {
        oboe::AudioStreamBuilder output;
        output.setDirection(oboe::Direction::Output)->setPerformanceMode(oboe::PerformanceMode::LowLatency)->setSharingMode(sharing)
            ->setFormat(oboe::AudioFormat::Float)->setChannelCount(OutputChannels)->setUsage(oboe::Usage::Media)
            ->setContentType(oboe::ContentType::Music)->setDataCallback(this)->setErrorCallback(this);
        auto result = output.openStream(outputStream_); if (result != oboe::Result::OK) return result;
        sampleRate_ = outputStream_->getSampleRate(); framesPerBurst_ = std::max(1, outputStream_->getFramesPerBurst());
        outputCapacityFrames_ = outputStream_->getBufferCapacityInFrames(); requestedOutputBufferFrames_ = framesPerBurst_ * 2;
        const auto outputSize = outputStream_->setBufferSizeInFrames(requestedOutputBufferFrames_);
        if (outputSize) requestedOutputBufferFrames_ = outputSize.value();
        const int requestedInputCapacity = std::max(framesPerBurst_ * 2, outputCapacityFrames_ * 2);
        oboe::AudioStreamBuilder input;
        input.setDirection(oboe::Direction::Input)->setPerformanceMode(oboe::PerformanceMode::LowLatency)->setSharingMode(sharing)
            ->setFormat(oboe::AudioFormat::Float)->setChannelCount(InputChannels)->setSampleRate(sampleRate_)
            ->setBufferCapacityInFrames(requestedInputCapacity)->setInputPreset(oboe::InputPreset::Generic);
        result = input.openStream(inputStream_); if (result != oboe::Result::OK) return result;
        inputPreset_ = static_cast<int32_t>(oboe::InputPreset::Generic); inputCapacityFrames_ = inputStream_->getBufferCapacityInFrames();
        requestedInputBufferFrames_ = std::max(1, inputStream_->getFramesPerBurst()) * 2;
        const auto inputSize = inputStream_->setBufferSizeInFrames(requestedInputBufferFrames_);
        if (inputSize) requestedInputBufferFrames_ = inputSize.value();
        setInputStream(inputStream_.get()); setOutputStream(outputStream_.get()); setNumInputBurstsCushion(1); return oboe::Result::OK;
    }
    void ProcessReverb(float dry, float& wetL, float& wetR)
    {
        const int sr = std::max(1, sampleRate_); const float a = smoothAmbience_; const float e = smoothEcho_;
        early_.Write(dry); const float earlyL = early_.Read(static_cast<int>(sr * 0.013f)) * 0.42f + early_.Read(static_cast<int>(sr * 0.029f)) * 0.24f;
        const float earlyR = early_.Read(static_cast<int>(sr * 0.019f)) * 0.38f + early_.Read(static_cast<int>(sr * 0.037f)) * 0.26f;
        echoDelay_.Write(dry); const int echoFrames = static_cast<int>(sr * Lerp(0.052f, 0.118f, e)); const float echoTap = echoDelay_.Read(echoFrames);
        const float feedback = std::min(0.78f, Lerp(0.48f, 0.76f, a)); const float damping = Lerp(0.42f, 0.26f, a);
        const float excitation = dry * (0.18f + 0.20f * a);
        float revL = combL_[0].ProcessComb(excitation, static_cast<int>(sr * 0.0297f), feedback, damping)
                   + combL_[1].ProcessComb(excitation, static_cast<int>(sr * 0.0371f), feedback * 0.97f, damping)
                   + combL_[2].ProcessComb(excitation, static_cast<int>(sr * 0.0411f), feedback * 0.95f, damping)
                   + combL_[3].ProcessComb(excitation, static_cast<int>(sr * 0.0437f), feedback * 0.93f, damping);
        float revR = combR_[0].ProcessComb(excitation, static_cast<int>(sr * 0.0307f), feedback, damping)
                   + combR_[1].ProcessComb(excitation, static_cast<int>(sr * 0.0383f), feedback * 0.97f, damping)
                   + combR_[2].ProcessComb(excitation, static_cast<int>(sr * 0.0422f), feedback * 0.95f, damping)
                   + combR_[3].ProcessComb(excitation, static_cast<int>(sr * 0.0451f), feedback * 0.93f, damping);
        revL *= 0.25f; revR *= 0.25f;
        revL = allpassL_[1].ProcessAllpass(allpassL_[0].ProcessAllpass(revL, static_cast<int>(sr * 0.0050f), 0.5f), static_cast<int>(sr * 0.0017f), 0.5f);
        revR = allpassR_[1].ProcessAllpass(allpassR_[0].ProcessAllpass(revR, static_cast<int>(sr * 0.0053f), 0.5f), static_cast<int>(sr * 0.0019f), 0.5f);
        const float ambienceWet = a * 0.62f; const float echoWet = e * 0.34f;
        wetL = Sanitize(revL * ambienceWet + earlyL * (0.08f + 0.24f * e) + echoTap * echoWet);
        wetR = Sanitize(revR * ambienceWet + earlyR * (0.08f + 0.24f * e) + echoTap * echoWet * 0.92f);
    }
    void SmoothParameters(float k)
    {
        smoothGain_ += (gain_.load() - smoothGain_) * k; smoothInputDrive_ += (inputDrive_.load() - smoothInputDrive_) * k;
        smoothDistanceGain_ += (distanceGain_.load() - smoothDistanceGain_) * k; smoothSafetyGain_ += (safetyGain_.load() - smoothSafetyGain_) * k;
        smoothAmbience_ += (ambience_.load() - smoothAmbience_) * k; smoothEcho_ += (echo_.load() - smoothEcho_) * k; smoothDynamics_ += (dynamics_.load() - smoothDynamics_) * k;
    }
    static float SoftLimit(float v, float ceiling)
    {
        if (!std::isfinite(v)) return 0.0f; const float a = std::fabs(v); if (a <= ceiling) return v;
        const float headroom = std::max(0.001f, 0.999f - ceiling); return std::copysign(std::min(0.999f, ceiling + headroom * std::tanh((a - ceiling) / headroom)), v);
    }
    static void SmoothMeter(std::atomic<float>& m, float peak) { m.store(m.load() * 0.82f + std::min(8.0f, peak) * 0.18f); }
    void DecayMeters() { preDspLevel_.store(preDspLevel_.load() * 0.92f); postDspLevel_.store(postDspLevel_.load() * 0.92f); outputLevel_.store(outputLevel_.load() * 0.92f); }
    void ResetDsp()
    {
        early_.Reset(); echoDelay_.Reset(); for (auto& d : combL_) d.Reset(); for (auto& d : combR_) d.Reset(); for (auto& d : allpassL_) d.Reset(); for (auto& d : allpassR_) d.Reset();
        hpInput_ = hpOutput_ = envelope_ = 0.0f; smoothGain_ = gain_.load(); smoothInputDrive_ = inputDrive_.load(); smoothDistanceGain_ = distanceGain_.load(); smoothSafetyGain_ = safetyGain_.load(); smoothAmbience_ = ambience_.load(); smoothEcho_ = echo_.load(); smoothDynamics_ = dynamics_.load();
    }
    void ResetStats()
    {
        callbackCount_.store(0); shortReadCount_.store(0); frameMismatchCount_.store(0); requestedInputFrameCount_.store(0); receivedInputFrameCount_.store(0);
        preDspLevel_.store(0); postDspLevel_.store(0); outputLevel_.store(0); compressorReductionDb_.store(0); limiterReductionDb_.store(0); lastStreamError_.store(0);
    }
    void CloseOwnedStreams()
    {
        setInputStream(nullptr); setOutputStream(nullptr);
        if (inputStream_) { inputStream_->requestStop(); inputStream_->close(); inputStream_.reset(); }
        if (outputStream_) { outputStream_->requestStop(); outputStream_->close(); outputStream_.reset(); }
    }
    void SetError(oboe::Result r) { lastStreamError_.store(static_cast<int32_t>(r), std::memory_order_release); }

    std::shared_ptr<oboe::AudioStream> inputStream_, outputStream_;
    int32_t sampleRate_ = 48000, framesPerBurst_ = 0, inputPreset_ = 0, inputCapacityFrames_ = 0, outputCapacityFrames_ = 0;
    int32_t requestedInputBufferFrames_ = 0, requestedOutputBufferFrames_ = 0;
    Delay<8192> early_; Delay<16384> echoDelay_; std::array<Delay<4096>, 4> combL_, combR_; std::array<Delay<1024>, 2> allpassL_, allpassR_;
    float hpInput_ = 0, hpOutput_ = 0, envelope_ = 0;
    float smoothGain_ = 1, smoothInputDrive_ = 1, smoothDistanceGain_ = 1, smoothSafetyGain_ = 1, smoothAmbience_ = 0, smoothEcho_ = 0, smoothDynamics_ = 0;
    std::atomic<float> gain_{1}, inputDrive_{1}, distanceGain_{1}, safetyGain_{1}, ambience_{0}, echo_{0}, dynamics_{0}, highPassHz_{90};
    std::atomic<bool> muted_{false}, running_{false};
    std::atomic<float> actualGain_{1}, actualInputDrive_{1}, actualDistanceGain_{1}, actualSafetyGain_{1}, preDspLevel_{0}, postDspLevel_{0}, outputLevel_{0}, compressorReductionDb_{0}, limiterReductionDb_{0};
    std::atomic<int32_t> lastStreamError_{0};
    std::atomic<uint64_t> callbackCount_{0}, shortReadCount_{0}, frameMismatchCount_{0}, requestedInputFrameCount_{0}, receivedInputFrameCount_{0};
};

OboeMonitor& Monitor() { static OboeMonitor monitor; return monitor; }
bool CopyParameters(const TsukiVoxNativeParameters* source, uint32_t size, TsukiVoxNativeParameters& destination)
{
    if (!source || size < sizeof(TsukiVoxNativeParameters) || source->size < sizeof(TsukiVoxNativeParameters) || source->version != ApiVersion) return false;
    destination = {}; std::memcpy(&destination, source, sizeof(destination)); destination.size = sizeof(destination); return true;
}
}

extern "C"
{
int TsukiVoxAudio_GetApiVersion() { return ApiVersion; }
int TsukiVoxAudio_StartV3(const TsukiVoxNativeParameters* p, uint32_t size) { TsukiVoxNativeParameters safe{}; return CopyParameters(p, size, safe) && Monitor().Start(safe) ? NativeSuccess : NativeFailure; }
int TsukiVoxAudio_SetParametersV3(const TsukiVoxNativeParameters* p, uint32_t size) { TsukiVoxNativeParameters safe{}; if (!CopyParameters(p, size, safe)) return NativeFailure; Monitor().SetParameters(safe); return NativeSuccess; }
void TsukiVoxAudio_Stop() { Monitor().Stop(); }
int TsukiVoxAudio_GetStatsV3(void* destination, uint32_t size)
{
    if (!destination || size < 8) return NativeFailure; auto stats = Monitor().GetStats();
    uint32_t requestedVersion = 0; std::memcpy(&requestedVersion, static_cast<uint8_t*>(destination) + sizeof(uint32_t), sizeof(requestedVersion));
    if (requestedVersion != ApiVersion) return NativeFailure; std::memcpy(destination, &stats, std::min<size_t>(size, sizeof(stats))); return NativeSuccess;
}
const char* TsukiVoxAudio_GetLastError()
{
    static thread_local std::array<char, 96> text{}; const auto code = Monitor().GetStats().lastStreamError;
    const char* message = code == 0 ? "" : oboe::convertToText(static_cast<oboe::Result>(code)); std::strncpy(text.data(), message, text.size() - 1); text.back() = '\0'; return text.data();
}
}
