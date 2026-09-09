param(
    [string]$UnityRoot = 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor'
)
$ErrorActionPreference = 'Stop'
$cmake = Join-Path $UnityRoot 'Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe'
$testBuild = Join-Path $PSScriptRoot 'build\host-tests'
& $cmake -S (Join-Path $PSScriptRoot 'tests') -B $testBuild -G 'Visual Studio 17 2022' -A x64
if ($LASTEXITCODE -ne 0) { throw 'Host DSP test configure failed. Install Visual Studio C++ build tools.' }
& $cmake --build $testBuild --config Release
if ($LASTEXITCODE -ne 0) { throw 'Host DSP test build failed.' }
& (Join-Path $testBuild 'Release\vocal_dsp_tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Host DSP tests failed.' }
