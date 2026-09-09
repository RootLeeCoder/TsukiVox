#pragma once
#include "audio_abi.h"
#include <algorithm>
#include <array>
#include <cmath>

namespace tsukivox
{
// All storage belongs to the engine. Prepare/Reset run only after streams stop.
inline float Finite(float value)
{
    return std::isfinite(value) && std::fabs(value) > 1.0e-20f ? value : 0.0f;
}

template <size_t Capacity> class DelayLine
{
public:
    void Reset() { data_.fill(0); write_ = 0; }
    float Read(float frames) const
    {
        frames = std::clamp(frames, 1.0f, static_cast<float>(Capacity - 2));
        const auto whole = static_cast<size_t>(frames);
        const float fraction = frames - whole;
        const auto index = (write_ + Capacity - whole) % Capacity;
        return data_[index] * (1 - fraction) + data_[(index + Capacity - 1) % Capacity] * fraction;
    }
    void Write(float value)
    {
        data_[write_] = Finite(value);
        write_ = (write_ + 1) % Capacity;
    }
private:
    std::array<float, Capacity> data_{};
    size_t write_ = 0;
};

struct DspMeters
{
    float pre = 0, post = 0, output = 0, compressionDb = 0, limiterDb = 0;
};

// Shared with the Android parameter boundary and host regression tests.
// Decay is RT60 seconds, not a feedback coefficient.
inline float ClampRoomDecay(float seconds)
{
    return std::isfinite(seconds) ? std::clamp(seconds, 0.1f, 8.0f) : 1.2f;
}

inline float ClampRoomPreDelay(float ms)
{
    return std::isfinite(ms) ? std::clamp(ms, 0.0f, 35.0f) : 0.0f;
}

class VocalDsp
{
public:
    static constexpr int Version = 4;
    // No lookahead, block buffering, resampler or delay on the dry branch.
    // Frequency-dependent IIR phase and wet predelay are not transport latency.
    static constexpr float AlgorithmLatencyMs = 0;
    static constexpr int FadeFrames = 32;

    void Prepare(int sampleRate, const TsukiVoxNativeParameters& parameters)
    {
        rate_ = std::clamp(sampleRate, 8000, 96000);
        smoothing_ = 1 - std::exp(-1.0f / (0.025f * rate_));
        attack_ = 1 - std::exp(-1.0f / (0.006f * rate_));
        release_ = 1 - std::exp(-1.0f / (0.140f * rate_));
        dcAlpha_ = std::exp(-2 * Pi * 15 / rate_);
        wetLowAlpha_ = 1 - std::exp(-2 * Pi * 3200 / rate_);
        wetBassAlpha_ = 1 - std::exp(-2 * Pi * 220 / rate_);
        voiceLowAlpha_ = 1 - std::exp(-2 * Pi * 5500 / rate_);
        roomDampingAlpha_ = 1 - std::exp(-2 * Pi * 4200 / rate_);
        Reset();
        smooth_ = parameters;
        dryMix_ = parameters.dspPreset == 0 ? 1.0f : 0.0f;
        // Stream start is already gated by Unity's monitor toggle and safety gain.
        // Do not apply an additional silent startup ramp: it made the first spoken
        // phrase effectively inaudible and was perceived as a disabled microphone.
        muteGain_ = parameters.muted ? 0.0f : 1.0f;
    }

    void Reset()
    {
        early_.Reset(); echo_.Reset(); doubling_.Reset();
        for (auto& line : diffusion_) line.Reset();
        for (auto& line : room_) line.Reset();
        damping_.fill(0);
        dcInput_ = dcOutput_ = hpInput_ = hpOutput_ = envelope_ = 0;
        wetLow_ = wetLowSecond_ = wetBass_ = voiceLow_ = 0;
        gate_ = 1; muteGain_ = 0; dryMix_ = 0;
        previousL_ = previousR_ = concealL_ = concealR_ = 0;
        missingFrames_ = 0; resumeFrames_ = FadeFrames;
    }

