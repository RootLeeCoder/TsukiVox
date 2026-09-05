using System.Runtime.InteropServices;
using NUnit.Framework;

namespace TsukiVox.AudioPrototype.Tests
{
    public sealed class NativeAudioPolicyTests
    {
        [TestCase(NativeMonitorProfile.Production, NativeMonitorProfiles.VoicePerformanceInputPreset, 0, 2, 2, NativeMonitorProfile.VoicePerformanceCushion0)]
        [TestCase(NativeMonitorProfile.BaselineGenericCushion1, NativeMonitorProfiles.GenericInputPreset, 1, 2, 2, NativeMonitorProfile.BaselineGenericCushion1)]
        [TestCase(NativeMonitorProfile.VoiceRecognitionCushion0, NativeMonitorProfiles.VoiceRecognitionInputPreset, 0, 2, 2, NativeMonitorProfile.VoiceRecognitionCushion0)]
        [TestCase(NativeMonitorProfile.VoicePerformanceCushion0, NativeMonitorProfiles.VoicePerformanceInputPreset, 0, 2, 2, NativeMonitorProfile.VoicePerformanceCushion0)]
        [TestCase(NativeMonitorProfile.VoicePerformanceCushion1, NativeMonitorProfiles.VoicePerformanceInputPreset, 1, 2, 2, NativeMonitorProfile.VoicePerformanceCushion1)]
        [TestCase(NativeMonitorProfile.VoicePerformanceCushion0Output1, NativeMonitorProfiles.VoicePerformanceInputPreset, 0, 2, 1, NativeMonitorProfile.VoicePerformanceCushion0Output1)]
        public void ProfileMappingMatchesNativeContract(
            NativeMonitorProfile profile,
            int expectedPreset,
            int expectedCushion,
            int expectedInputBursts,
            int expectedOutputBursts,
            NativeMonitorProfile expectedResolvedProfile)
        {
            var configuration = NativeMonitorProfiles.Resolve(profile);

            Assert.That(configuration.InputPreset, Is.EqualTo(expectedPreset));
            Assert.That(configuration.InputBurstsCushion, Is.EqualTo(expectedCushion));
            Assert.That(configuration.InputBufferBursts, Is.EqualTo(expectedInputBursts));
            Assert.That(configuration.OutputBufferBursts, Is.EqualTo(expectedOutputBursts));
            Assert.That(configuration.ResolvedProfile, Is.EqualTo(expectedResolvedProfile));
        }

        [Test]
        public void AbiSizesAndVersionMatchNativeV4()
        {
            Assert.That(NativeAudioAbi.ApiVersion, Is.EqualTo(4));
            Assert.That(Marshal.SizeOf<TsukiVoxNativeParameters>(), Is.EqualTo(NativeAudioAbi.ParameterSize));
            Assert.That(Marshal.SizeOf<TsukiVoxNativeStats>(), Is.EqualTo(NativeAudioAbi.StatsSize));
        }

        [Test]
        public void MonitorPreferenceDefaultsToLowLatencyAndAcceptsValidStoredValues()
        {
            Assert.That(AudioMonitorPreference.Resolve(false, 0), Is.EqualTo(MonitorMode.OboeLowLatency));
            Assert.That(
                AudioMonitorPreference.Resolve(true, (int)MonitorMode.UnitySpatialSpeakers),
                Is.EqualTo(MonitorMode.UnitySpatialSpeakers));
            Assert.That(
                AudioMonitorPreference.Resolve(true, (int)MonitorMode.OboeLowLatency),
                Is.EqualTo(MonitorMode.OboeLowLatency));
        }

        [Test]
        public void InvalidMonitorPreferenceRestoresLowLatencyDefault()
        {
            Assert.That(AudioMonitorPreference.Resolve(true, -1), Is.EqualTo(MonitorMode.OboeLowLatency));
            Assert.That(AudioMonitorPreference.Resolve(true, 999), Is.EqualTo(MonitorMode.OboeLowLatency));
        }

        [Test]
        public void NativeFeedbackSafetyKeepsDefaultAndMaximumOutputBelowUnity()
        {
            Assert.That(
                NativeFeedbackSafetyTuning.CalculateOutputGain(1f),
                Is.EqualTo(NativeFeedbackSafetyTuning.DefaultOutputGain).Within(0.0001f));
            Assert.That(
                NativeFeedbackSafetyTuning.CalculateOutputGain(3f),
                Is.EqualTo(NativeFeedbackSafetyTuning.MaximumOutputGain).Within(0.0001f));
            Assert.That(NativeFeedbackSafetyTuning.CalculateOutputGain(float.NaN), Is.Zero);
        }

