#Requires -Version 5.1

[CmdletBinding()]
param(
    [string] $UnityEditorPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-ProcessArgument {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string] $Value
    )

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append([char] 34)
    $backslashCount = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq [char] 92) {
            $backslashCount++
            continue
        }

        if ($character -eq [char] 34) {
            for ($index = 0; $index -lt (($backslashCount * 2) + 1); $index++) {
                [void] $builder.Append([char] 92)
            }

            [void] $builder.Append([char] 34)
            $backslashCount = 0
            continue
        }

        for ($index = 0; $index -lt $backslashCount; $index++) {
            [void] $builder.Append([char] 92)
        }

        [void] $builder.Append($character)
        $backslashCount = 0
    }

    for ($index = 0; $index -lt ($backslashCount * 2); $index++) {
        [void] $builder.Append([char] 92)
    }

    [void] $builder.Append([char] 34)
    return $builder.ToString()
}

function Write-UnityLogTail {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    Write-Output "Unity log tail: $Path"
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Get-Content -LiteralPath $Path -Tail 200
    }
    else {
        Write-Output 'Unity did not create a log file.'
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'unity/ReactorGame'))
$toolchainPath = Join-Path $repositoryRoot 'unity/toolchain.json'
$prepareScriptPath = Join-Path $PSScriptRoot 'Prepare-UnityCore.ps1'

if (-not (Test-Path -LiteralPath $toolchainPath -PathType Leaf)) {
    throw "Unity toolchain file was not found: $toolchainPath"
}

if (-not (Test-Path -LiteralPath $projectPath -PathType Container)) {
    throw "Unity project was not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $prepareScriptPath -PathType Leaf)) {
    throw "Unity preparation script was not found: $prepareScriptPath"
}

$toolchain = Get-Content -Raw -LiteralPath $toolchainPath | ConvertFrom-Json
$expectedVersion = [string] $toolchain.unityEditor.version
$expectedRevision = [string] $toolchain.unityEditor.revision
if ([string]::IsNullOrWhiteSpace($expectedVersion) -or
    [string]::IsNullOrWhiteSpace($expectedRevision) -or
    $expectedVersion -notmatch '^[0-9A-Za-z._-]+$' -or
    $expectedRevision -notmatch '^[0-9A-Za-z._-]+$') {
    throw 'unity/toolchain.json does not contain safe Unity version and revision pins.'
}

$expectedProductVersion = '{0}_{1}' -f $expectedVersion, $expectedRevision
$resolvedUnityEditorPath = $null
if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $editorCandidates = @()
    $localAppData = [System.Environment]::GetEnvironmentVariable('LOCALAPPDATA')
    if (-not [string]::IsNullOrWhiteSpace($localAppData)) {
        $editorCandidates += Join-Path $localAppData "Unity\Editors\$expectedVersion\Editor\Unity.exe"
    }

    $programFilesRoots = @(
        [System.Environment]::GetEnvironmentVariable('ProgramFiles'),
        [System.Environment]::GetEnvironmentVariable('ProgramFiles(x86)'),
        [System.Environment]::GetEnvironmentVariable('ProgramW6432'),
        'C:\Program Files',
        'C:\Program Files (x86)'
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
    foreach ($programFilesRoot in $programFilesRoots) {
        $editorCandidates += Join-Path $programFilesRoot "Unity\Hub\Editor\$expectedVersion\Editor\Unity.exe"
        $editorCandidates += Join-Path $programFilesRoot "Unity\$expectedVersion\Editor\Unity.exe"
    }

    $existingEditorCandidates = @(
        $editorCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    )
    if ($existingEditorCandidates.Count -eq 0) {
        throw "Unity Editor $expectedVersion was not found. Install the pinned editor or pass -UnityEditorPath."
    }

    $foundVersions = @()
    foreach ($candidatePath in $existingEditorCandidates) {
        $candidateVersion = [string] (Get-Item -LiteralPath $candidatePath).VersionInfo.ProductVersion
        $candidateResolvedPath = (Resolve-Path -LiteralPath $candidatePath).ProviderPath
        $foundVersions += "${candidateResolvedPath}: $candidateVersion"
        if ([string]::Equals($candidateVersion, $expectedProductVersion, [System.StringComparison]::Ordinal)) {
            $resolvedUnityEditorPath = $candidateResolvedPath
            break
        }
    }

    if ($null -eq $resolvedUnityEditorPath) {
        throw "No discovered Unity Editor matches pinned product version '$expectedProductVersion'. Found: $($foundVersions -join '; ')"
    }
}
else {
    if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
        throw "Unity Editor path was not found: $UnityEditorPath"
    }

    $resolvedUnityEditorPath = (Resolve-Path -LiteralPath $UnityEditorPath).ProviderPath
    $actualProductVersion = [string] (Get-Item -LiteralPath $resolvedUnityEditorPath).VersionInfo.ProductVersion
    if (-not [string]::Equals($actualProductVersion, $expectedProductVersion, [System.StringComparison]::Ordinal)) {
        throw "Unity Editor product version '$actualProductVersion' does not match pinned '$expectedProductVersion'."
    }
}