    float Frames(float ms) const { return rate_ * ms / 1000.0f; }
    float DryGain() const { return smooth_.dryGain; }
    float WetGain() const
    {
        return std::min(0.8f, (smooth_.reverbSend < 0.0001f ? 0 : smooth_.reverbSend) +
            (smooth_.echoWet < 0.0001f ? 0 : smooth_.echoWet) +
            (smooth_.doublingAmount < 0.0001f ? 0 : smooth_.doublingAmount)) * (1 - dryMix_);
    }
    const TsukiVoxNativeParameters& Smoothed() const { return smooth_; }

    static float Limit(float sample, float ceiling)
    {
        sample = Finite(sample);
        const float knee = ceiling * 0.85f;
        const float magnitude = std::fabs(sample);
        if (magnitude <= knee) return sample;
        const float result = knee + (ceiling - knee) * std::tanh((magnitude - knee) / (ceiling - knee));
        return std::copysign(std::min(result, ceiling), sample);
    }

    DspMeters Process(const float* input, int inputFrames, float* output, int outputFrames,
                     const TsukiVoxNativeParameters& target)
    {
        DspMeters meters;
        const int available = input ? std::clamp(inputFrames, 0, outputFrames) : 0;
        for (int i = 0; i < outputFrames; ++i)
        {
            Smooth(target);
            const bool valid = i < available;
            const float source = valid && !target.muted ? std::clamp(Finite(input[i]), -8.0f, 8.0f) : 0;
            const float dc = Finite(source - dcInput_ + dcAlpha_ * dcOutput_);
            dcInput_ = source; dcOutput_ = dc;
            const float hpAlpha = std::exp(-2 * Pi * smooth_.highPassHz / rate_);
            const float hp = Finite(hpAlpha * (hpOutput_ + dc - hpInput_));
            hpInput_ = dc; hpOutput_ = hp;
            meters.pre = std::max(meters.pre, std::fabs(hp));
            voiceLow_ = Finite(voiceLow_ + voiceLowAlpha_ * (hp - voiceLow_));
            const float vocal = voiceLow_ + (hp - voiceLow_) * dryMix_;
            const float driven = vocal * (smooth_.inputDrive + (1 - smooth_.inputDrive) * dryMix_);
            const float level = std::fabs(driven);
            envelope_ = Finite(envelope_ + (level - envelope_) * (level > envelope_ ? attack_ : release_));
            // Gentle continuous downward expansion: no hard gate on consonants or tails.
            // Detect before drive/makeup: amplification must not keep quiet
            // microphone leakage open. Only excitation is expanded, never tails.
            const float gateTarget = std::clamp(envelope_ / std::max(1.0f, smooth_.inputDrive) / 0.0025f, 0.08f, 1.0f);
            gate_ += (gateTarget - gate_) * (gateTarget > gate_ ? attack_ : release_);
            const float ratio = 2 + smooth_.dynamics;
            const float thresholdDb = -12 - 8 * smooth_.dynamics;
            const float over = 20 * std::log10(std::max(envelope_, 1.0e-7f)) - thresholdDb;
            float reduction = 0;
            if (over > 3) reduction = over * (1 - 1 / ratio);
            else if (over > -3) reduction = (over + 3) * (over + 3) * (1 - 1 / ratio) / 12;
            reduction *= 1 - dryMix_;
            const float compression = std::pow(10.0f, -reduction / 20);
            // Keep makeup modest: below the compressor knee it also amplifies
            // recaptured speaker leakage. Room presence comes from the wet field.
            const float makeup = 1.0f + 0.6f * smooth_.dynamics * (1 - dryMix_);
            const float processed = driven * compression * (gate_ + (1 - gate_) * dryMix_) * makeup;
            const float dry = Limit(processed, 1.2f);
            meters.post = std::max(meters.post, std::fabs(dry));
            meters.compressionDb = std::max(meters.compressionDb, reduction);
            float wetL, wetR;
            Effects(dry, wetL, wetR);
            const float outputGain = smooth_.gain * smooth_.distanceGain * smooth_.safetyGain * muteGain_;
            const float dryBalance = 1 - 0.35f * WetGain();
            float left = (dry * smooth_.dryGain * dryBalance + wetL) * outputGain;
            float right = (dry * smooth_.dryGain * dryBalance + wetR) * outputGain;
            const float ceiling = target.dspPreset == 3 ? 0.82f : 0.96f - 0.04f * smooth_.dynamics;
            const float peak = std::max(std::fabs(left), std::fabs(right));
            left = Limit(left, ceiling); right = Limit(right, ceiling);
            const float limited = std::max(std::fabs(left), std::fabs(right));
            if (peak > 1.0e-7f)
                meters.limiterDb = std::max(meters.limiterDb, -20 * std::log10(std::max(limited / peak, 1.0e-7f)));

            // Advance all DSP with zero input during missing frames. Conceal the output
            // boundary with a bounded tail ramp, then silence; fade back in on recovery.
            if (!valid)
            {
                if (missingFrames_ == 0) { concealL_ = previousL_; concealR_ = previousR_; }
                missingFrames_ = std::min(FadeFrames, missingFrames_ + 1);
                const float fade = 1 - static_cast<float>(missingFrames_) / FadeFrames;
                left = concealL_ * fade; right = concealR_ * fade;
                resumeFrames_ = 0;
            }
            else
            {
                missingFrames_ = 0;
                resumeFrames_ = std::min(FadeFrames, resumeFrames_ + 1);
                float fade = static_cast<float>(resumeFrames_) / FadeFrames;
                if (available < outputFrames && available - i <= FadeFrames)
                    fade *= static_cast<float>(available - i - 1) / std::max(1, std::min(FadeFrames, available) - 1);
                left *= fade; right *= fade;
            }
            // Ceiling remains valid during concealment and a preset change.
            left = std::clamp(left, -ceiling, ceiling); right = std::clamp(right, -ceiling, ceiling);
            output[i * 2] = previousL_ = left; output[i * 2 + 1] = previousR_ = right;
            meters.output = std::max(meters.output, std::max(std::fabs(left), std::fabs(right)));
        }
        return meters;
    }

private:
    void Smooth(const TsukiVoxNativeParameters& p)
    {
#define SMOOTH(field) smooth_.field += (p.field - smooth_.field) * smoothing_
        SMOOTH(gain); SMOOTH(inputDrive); SMOOTH(distanceGain); SMOOTH(safetyGain);
        SMOOTH(dynamics); SMOOTH(highPassHz); SMOOTH(dryGain); SMOOTH(reverbSend);
        SMOOTH(reverbPreDelayMs); SMOOTH(reverbDecay); SMOOTH(reverbWidth);
        SMOOTH(echoDelayMs); SMOOTH(echoFeedback); SMOOTH(echoWet); SMOOTH(doublingAmount);
#undef SMOOTH
        dryMix_ += ((p.dspPreset == 0 ? 1.0f : 0.0f) - dryMix_) * smoothing_;
        muteGain_ += ((p.muted ? 0.0f : 1.0f) - muteGain_) * smoothing_;
        if (p.dspPreset == 0 && dryMix_ > 0.9999f) dryMix_ = 1;
    }

