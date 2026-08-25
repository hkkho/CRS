#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $UnityEditorPath,

    [ValidateNotNullOrEmpty()]
    [string] $OutputPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-unity-demo\ReactorGameDemo.exe'),

    [ValidateNotNullOrEmpty()]
    [string] $LogPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-unity-demo.log')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'unity/ReactorGame'
$toolchainPath = Join-Path $repositoryRoot 'unity/toolchain.json'
$toolchain = Get-Content -Raw -LiteralPath $toolchainPath | ConvertFrom-Json
$expectedVersion = $toolchain.unityEditor.version
$expectedRevision = $toolchain.unityEditor.revision

if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $editorCandidates = @(
        (Join-Path $env:LOCALAPPDATA "Unity\Editors\$expectedVersion\Editor\Unity.exe"),
        "C:\Program Files\Unity\Hub\Editor\$expectedVersion\Editor\Unity.exe",
        "C:\Program Files\Unity\$expectedVersion\Editor\Unity.exe"
    )
    $UnityEditorPath = $editorCandidates |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($UnityEditorPath) -or
    -not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
    throw "Unity Editor $expectedVersion was not found. Install the pinned editor or pass -UnityEditorPath."
}

$UnityEditorPath = (Resolve-Path -LiteralPath $UnityEditorPath).Path
$expectedProductVersion = "${expectedVersion}_${expectedRevision}"
$actualProductVersion = (Get-Item -LiteralPath $UnityEditorPath).VersionInfo.ProductVersion
if ($actualProductVersion -ne $expectedProductVersion) {
    throw "Unity Editor product version '$actualProductVersion' does not match pinned '$expectedProductVersion'."
}

$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutputPath
if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw 'The demo output path must include a parent directory.'
}
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$resolvedLogPath = [System.IO.Path]::GetFullPath($LogPath)
$logDirectory = Split-Path -Parent $resolvedLogPath
if (-not [string]::IsNullOrWhiteSpace($logDirectory)) {
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
}

# This intentionally uses a normal BuildPipeline player. Do not add
# -runTests/-testPlatform/-testResults here: Unity Test Runner players use a
# local result channel and are not the offline graphical demo.
$unityArguments = @(
    '-batchmode',
    '-quit',
    '-accept-apiupdate',
    '-projectPath', "`"$projectPath`"",
    '-executeMethod', 'ReactorGame.Unity.Editor.BuildDemo.Run',
    '-canduDemoOutputPath', "`"$resolvedOutputPath`"",
    '-logFile', "`"$resolvedLogPath`""
)
$unityStartInfo = New-Object System.Diagnostics.ProcessStartInfo
$unityStartInfo.FileName = $UnityEditorPath
$unityStartInfo.Arguments = $unityArguments -join ' '
$unityStartInfo.UseShellExecute = $false
$unityStartInfo.CreateNoWindow = $true
$unityProcess = [System.Diagnostics.Process]::Start($unityStartInfo)
try {
    $unityProcess.WaitForExit()
    $unityExitCode = $unityProcess.ExitCode
}
finally {
    $unityProcess.Dispose()
}

$demoDataPath = Join-Path $outputDirectory (
    [System.IO.Path]::GetFileNameWithoutExtension($resolvedOutputPath) + '_Data')
if ($unityExitCode -ne 0 -or
    -not (Test-Path -LiteralPath $resolvedOutputPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $demoDataPath -PathType Container)) {
    if (Test-Path -LiteralPath $resolvedLogPath) {
        Get-Content -Tail 200 -LiteralPath $resolvedLogPath
    }
    throw "Unity offline graphical demo build failed (exit=$unityExitCode)."
}

Write-Output "Offline Unity graphical demo built without Test Runner networking: $resolvedOutputPath"
