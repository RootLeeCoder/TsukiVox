using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace TsukiVox.AudioPrototype.Tests
{
    public sealed class NativeAudioPolicyTests
    {
        [TestCase(NativeMonitorProfile.Production, NativeMonitorProfiles.UnprocessedInputPreset, 0, 2, 2, NativeMonitorProfile.Production)]
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
        public void AbiSizesAndVersionMatchNativeV5()
        {
            Assert.That(NativeAudioAbi.ApiVersion, Is.EqualTo(5));
            Assert.That(Marshal.SizeOf<TsukiVoxNativeParameters>(), Is.EqualTo(NativeAudioAbi.ParameterSize));
            Assert.That(Marshal.SizeOf<TsukiVoxNativeStats>(), Is.EqualTo(NativeAudioAbi.StatsSize));
        }

        [Test]
        public void MonitorPreferenceDefaultsToSpatialAndAcceptsValidStoredValues()
        {
            Assert.That(AudioMonitorPreference.Resolve(false, 0), Is.EqualTo(MonitorMode.UnitySpatialSpeakers));
            Assert.That(
                AudioMonitorPreference.Resolve(true, (int)MonitorMode.UnitySpatialSpeakers),
                Is.EqualTo(MonitorMode.UnitySpatialSpeakers));
            Assert.That(
                AudioMonitorPreference.Resolve(true, (int)MonitorMode.OboeLowLatency),
                Is.EqualTo(MonitorMode.OboeLowLatency));
        }

        [Test]
        public void InvalidMonitorPreferenceRestoresSpatialDefault()
        {
            Assert.That(AudioMonitorPreference.Resolve(true, -1), Is.EqualTo(MonitorMode.UnitySpatialSpeakers));
            Assert.That(AudioMonitorPreference.Resolve(true, 999), Is.EqualTo(MonitorMode.UnitySpatialSpeakers));
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
            Assert.That(NativeFeedbackSafetyTuning.CalculateInputDrive(0.65f), Is.EqualTo(2.4625f).Within(0.0001f));
            Assert.That(NativeFeedbackSafetyTuning.CalculateInputDrive(2f), Is.EqualTo(3.25f).Within(0.0001f));
            Assert.That(NativeFeedbackSafetyTuning.CalculateHighPassHz(0.65f), Is.EqualTo(89.5f).Within(0.0001f));
            Assert.That(NativeFeedbackSafetyTuning.CalculateHighPassHz(-1f), Is.EqualTo(70f).Within(0.0001f));
        }

        [Test]
        public void NativeEffectTuningDisablesWetPathForDryReference()
        {
            var tuning = NativeEffectTuning.ForPreset(NativeDspPreset.DryReference, 1f, 1f, 0.65f);

            Assert.That(tuning.ReverbSend, Is.Zero);
            Assert.That(tuning.EchoWet, Is.Zero);
            Assert.That(tuning.DoublingAmount, Is.Zero);
            Assert.That(tuning.DryGain, Is.EqualTo(1f));
        }

        [Test]
        public void NativeEffectTuningKeepsKtvRoomWetEffectsBounded()
        {
            var tuning = NativeEffectTuning.ForPreset(NativeDspPreset.KtvRoom, 1f, 1f, 0.65f);

            Assert.That(tuning.ReverbPreDelayMs, Is.InRange(0f, 35f));
            Assert.That(tuning.ReverbDecay, Is.InRange(4f, 8f));
            Assert.That(tuning.EchoDelayMs, Is.InRange(45f, 120f));
            Assert.That(tuning.EchoFeedback, Is.InRange(0f, 0.6f));
            Assert.That(tuning.EchoWet, Is.InRange(0f, 0.4f));
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
        public void ExperimentalProfileRetainsSafetyFallback()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0.0, stats);
            stats.running = 0;
            stats.lastStreamError = -899;
            stats.inputXRunCount = 20;
            stats.shortReadCount = 100;

            Assert.That(policy.Observe(5.0, stats, false, false), Is.EqualTo(NativeStabilityAction.RestartSafe));
        }

        [Test]
        public void AbiRejectsWrongVersionOrLayout()
        {
            Assert.That(NativeAudioAbi.IsCompatible(5, 96, 288), Is.True);
            Assert.That(NativeAudioAbi.IsCompatible(4, 96, 288), Is.False);
            Assert.That(NativeAudioAbi.IsCompatible(5, 52, 288), Is.False);
            Assert.That(NativeAudioAbi.IsCompatible(5, 96, 256), Is.False);
            Assert.That(Marshal.OffsetOf<TsukiVoxNativeParameters>("inputProcessingMode").ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<TsukiVoxNativeStats>("callbackAverageMs").ToInt32(), Is.EqualTo(244));
            Assert.That(Marshal.OffsetOf<TsukiVoxNativeStats>("callbackCount").ToInt32(), Is.EqualTo(248));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(999)]
        public void MigrationChangesOnlyModeAndMarkerAndRunsOnce(int oldMode)
        {
            var preferences = new Dictionary<string, int>
            {
                [AudioModeMigration.ModeKey] = oldMode,
                ["TsukiVox.Audio.Preset.v1"] = 2,
                ["TsukiVox.Audio.DistanceMonitoring"] = 0,
                ["volume"] = 87,
                ["ambience"] = 44,
                ["echo"] = 36,
                ["dynamics"] = 65,
                ["safety"] = 1,
            };
            var before = new Dictionary<string, int>(preferences);
            Func<string, int, int> read = (key, fallback) => preferences.TryGetValue(key, out var value) ? value : fallback;
            Action<string, int> write = (key, value) => preferences[key] = value;
            AudioModeMigration.Apply(read, write);
            Assert.That(preferences[AudioModeMigration.ModeKey], Is.EqualTo((int)AudioMonitorPreference.Resolve(true, oldMode)));
            foreach (var pair in before)
                if (pair.Key != AudioModeMigration.ModeKey) Assert.That(preferences[pair.Key], Is.EqualTo(pair.Value));
            preferences[AudioModeMigration.ModeKey] = (int)MonitorMode.UnitySpatialSpeakers;
            AudioModeMigration.Apply(read, write);
            Assert.That(preferences[AudioModeMigration.ModeKey], Is.EqualTo((int)MonitorMode.UnitySpatialSpeakers));
        }

        [TestCase(NativeInputProcessingMode.Natural, 9)]
        [TestCase(NativeInputProcessingMode.Generic, 1)]
        [TestCase(NativeInputProcessingMode.VoiceRecognition, 6)]
        [TestCase(NativeInputProcessingMode.VoicePerformance, 10)]
        public void InputModesResolveIndependentlyOfBuffers(NativeInputProcessingMode mode, int preset)
        {
            Assert.That(NativeInputProcessing.ResolvePreset(mode), Is.EqualTo(preset));
            var p = NativeParameterFactory.Create(NativeMonitorProfile.BaselineGenericCushion1,
                NativeDspPreset.KtvRoom, true, 1, 1, 1, 0.55f, 0.3f, 0.65f, false, mode);
            Assert.That(p.inputProcessingMode, Is.EqualTo((int)mode));
            Assert.That(p.forceSafeConfiguration, Is.EqualTo(1));
        }

        [Test]
        public void NaturalFallsBackToGenericWhenUnprocessedIsUnavailable()
        {
            Assert.That(NativeInputProcessing.ResolvePreset(NativeInputProcessingMode.Natural, false), Is.EqualTo(1));
            Assert.That(NativeInputProcessing.Normalize(999), Is.EqualTo(NativeInputProcessingMode.Natural));
        }

        [TestCase(NativeDspPreset.DryReference, 1f, 0f, 0f, 0f)]
        [TestCase(NativeDspPreset.KtvRoom, 0.815f, 0.66746f, 0.027f, 0.65f)]
        [TestCase(NativeDspPreset.StrongKtv, 0.6532f, 0.89027f, 0.0744f, 0.9f)]
        [TestCase(NativeDspPreset.SafeSmallRoom, 0.98f, 0.1428f, 0.0072f, 0.48f)]
        public void PresetDefaultsMapToV5(NativeDspPreset preset, float dry, float room, float echo, float dynamics)
        {
            var defaults = NativePresetDefaults.ForPreset(preset);
            var p = NativeParameterFactory.Create(NativeMonitorProfile.Production, preset, false,
                1, 0.4f, 0.7f, defaults.Ambience, defaults.Echo, defaults.Dynamics, false);
            Assert.That(p.dryGain, Is.EqualTo(dry).Within(0.0001f));
            Assert.That(p.reverbSend, Is.EqualTo(room).Within(0.0001f));
            Assert.That(p.echoWet, Is.EqualTo(echo).Within(0.0001f));
            Assert.That(p.dynamics, Is.EqualTo(dynamics).Within(0.0001f));
            Assert.That(p.gain, Is.EqualTo(NativeFeedbackSafetyTuning.DefaultOutputGain).Within(0.0001f));
            Assert.That(p.distanceGain, Is.EqualTo(0.4f));
            Assert.That(p.safetyGain, Is.EqualTo(0.7f));
        }

        [Test]
        public void ZeroSlidersDisableWetAndDynamicsControlsDrive()
        {
            foreach (NativeDspPreset preset in Enum.GetValues(typeof(NativeDspPreset)))
            {
                var p = NativeParameterFactory.Create(NativeMonitorProfile.Production, preset, false,
                    1, 1, 1, 0, 0, 0, false);
                Assert.That(p.reverbSend + p.echoWet + p.doublingAmount, Is.Zero);
                Assert.That(p.inputDrive, Is.EqualTo(1f));
            }
            var strong = NativeParameterFactory.Create(NativeMonitorProfile.Production, NativeDspPreset.StrongKtv,
                false, 1, -1, float.NaN, 1, 1, 1, true);
            Assert.That(strong.inputDrive, Is.EqualTo(3.25f));
            Assert.That(strong.distanceGain, Is.Zero);
            Assert.That(strong.safetyGain, Is.Zero);
            Assert.That(strong.muted, Is.EqualTo(1));
        }

        [Test]
        public void CallbackOverrunsWarmUpRestartThenFallback()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0, stats);
            stats.callbackOverrunCount = 12;
            Assert.That(policy.Observe(1, stats, true, false), Is.EqualTo(NativeStabilityAction.None));
            Assert.That(policy.Observe(2, stats, true, false), Is.EqualTo(NativeStabilityAction.None));
            stats.callbackOverrunCount++;
            Assert.That(policy.Observe(2.1, stats, true, false), Is.EqualTo(NativeStabilityAction.RestartSafe));
            stats = RunningStats();
            policy.Reset(3, stats);
            policy.Observe(5, stats, true, true);
            stats.callbackOverrunCount = 2;
            Assert.That(policy.Observe(5.1, stats, true, true), Is.EqualTo(NativeStabilityAction.None));
            stats.callbackOverrunCount = 3;
            Assert.That(policy.Observe(5.2, stats, true, true), Is.EqualTo(NativeStabilityAction.FallbackUnity));
        }

        [Test]
        public void CallbackBusyForOneSecondTriggersSafeRestart()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0, stats);
            policy.Observe(2, stats, true, false);
            stats.callbackCpuLoad = 0.95f;
            Assert.That(policy.Observe(2.5, stats, true, false), Is.EqualTo(NativeStabilityAction.None));
            Assert.That(policy.Observe(3.6, stats, true, false), Is.EqualTo(NativeStabilityAction.RestartSafe));
        }

        [Test]
        public void CounterWindowExpiresOldInstability()
        {
            var policy = new NativeStabilityPolicy();
            var stats = RunningStats();
            policy.Reset(0, stats);
            policy.Observe(2, stats, true, true);
            stats.callbackOverrunCount = 2;
            policy.Observe(3, stats, true, true);
            Assert.That(policy.Observe(14, stats, true, true), Is.EqualTo(NativeStabilityAction.None));
            stats.callbackOverrunCount++;
            Assert.That(policy.Observe(14.1, stats, true, true), Is.EqualTo(NativeStabilityAction.None));
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
