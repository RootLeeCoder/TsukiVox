[CmdletBinding()]
param(
    [switch]$InstallOnly,
    [switch]$BuildOnly,
    [switch]$Release,
    [string]$DeviceSerial,
    [string]$UnityPath,
    [string]$ApkPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($ApkPath)) {
    $ApkPath = Join-Path $projectRoot 'build\TsukiVox-Quest.apk'
}
elseif (-not [IO.Path]::IsPathRooted($ApkPath)) {
    $ApkPath = Join-Path $projectRoot $ApkPath
}
$ApkPath = [IO.Path]::GetFullPath($ApkPath)
$buildDirectory = Split-Path -Parent $ApkPath
$buildReceiptPath = [IO.Path]::ChangeExtension($ApkPath, '.build.json')
$buildRequestPath = Join-Path $buildDirectory 'quest-build.request.json'
$buildResultPath = Join-Path $buildDirectory 'quest-build.result.json'
$deployReceiptPath = Join-Path $buildDirectory 'last-deploy.json'
$unityLogPath = Join-Path $buildDirectory 'unity-build.log'
$launchLogPath = Join-Path $buildDirectory 'quest-launch.log'
$packageName = 'com.tsukivox.audio'

if ($InstallOnly -and $BuildOnly) {
    throw 'InstallOnly and BuildOnly cannot be used together.'
}

function Resolve-UnityPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path -LiteralPath $RequestedPath -PathType Leaf)) {
            throw "Unity executable was not found: $RequestedPath"
        }
        return [IO.Path]::GetFullPath($RequestedPath)
    }

    $versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
    $versionLine = Get-Content -LiteralPath $versionFile | Select-Object -First 1
    if ($versionLine -notmatch '^m_EditorVersion:\s*(.+)$') {
        throw "Could not read the Unity version from $versionFile"
    }

    $candidate = "C:\Program Files\Unity\Hub\Editor\$($Matches[1].Trim())\Editor\Unity.exe"
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Unity $($Matches[1].Trim()) was not found at $candidate"
    }
    return $candidate
}

function Test-ProjectOpenInUnity {
    try {
        $escapedRoot = [Regex]::Escape($projectRoot)
        $processes = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop
        return @($processes | Where-Object {
            $_.CommandLine -match $escapedRoot -and $_.CommandLine -notmatch 'AssetImportWorker'
        }).Count -gt 0
    }
    catch {
        return $false
    }
}

function Resolve-AdbPath {
    param([string]$ResolvedUnityPath)

    $editorDirectory = Split-Path -Parent $ResolvedUnityPath
    $bundledAdb = Join-Path $editorDirectory 'Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
    if (Test-Path -LiteralPath $bundledAdb -PathType Leaf) {
        return $bundledAdb
    }

    $adbCommand = Get-Command adb -ErrorAction SilentlyContinue
    if ($null -ne $adbCommand) {
        return $adbCommand.Source
    }
    throw 'adb.exe was not found in the Unity Android SDK or PATH.'
}

function Invoke-Adb {
    param(
        [string]$AdbPath,
        [string[]]$Arguments,
        [string]$Serial,
        [switch]$AllowFailure
    )

    $allArguments = @()
    if (-not [string]::IsNullOrWhiteSpace($Serial)) {
        $allArguments += @('-s', $Serial)
    }
    $allArguments += $Arguments
    $output = & $AdbPath @allArguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = [string]::Join([Environment]::NewLine, @($output))
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "adb $($Arguments -join ' ') failed with exit code $exitCode.`n$text"
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Text = $text
    }
}

function Resolve-QuestDevice {
    param(
        [string]$AdbPath,
        [string]$RequestedSerial
    )

    $result = Invoke-Adb -AdbPath $AdbPath -Arguments @('devices') -Serial ''
    $devices = foreach ($line in ($result.Text -split "`r?`n")) {
        if ($line -match '^(\S+)\s+(device|offline|unauthorized)$') {
            [pscustomobject]@{ Serial = $Matches[1]; State = $Matches[2] }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedSerial)) {
        $match = @($devices | Where-Object { $_.Serial -eq $RequestedSerial })
        if ($match.Count -eq 0) {
            throw "ADB device '$RequestedSerial' was not found. Connect the Quest 3 and allow USB debugging."
        }
        if ($match[0].State -ne 'device') {
            throw "ADB device '$RequestedSerial' is $($match[0].State). Unlock the headset and approve USB debugging."
        }
        return $match[0].Serial
    }

    $readyDevices = @($devices | Where-Object { $_.State -eq 'device' })
    if ($readyDevices.Count -eq 0) {
        $states = @($devices | ForEach-Object { "$($_.Serial)=$($_.State)" }) -join ', '
        if ([string]::IsNullOrWhiteSpace($states)) {
            $states = 'no devices listed'
        }
        throw "No authorized Quest device is available ($states). Connect the Quest 3 and allow USB debugging."
    }
    if ($readyDevices.Count -gt 1) {
        throw "Multiple ADB devices are ready. Pass -DeviceSerial with one of: $($readyDevices.Serial -join ', ')"
    }
    return $readyDevices[0].Serial
}

