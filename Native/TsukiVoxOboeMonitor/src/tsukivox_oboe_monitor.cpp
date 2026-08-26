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
constexpr int32_t ApiVersion = 4;
constexpr int32_t InputChannels = 1;
constexpr int32_t OutputChannels = 2;
constexpr int32_t MaxCallbackFrames = 4096;
constexpr float Pi = 3.14159265358979323846f;
constexpr float Epsilon = 1.0e-12f;
constexpr int NativeSuccess = 1;
constexpr int NativeFailure = 0;
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
};

struct ProfileConfig
{
    int32_t resolvedProfile;
    oboe::InputPreset inputPreset;
    int32_t inputBurstsCushion;
    int32_t inputBufferBursts;
    int32_t outputBufferBursts;
};

struct TsukiVoxNativeParameters
{
    uint32_t size; uint32_t version;
    int32_t requestedProfile; int32_t forceSafeConfiguration;
    float gain; float inputDrive; float distanceGain; float safetyGain;
    float ambience; float echo; float dynamics;
    int32_t muted; float highPassHz;
};

struct TsukiVoxNativeStats
{
    uint32_t size; uint32_t version;
    int32_t running; int32_t requestedProfile; int32_t resolvedProfile; int32_t fallbackStage;
    int32_t sampleRate; int32_t audioApi;
    int32_t inputPerformanceMode; int32_t outputPerformanceMode;
    int32_t inputSharingMode; int32_t outputSharingMode;
    int32_t inputMMapUsed; int32_t outputMMapUsed; int32_t inputPreset;
    int32_t inputFramesPerBurst; int32_t outputFramesPerBurst;
    int32_t inputChannelCount; int32_t outputChannelCount; int32_t inputFormat; int32_t outputFormat;
    int32_t inputCapacityFrames; int32_t outputCapacityFrames;
    int32_t requestedInputBufferFrames; int32_t requestedOutputBufferFrames;
    int32_t inputBufferFrames; int32_t outputBufferFrames;
    int32_t inputBurstsCushion; int32_t lastCallbackFrames; int32_t minCallbackFrames; int32_t maxCallbackFrames;
    int32_t inputXRunCount; int32_t outputXRunCount; int32_t inputXRunSupported; int32_t outputXRunSupported;
    int32_t lastStreamError; int32_t latencyValid; int32_t latencyErrorCount; int32_t fallbackReason;
    float actualGain; float actualInputDrive; float actualDistanceGain; float actualSafetyGain;
    float preDspLevel; float postDspLevel; float outputLevel;
    float compressorReductionDb; float limiterReductionDb;
    float inputLatencyMs; float outputLatencyMs; float roundTripLatencyMs; float startToFirstInputMs;
    uint64_t callbackCount; uint64_t shortReadCount; uint64_t frameMismatchCount;
    uint64_t requestedInputFrameCount; uint64_t receivedInputFrameCount;
};
static_assert(sizeof(TsukiVoxNativeParameters) == 52, "Parameter ABI changed");
static_assert(sizeof(TsukiVoxNativeStats) == 248, "Stats ABI changed");