$runId = [System.Guid]::NewGuid().ToString('N')
$runRoot = [System.IO.Path]::GetFullPath(
    (Join-Path ([System.IO.Path]::GetTempPath()) "candu-unity-import-$runId"))
$repositoryPrefix = $repositoryRoot.TrimEnd([char] 92, [char] 47) + [System.IO.Path]::DirectorySeparatorChar
if ([string]::Equals($runRoot, $repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $runRoot.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Temporary Unity smoke paths must not be inside the repository: $runRoot"
}

[System.IO.Directory]::CreateDirectory($runRoot) | Out-Null
$artifactsPath = Join-Path $runRoot 'artifacts'
[System.IO.Directory]::CreateDirectory($artifactsPath) | Out-Null
$logPath = Join-Path $runRoot 'unity.log'
$smokeToken = [System.Guid]::NewGuid().ToString('N')
$smokeMarkerPath = Join-Path $runRoot 'unity-smoke.marker'

& $prepareScriptPath -ArtifactsPath $artifactsPath
if ($LASTEXITCODE -ne 0) {
    throw "Unity preparation exited with code $LASTEXITCODE."
}

$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-accept-apiupdate',
    '-projectPath', $projectPath,
    '-executeMethod', 'ReactorGame.Unity.Editor.ImportSmoke.Run',
    '-canduSmokeMarkerPath', $smokeMarkerPath,
    '-canduSmokeMarkerToken', $smokeToken,
    '-logFile', $logPath
)
$unityStartInfo = New-Object System.Diagnostics.ProcessStartInfo
$unityStartInfo.FileName = $resolvedUnityEditorPath
$unityStartInfo.UseShellExecute = $false
$unityStartInfo.CreateNoWindow = $true
$unityStartInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$unityStartInfo.WorkingDirectory = $projectPath

$argumentListProperty = $unityStartInfo.GetType().GetProperty('ArgumentList')
if ($null -ne $argumentListProperty) {
    $argumentList = $argumentListProperty.GetValue($unityStartInfo, $null)
    foreach ($unityArgument in $unityArguments) {
        [void] $argumentList.Add([string] $unityArgument)
    }
}
else {
    $unityStartInfo.Arguments = ($unityArguments | ForEach-Object {
        ConvertTo-ProcessArgument -Value ([string] $_)
    }) -join ' '
}

$unityProcess = $null
$unityExitCode = $null
try {
    $unityProcess = [System.Diagnostics.Process]::Start($unityStartInfo)
    if ($null -eq $unityProcess) {
        throw 'Unity process could not be started.'
    }

    $unityProcess.WaitForExit()
    $unityExitCode = $unityProcess.ExitCode
}
catch {
    Write-UnityLogTail -Path $logPath
    throw
}
finally {
    if ($null -ne $unityProcess) {
        $unityProcess.Dispose()
    }
}

$markerMatches = $false
$markerReadError = $null
if (Test-Path -LiteralPath $smokeMarkerPath -PathType Leaf) {
    try {
        $markerContent = [System.IO.File]::ReadAllText($smokeMarkerPath)
        $markerMatches = [string]::Equals(
            $markerContent,
            $smokeToken,
            [System.StringComparison]::Ordinal)
    }
    catch {
        $markerReadError = $_.Exception.Message
    }
}

if ($unityExitCode -ne 0 -or -not $markerMatches) {
    Write-Output "Unity import/compile smoke failed (exit=$unityExitCode). Log: $logPath"
    if ($null -ne $markerReadError) {
        Write-Output "The smoke marker could not be read: $markerReadError"
    }

    Write-UnityLogTail -Path $logPath
    if ($unityExitCode -ne 0) {
        throw "Unity import/compile smoke exited with code $unityExitCode."
    }

    throw 'Unity exited without writing the exact import/compile smoke marker token.'
}

Write-Output "Unity import/compile smoke passed. Log: $logPath"
