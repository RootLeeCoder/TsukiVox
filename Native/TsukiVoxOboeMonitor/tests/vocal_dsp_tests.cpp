#include "vocal_dsp.h"
#include <atomic>
#include <cstdlib>
#include <iostream>
#include <limits>
#include <memory>
#include <new>
#include <vector>

static std::atomic<size_t> allocations{0};
void* operator new(size_t size)
{
    allocations.fetch_add(1);
    if (auto* p = std::malloc(size)) return p;
    throw std::bad_alloc();
}
void operator delete(void* p) noexcept { std::free(p); }
void operator delete(void* p, size_t) noexcept { std::free(p); }
void* operator new[](size_t n) { return ::operator new(n); }
void operator delete[](void* p) noexcept { std::free(p); }
void operator delete[](void* p, size_t) noexcept { std::free(p); }

using namespace tsukivox;
static void Check(bool ok, const char* label)
{
    if (!ok) { std::cerr << "FAIL: " << label << '\n'; std::exit(1); }
}
static TsukiVoxNativeParameters Parameters()
{
    TsukiVoxNativeParameters p{};
    p.size = sizeof(p); p.version = ApiVersion;
    p.dspPreset = 1; p.gain = 0.72f; p.inputDrive = 2.4625f;
    p.distanceGain = p.safetyGain = 1; p.dynamics = 0.65f;
    p.highPassHz = 89.5f; p.dryGain = 0.815f;
    p.reverbSend = 0.66746f; p.reverbDecay = ClampRoomDecay(4.475f); p.reverbWidth = 0.4475f;
    p.reverbPreDelayMs = ClampRoomPreDelay(24.65f); p.echoDelayMs = 69.6f; p.echoFeedback = 0.036f;
    p.echoWet = 0.027f; p.doublingAmount = 0.0105f;
    return p;
}
static void Silence(VocalDsp& dsp, const TsukiVoxNativeParameters& p, int frames)
{
    std::array<float, 256> input{};
    std::array<float, 512> output{};
    while (frames > 0)
    {
        const int count = std::min(frames, 256);
        dsp.Process(input.data(), count, output.data(), count, p);
        frames -= count;
    }
}