        [Test]
        public void NativeFeedbackSafetyLimitsToneControlsWithoutRemovingBody()
        {
            Assert.That(NativeFeedbackSafetyTuning.CalculateInputDrive(0.65f), Is.EqualTo(3.6f).Within(0.0001f));
            Assert.That(NativeFeedbackSafetyTuning.CalculateInputDrive(2f), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(NativeFeedbackSafetyTuning.CalculateHighPassHz(0.65f), Is.EqualTo(78f).Within(0.0001f));
            Assert.That(NativeFeedbackSafetyTuning.CalculateHighPassHz(-1f), Is.EqualTo(65f).Within(0.0001f));
        }

        [Test]
        public void LatencyWindowRejectsInvalidSamplesAndCalculatesMedianAndP95()
        {
            var window = new NativeLatencyWindow();
            window.Add(0.0, float.NaN);
            window.Add(0.0, -1f);
            window.Add(0.0, 1f);
            window.Add(1.0, 2f);
            window.Add(2.0, 3f);
            window.Add(3.0, 100f);

            var summary = window.GetSummary(3.0);

            Assert.That(summary.Count, Is.EqualTo(4));
            Assert.That(summary.Median, Is.EqualTo(2f));
            Assert.That(summary.P95, Is.EqualTo(100f));
        }

        [Test]
        public void LatencyWindowDropsSamplesOlderThanThirtySeconds()
        {
            var window = new NativeLatencyWindow();
            window.Add(0.0, 1f);
            window.Add(1.0, 2f);
            window.Add(2.0, 3f);
            window.Add(31.1, 4f);

            var summary = window.GetSummary(31.1);

            Assert.That(summary.Count, Is.EqualTo(2));
            Assert.That(summary.Median, Is.EqualTo(3f));
            Assert.That(summary.P95, Is.EqualTo(4f));
        }

        [Test]
        public void FastProductionProfileRestartsSafeAfterFirstNewXRun()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0.0, stats);
            Assert.That(policy.Observe(2.0, stats, true, false), Is.EqualTo(NativeStabilityAction.None));
            stats.inputXRunCount = 1;

            var action = policy.Observe(2.1, stats, true, false);

            Assert.That(action, Is.EqualTo(NativeStabilityAction.RestartSafe));
        }

        [Test]
        public void FastProductionProfileRestartsSafeAfterThreeShortReads()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0.0, stats);
            Assert.That(policy.Observe(2.0, stats, true, false), Is.EqualTo(NativeStabilityAction.None));
            stats.shortReadCount = 2;
            Assert.That(policy.Observe(2.1, stats, true, false), Is.EqualTo(NativeStabilityAction.None));
            stats.shortReadCount = 3;

            Assert.That(policy.Observe(2.2, stats, true, false), Is.EqualTo(NativeStabilityAction.RestartSafe));
        }

        [Test]
        public void SafeProductionProfileFallsBackAtConfiguredThresholds()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0.0, stats);
            Assert.That(policy.Observe(2.0, stats, true, true), Is.EqualTo(NativeStabilityAction.None));
            stats.shortReadCount = 9;
            Assert.That(policy.Observe(2.1, stats, true, true), Is.EqualTo(NativeStabilityAction.None));
            stats.shortReadCount = 10;
            Assert.That(policy.Observe(2.2, stats, true, true), Is.EqualTo(NativeStabilityAction.FallbackUnity));

            policy.Reset(3.0, RunningStats());
            stats = RunningStats();
            Assert.That(policy.Observe(5.0, stats, true, true), Is.EqualTo(NativeStabilityAction.None));
            stats.outputXRunCount = 3;
            Assert.That(policy.Observe(5.1, stats, true, true), Is.EqualTo(NativeStabilityAction.FallbackUnity));
        }

        [Test]
        public void ExperimentalProfileNeverAutoDowngrades()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0.0, stats);
            stats.running = 0;
            stats.lastStreamError = -899;
            stats.inputXRunCount = 20;
            stats.shortReadCount = 100;

            Assert.That(policy.Observe(5.0, stats, false, false), Is.EqualTo(NativeStabilityAction.None));
        }

        private static TsukiVoxNativeStats RunningStats()
        {
            return new TsukiVoxNativeStats
            {
                running = 1,
                inputXRunSupported = 1,
                outputXRunSupported = 1,
            };
        }
    }
}