template <typename T> T ClampFinite(T v, T lo, T hi, T fallback)
{ return std::isfinite(v) ? std::clamp(v, lo, hi) : fallback; }
inline float Lerp(float a, float b, float t) { return a + (b - a) * t; }
inline float Sanitize(float v) { return std::isfinite(v) && std::fabs(v) > Epsilon ? v : 0.0f; }
int32_t ValueOrZero(const oboe::ResultWithValue<int32_t>& r) { return r ? r.value() : 0; }

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
        case Production: return "production";
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
        Stop(); requestedProfile_ = NormalizeProfile(p.requestedProfile);
        forceSafeConfiguration_ = p.forceSafeConfiguration != 0;
        SetDspParameters(p); ResetDsp(); ResetStats();
        const auto requestedConfig = ResolveProfile(requestedProfile_);
        const auto safeConfig = MakeSafeConfig(requestedConfig);
        const ProfileConfig genericSafe{BaselineGenericCushion1, oboe::InputPreset::Generic, 1, 2, 2};
        oboe::Result result = oboe::Result::ErrorInternal;

        if (requestedProfile_ == Production)
        {
            if (!forceSafeConfiguration_)
            {
                result = TryOpenStreams(requestedConfig, oboe::SharingMode::Exclusive, NoFallback);
            }
            if (result != oboe::Result::OK)
            {
                result = TryOpenStreams(safeConfig, oboe::SharingMode::Exclusive,
                    forceSafeConfiguration_ ? RuntimeSafe : SafeExclusive);
            }
            if (result != oboe::Result::OK)
            {
                result = TryOpenStreams(safeConfig, oboe::SharingMode::Shared,
                    forceSafeConfiguration_ ? RuntimeSafe : SafeShared);
            }
            if (result != oboe::Result::OK)
            {
                result = TryOpenStreams(genericSafe, oboe::SharingMode::Shared, GenericSafeShared);
            }
        }
        else
        {
            result = TryOpenStreams(requestedConfig, oboe::SharingMode::Exclusive, NoFallback);
            if (result != oboe::Result::OK)
            {
                result = TryOpenStreams(requestedConfig, oboe::SharingMode::Shared, ExperimentShared);
            }
        }

        if (result != oboe::Result::OK) { SetError(result); CloseOwnedStreams(); return false; }
        streamStartNanos_ = NowNanos();
        result = oboe::FullDuplexStream::start();
        if (result != oboe::Result::OK) { SetError(result); CloseOwnedStreams(); return false; }
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
        running_.store(true, std::memory_order_release); return true;
    }
    void Stop()
    {
        running_.store(false, std::memory_order_release);
        oboe::FullDuplexStream::stop(); CloseOwnedStreams();
    }
    bool SetParameters(const TsukiVoxNativeParameters& p)
    {
        if (NormalizeProfile(p.requestedProfile) != requestedProfile_ ||
            (p.forceSafeConfiguration != 0) != forceSafeConfiguration_) return false;
        SetDspParameters(p); return true;
    }
    void SetDspParameters(const TsukiVoxNativeParameters& p)
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
        s.actualGain = actualGain_.load(); s.actualInputDrive = actualInputDrive_.load();
        s.actualDistanceGain = actualDistanceGain_.load(); s.actualSafetyGain = actualSafetyGain_.load();
        s.preDspLevel = preDspLevel_.load(); s.postDspLevel = postDspLevel_.load(); s.outputLevel = outputLevel_.load();
        s.compressorReductionDb = compressorReductionDb_.load(); s.limiterReductionDb = limiterReductionDb_.load();
        s.callbackCount = callbackCount_.load(); s.shortReadCount = shortReadCount_.load(); s.frameMismatchCount = frameMismatchCount_.load();
        s.requestedInputFrameCount = requestedInputFrameCount_.load(); s.receivedInputFrameCount = receivedInputFrameCount_.load();
        UpdateObservedStartupLatency();
        s.startToFirstInputMs = startToFirstInputMs_.load();
        UpdateLatencyEstimate(s);
        return s;
    }

    int32_t GetLastStreamError() const { return lastStreamError_.load(std::memory_order_acquire); }

    oboe::DataCallbackResult onBothStreamsReady(const void* inputData, int32_t inFrames, void* outputData, int32_t outFrames) override
    {
        auto* out = static_cast<float*>(outputData); const auto* in = static_cast<const float*>(inputData);
        if (!out || outFrames <= 0) return oboe::DataCallbackResult::Continue;
        std::fill(out, out + static_cast<size_t>(outFrames) * OutputChannels, 0.0f);
        lastCallbackFrames_.store(outFrames);
        UpdateMinimum(minCallbackFrames_, outFrames); UpdateMaximum(maxCallbackFrames_, outFrames);
        callbackCount_.fetch_add(1); requestedInputFrameCount_.fetch_add(static_cast<uint64_t>(outFrames));
        const int available = std::max(0, inFrames); receivedInputFrameCount_.fetch_add(static_cast<uint64_t>(std::min(available, outFrames)));
        if (inFrames < outFrames) shortReadCount_.fetch_add(1);
        if (inFrames != outFrames) frameMismatchCount_.fetch_add(1);
        if (inFrames > 0) firstValidInputSeen_.store(true, std::memory_order_release);
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

    void UpdateObservedStartupLatency()
    {
        if (!firstValidInputSeen_.load(std::memory_order_acquire) || startToFirstInputMs_.load() >= 0.0f || streamStartNanos_ <= 0)
        {
            return;
        }
        const auto elapsedMs = static_cast<float>((NowNanos() - streamStartNanos_) / 1000000.0);
        startToFirstInputMs_.store(std::max(0.0f, elapsedMs));
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
            fallbackStage_ = fallbackStage;
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
            ->setFormat(oboe::AudioFormat::Float)->setChannelCount(OutputChannels)->setUsage(oboe::Usage::Media)
            ->setContentType(oboe::ContentType::Music)->setDataCallback(this)->setErrorCallback(this);
        auto result = output.openStream(outputStream_); if (result != oboe::Result::OK) return result;
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
            ->setFormat(oboe::AudioFormat::Float)->setChannelCount(InputChannels)->setSampleRate(sampleRate_)
            ->setBufferCapacityInFrames(requestedInputCapacity)->setInputPreset(config.inputPreset);
        result = input.openStream(inputStream_); if (result != oboe::Result::OK) return result;
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
        lastCallbackFrames_.store(0); minCallbackFrames_.store(MaxCallbackFrames); maxCallbackFrames_.store(0);
        latencyErrorCount_.store(0); firstValidInputSeen_.store(false); startToFirstInputMs_.store(-1.0f); streamStartNanos_ = 0;
    }
    void CloseOwnedStreams()
    {
        setInputStream(nullptr); setOutputStream(nullptr);
        if (inputStream_) { inputStream_->requestStop(); inputStream_->close(); inputStream_.reset(); }
        if (outputStream_) { outputStream_->requestStop(); outputStream_->close(); outputStream_.reset(); }
    }
    void SetError(oboe::Result r) { lastStreamError_.store(static_cast<int32_t>(r), std::memory_order_release); }

    std::shared_ptr<oboe::AudioStream> inputStream_, outputStream_;
    int32_t requestedProfile_ = Production, resolvedProfile_ = VoicePerformanceCushion0, fallbackStage_ = NoFallback;
    bool forceSafeConfiguration_ = false;
    int32_t sampleRate_ = 48000, inputFramesPerBurst_ = 0, outputFramesPerBurst_ = 0, inputPreset_ = 0;
    int32_t inputCapacityFrames_ = 0, outputCapacityFrames_ = 0, inputBurstsCushion_ = 0;
    int32_t requestedInputBufferFrames_ = 0, requestedOutputBufferFrames_ = 0;
    int64_t streamStartNanos_ = 0;
    Delay<8192> early_; Delay<16384> echoDelay_; std::array<Delay<4096>, 4> combL_, combR_; std::array<Delay<1024>, 2> allpassL_, allpassR_;
    float hpInput_ = 0, hpOutput_ = 0, envelope_ = 0;
    float smoothGain_ = 1, smoothInputDrive_ = 1, smoothDistanceGain_ = 1, smoothSafetyGain_ = 1, smoothAmbience_ = 0, smoothEcho_ = 0, smoothDynamics_ = 0;
    std::atomic<float> gain_{1}, inputDrive_{1}, distanceGain_{1}, safetyGain_{1}, ambience_{0}, echo_{0}, dynamics_{0}, highPassHz_{90};
    std::atomic<bool> muted_{false}, running_{false};
    std::atomic<float> actualGain_{1}, actualInputDrive_{1}, actualDistanceGain_{1}, actualSafetyGain_{1}, preDspLevel_{0}, postDspLevel_{0}, outputLevel_{0}, compressorReductionDb_{0}, limiterReductionDb_{0};
    std::atomic<float> startToFirstInputMs_{-1.0f};
    std::atomic<bool> firstValidInputSeen_{false};
    std::atomic<int32_t> lastStreamError_{0}, latencyErrorCount_{0};
    std::atomic<int32_t> lastCallbackFrames_{0}, minCallbackFrames_{MaxCallbackFrames}, maxCallbackFrames_{0};
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
int TsukiVoxAudio_StartV4(const TsukiVoxNativeParameters* p, uint32_t size) { TsukiVoxNativeParameters safe{}; return CopyParameters(p, size, safe) && Monitor().Start(safe) ? NativeSuccess : NativeFailure; }
int TsukiVoxAudio_SetParametersV4(const TsukiVoxNativeParameters* p, uint32_t size) { TsukiVoxNativeParameters safe{}; return CopyParameters(p, size, safe) && Monitor().SetParameters(safe) ? NativeSuccess : NativeFailure; }
void TsukiVoxAudio_Stop() { Monitor().Stop(); }
int TsukiVoxAudio_GetStatsV4(void* destination, uint32_t size)
{
    if (!destination || size < 8) return NativeFailure; auto stats = Monitor().GetStats();
    uint32_t requestedVersion = 0; std::memcpy(&requestedVersion, static_cast<uint8_t*>(destination) + sizeof(uint32_t), sizeof(requestedVersion));
    if (requestedVersion != ApiVersion) return NativeFailure; std::memcpy(destination, &stats, std::min<size_t>(size, sizeof(stats))); return NativeSuccess;
}
const char* TsukiVoxAudio_GetLastError()
{
    static thread_local std::array<char, 96> text{}; const auto code = Monitor().GetLastStreamError();
    const char* message = code == 0 ? "" : oboe::convertToText(static_cast<oboe::Result>(code)); std::strncpy(text.data(), message, text.size() - 1); text.back() = '\0'; return text.data();
}
}
