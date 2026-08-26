param(
    [string] $UnityRoot = "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor",
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$AndroidPlayer = Join-Path $UnityRoot "Data\PlaybackEngines\AndroidPlayer"
$CMake = Join-Path $AndroidPlayer "SDK\cmake\3.22.1\bin\cmake.exe"
$Ninja = Join-Path $AndroidPlayer "SDK\cmake\3.22.1\bin\ninja.exe"
$Ndk = Join-Path $AndroidPlayer "NDK"
$Toolchain = Join-Path $Ndk "build\cmake\android.toolchain.cmake"
$BuildDir = Join-Path $PSScriptRoot "build\android-arm64"
$PluginDir = Join-Path $ProjectRoot "Assets\Plugins\Android\libs\arm64-v8a"
$OutputSo = Join-Path $BuildDir "libtsukivox_oboe_monitor.so"
$OboeDir = Join-Path $PSScriptRoot "third_party\oboe"
$CxxShared = Join-Path $Ndk "toolchains\llvm\prebuilt\windows-x86_64\sysroot\usr\lib\aarch64-linux-android\libc++_shared.so"

if (!(Test-Path $CMake)) {
    throw "CMake was not found at $CMake"
}

if (!(Test-Path $Ninja)) {
    throw "Ninja was not found at $Ninja"
}

if (!(Test-Path $Toolchain)) {
    throw "Android NDK toolchain was not found at $Toolchain"
}

New-Item -ItemType Directory -Force -Path $BuildDir, $PluginDir | Out-Null

if (!(Test-Path (Join-Path $OboeDir "CMakeLists.txt"))) {
    New-Item -ItemType Directory -Force -Path (Split-Path $OboeDir) | Out-Null
    & git clone --depth 1 --branch 1.10.0 https://github.com/google/oboe.git $OboeDir
    if ($LASTEXITCODE -ne 0 -or !(Test-Path (Join-Path $OboeDir "CMakeLists.txt"))) {
        if (Test-Path $OboeDir) {
            Remove-Item -LiteralPath $OboeDir -Recurse -Force -ErrorAction SilentlyContinue
        }

        $Archive = Join-Path (Split-Path $OboeDir) "oboe-1.10.0.zip"
        $ExtractRoot = Join-Path (Split-Path $OboeDir) "oboe-1.10.0"
        Invoke-WebRequest -Uri "https://github.com/google/oboe/archive/refs/tags/1.10.0.zip" -OutFile $Archive
        Expand-Archive -Force -Path $Archive -DestinationPath (Split-Path $OboeDir)
        Move-Item -Force -Path $ExtractRoot -Destination $OboeDir
    }
}

if (!(Test-Path (Join-Path $OboeDir "CMakeLists.txt"))) {
    throw "Oboe 1.10.0 source could not be fetched."
}

& $CMake -S $PSScriptRoot -B $BuildDir -G Ninja `
    "-DCMAKE_MAKE_PROGRAM=$Ninja" `
    "-DCMAKE_TOOLCHAIN_FILE=$Toolchain" `
    "-DANDROID_ABI=arm64-v8a" `
    "-DANDROID_PLATFORM=android-26" `
    "-DANDROID_STL=c++_shared" `
    "-DCMAKE_ANDROID_STL_TYPE=c++_shared" `
    "-DCMAKE_BUILD_TYPE=$Configuration"
if ($LASTEXITCODE -ne 0) {
    throw "CMake configure failed."
}

& $CMake --build $BuildDir --config $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Native plugin build failed."
}

Copy-Item -Force -Path $OutputSo -Destination $PluginDir
Copy-Item -Force -Path $CxxShared -Destination $PluginDir
Write-Host "Copied TsukiVox native audio API v4 (Oboe 1.10.0) to $PluginDir"