int main()
{
    auto dsp = std::make_unique<VocalDsp>();
    auto reference = std::make_unique<VocalDsp>();
    auto p = Parameters();
    Check(ClampRoomDecay(4.475f) == 4.475f && ClampRoomDecay(7.3f) == 7.3f,
          "Android boundary preserves long KTV RT60 seconds");
    Check(ClampRoomDecay(100) == 8 && ClampRoomDecay(-1) == 0.1f &&
          std::isfinite(ClampRoomDecay(std::numeric_limits<float>::quiet_NaN())), "bounded invalid decay");
    Check(ClampRoomPreDelay(35) == 35 && ClampRoomPreDelay(100) == 35, "wet predelay boundary");
    Check(sizeof(p) == 96 && sizeof(TsukiVoxNativeStats) == 288, "ABI sizes");
    Check(ValidHeader(&p, sizeof(p), sizeof(p)), "V5 accepted");
    p.version = 4;
    Check(!ValidHeader(&p, sizeof(p), sizeof(p)), "V4 rejected");
    p.version = 5; p.size = 52;
    Check(!ValidHeader(&p, sizeof(p), sizeof(p)), "wrong header size rejected");
    Check(!ValidHeader(&p, 4, sizeof(p)), "truncated buffer rejected before header read");
    p = Parameters();
    p.gain = 0.92f;
    std::array<float, 192> input{};
    std::array<float, 386> out{};
    for (const int rate : {32000, 44100, 48000, 96000})
    {
        dsp->Prepare(rate, p);
        Check(std::fabs(dsp->Frames(120) - rate * 0.12f) < 0.01f, "milliseconds to frames");
        DelayLine<16384> line;
        line.Write(1);
        const int delay = static_cast<int>(dsp->Frames(120));
        for (int i = 1; i < delay; ++i)
        {
            Check(line.Read(static_cast<float>(delay)) == 0, "no early delay output");
            line.Write(0);
        }
        Check(line.Read(static_cast<float>(delay)) == 1, "delay read/write order");
        Silence(*dsp, p, rate / 2);
        input.fill(0); input[0] = 1;
        out.front() = 123; out.back() = 456;
        const auto before = allocations.load();
        for (int block = 0; block < rate / 48; ++block)
        {
            dsp->Process(input.data(), 192, out.data() + 1, 192, p);
            for (int i = 1; i <= 384; ++i)
                Check(std::isfinite(out[i]) && std::fabs(out[i]) <= 0.93401f, "impulse finite and ceiling bounded");
            input.fill(0);
        }
        Check(allocations.load() == before, "zero DSP callback allocations");
        Check(out.front() == 123 && out.back() == 456, "output canaries intact");
    }
    std::cout << "PASS ABI, sample rates, impulse, output bounds, callback allocation guard\n";

    p.dspPreset = 0;
    auto dry = p;
    dry.reverbSend = dry.echoWet = dry.doublingAmount = 0;
    dsp->Prepare(48000, p); reference->Prepare(48000, dry);
    Silence(*dsp, p, 24000); Silence(*reference, dry, 24000);
    std::array<float, 384> expected{};
    for (int block = 0; block < 100; ++block)
    {
        for (int i = 0; i < 192; ++i) input[i] = std::sin((block * 192 + i) * 0.07f) * 0.2f;
        dsp->Process(input.data(), 192, out.data(), 192, p);
        reference->Process(input.data(), 192, expected.data(), 192, dry);
        for (int i = 0; i < 384; ++i) Check(out[i] == expected[i], "DryReference rejects all wet branches");
        for (int i = 0; i < 192; ++i) Check(out[i * 2] == out[i * 2 + 1], "dry centered stereo");
    }
    for (int i = -10000; i <= 10000; ++i)
        Check(std::fabs(VocalDsp::Limit(i * 0.01f, 0.82f)) <= 0.82f, "limiter never exceeds ceiling");
    input.fill(std::numeric_limits<float>::infinity());
    input[0] = std::numeric_limits<float>::quiet_NaN();
    dsp->Process(input.data(), 192, out.data(), 192, p);
    for (int i = 0; i < 384; ++i) Check(std::isfinite(out[i]), "nonfinite input sanitized");
    std::cout << "PASS dry reference, stereo center, limiter, nonfinite input\n";

    p = Parameters(); dsp->Prepare(48000, p);
    float previous = 0;
    for (int block = 0; block < 800; ++block)
    {
        const bool enabled = (block / 100) % 2 == 0;
        p.reverbSend = enabled ? 1.0f : 0; p.echoWet = enabled ? 0.4f : 0;
        p.doublingAmount = enabled ? 0.25f : 0;
        p.echoDelayMs = enabled ? 120 : 45;
        p.reverbPreDelayMs = enabled ? 8 : 0;
        for (int i = 0; i < 192; ++i) input[i] = 0.1f * std::sin((block * 192 + i) * 0.05f);
        dsp->Process(input.data(), 192, out.data(), 192, p);
        for (int i = 0; i < 192; ++i)
        {
            Check(std::fabs(out[i * 2] - previous) < 0.25f, "continuous effect toggles and delay changes");
            previous = out[i * 2];
        }
    }
    Check(dsp->WetGain() == 0, "zero controls completely close wet paths");
    dsp->Prepare(48000, p);
    input.fill(0);
    dsp->Process(input.data(), 192, out.data(), 192, p);
    for (int i = 0; i < 384; ++i) Check(out[i] == 0, "restart clears filter and delay states");
    p = Parameters(); dsp->Prepare(48000, p);
    for (int block = 0; block < 100; ++block)
    {
        for (int i = 0; i < 192; ++i) input[i] = 0.2f * std::sin((block * 192 + i) * 0.07f);
        dsp->Process(input.data(), 192, out.data(), 192, p);
    }
    const float last = out[382];
    dsp->Process(nullptr, 0, out.data(), 192, p);
    Check(std::fabs(out[0] - last) <= std::fabs(last) / 32 + 1e-6f, "empty read continues tail ramp");
    for (int i = 32; i < 192; ++i) Check(out[i * 2] == 0 && out[i * 2 + 1] == 0, "empty read becomes silence");
    dsp->Process(input.data(), 17, out.data(), 192, p);
    Check(out[32] == 0 && out[34] == 0, "partial read fades exactly to zero");
    dsp->Process(input.data(), 192, out.data(), 192, p);
    Check(std::fabs(out[0]) < 0.03f, "input recovery fades in");
    std::cout << "PASS smooth effect changes, zero wet, restart clearing, short-read concealment\n";

    // A real zero-valued capture must let the wet tail decay, unlike an input
    // underrun. Check useful late energy and eventual decay at every device rate.
    for (int rate : {32000, 44100, 48000, 96000})
    for (float decaySeconds : {4.475f, 7.3f, 8.0f})
    {
        p = Parameters(); p.dryGain = 0; p.echoWet = p.doublingAmount = 0;
        p.reverbDecay = ClampRoomDecay(decaySeconds);
        dsp->Prepare(rate, p);
        auto truncated = p; truncated.reverbDecay = 1.2f;
        reference->Prepare(rate, truncated);
        double earlyEnergy = 0, lateEnergy = 0, endEnergy = 0, truncatedLateEnergy = 0;
        for (int block = 0; block < rate * 12 / 192; ++block)
        {
            input.fill(0);
            if (block == 0) input[0] = 0.4f;
            dsp->Process(input.data(), 192, out.data(), 192, p);
            reference->Process(input.data(), 192, expected.data(), 192, truncated);
            for (int i = 0; i < 192; ++i)
            {
                const float time = float(block * 192 + i) / rate;
                const double energy = out[i * 2] * out[i * 2] + out[i * 2 + 1] * out[i * 2 + 1];
                if (time >= 0.05f && time < 0.25f) earlyEnergy += energy;
                if (time >= 1.5f && time < 2.5f)
                {
                    lateEnergy += energy;
                    truncatedLateEnergy += expected[i * 2] * expected[i * 2] + expected[i * 2 + 1] * expected[i * 2 + 1];
                }
                if (time >= 11) endEnergy += energy;
            }
        }
        Check(lateEnergy > earlyEnergy * 0.001, "long KTV field survives beyond 1.5 seconds");
        Check(lateEnergy > truncatedLateEnergy * 100, "long tail exceeds old Android clamp by at least 20 dB");
        Check(endEnergy < lateEnergy * 0.001, "room tail decays instead of sustaining");

        double energies[2]{};
        for (int band = 0; band < 2; ++band)
        {
            dsp->Prepare(rate, p);
            const float hz = band == 0 ? 800.0f : 8000.0f;
            for (int block = 0; block < rate * 2 / 192; ++block)
            {
                for (int i = 0; i < 192; ++i)
                    input[i] = 0.02f * std::sin(6.2831853f * hz * (block * 192 + i) / rate);
                dsp->Process(input.data(), 192, out.data(), 192, p);
                if (block * 192 > rate)
                    for (int i = 0; i < 384; ++i) energies[band] += out[i] * out[i];
            }
        }
        std::cout << "rate " << rate << " RT60 " << decaySeconds << " late/early " << lateEnergy / earlyEnergy
                  << " high/mid " << energies[1] / energies[0] << '\n';
        Check(energies[1] < energies[0] * 0.15, "wet path attenuates sharp high-frequency energy");
    }
    std::cout << "PASS late room tail, silence decay, high-frequency damping at four sample rates\n";
    return 0;
}
