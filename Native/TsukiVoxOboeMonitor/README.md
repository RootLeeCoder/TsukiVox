# Quest low-latency monitor (ABI V5)

The monitor owns its microphone and fixed, head-independent stereo output. Unity
continues to play backing tracks; microphone audio is never routed back through a
Unity AudioSource. Spatial speakers remain available from the existing mode control.

## Signal path and tuning

`vocal_dsp.h` is the same allocation-free DSP used by Android and host tests:

- DC blocker, 70–100 Hz high-pass and smoothed level detector.
- Input-referred downward expansion (8% floor), soft-knee 2:1–3:1 compression
  and reduced makeup. Expansion affects excitation, not existing room tails.
- A 5.5 kHz voice low-pass (bypassed in DryReference); wet excitation has a
  220 Hz bass cut and two 3.2 kHz low-pass stages, all sample-rate aware.
- Direct dry signal plus four early reflections, three allpass diffusers and a damped four-line Hadamard
  feedback network. Room predelay affects the wet branch only.
- Independent 45–120 ms echo and a centered 9 ms doubling tap.
- Smoothed controls, bounded wet mix, distance/safety/output gain and final limiter.

The limiter ceiling is 0.82 for SafeSmallRoom and 0.92–0.96 for other presets.
KtvRoom maps ambience to 2–6.5 s RT60 (default 4.475 s); StrongKtv maps it to
3–8 s (default 7.3 s). Wet send follows a square-root curve so normal slider
positions are already lush, while dry gain decreases. SafeSmallRoom is unchanged.
DryReference bypasses expansion, compression, drive and all wet effects. Setting
ambience and echo to zero closes every wet branch after a short parameter ramp;
disabled delay lines continue advancing with zeros so stale tails cannot reappear.
Reverb decay is specified in seconds (RT60 before damping), not feedback gain.

DSP revision 4 fixes an Android boundary clamp that silently capped RT60 at 1.2 s.
The boundary now preserves up to 8 s RT60 and 35 ms wet-only predelay, with shared
validation used by host tests. Longer 43.7–79.3 ms room lines retain the midrange
tail under damping. Input drive, makeup, expansion and wet band-limiting remain
at revision 3 settings. Longer decay does raise room feedback; actual acoustic
stability must be checked on the headset. It does not add dry-path buffering.
This is feedback-risk reduction, not acoustic echo cancellation
or adaptive howl detection; stability at 60% Quest speaker volume still requires
on-device listening. Host tests check tail survival/decay and high-frequency wet
attenuation at 32/44.1/48/96 kHz, not the headset's acoustic transfer function.
They cover 4.475/7.3/8 s decays, compare 1.5–2.5 s tail energy against the old
1.2 s clamp (at least 20 dB improvement), and check decay over a 12 s render.

The dry path has **0 samples of added buffering or lookahead**, which is what
`dspAlgorithmLatencyMs = 0` means. It does not mean acoustic latency is zero.
High-pass phase, wet reflections, Android input/output buffering, transducers and
acoustic travel must be considered separately. Platform latency estimates are
diagnostics only.

## Stream control and diagnostics

Natural requests Unprocessed. Open failures retry Shared and then Generic at the
same buffer sizes, followed by a cushioned configuration when needed. Buffer
profiles and input processing mode are independent. All selectable profiles retain
automatic safety fallback, including the one-output-burst experiment.

Streams request AAudio, 48 kHz, float, mono input and stereo output. The primary
profile requests two bursts on each stream with no input cushion; safe mode adds
one input burst cushion. Android may clamp buffer requests; diagnostics report
both requested and actual sizes. No application-created AGC, NS or AEC is enabled;
an OEM may still apply processing internally.

After a two-second warmup, new XRuns, repeated short reads, callback deadline misses
or sustained high callback load restart once with cushion 1. Continued instability
uses spatial speakers. Stop, pause and permission loss close streams before DSP
reset. Callback timing includes Oboe's duplex read as well as our DSP. Average/max
times are session statistics; CPU load is a smoothed fraction of callback budget.
Counters reset on each native start; Unity retains restart count and the last eight
timestamped transition records for the app session.

Open **设置 > 诊断与支持 > 音频调试** to cycle Natural, VoiceRecognition,
VoicePerformance and Generic, or select primary/safe/one-burst buffers. Effect
parameters and stream statistics occupy separate detail pages. Input choice is
persistent; experimental buffer selection is session-only. The existing preset,
ambience, echo and dynamics controls remain the tuning interface.

V5 parameters are 96 bytes and statistics 288 bytes, packed at 8-byte alignment.
Both start with size/version; V4 or incorrect layouts are rejected before copying.
New installs and the one-time V5 migration select low latency. Migration only writes
MonitorMode and its completion marker; existing tone, volume and safety settings
are preserved. Manual spatial selection is respected after migration.

## Build and automated checks

From the repository root:

```powershell
pwsh -NoLogo -NoProfile -File Native/TsukiVoxOboeMonitor/test.ps1
pwsh -NoLogo -NoProfile -File Native/TsukiVoxOboeMonitor/build.ps1
pwsh -NoLogo -NoProfile -File Tools/Deploy-Quest.ps1
```

Host tests use Visual Studio 2022 C++ tools and Unity's CMake, without Android or
Quest. The portable `tests/CMakeLists.txt` also works with another host C++17
compiler. Tests cover ABI rejection, sample rate conversion, delay tap indexing,
impulse bounds, zero callback allocations, DryReference, limiter ceiling, invalid
input, smooth effect changes, reset and missing-input concealment.

Run Unity EditMode tests with the editor closed (omit `-quit`; the test runner exits):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe' -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults build/native-audio-editmode.xml -logFile build/unity-editmode.log
```

Native build uses Oboe 1.10.0, C++17, API 26, arm64-v8a, 16 KB ELF alignment and
bundled `libc++_shared.so`. Build/deployment receipts and test outputs belong under
ignored `build/`, not source control.

## Device acceptance still required

Automated tests cannot establish SingRoom-equivalent tone or acoustic latency.
Use Quest 3 speakers, consistent headset volume, microphone distance and reference
recording level. Record the Build ID, input mode, preset and actual stream settings.

1. Compare spatial mode, an archived pre-rewrite APK, this build, and installed
   SingRoom. The old low-latency DSP is not retained inside the new binary.
2. Record at least 30 repeated impulses using synchronized input-reference and
   speaker-capture channels. Use physical isolation/direct-leakage control and
   cross-correlation of each pulse; account for recorder channel skew and microphone
   placement. Report per-pulse delays, p50 and p95. Target p50 ≤ 40 ms, p95 ≤ 50 ms.
   A single recorder hearing both direct voice and speakers can mistake leakage
   for the monitor output; it is not sufficient without separating the two paths.
3. Sing for ten minutes with video/backing audio playing. Check audible dropouts,
   pops, restart history, XRuns and callback overruns. Verify centered dry voice,
   natural consonants/tails, short room fullness and restrained echo.
4. Test zero ambience/echo, all presets, all input modes, distance attenuation and
   near-face safety. Verify normal stop/start, headset pause/resume, and native
   stream loss recovering through safe mode to spatial speakers.
5. Verify controller hits and legibility on both diagnostic pages, copy complete
   diagnostics, and compare its Build ID with `build/last-deploy.json`.
6. Verify first-install permission behavior on a fresh test installation/profile
   with user agreement. Routine `adb install -r` preserves existing permissions and
   data and cannot establish this fresh-install result.

Do not mark device acceptance complete from platform latency estimates, successful
installation, a sleeping headset or the system controller launch dialog.
