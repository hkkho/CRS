[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version5SourcePath,

    [Parameter(Mandatory)]
    [string]$ArtifactsPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-ExistingDirectoryPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description
    )

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Container) "$Description does not exist or is not a directory: $Path"
    return (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
}

function Get-PlannedDirectoryPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return [IO.Path]::GetFullPath($Path)
}

function Test-PathInside {
    param(
        [Parameter(Mandatory)]
        [string]$CandidatePath,

        [Parameter(Mandatory)]
        [string]$ParentPath
    )

    $candidate = [IO.Path]::GetFullPath($CandidatePath).TrimEnd('\', '/')
    $parent = [IO.Path]::GetFullPath($ParentPath).TrimEnd('\', '/')
    if ([string]::Equals($candidate, $parent, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    return $candidate.StartsWith($parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-OutsideRepository {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    Assert-Condition (-not (Test-PathInside -CandidatePath $Path -ParentPath $RepositoryRoot)) "$Description must be outside the Git repository: $Path"
}

function Get-Sha256HexFromBytes {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes
    )

    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return (([BitConverter]::ToString($algorithm.ComputeHash($Bytes))) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Get-FileSha256 {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash.ToLowerInvariant()
}

function ConvertTo-WindowsCommandLineArgument {
    param(
        [Parameter(Mandatory)]
        [string]$Value
    )

    $builder = New-Object Text.StringBuilder
    [void]$builder.Append('"')
    $backslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ([int][char]$character -eq 92) {
            $backslashes++
            continue
        }

        if ([int][char]$character -eq 34) {
            for ($index = 0; $index -lt (($backslashes * 2) + 1); $index++) {
                [void]$builder.Append([char]92)
            }
            [void]$builder.Append([char]34)
            $backslashes = 0
            continue
        }

        for ($index = 0; $index -lt $backslashes; $index++) {
            [void]$builder.Append([char]92)
        }
        [void]$builder.Append($character)
        $backslashes = 0
    }

    for ($index = 0; $index -lt ($backslashes * 2); $index++) {
        [void]$builder.Append([char]92)
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function Invoke-NativeChecked {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Description
    )

    $command = @(Get-Command -Name $FilePath -CommandType Application -ErrorAction Stop)[0]
    $startInfo = New-Object Diagnostics.ProcessStartInfo
    $startInfo.FileName = $command.Path
    $startInfo.Arguments = (($Arguments | ForEach-Object {
        ConvertTo-WindowsCommandLineArgument -Value $_
    }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object Diagnostics.Process
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result
    $exitCode = $process.ExitCode
    $process.Dispose()

    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode.$([Environment]::NewLine)stdout:$([Environment]::NewLine)$stdout$([Environment]::NewLine)stderr:$([Environment]::NewLine)$stderr"
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Stdout = $stdout.Trim()
        Stderr = $stderr.Trim()
    }
}

function Get-GitValue {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$Description
    )

    $result = Invoke-NativeChecked -FilePath 'git' -Arguments (@('-C', $RepositoryPath) + $Arguments) -Description $Description
    Assert-Condition ([string]::IsNullOrWhiteSpace($result.Stderr)) "$Description emitted unexpected stderr: $($result.Stderr)"
    return $result.Stdout.Trim()
}

function Assert-CleanGitFile {
    param(
        [Parameter(Mandatory)]
        [string]$RepositoryPath,

        [Parameter(Mandatory)]
        [string]$RelativePath
    )

    $command = @(Get-Command -Name git -CommandType Application -ErrorAction Stop)[0]
    & $command.Path -C $RepositoryPath diff --no-ext-diff --quiet HEAD -- $RelativePath
    Assert-Condition ($LASTEXITCODE -eq 0) "Upstream source file is modified or cannot be checked: $RelativePath"
}

function Convert-CheckoutBytesToCanonicalLf {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes,

        [Parameter(Mandatory)]
        [string]$Description
    )

    $canonical = [Collections.Generic.List[byte]]::new()
    for ($index = 0; $index -lt $Bytes.Length; $index++) {
        if ($Bytes[$index] -eq 13) {
            Assert-Condition (($index + 1) -lt $Bytes.Length -and $Bytes[$index + 1] -eq 10) "$Description contains a bare carriage return at byte $index."
            [void]$canonical.Add(10)
            $index++
        }
        else {
            [void]$canonical.Add($Bytes[$index])
        }
    }

    return $canonical.ToArray()
}

function Assert-EmptyStderr {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Stderr,

        [Parameter(Mandatory)]
        [string]$Description
    )

    Assert-Condition ([string]::IsNullOrWhiteSpace($Stderr)) "$Description emitted unexpected stderr: $Stderr"
}

function Assert-SafeRelativePath {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description
    )

    Assert-Condition (-not [IO.Path]::IsPathRooted($Path)) "$Description must be relative: $Path"
    $segments = $Path.Replace('/', '\').Split('\')
    Assert-Condition (-not ($segments -contains '..')) "$Description must not traverse parents: $Path"
}

function Convert-ScientificTokenToDouble {
    param(
        [Parameter(Mandatory)]
        [string]$Value,

        [Parameter(Mandatory)]
        [string]$Description
    )

    try {
        return [double]::Parse($Value, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "$Description is not a finite scientific value: $Value"
    }
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..') -ErrorAction Stop).ProviderPath
$caseDescriptorPath = Join-Path $repositoryRoot 'reference\donjon5\P1-T03-AFA_180_310_type1_dual.case.json'
Assert-Condition (Test-Path -LiteralPath $caseDescriptorPath -PathType Leaf) "Missing P1-T03 case descriptor: $caseDescriptorPath"
$case = Get-Content -LiteralPath $caseDescriptorPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop

Assert-Condition ($case.format -eq 'candu.reference-donjon5-smoke-case/v1') "Unsupported P1-T03 case descriptor format: $($case.format)"
Assert-Condition ($case.task_id -eq 'P1-T03') 'P1-T03 case descriptor has an unexpected task ID.'
Assert-Condition ($case.version5_source.ref -eq 'v5.1.0') 'P1-T03 case descriptor must pin Version5 v5.1.0.'
Assert-Condition ($case.prepared_group_constants.format -eq 'HDF5') 'P1-T03 case descriptor must identify HDF5 prepared group constants.'
Assert-Condition ($case.execution.container_platform -eq 'linux/amd64') 'P1-T03 case descriptor must require linux/amd64.'
Assert-Condition ($case.execution.container_network -eq 'none') 'P1-T03 case descriptor must require an offline container.'
Assert-Condition ($case.execution.image_pull_policy -eq 'never') 'P1-T03 case descriptor must forbid image pulls.'
Assert-Condition ($case.execution.program_path -eq '/dragon/5.1/Donjon/bin/Linux_x86_64/Donjon') 'P1-T03 case descriptor has an unexpected DONJON executable path.'
Assert-Condition ($case.version5_source.staged_files.Count -eq 4) 'P1-T03 case descriptor must stage exactly the deck, assertion procedure, and two HDF5 inputs.'
Assert-Condition ($case.execution.convergence.maxout -eq 200) 'P1-T03 case descriptor must preserve MAXOUT=200.'
Assert-Condition ($case.execution.convergence.epsout -eq '1.00E-04') 'P1-T03 case descriptor must preserve EPSOUT=1.00E-04.'
Assert-Condition ($case.execution.convergence.expected_final_outer_iteration -eq 15) 'P1-T03 case descriptor must require final outer iteration 15.'
Assert-Condition ($case.execution.convergence.fatal_markers.Count -eq 2) 'P1-T03 case descriptor must list two FLDDIR convergence failures.'
Assert-Condition ($case.execution.runtime_warning_policy.required_count -eq 2) 'P1-T03 case descriptor must require exactly two allowlisted runtime warnings.'
Assert-Condition ($case.execution.runtime_warning_policy.allowed_exact_text -eq 'SPHAPX: WARNING -- Record MEDIA_VOLUME is missing in the Apex file. Volume set to 1.0') 'P1-T03 case descriptor has an unexpected runtime-warning allowlist.'

$expectedStagedNames = @('AFA_180_310_type1_dual.x2m', 'assertS.c2m', 'AFA_180.h5', 'AFA_310.h5')
for ($index = 0; $index -lt $expectedStagedNames.Count; $index++) {
    Assert-Condition ($case.version5_source.staged_files[$index].staged_name -eq $expectedStagedNames[$index]) "Unexpected staged file name at index $index."
}

$version5Root = Get-ExistingDirectoryPath -Path $Version5SourcePath -Description 'Version5 source path'
$artifactsRoot = Get-PlannedDirectoryPath -Path $ArtifactsPath
Assert-OutsideRepository -Path $version5Root -Description 'Version5 source path' -RepositoryRoot $repositoryRoot
Assert-OutsideRepository -Path $artifactsRoot -Description 'Artifacts path' -RepositoryRoot $repositoryRoot
Assert-Condition (-not (Test-Path -LiteralPath $artifactsRoot)) "Artifacts path must not already exist; choose a new external directory: $artifactsRoot"

$sourceCommit = Get-GitValue -RepositoryPath $version5Root -Arguments @('rev-parse', 'HEAD') -Description 'Version5 source commit check'
$sourceTree = Get-GitValue -RepositoryPath $version5Root -Arguments @('rev-parse', 'HEAD^{tree}') -Description 'Version5 source tree check'
$tagCommit = Get-GitValue -RepositoryPath $version5Root -Arguments @('rev-parse', 'v5.1.0^{commit}') -Description 'Version5 v5.1.0 tag check'
Assert-Condition ($sourceCommit -eq $case.version5_source.commit) "Version5 source commit mismatch: expected $($case.version5_source.commit), found $sourceCommit."
Assert-Condition ($sourceTree -eq $case.version5_source.tree) "Version5 source tree mismatch: expected $($case.version5_source.tree), found $sourceTree."
Assert-Condition ($tagCommit -eq $case.version5_source.commit) "Version5 v5.1.0 tag mismatch: expected $($case.version5_source.commit), found $tagCommit."

New-Item -ItemType Directory -Path $artifactsRoot -ErrorAction Stop | Out-Null
$stagedRoot = Join-Path $artifactsRoot 'staged'
$outputRoot = Join-Path $artifactsRoot 'output'
$metadataRoot = Join-Path $artifactsRoot 'metadata'
foreach ($directory in @($stagedRoot, $outputRoot, $metadataRoot)) {
    New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
}

$stagedFileRecords = [Collections.Generic.List[object]]::new()
$stagedNames = @{}
foreach ($file in $case.version5_source.staged_files) {
    Assert-SafeRelativePath -Path $file.source_path -Description 'Upstream source path'
    Assert-SafeRelativePath -Path $file.staged_name -Description 'Staged file name'
    Assert-Condition ([IO.Path]::GetFileName($file.staged_name) -eq $file.staged_name) "Staged file name must not contain a directory: $($file.staged_name)"
    Assert-Condition (-not $stagedNames.ContainsKey($file.staged_name)) "P1-T03 case descriptor repeats a staged file name: $($file.staged_name)"
    $stagedNames[$file.staged_name] = $true

    $sourceFilePath = [IO.Path]::GetFullPath((Join-Path $version5Root $file.source_path))
    $stagedFilePath = [IO.Path]::GetFullPath((Join-Path $stagedRoot $file.staged_name))
    Assert-Condition (Test-PathInside -CandidatePath $sourceFilePath -ParentPath $version5Root) "Upstream source path escapes the Version5 checkout: $($file.source_path)"
    Assert-Condition (Test-PathInside -CandidatePath $stagedFilePath -ParentPath $stagedRoot) "Staged file path escapes the artifact directory: $($file.staged_name)"
    Assert-Condition (Test-Path -LiteralPath $sourceFilePath -PathType Leaf) "Missing required upstream source file: $($file.source_path)"
    Assert-CleanGitFile -RepositoryPath $version5Root -RelativePath $file.source_path

    $gitBlob = Get-GitValue -RepositoryPath $version5Root -Arguments @('rev-parse', "HEAD:$($file.source_path)") -Description "Git blob check for $($file.source_path)"
    $gitObjectType = Get-GitValue -RepositoryPath $version5Root -Arguments @('cat-file', '-t', "HEAD:$($file.source_path)") -Description "Git object type check for $($file.source_path)"
    Assert-Condition ($gitObjectType -eq 'blob') "Required upstream source object is not a blob: $($file.source_path)"
    Assert-Condition ($gitBlob -eq $file.git_blob) "Git blob mismatch for $($file.source_path): expected $($file.git_blob), found $gitBlob."

    [byte[]]$checkoutBytes = [IO.File]::ReadAllBytes($sourceFilePath)
    [byte[]]$stagedBytes = @()
    [string]$expectedSha256 = ''
    [Int64]$expectedSize = 0
    switch ([string]$file.content_kind) {
        'canonical-lf-text' {
            $stagedBytes = Convert-CheckoutBytesToCanonicalLf -Bytes $checkoutBytes -Description $file.source_path
            $expectedSha256 = $file.canonical_lf_sha256
            $expectedSize = [Int64]$file.canonical_lf_size_bytes
        }
        'binary' {
            $stagedBytes = $checkoutBytes
            $expectedSha256 = $file.sha256
            $expectedSize = [Int64]$file.size_bytes
        }
        default {
            throw "Unsupported P1-T03 staged content kind for $($file.source_path): $($file.content_kind)"
        }
    }

    Assert-Condition ($stagedBytes.Length -eq $expectedSize) "Verified source size mismatch for $($file.source_path): expected $expectedSize, found $($stagedBytes.Length)."
    $stagedSha256 = Get-Sha256HexFromBytes -Bytes $stagedBytes
    Assert-Condition ($stagedSha256 -eq $expectedSha256) "Verified source SHA-256 mismatch for $($file.source_path): expected $expectedSha256, found $stagedSha256."
    [IO.File]::WriteAllBytes($stagedFilePath, $stagedBytes)
    Assert-Condition ((Get-FileSha256 -Path $stagedFilePath) -eq $expectedSha256) "Staged file SHA-256 mismatch after write: $($file.staged_name)"

    $stagedFileRecords.Add([ordered]@{
        source_path = $file.source_path
        staged_name = $file.staged_name
        content_kind = $file.content_kind
        git_blob = $gitBlob
        sha256 = $expectedSha256
        size_bytes = $expectedSize
    })
}

$deckText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes((Join-Path $stagedRoot 'AFA_180_310_type1_dual.x2m')))
$expectedAssertionText = "assertS FLUX :: 'K-EFFECTIVE' 1 $($case.execution.upstream_assertion.expected_value) ;"
Assert-Condition ($deckText.IndexOf($expectedAssertionText, [StringComparison]::Ordinal) -ge 0) 'The staged DONJON deck does not contain the unchanged upstream K-EFFECTIVE assertion.'

$image = $case.execution.container_image
$imageInspect = Invoke-NativeChecked -FilePath 'docker' -Arguments @('image', 'inspect', $image, '--format', '{{json .}}') -Description 'Pinned DONJON Docker image inspection'
Assert-EmptyStderr -Stderr $imageInspect.Stderr -Description 'Pinned DONJON Docker image inspection'
$imageInfo = $imageInspect.Stdout | ConvertFrom-Json -ErrorAction Stop
Assert-Condition (@($imageInfo.RepoDigests) -contains $image) "Pinned DONJON Docker image digest is not present locally: $image"
Assert-Condition ($imageInfo.Os -eq 'linux' -and $imageInfo.Architecture -eq 'amd64') "Pinned DONJON Docker image is not linux/amd64: $($imageInfo.Os)/$($imageInfo.Architecture)"

$dockerVersion = Invoke-NativeChecked -FilePath 'docker' -Arguments @('version', '--format', '{{json .}}') -Description 'Docker version inspection'
Assert-EmptyStderr -Stderr $dockerVersion.Stderr -Description 'Docker version inspection'

$environmentProbe = @'
set -eu
uname -m
cat /etc/os-release
gfortran --version | head -n 1
sha256sum /dragon/5.1/Donjon/bin/Linux_x86_64/Donjon
'@
$environment = Invoke-NativeChecked -FilePath 'docker' -Arguments @('run', '--rm', '--platform', 'linux/amd64', '--network', 'none', '--pull', 'never', '--read-only', '--tmpfs', '/tmp:rw,noexec,nosuid,nodev,size=64m', '--env', 'OMP_NUM_THREADS=1', '--env', 'TZ=UTC', '--env', 'LC_ALL=C', '--env', 'LANG=C', '--entrypoint', '/bin/sh', $image, '-lc', $environmentProbe) -Description 'Offline DONJON environment inspection'
Assert-EmptyStderr -Stderr $environment.Stderr -Description 'Offline DONJON environment inspection'
Assert-Condition ([regex]::IsMatch($environment.Stdout, '(?m)^x86_64$')) 'Pinned DONJON container did not report an amd64 userland.'
Assert-Condition ([regex]::IsMatch($environment.Stdout, '(?m)^GNU Fortran .* 13\.3\.0$')) 'Pinned DONJON image does not report the expected gfortran 13.3.0 compiler.'
$programHashMatch = [regex]::Match($environment.Stdout, '(?m)^(?<hash>[0-9a-f]{64})\s+/dragon/5\.1/Donjon/bin/Linux_x86_64/Donjon$')
Assert-Condition $programHashMatch.Success 'DONJON executable hash was not emitted by the pinned image.'
$programSha256 = $programHashMatch.Groups['hash'].Value
Assert-Condition ($programSha256 -eq $case.execution.program_sha256) "DONJON executable hash mismatch: expected $($case.execution.program_sha256), found $programSha256."

$containerRun = @'
set -eu
test -r /input/AFA_180_310_type1_dual.x2m
test -r /input/assertS.c2m
test -r /input/AFA_180.h5
test -r /input/AFA_310.h5
mkdir -p /work/run
cp /input/AFA_180_310_type1_dual.x2m /input/assertS.c2m /input/AFA_180.h5 /input/AFA_310.h5 /work/run/
cd /work/run
exec /dragon/5.1/Donjon/bin/Linux_x86_64/Donjon < AFA_180_310_type1_dual.x2m > /output/AFA_180_310_type1_dual.result 2> /output/AFA_180_310_type1_dual.stderr
'@
$runStartedUtc = [DateTimeOffset]::UtcNow.ToString('o')
$run = Invoke-NativeChecked -FilePath 'docker' -Arguments @('run', '--rm', '--platform', 'linux/amd64', '--network', 'none', '--pull', 'never', '--read-only', '--tmpfs', '/tmp:rw,noexec,nosuid,nodev,size=64m', '--tmpfs', '/work:rw,noexec,nosuid,nodev,size=64m', '--env', 'OMP_NUM_THREADS=1', '--env', 'TZ=UTC', '--env', 'LC_ALL=C', '--env', 'LANG=C', '--mount', "type=bind,source=$stagedRoot,target=/input,readonly", '--mount', "type=bind,source=$outputRoot,target=/output", '--entrypoint', '/bin/sh', $image, '-lc', $containerRun) -Description 'P1-T03 offline DONJON AFA_180_310_type1_dual run'
$runEndedUtc = [DateTimeOffset]::UtcNow.ToString('o')
Assert-EmptyStderr -Stderr $run.Stderr -Description 'P1-T03 offline DONJON AFA_180_310_type1_dual run'
Assert-Condition ([string]::IsNullOrWhiteSpace($run.Stdout)) "P1-T03 offline DONJON container emitted unexpected stdout: $($run.Stdout)"

$resultPath = Join-Path $outputRoot 'AFA_180_310_type1_dual.result'
Assert-Condition (Test-Path -LiteralPath $resultPath -PathType Leaf) "DONJON did not produce the required result file: $resultPath"
$resultItem = Get-Item -LiteralPath $resultPath -ErrorAction Stop
Assert-Condition ($resultItem.Length -gt 0) 'DONJON produced an empty result file.'
$resultText = [IO.File]::ReadAllText($resultPath)
$donjonStderrPath = Join-Path $outputRoot 'AFA_180_310_type1_dual.stderr'
Assert-Condition (Test-Path -LiteralPath $donjonStderrPath -PathType Leaf) "DONJON did not produce the required stderr file: $donjonStderrPath"
$donjonStderrItem = Get-Item -LiteralPath $donjonStderrPath -ErrorAction Stop
$donjonStderr = [IO.File]::ReadAllText($donjonStderrPath)
Assert-Condition ($donjonStderrItem.Length -eq 0) "DONJON program stderr must be empty; found $($donjonStderrItem.Length) bytes."
Assert-EmptyStderr -Stderr $donjonStderr -Description 'P1-T03 DONJON program stderr'

$failurePatterns = @(
    '(?im)\b(?:x?abort|test\s+fail(?:ure|ed)|kernel\s+error|fatal\s+error|input\s+error)\b',
    '(?im)\b(?:non[- ]?converg(?:ence|ed|ing)?|not\s+converg(?:ed|ing)?)\b',
    '(?i)(?<![A-Za-z0-9_-])nan(?![A-Za-z0-9_-])',
    '(?i)(?<![A-Za-z0-9_-])[+-]?(?:inf|infinity)(?![A-Za-z0-9_-])'
)
foreach ($pattern in $failurePatterns) {
    Assert-Condition (-not [regex]::IsMatch($resultText, $pattern)) "DONJON result contains a fatal diagnostic matching: $pattern"
}

foreach ($fatalMarker in $case.execution.convergence.fatal_markers) {
    Assert-Condition ($resultText.IndexOf($fatalMarker, [StringComparison]::Ordinal) -lt 0) "DONJON result contains fatal FLDDIR convergence diagnostic: $fatalMarker"
}

$runtimeCompletionPattern = '(?m)^\s*>\|' + [regex]::Escape($case.execution.runtime_markers.completion) + '\s*\|>\d+\s*$'
$runtimeCompletionMatches = [regex]::Matches($resultText, $runtimeCompletionPattern)
Assert-Condition ($runtimeCompletionMatches.Count -eq 1) "Expected one runtime completion marker, found $($runtimeCompletionMatches.Count)."

$runtimeSuccessPattern = '(?m)^\s*>\|' + [regex]::Escape($case.execution.runtime_markers.successful_assertion_prefix) + '\s+(?<delta>[+-]?(?:[0-9]+(?:\.[0-9]+)?|\.[0-9]+)[Ee][+-][0-9]+)\s*\|>\d+\s*$'
$successfulAssertions = [regex]::Matches($resultText, $runtimeSuccessPattern)
Assert-Condition ($successfulAssertions.Count -eq 1) "Expected one upstream runtime TEST SUCCESSFUL assertion marker, found $($successfulAssertions.Count)."
$successfulAssertionDelta = $successfulAssertions[0].Groups['delta'].Value.ToUpperInvariant()

$normalEndPattern = '(?im)^\s*' + [regex]::Escape($case.execution.runtime_markers.normal_end) + '\s*$'
$normalEndMatches = [regex]::Matches($resultText, $normalEndPattern)
Assert-Condition ($normalEndMatches.Count -eq 1) "Expected one DONJON normal-end marker, found $($normalEndMatches.Count)."

$runtimeWarningMatches = [regex]::Matches($resultText, '(?m)^\s*(?<value>[^\r\n]*\bWARNING\b[^\r\n]*)$')
Assert-Condition ($runtimeWarningMatches.Count -eq $case.execution.runtime_warning_policy.required_count) "Expected $($case.execution.runtime_warning_policy.required_count) runtime WARNING lines, found $($runtimeWarningMatches.Count)."
$runtimeWarnings = [Collections.Generic.List[string]]::new()
foreach ($warningMatch in $runtimeWarningMatches) {
    $runtimeWarning = $warningMatch.Groups['value'].Value.Trim()
    Assert-Condition ($runtimeWarning -eq $case.execution.runtime_warning_policy.allowed_exact_text) "DONJON emitted a non-allowlisted runtime WARNING: $runtimeWarning"
    $runtimeWarnings.Add($runtimeWarning)
}

$effectiveMultiplicationMatches = [regex]::Matches($resultText, '(?im)^\s*FLDDIR:\s+EFFECTIVE\s+MULTIPLICATION\s+FACTOR\s*=\s*(?<value>[+-]?[0-9]+(?:\.[0-9]+)?E[+-][0-9]+)\s*$')
Assert-Condition ($effectiveMultiplicationMatches.Count -eq 1) "Expected one effective multiplication factor, found $($effectiveMultiplicationMatches.Count)."
$actualEffectiveMultiplicationFactor = $effectiveMultiplicationMatches[0].Groups['value'].Value.ToUpperInvariant()
Assert-Condition ($actualEffectiveMultiplicationFactor -eq $case.execution.expected_effective_multiplication_factor) "Effective multiplication factor mismatch: expected $($case.execution.expected_effective_multiplication_factor), found $actualEffectiveMultiplicationFactor."

$maxoutMatches = [regex]::Matches($resultText, '(?im)^\s*MAXOUT\s+(?<value>[0-9]+)\s+\(MAXIMUM NUMBER OF OUTER ITERATIONS\)\s*$')
Assert-Condition ($maxoutMatches.Count -eq 1) "Expected one MAXOUT record, found $($maxoutMatches.Count)."
$actualMaxout = [int]$maxoutMatches[0].Groups['value'].Value
Assert-Condition ($actualMaxout -eq $case.execution.convergence.maxout) "MAXOUT mismatch: expected $($case.execution.convergence.maxout), found $actualMaxout."

$epsoutMatches = [regex]::Matches($resultText, '(?im)^\s*EPSOUT\s+(?<value>[+-]?[0-9]+(?:\.[0-9]+)?E[+-][0-9]+)\s+\(OUTER ITERATION (?:KEFF|FLUX) EPSILON\)\s*$')
Assert-Condition ($epsoutMatches.Count -eq 2) "Expected two EPSOUT records, found $($epsoutMatches.Count)."
$emittedEpsout = $epsoutMatches[0].Groups['value'].Value.ToUpperInvariant()
foreach ($epsoutMatch in $epsoutMatches) {
    $epsoutValue = $epsoutMatch.Groups['value'].Value.ToUpperInvariant()
    Assert-Condition ($epsoutValue -eq $case.execution.convergence.epsout) "EPSOUT mismatch: expected $($case.execution.convergence.epsout), found $epsoutValue."
}

$outerSectionMatch = [regex]::Match($resultText, '(?ms)^\s*FLDDIR:\s+ITERATIVE PROCEDURE BASED ON PRECONDITIONED POWER METHOD.*?^\s*FLDDIR:\s+CPU TIME USED TO SOLVE THE TRIANGULAR LINEAR SYSTEMS')
Assert-Condition $outerSectionMatch.Success 'DONJON result is missing the FLDDIR outer-iteration section.'
$scientificTokenPattern = '^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[Ee][+-]?[0-9]+)?$'
$outerRows = [Collections.Generic.List[object]]::new()
foreach ($line in ($outerSectionMatch.Value -split '\r?\n')) {
    $tokens = @($line.Trim() -split '\s+' | Where-Object { $_ -ne '' })
    if ($tokens.Count -lt 4 -or $tokens[0] -notmatch '^[0-9]+$' -or $tokens[-1] -notmatch '^[0-9]+$') {
        continue
    }

    $isOuterRow = $true
    for ($tokenIndex = 1; $tokenIndex -lt ($tokens.Count - 1); $tokenIndex++) {
        if ($tokens[$tokenIndex] -notmatch $scientificTokenPattern) {
            $isOuterRow = $false
            break
        }
    }
    if ($isOuterRow) {
        $outerRows.Add([pscustomobject]@{
            iteration = [int]$tokens[0]
            dels = $tokens[$tokens.Count - 3].ToUpperInvariant()
            delt = $tokens[$tokens.Count - 2].ToUpperInvariant()
        })
    }
}

Assert-Condition ($outerRows.Count -gt 0) 'DONJON result has no parseable FLDDIR outer-iteration rows.'
$finalOuterRow = $outerRows[$outerRows.Count - 1]
Assert-Condition ($finalOuterRow.iteration -eq $case.execution.convergence.expected_final_outer_iteration) "Final FLDDIR iteration mismatch: expected $($case.execution.convergence.expected_final_outer_iteration), found $($finalOuterRow.iteration)."
Assert-Condition ($finalOuterRow.dels -eq $case.execution.convergence.expected_final_dels) "Final FLDDIR DELS mismatch: expected $($case.execution.convergence.expected_final_dels), found $($finalOuterRow.dels)."
Assert-Condition ($finalOuterRow.delt -eq $case.execution.convergence.expected_final_delt) "Final FLDDIR DELT mismatch: expected $($case.execution.convergence.expected_final_delt), found $($finalOuterRow.delt)."
$finalDeltValue = Convert-ScientificTokenToDouble -Value $finalOuterRow.delt -Description 'Final FLDDIR DELT'
$emittedEpsoutValue = Convert-ScientificTokenToDouble -Value $emittedEpsout -Description 'FLDDIR EPSOUT'
Assert-Condition ($finalDeltValue -le $emittedEpsoutValue) "Final FLDDIR DELT $($finalOuterRow.delt) exceeds emitted EPSOUT $emittedEpsout."

$runRecordPath = Join-Path $metadataRoot 'P1-T03-AFA_180_310_type1_dual-run-record.json'
$runRecord = [ordered]@{
    format = 'candu.reference-donjon5-smoke-run/v1'
    task_id = 'P1-T03'
    case_descriptor = [ordered]@{
        path = 'reference/donjon5/P1-T03-AFA_180_310_type1_dual.case.json'
        sha256 = Get-FileSha256 -Path $caseDescriptorPath
    }
    version5_source = [ordered]@{
        path = $version5Root
        commit = $sourceCommit
        tree = $sourceTree
        tag_commit = $tagCommit
        staged_files = $stagedFileRecords.ToArray()
    }
    prepared_group_constants = [ordered]@{
        format = $case.prepared_group_constants.format
        source_storage = $case.prepared_group_constants.source_storage
        assets = @($case.prepared_group_constants.assets)
        external_staging_path = $stagedRoot
    }
    execution_environment = [ordered]@{
        docker_version = $dockerVersion.Stdout
        image = $image
        image_id = $imageInfo.Id
        image_repo_digests = @($imageInfo.RepoDigests)
        platform = $case.execution.container_platform
        offline_network = $case.execution.container_network
        image_pull_policy = $case.execution.image_pull_policy
        deterministic_environment = $case.execution.deterministic_environment
        container_probe = $environment.Stdout
        program_path = $case.execution.program_path
        program_sha256 = $programSha256
        runner_sha256 = Get-FileSha256 -Path $PSCommandPath
    }
    run = [ordered]@{
        started_utc = $runStartedUtc
        ended_utc = $runEndedUtc
        docker_stdout = $run.Stdout
        docker_stderr = $run.Stderr
        result_path = $resultPath
        result_sha256 = Get-FileSha256 -Path $resultPath
        result_size_bytes = $resultItem.Length
        donjon_stderr_path = $donjonStderrPath
        donjon_stderr_sha256 = Get-FileSha256 -Path $donjonStderrPath
        donjon_stderr_size_bytes = $donjonStderrItem.Length
        donjon_stderr = $donjonStderr
        result_hash_policy = $case.execution.raw_result_policy
        effective_multiplication_factor = $actualEffectiveMultiplicationFactor
        upstream_assertion = $case.execution.upstream_assertion
        successful_assertion_count = $successfulAssertions.Count
        runtime_markers = [ordered]@{
            completion = [ordered]@{
                expected = $case.execution.runtime_markers.completion
                count = $runtimeCompletionMatches.Count
            }
            successful_assertion = [ordered]@{
                expected_prefix = $case.execution.runtime_markers.successful_assertion_prefix
                count = $successfulAssertions.Count
                emitted_delta = $successfulAssertionDelta
            }
            normal_end = [ordered]@{
                expected = $case.execution.runtime_markers.normal_end
                count = $normalEndMatches.Count
            }
        }
        runtime_warnings = [ordered]@{
            allowed_exact_text = $case.execution.runtime_warning_policy.allowed_exact_text
            required_count = $case.execution.runtime_warning_policy.required_count
            actual_count = $runtimeWarnings.Count
            values = $runtimeWarnings.ToArray()
        }
        convergence = [ordered]@{
            maxout = $actualMaxout
            epsout = $emittedEpsout
            final_outer_iteration = $finalOuterRow.iteration
            final_dels = $finalOuterRow.dels
            final_delt = $finalOuterRow.delt
            final_delt_less_than_or_equal_epsout = ($finalDeltValue -le $emittedEpsoutValue)
        }
    }
}
[IO.File]::WriteAllText($runRecordPath, ($runRecord | ConvertTo-Json -Depth 12), (New-Object Text.UTF8Encoding($false)))

Write-Output "PASS: P1-T03 DONJON AFA_180_310_type1_dual smoke case completed. External record: $runRecordPath"