    void Effects(float dry, float& left, float& right)
    {
        // Band-limit all wet excitation, including echo, before it can circulate.
        wetLow_ = Finite(wetLow_ + wetLowAlpha_ * (dry - wetLow_));
        wetLowSecond_ = Finite(wetLowSecond_ + wetLowAlpha_ * (wetLow_ - wetLowSecond_));
        wetBass_ = Finite(wetBass_ + wetBassAlpha_ * (wetLowSecond_ - wetBass_));
        dry = wetLowSecond_ - wetBass_;
        const float roomGain = smooth_.reverbSend < 0.0001f ? 0 : smooth_.reverbSend * (1 - dryMix_);
        const float echoGain = smooth_.echoWet < 0.0001f ? 0 : smooth_.echoWet * (1 - dryMix_);
        const float doubleGain = smooth_.doublingAmount < 0.0001f ? 0 : smooth_.doublingAmount * (1 - dryMix_);
        const float total = roomGain + echoGain + doubleGain;
        const float wetScale = total > 0.9f ? 0.9f / total : 1;
        // Always advance with zeros when disabled so old tails never freeze/reappear.
        const float preFrames = Frames(smooth_.reverbPreDelayMs);
        float excitation = preFrames < 1 ? dry : early_.Read(preFrames);
        // Unity-gain allpasses spread each reflection into a dense field without
        // increasing the room feedback coefficient or delaying the dry voice.
        constexpr std::array<float, 3> diffusionMs{5.3f, 7.9f, 12.7f};
        for (size_t j = 0; j < diffusion_.size(); ++j)
        {
            const float delayed = diffusion_[j].Read(Frames(diffusionMs[j]));
            const float diffused = delayed - 0.6f * excitation;
            diffusion_[j].Write(roomGain > 0 ? excitation + 0.6f * diffused : 0);
            excitation = diffused;
        }
        const float early = early_.Read(preFrames + Frames(4)) * 0.34f +
                            early_.Read(preFrames + Frames(9)) * 0.24f +
                            early_.Read(preFrames + Frames(17)) * 0.16f +
                            early_.Read(preFrames + Frames(23)) * 0.10f;
        early_.Write(roomGain > 0 ? dry : 0);
        std::array<float, 4> tap{};
        // Longer, unequal lines retain a rich midrange tail despite HF damping.
        // The longest still fits the allocated delay at 96 kHz.
        constexpr std::array<float, 4> times{43.7f, 53.3f, 67.7f, 79.3f};
        for (size_t j = 0; j < tap.size(); ++j)
        {
            const float delayed = room_[j].Read(Frames(times[j]));
            damping_[j] = Finite(damping_[j] + (delayed - damping_[j]) * roomDampingAlpha_);
            tap[j] = damping_[j];
        }
        // Orthogonal Hadamard feedback mixes all four lines, with independent damping.
        const std::array<float, 4> feedback{
            (tap[0] + tap[1] + tap[2] + tap[3]) * 0.5f,
            (tap[0] - tap[1] + tap[2] - tap[3]) * 0.5f,
            (tap[0] + tap[1] - tap[2] - tap[3]) * 0.5f,
            (tap[0] - tap[1] - tap[2] + tap[3]) * 0.5f};
        for (size_t j = 0; j < tap.size(); ++j)
        {
            const float decay = std::pow(0.001f, times[j] / (1000 * smooth_.reverbDecay));
            room_[j].Write(roomGain > 0 ? excitation * 0.42f + feedback[j] * decay : 0);
        }
        const float mid = early * 0.65f + (tap[0] + tap[1] + tap[2] + tap[3]) * 0.56f;
        const float side = (tap[0] - tap[1] + tap[2] - tap[3]) * 0.40f * smooth_.reverbWidth;
        const float echoTap = echo_.Read(Frames(smooth_.echoDelayMs));
        echo_.Write(echoGain > 0 ? dry + echoTap * smooth_.echoFeedback : 0);
        const float doubled = doubling_.Read(Frames(9));
        doubling_.Write(doubleGain > 0 ? dry : 0);
        const float center = echoTap * echoGain + doubled * doubleGain;
        left = Finite(((mid + side) * roomGain + center) * wetScale);
        right = Finite(((mid - side) * roomGain + center) * wetScale);
    }

    static constexpr float Pi = 3.14159265358979323846f;
    int rate_ = 48000;
    float smoothing_ = 0, attack_ = 0, release_ = 0, dcAlpha_ = 0;
    float dcInput_ = 0, dcOutput_ = 0, hpInput_ = 0, hpOutput_ = 0, envelope_ = 0;
    float wetLowAlpha_ = 0, wetBassAlpha_ = 0, voiceLowAlpha_ = 0, roomDampingAlpha_ = 0;
    float wetLow_ = 0, wetLowSecond_ = 0, wetBass_ = 0, voiceLow_ = 0;
    float gate_ = 1, muteGain_ = 0, dryMix_ = 0;
    float previousL_ = 0, previousR_ = 0, concealL_ = 0, concealR_ = 0;
    int missingFrames_ = 0, resumeFrames_ = FadeFrames;
    TsukiVoxNativeParameters smooth_{};
    DelayLine<8192> early_;
    DelayLine<16384> echo_;
    DelayLine<2048> doubling_;
    std::array<DelayLine<2048>, 3> diffusion_;
    std::array<DelayLine<8192>, 4> room_;
    std::array<float, 4> damping_{};
};
}
