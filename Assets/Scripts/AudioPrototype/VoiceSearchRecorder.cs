using System;
using UnityEngine;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// Captures a short dry voice clip for voice search.
    ///
    /// This is a read-only consumer of <see cref="QuestAudioPrototype"/>'s existing
    /// microphone ring buffer: it copies dry samples, downsamples them to 16 kHz mono
    /// and packs a WAV payload. It never changes gain, filters, presets or the
    /// monitoring chain, so vocal monitoring keeps running untouched while recording.
    /// </summary>
    public sealed class VoiceSearchRecorder
    {
        /// <summary>Upload sample rate expected by the voice search endpoint.</summary>
        public const int UploadSampleRate = 16000;

        private const int MaxRecordSeconds = 6;
        private const int BufferSeconds = 7;
        private const float SilenceRms = 0.012f;
        private const float TrailingSilenceSeconds = 0.8f;
        private const float LeadingSilenceTimeoutSeconds = 2.5f;
        private const float MinimumSpeechSeconds = 0.4f;

        private readonly QuestAudioPrototype audioPrototype;
        private readonly float[] captureBuffer = new float[UploadSampleRate * BufferSeconds];
        private readonly float[] scratch;

        private int captureCount;
        private int trimmedSampleCount = -1;
        private int lastReadPosition = -1;
        private int sourceSampleRate = 48000;
        private float accumulator;
        private float elapsedSeconds;
        private float silenceSeconds;
        private float peakRms;
        private bool speechDetected;

        public VoiceSearchRecorder(QuestAudioPrototype audioPrototype)
        {
            this.audioPrototype = audioPrototype;
            // One Update worth of 48 kHz audio is well under 0.25 s even on a bad frame.
            scratch = new float[12000];
        }

        public VoiceSearchRecorderState State { get; private set; } = VoiceSearchRecorderState.Idle;

        /// <summary>Seconds captured so far, for the recording indicator.</summary>
        public float ElapsedSeconds => elapsedSeconds;

        /// <summary>Smoothed input level in 0..1, for the level meter.</summary>
        public float Level { get; private set; }

        /// <summary>
        /// Duration of the payload that will actually be uploaded, in milliseconds.
        /// This reflects the trimmed clip, not the raw capture, so the reported
        /// duration always matches the bytes sent to the server.
        /// </summary>
        public int CapturedMilliseconds => Mathf.RoundToInt(PayloadSampleCount * 1000f / UploadSampleRate);

        /// <summary>Sample count after trailing silence has been trimmed.</summary>
        private int PayloadSampleCount
        {
            get
            {
                if (captureCount <= 0)
                {
                    return 0;
                }
                if (trimmedSampleCount < 0)
                {
                    trimmedSampleCount = TrimTrailingSilence();
                }
                return trimmedSampleCount;
            }
        }

        /// <summary>True when the dry tap is usable; false on the native Oboe backend.</summary>
        public bool CanRecord => audioPrototype != null && audioPrototype.IsDryCaptureAvailable;

        /// <summary>
        /// Begins capture. Returns false when no dry tap is available, which happens
        /// while the native Oboe backend is active because it exposes levels only.
        /// </summary>
        public bool TryBegin()
        {
            if (!CanRecord)
            {
                State = VoiceSearchRecorderState.Unavailable;
                return false;
            }

            captureCount = 0;
            trimmedSampleCount = -1;
            accumulator = 0f;
            elapsedSeconds = 0f;
            silenceSeconds = 0f;
            peakRms = 0f;
            speechDetected = false;
            Level = 0f;
            sourceSampleRate = Mathf.Max(8000, audioPrototype.DryCaptureSampleRate);
            lastReadPosition = audioPrototype.DryCapturePosition;
            State = VoiceSearchRecorderState.Recording;
            return true;
        }

        public void Cancel()
        {
            captureCount = 0;
            trimmedSampleCount = -1;
            elapsedSeconds = 0f;
            Level = 0f;
            State = VoiceSearchRecorderState.Idle;
        }

        /// <summary>
        /// Pumps the recorder. Call once per frame while recording; returns the state
        /// so the UI can react to automatic completion.
        /// </summary>
        public VoiceSearchRecorderState Tick(float deltaTime)
        {
            if (State != VoiceSearchRecorderState.Recording)
            {
                return State;
            }
            if (!CanRecord)
            {
                State = VoiceSearchRecorderState.Unavailable;
                return State;
            }

            elapsedSeconds += deltaTime;
            DrainMicrophone();

            if (!speechDetected && elapsedSeconds >= LeadingSilenceTimeoutSeconds)
            {
                // Nothing was said. Finish locally so no billable request is made.
                State = VoiceSearchRecorderState.NoSpeech;
                return State;
            }
            if (speechDetected && silenceSeconds >= TrailingSilenceSeconds)
            {
                State = HasEnoughSpeech()
                    ? VoiceSearchRecorderState.Completed
                    : VoiceSearchRecorderState.NoSpeech;
                return State;
            }
            if (elapsedSeconds >= MaxRecordSeconds || captureCount >= UploadSampleRate * MaxRecordSeconds)
            {
                State = HasEnoughSpeech()
                    ? VoiceSearchRecorderState.Completed
                    : VoiceSearchRecorderState.NoSpeech;
                return State;
            }

            return State;
        }

        /// <summary>
        /// Builds the WAV payload for upload. Returns null when nothing usable was captured.
        /// </summary>
        public byte[] BuildWav()
        {
            if (captureCount <= 0)
            {
                return null;
            }
            return EncodeWav(captureBuffer, PayloadSampleCount, UploadSampleRate);
        }

        /// <summary>Clears the captured audio. Called right after upload so no copy lingers.</summary>
        public void ClearCapturedAudio()
        {
            Array.Clear(captureBuffer, 0, captureBuffer.Length);
            captureCount = 0;
            trimmedSampleCount = -1;
        }

        private bool HasEnoughSpeech()
        {
            // Judge the trimmed payload: a clip that shrinks below the minimum after
            // trimming would otherwise be uploaded and rejected by the server.
            return PayloadSampleCount >= UploadSampleRate * MinimumSpeechSeconds;
        }

        /// <summary>
        /// Drops trailing silence so the uploaded clip stays short. Providers bill by
        /// duration, and a shorter payload also uploads faster over Wi-Fi.
        /// </summary>
        private int TrimTrailingSilence()
        {
            var keepSilence = Mathf.RoundToInt(UploadSampleRate * 0.25f);
            var window = Mathf.Max(1, UploadSampleRate / 100);
            var index = captureCount;

            while (index > window)
            {
                var sum = 0f;
                for (var offset = index - window; offset < index; offset += 1)
                {
                    sum += captureBuffer[offset] * captureBuffer[offset];
                }
                if (Mathf.Sqrt(sum / window) > SilenceRms)
                {
                    break;
                }
                index -= window;
            }

            return Mathf.Min(captureCount, index + keepSilence);
        }

        private void DrainMicrophone()
        {
            var writePosition = audioPrototype.DryCapturePosition;
            var totalSamples = audioPrototype.DryCaptureBufferSamples;
            if (writePosition < 0 || totalSamples <= 0)
            {
                return;
            }
            if (lastReadPosition < 0)
            {
                lastReadPosition = writePosition;
                return;
            }

            var available = writePosition - lastReadPosition;
            if (available < 0)
            {
                available += totalSamples;
            }
            if (available <= 0)
            {
                return;
            }

            // If a frame hitched badly the ring buffer may already have wrapped past
            // unread audio. Keep only what is still valid instead of reading garbage.
            var maxReadable = Mathf.Min(scratch.Length, totalSamples);
            if (available > maxReadable)
            {
                lastReadPosition = writePosition - maxReadable;
                if (lastReadPosition < 0)
                {
                    lastReadPosition += totalSamples;
                }
                available = maxReadable;
            }

            var chunk = new float[available];
            if (!audioPrototype.TryReadDrySamples(chunk, lastReadPosition))
            {
                return;
            }
            lastReadPosition = writePosition;

            Append(chunk);
            UpdateSpeechState(chunk);
        }

        /// <summary>
        /// Downsamples the incoming 48 kHz dry audio to 16 kHz by averaging whole
        /// input windows. Averaging also acts as a cheap anti-alias filter, which
        /// plain sample dropping would not.
        /// </summary>
        private void Append(float[] source)
        {
            var ratio = sourceSampleRate / (float)UploadSampleRate;
            var sum = 0f;
            var count = 0;

            for (var index = 0; index < source.Length; index += 1)
            {
                sum += source[index];
                count += 1;
                accumulator += 1f;

                if (accumulator < ratio)
                {
                    continue;
                }

                accumulator -= ratio;
                if (captureCount < captureBuffer.Length)
                {
                    captureBuffer[captureCount] = count > 0 ? sum / count : 0f;
                    captureCount += 1;
                    // New audio invalidates any cached trim result.
                    trimmedSampleCount = -1;
                }
                sum = 0f;
                count = 0;
            }
        }

        private void UpdateSpeechState(float[] samples)
        {
            var sum = 0f;
            for (var index = 0; index < samples.Length; index += 1)
            {
                sum += samples[index] * samples[index];
            }
            var rms = samples.Length > 0 ? Mathf.Sqrt(sum / samples.Length) : 0f;
            peakRms = Mathf.Max(peakRms, rms);
            Level = Mathf.Lerp(Level, Mathf.Clamp01(rms * 12f), 0.35f);

            var seconds = samples.Length / (float)sourceSampleRate;
            if (rms > SilenceRms)
            {
                speechDetected = true;
                silenceSeconds = 0f;
                return;
            }
            if (speechDetected)
            {
                silenceSeconds += seconds;
            }
        }

        /// <summary>Packs mono float samples into a 16-bit PCM WAV payload.</summary>
        public static byte[] EncodeWav(float[] samples, int sampleCount, int sampleRate)
        {
            var count = Mathf.Clamp(sampleCount, 0, samples?.Length ?? 0);
            var dataBytes = count * 2;
            var payload = new byte[44 + dataBytes];

            WriteAscii(payload, 0, "RIFF");
            WriteInt32(payload, 4, 36 + dataBytes);
            WriteAscii(payload, 8, "WAVE");
            WriteAscii(payload, 12, "fmt ");
            WriteInt32(payload, 16, 16);
            WriteInt16(payload, 20, 1);
            WriteInt16(payload, 22, 1);
            WriteInt32(payload, 24, sampleRate);
            WriteInt32(payload, 28, sampleRate * 2);
            WriteInt16(payload, 32, 2);
            WriteInt16(payload, 34, 16);
            WriteAscii(payload, 36, "data");
            WriteInt32(payload, 40, dataBytes);

            var offset = 44;
            for (var index = 0; index < count; index += 1)
            {
                var value = (int)(Mathf.Clamp(samples[index], -1f, 1f) * short.MaxValue);
                payload[offset] = (byte)(value & 0xFF);
                payload[offset + 1] = (byte)((value >> 8) & 0xFF);
                offset += 2;
            }

            return payload;
        }

        private static void WriteAscii(byte[] target, int offset, string value)
        {
            for (var index = 0; index < value.Length; index += 1)
            {
                target[offset + index] = (byte)value[index];
            }
        }

        private static void WriteInt32(byte[] target, int offset, int value)
        {
            target[offset] = (byte)(value & 0xFF);
            target[offset + 1] = (byte)((value >> 8) & 0xFF);
            target[offset + 2] = (byte)((value >> 16) & 0xFF);
            target[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteInt16(byte[] target, int offset, int value)
        {
            target[offset] = (byte)(value & 0xFF);
            target[offset + 1] = (byte)((value >> 8) & 0xFF);
        }
    }

    public enum VoiceSearchRecorderState
    {
        Idle,
        Recording,
        Completed,
        NoSpeech,
        Unavailable,
    }
}
