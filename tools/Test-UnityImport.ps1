#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $UnityEditorPath,

    [ValidateNotNullOrEmpty()]
    [string] $ArtifactsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-unity-import-artifacts'),

    [ValidateNotNullOrEmpty()]
    [string] $LogPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-unity-import.log')
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

& (Join-Path $PSScriptRoot 'Prepare-UnityCore.ps1') -ArtifactsPath $ArtifactsPath

$resolvedArtifactsPath = [System.IO.Path]::GetFullPath($ArtifactsPath)
$resolvedLogPath = [System.IO.Path]::GetFullPath($LogPath)
$smokeToken = [System.Guid]::NewGuid().ToString('N')
$smokeMarkerPath = Join-Path $resolvedArtifactsPath "unity-smoke-$smokeToken.success"
$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-accept-apiupdate',
    '-projectPath', "`"$projectPath`"",
    '-executeMethod', 'ReactorGame.Unity.Editor.ImportSmoke.Run',
    '-canduSmokeMarkerPath', "`"$smokeMarkerPath`"",
    '-canduSmokeMarkerToken', $smokeToken,
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
$smokeMarkerMatches = (Test-Path -LiteralPath $smokeMarkerPath -PathType Leaf) -and
    ((Get-Content -Raw -LiteralPath $smokeMarkerPath) -eq $smokeToken)
if ($unityExitCode -ne 0 -or -not $smokeMarkerMatches) {
    if (Test-Path -LiteralPath $resolvedLogPath) {
        Get-Content -Tail 200 -LiteralPath $resolvedLogPath
    }
    if ($unityExitCode -ne 0) {
        throw "Unity import/compile smoke exited with code $unityExitCode."
    }

    throw 'Unity exited without producing the import/compile smoke success marker.'
}

Write-Output "Unity import/compile smoke passed. Log: $resolvedLogPath"
