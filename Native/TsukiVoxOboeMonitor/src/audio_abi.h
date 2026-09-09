#pragma once
#include <cstdint>
#include <cstring>
namespace tsukivox
{
constexpr int32_t ApiVersion = 5;
struct TsukiVoxNativeParameters
{
    uint32_t size; uint32_t version;
    int32_t requestedProfile; int32_t forceSafeConfiguration;
    int32_t inputProcessingMode; int32_t dspPreset;
    float gain; float inputDrive; float distanceGain; float safetyGain;
    float ambience; float echo; float dynamics;
    float dryGain; float reverbSend; float reverbPreDelayMs; float reverbDecay; float reverbWidth;
    float echoDelayMs; float echoFeedback; float echoWet; float doublingAmount;
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
    int32_t inputProcessingMode; int32_t dspPreset; int32_t dspVersion; int32_t callbackOverrunCount;
    float actualGain; float actualInputDrive; float actualDistanceGain; float actualSafetyGain;
    float preDspLevel; float postDspLevel; float outputLevel;
    float compressorReductionDb; float limiterReductionDb;
    float inputLatencyMs; float outputLatencyMs; float roundTripLatencyMs; float startToFirstInputMs;
    float dspAlgorithmLatencyMs; float callbackCpuLoad; float callbackMaxMs; float dryGain; float wetGain; float callbackAverageMs;
    uint64_t callbackCount; uint64_t shortReadCount; uint64_t frameMismatchCount;
    uint64_t requestedInputFrameCount; uint64_t receivedInputFrameCount;
};
static_assert(sizeof(TsukiVoxNativeParameters) == 96, "Parameter ABI changed");
static_assert(sizeof(TsukiVoxNativeStats) == 288, "Stats ABI changed");


inline bool ValidHeader(const void* data, uint32_t size, uint32_t expected)
{
    if (!data || size < expected) return false;
    uint32_t header[2];
    std::memcpy(header, data, sizeof(header));
    return header[0] == expected && header[1] == ApiVersion;
}
}