New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null
$resolvedUnityPath = Resolve-UnityPath -RequestedPath $UnityPath

if (-not $InstallOnly) {
    foreach ($stalePath in @($buildReceiptPath, $buildRequestPath, $buildResultPath)) {
        if (Test-Path -LiteralPath $stalePath) {
            Remove-Item -LiteralPath $stalePath -Force
        }
    }
    $buildTime = [DateTimeOffset]::UtcNow
    $buildId = $buildTime.ToString('yyyyMMdd-HHmmss')
    if (Test-ProjectOpenInUnity) {
        $request = [ordered]@{
            outputPath = $ApkPath
            buildId = $buildId
            buildTimeUtc = $buildTime.ToString('O')
            releaseBuild = [bool]$Release
            resultPath = $buildResultPath
        }
        $requestTempPath = "$buildRequestPath.tmp"
        $requestJson = $request | ConvertTo-Json
        [IO.File]::WriteAllText($requestTempPath, $requestJson, [Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $requestTempPath -Destination $buildRequestPath -Force
        Write-Host "Unity Editor is open; submitted Build ID $buildId to the active editor ..."

        $acceptDeadline = [DateTime]::UtcNow.AddMinutes(2)
        $buildDeadline = [DateTime]::UtcNow.AddMinutes(30)
        while (-not (Test-Path -LiteralPath $buildResultPath -PathType Leaf)) {
            if ((Test-Path -LiteralPath $buildRequestPath) -and [DateTime]::UtcNow -gt $acceptDeadline) {
                throw 'The open Unity Editor did not accept the build request within two minutes. Check the Console for script compilation errors.'
            }
            if ([DateTime]::UtcNow -gt $buildDeadline) {
                throw 'The open Unity Editor did not finish the Quest build within 30 minutes.'
            }
            Start-Sleep -Seconds 1
        }

        $buildResult = Get-Content -LiteralPath $buildResultPath -Raw | ConvertFrom-Json
        if (-not $buildResult.succeeded) {
            throw "Unity build failed in the open editor.`n$($buildResult.error)"
        }
    }
    else {
        $unityArguments = @(
            '-batchmode',
            '-quit',
            '-nographics',
            '-projectPath', $projectRoot,
            '-buildTarget', 'Android',
            '-executeMethod', 'TsukiVox.AudioPrototype.Editor.QuestCommandLineBuild.BuildAndroid',
            '-tsukivoxOutput', $ApkPath,
            '-tsukivoxBuildId', $buildId,
            '-tsukivoxBuildTimeUtc', $buildTime.ToString('O'),
            '-logFile', $unityLogPath
        )
        if ($Release) {
            $unityArguments += '-tsukivoxRelease'
        }

        Write-Host "Building Quest APK with Build ID $buildId in headless Unity ..."
        & $resolvedUnityPath @unityArguments
        $unityExitCode = $LASTEXITCODE
        if ($unityExitCode -ne 0 -or -not (Test-Path -LiteralPath $buildReceiptPath -PathType Leaf)) {
            $logTail = if (Test-Path -LiteralPath $unityLogPath) {
                (Get-Content -LiteralPath $unityLogPath -Tail 80) -join [Environment]::NewLine
            }
            else {
                'Unity did not create a build log.'
            }
            throw "Unity build failed with exit code $unityExitCode.`n$logTail"
        }
    }
}

if (-not (Test-Path -LiteralPath $ApkPath -PathType Leaf)) {
    throw "APK was not found: $ApkPath"
}
if (-not (Test-Path -LiteralPath $buildReceiptPath -PathType Leaf)) {
    throw "Build receipt was not found: $buildReceiptPath"
}

$buildReceipt = Get-Content -LiteralPath $buildReceiptPath -Raw | ConvertFrom-Json -DateKind String
if ([string]::IsNullOrWhiteSpace([string]$buildReceipt.buildId)) {
    throw "Build receipt does not contain a Build ID: $buildReceiptPath"
}
if ([string]::IsNullOrWhiteSpace([string]$buildReceipt.androidVersionName)) {
    throw "Build receipt does not contain an Android version name: $buildReceiptPath"
}
Write-Host "APK ready: $ApkPath"
Write-Host "Build ID: $($buildReceipt.buildId)"
Write-Host "SHA-256: $($buildReceipt.apkSha256)"

if ($BuildOnly) {
    Write-Host 'Build-only mode complete; device installation was skipped.'
    exit 0
}

$adbPath = Resolve-AdbPath -ResolvedUnityPath $resolvedUnityPath
$serial = Resolve-QuestDevice -AdbPath $adbPath -RequestedSerial $DeviceSerial
Write-Host "Installing on Quest device $serial ..."
$installArguments = @('install', '-r')
if ($buildReceipt.developmentBuild) {
    $installArguments += '-d'
}
$installArguments += $ApkPath
$installResult = Invoke-Adb -AdbPath $adbPath -Arguments $installArguments -Serial $serial
if ($installResult.Text -notmatch '(?m)^Success\s*$') {
    throw "ADB did not report a successful install.`n$($installResult.Text)"
}

$packageResult = Invoke-Adb -AdbPath $adbPath -Arguments @('shell', 'dumpsys', 'package', $packageName) -Serial $serial
$versionMatch = [Regex]::Match($packageResult.Text, '(?m)^\s*versionName=(.+?)\s*$')
$installedVersionName = if ($versionMatch.Success) { $versionMatch.Groups[1].Value } else { '' }
$installVerified = $installedVersionName -eq [string]$buildReceipt.androidVersionName
if (-not $installVerified) {
    throw "Installed package version '$installedVersionName' does not match expected version '$($buildReceipt.androidVersionName)'."
}

Invoke-Adb -AdbPath $adbPath -Arguments @('shell', 'am', 'force-stop', $packageName) -Serial $serial | Out-Null
Invoke-Adb -AdbPath $adbPath -Arguments @('logcat', '-c') -Serial $serial | Out-Null
Invoke-Adb -AdbPath $adbPath -Arguments @('shell', 'am', 'start', '-n', "$packageName/com.unity3d.player.UnityPlayerGameActivity") -Serial $serial | Out-Null

$expectedMarker = "[TsukiVox Build] id=$($buildReceipt.buildId)"
$deadline = [DateTime]::UtcNow.AddSeconds(30)
$launchVerified = $false
$launchBlockedByQuest = $false
$appCrashed = $false
$launchLog = ''
while ([DateTime]::UtcNow -lt $deadline) {
    $logResult = Invoke-Adb -AdbPath $adbPath -Arguments @('logcat', '-d', '-v', 'brief') -Serial $serial -AllowFailure
    $launchLog = $logResult.Text
    if ($launchLog.Contains($expectedMarker)) {
        $launchVerified = $true
        break
    }
    if ($launchLog.Contains('RequiresControllersLaunchInterceptor') -or
        $launchLog.Contains('LaunchCheckControllerRequiredDialogActivity')) {
        $launchBlockedByQuest = $true
        break
    }
    if ($launchLog -match 'FATAL EXCEPTION' -and $launchLog.Contains($packageName)) {
        $appCrashed = $true
        break
    }
    Start-Sleep -Seconds 1
}
$launchLog | Set-Content -LiteralPath $launchLogPath -Encoding utf8NoBOM
$launchStatus = if ($launchVerified) {
    'verified'
}
elseif ($launchBlockedByQuest) {
    'blocked_by_quest_controller_check'
}
elseif ($appCrashed) {
    'app_crashed'
}
else {
    'build_marker_not_observed'
}

$deployReceipt = [ordered]@{
    buildId = [string]$buildReceipt.buildId
    buildTimeUtc = [string]$buildReceipt.buildTimeUtc
    apkPath = $ApkPath
    apkSha256 = [string]$buildReceipt.apkSha256
    deviceSerial = $serial
    installedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    installedVersionName = $installedVersionName
    installVerified = $installVerified
    launchVerified = $launchVerified
    launchStatus = $launchStatus
}
$deployReceipt | ConvertTo-Json | Set-Content -LiteralPath $deployReceiptPath -Encoding utf8NoBOM

if ($appCrashed -or (-not $launchVerified -and -not $launchBlockedByQuest)) {
    throw "The APK installed and launched, but logcat did not report the expected Build ID '$($buildReceipt.buildId)' within 30 seconds. See $launchLogPath"
}

Write-Host "Quest installation verified: versionName=$installedVersionName"
if ($launchVerified) {
    Write-Host "Quest launch verified: $expectedMarker"
}
else {
    Write-Warning 'Quest blocked the unattended launch until controllers are available. The exact APK Build ID is installed; wake the headset/controllers to continue runtime validation.'
}
Write-Host "Deployment receipt: $deployReceiptPath"
