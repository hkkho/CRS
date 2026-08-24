[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version5SourcePath,

    [Parameter(Mandatory)]
    [string]$CompatibilityLibrarySourcePath,

    [Parameter(Mandatory)]
    [string]$CachePath,

    [Parameter(Mandatory)]
    [string]$ArtifactsPath,

    [Parameter(Mandatory)]
    [string]$AssetRetrievedAtUtc
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

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Container) `
        "$Description does not exist or is not a directory: $Path"
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

    return $candidate.StartsWith(
        $parent + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)
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

    Assert-Condition (-not (Test-PathInside -CandidatePath $Path -ParentPath $RepositoryRoot)) `
        "$Description must be outside the Git repository: $Path"
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

    $result = Invoke-NativeChecked -FilePath 'git' `
        -Arguments (@('-C', $RepositoryPath) + $Arguments) -Description $Description
    Assert-Condition ([string]::IsNullOrWhiteSpace($result.Stderr)) `
        "$Description emitted unexpected stderr: $($result.Stderr)"
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
    $exitCode = $LASTEXITCODE
    Assert-Condition ($exitCode -eq 0) `
        "Upstream source file is modified or cannot be checked: $RelativePath"
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
            Assert-Condition (($index + 1) -lt $Bytes.Length -and $Bytes[$index + 1] -eq 10) `
                "$Description contains a bare carriage return at byte $index."
            [void]$canonical.Add(10)
            $index++
        }
        else {
            [void]$canonical.Add($Bytes[$index])
        }
    }

    return $canonical.ToArray()
}

function Assert-AllowedContainerStderr {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Stderr,

        [Parameter(Mandatory)]
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Stderr)) {
        return
    }

    $normalized = $Stderr.Trim() -replace "`r`n", "`n"
    $allowedPattern = '^Note: The following floating-point exceptions are signalling:(?:\n|\s+)(?:IEEE_UNDERFLOW_FLAG\s+IEEE_DENORMAL|IEEE_DENORMAL\s+IEEE_UNDERFLOW_FLAG)$'
    Assert-Condition ([regex]::IsMatch($normalized, $allowedPattern)) `
        "$Description emitted unexpected stderr: $Stderr"
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..') -ErrorAction Stop).ProviderPath
$caseDescriptorPath = Join-Path $repositoryRoot 'reference\dragon5\P1-T02-lumpSS.case.json'
Assert-Condition (Test-Path -LiteralPath $caseDescriptorPath -PathType Leaf) `
    "Missing P1-T02 case descriptor: $caseDescriptorPath"
$case = Get-Content -LiteralPath $caseDescriptorPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
Assert-Condition ($case.format -eq 'candu.reference-dragon5-smoke-case/v1') `
    "Unsupported P1-T02 case descriptor format: $($case.format)"

$version5Root = Get-ExistingDirectoryPath -Path $Version5SourcePath -Description 'Version5 source path'
$libraryRoot = Get-ExistingDirectoryPath -Path $CompatibilityLibrarySourcePath -Description 'Compatibility library source path'
$cacheRoot = Get-ExistingDirectoryPath -Path $CachePath -Description 'Cache path'
$artifactsRoot = Get-PlannedDirectoryPath -Path $ArtifactsPath

Assert-OutsideRepository -Path $version5Root -Description 'Version5 source path' -RepositoryRoot $repositoryRoot
Assert-OutsideRepository -Path $libraryRoot -Description 'Compatibility library source path' -RepositoryRoot $repositoryRoot
Assert-OutsideRepository -Path $cacheRoot -Description 'Cache path' -RepositoryRoot $repositoryRoot
Assert-OutsideRepository -Path $artifactsRoot -Description 'Artifacts path' -RepositoryRoot $repositoryRoot
Assert-Condition (-not (Test-Path -LiteralPath $artifactsRoot)) `
    "Artifacts path must not already exist; choose a new external directory: $artifactsRoot"

$retrievedAt = [DateTimeOffset]::Parse(
    $AssetRetrievedAtUtc,
    [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()

$sourceCommit = Get-GitValue -RepositoryPath $version5Root -Arguments @('rev-parse', 'HEAD') `
    -Description 'Version5 source commit check'
$sourceTree = Get-GitValue -RepositoryPath $version5Root -Arguments @('rev-parse', 'HEAD^{tree}') `
    -Description 'Version5 source tree check'
Assert-Condition ($sourceCommit -eq $case.version5_source.commit) `
    "Version5 source commit mismatch: expected $($case.version5_source.commit), found $sourceCommit."
Assert-Condition ($sourceTree -eq $case.version5_source.tree) `
    "Version5 source tree mismatch: expected $($case.version5_source.tree), found $sourceTree."

$libraryCommit = Get-GitValue -RepositoryPath $libraryRoot -Arguments @('rev-parse', 'HEAD') `
    -Description 'Compatibility library commit check'
$libraryTree = Get-GitValue -RepositoryPath $libraryRoot -Arguments @('rev-parse', 'HEAD^{tree}') `
    -Description 'Compatibility library tree check'
$compatibilityPin = $case.nuclear_data.compatibility_pin
Assert-Condition ($libraryCommit -eq $compatibilityPin.commit) `
    "Compatibility library commit mismatch: expected $($compatibilityPin.commit), found $libraryCommit."
Assert-Condition ($libraryTree -eq $compatibilityPin.tree) `
    "Compatibility library tree mismatch: expected $($compatibilityPin.tree), found $libraryTree."

$assetPath = $case.nuclear_data.asset_path
$pointer = Get-GitValue -RepositoryPath $libraryRoot -Arguments @('show', "HEAD:$assetPath") `
    -Description 'Compatibility library LFS pointer check'
$oidMatch = [regex]::Match($pointer, '(?m)^oid sha256:(?<value>[0-9a-f]{64})$')
$sizeMatch = [regex]::Match($pointer, '(?m)^size (?<value>[0-9]+)$')
Assert-Condition ($oidMatch.Success -and $sizeMatch.Success) `
    "Compatibility library pointer is malformed for $assetPath."
Assert-Condition ($oidMatch.Groups['value'].Value -eq $compatibilityPin.lfs_oid_sha256) `
    'Compatibility library pointer LFS OID does not match the case descriptor.'
Assert-Condition ([Int64]$sizeMatch.Groups['value'].Value -eq [Int64]$compatibilityPin.compressed_size_bytes) `
    'Compatibility library pointer size does not match the case descriptor.'

$cacheFiles = @(Get-ChildItem -LiteralPath $cacheRoot -File -Force -Recurse -ErrorAction Stop)
$assetFileName = [IO.Path]::GetFileName($assetPath)
$assetCachePath = Join-Path $cacheRoot $assetFileName
Assert-Condition ($cacheFiles.Count -eq 1) `
    'Cache path must contain exactly one file: the selected P1-T02 Draglib asset.'
Assert-Condition (([IO.Path]::GetFullPath($cacheFiles[0].FullName)) -eq ([IO.Path]::GetFullPath($assetCachePath))) `
    "Cache path contains an unexpected file; expected only $assetFileName."
$assetItem = Get-Item -LiteralPath $assetCachePath -ErrorAction Stop
$assetSha256 = Get-FileSha256 -Path $assetCachePath
Assert-Condition ($assetItem.Length -eq [Int64]$compatibilityPin.compressed_size_bytes) `
    "Draglib compressed size mismatch: expected $($compatibilityPin.compressed_size_bytes), found $($assetItem.Length)."
Assert-Condition ($assetSha256 -eq $compatibilityPin.compressed_sha256) `
    "Draglib SHA-256 mismatch: expected $($compatibilityPin.compressed_sha256), found $assetSha256."
Assert-Condition ($assetSha256 -eq $compatibilityPin.lfs_oid_sha256) `
    'Draglib SHA-256 does not equal the verified Git-LFS OID.'
Assert-Condition ($assetSha256 -ne $case.nuclear_data.rejected_p1_t01_current_pin.lfs_oid_sha256) `
    'The P1-T01 current library object is explicitly incompatible with this source-era case.'

New-Item -ItemType Directory -Path $artifactsRoot -ErrorAction Stop | Out-Null
$stagedRoot = Join-Path $artifactsRoot 'staged'
$outputRoot = Join-Path $artifactsRoot 'output'
$metadataRoot = Join-Path $artifactsRoot 'metadata'
foreach ($directory in @($stagedRoot, $outputRoot, $metadataRoot)) {
    New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
}

foreach ($file in $case.version5_source.staged_files) {
    $sourceFilePath = Join-Path $version5Root $file.source_path
    Assert-Condition (Test-Path -LiteralPath $sourceFilePath -PathType Leaf) `
        "Missing required upstream source file: $($file.source_path)"
    Assert-CleanGitFile -RepositoryPath $version5Root -RelativePath $file.source_path
    $gitBlob = Get-GitValue -RepositoryPath $version5Root -Arguments @('rev-parse', "HEAD:$($file.source_path)") `
        -Description "Git blob check for $($file.source_path)"
    Assert-Condition ($gitBlob -eq $file.git_blob) `
        "Git blob mismatch for $($file.source_path): expected $($file.git_blob), found $gitBlob."

    [byte[]]$checkoutBytes = [IO.File]::ReadAllBytes($sourceFilePath)
    [byte[]]$canonicalBytes = Convert-CheckoutBytesToCanonicalLf -Bytes $checkoutBytes -Description $file.source_path
    Assert-Condition ($canonicalBytes.Length -eq [Int64]$file.canonical_lf_size_bytes) `
        "Canonical LF source size mismatch for $($file.source_path)."
    Assert-Condition ((Get-Sha256HexFromBytes -Bytes $canonicalBytes) -eq $file.canonical_lf_sha256) `
        "Canonical LF source SHA-256 mismatch for $($file.source_path)."
    [IO.File]::WriteAllBytes((Join-Path $stagedRoot $file.staged_name), $canonicalBytes)
}

$image = $case.execution.container_image
$imageInspect = Invoke-NativeChecked -FilePath 'docker' `
    -Arguments @('image', 'inspect', $image, '--format', '{{json .}}') `
    -Description 'Pinned DRAGON Docker image inspection'
Assert-AllowedContainerStderr -Stderr $imageInspect.Stderr -Description 'Pinned DRAGON Docker image inspection'
$imageInfo = $imageInspect.Stdout | ConvertFrom-Json -ErrorAction Stop
Assert-Condition (@($imageInfo.RepoDigests) -contains $image) `
    "Pinned DRAGON Docker image digest is not present locally: $image"

$dockerVersion = Invoke-NativeChecked -FilePath 'docker' -Arguments @('version', '--format', '{{json .}}') `
    -Description 'Docker version inspection'
Assert-AllowedContainerStderr -Stderr $dockerVersion.Stderr -Description 'Docker version inspection'
$environmentProbe = @'
set -eu
uname -a
cat /etc/os-release
gfortran --version | head -n 1
gcc --version | head -n 1
sha256sum /dragon/5.1/Dragon/bin/Linux_x86_64/Dragon
'@
$environment = Invoke-NativeChecked -FilePath 'docker' -Arguments @(
    'run', '--rm', '--platform', 'linux/amd64', '--network', 'none', '--pull', 'never', '--read-only',
    '--tmpfs', '/tmp:rw,noexec,nosuid,nodev,size=64m', '--env', 'OMP_NUM_THREADS=1',
    '--entrypoint', '/bin/sh', $image, '-lc', $environmentProbe
) -Description 'Offline DRAGON environment inspection'
Assert-AllowedContainerStderr -Stderr $environment.Stderr -Description 'Offline DRAGON environment inspection'
$programHashMatch = [regex]::Match(
    $environment.Stdout,
    '(?m)^(?<hash>[0-9a-f]{64})\s+/dragon/5\.1/Dragon/bin/Linux_x86_64/Dragon$')
Assert-Condition $programHashMatch.Success 'DRAGON executable hash was not emitted by the pinned image.'
$programSha256 = $programHashMatch.Groups['hash'].Value
Assert-Condition ($programSha256 -eq $case.execution.program_sha256) `
    "DRAGON executable hash mismatch: expected $($case.execution.program_sha256), found $programSha256."
Assert-Condition ($environment.Stdout -match '(?m)^GNU Fortran .* 13\.3\.0$') `
    'Pinned image does not report the expected gfortran 13.3.0 compiler.'

$containerRun = @'
set -eu
test -r /cache/draglibJeff3p1p1SHEM295_v5p1.gz
gzip -t /cache/draglibJeff3p1p1SHEM295_v5p1.gz
mkdir -p /work/run
cp /input/lumpSS.x2m /input/mixA1_lumpSS.c2m /input/assertS.c2m /work/run/
gzip -dc /cache/draglibJeff3p1p1SHEM295_v5p1.gz > /work/run/DLIB_295
test "$(wc -c < /work/run/DLIB_295)" = "$CANDU_EXPECTED_DECOMPRESSED_SIZE"
test "$(sha256sum /work/run/DLIB_295 | awk '{print $1}')" = "$CANDU_EXPECTED_DECOMPRESSED_SHA256"
cd /work/run
exec /dragon/5.1/Dragon/bin/Linux_x86_64/Dragon < lumpSS.x2m > /output/lumpSS.result 2> /output/lumpSS.stderr
'@
$runStartedUtc = [DateTimeOffset]::UtcNow.ToString('o')
$run = Invoke-NativeChecked -FilePath 'docker' -Arguments @(
    'run', '--rm', '--platform', 'linux/amd64', '--network', 'none', '--pull', 'never', '--read-only',
    '--tmpfs', '/tmp:rw,noexec,nosuid,nodev,size=64m', '--tmpfs', '/work:rw,noexec,nosuid,nodev,size=768m',
    '--env', 'OMP_NUM_THREADS=1',
    '--env', "CANDU_EXPECTED_DECOMPRESSED_SIZE=$($compatibilityPin.decompressed_size_bytes)",
    '--env', "CANDU_EXPECTED_DECOMPRESSED_SHA256=$($compatibilityPin.decompressed_sha256)",
    '--mount', "type=bind,source=$cacheRoot,target=/cache,readonly",
    '--mount', "type=bind,source=$stagedRoot,target=/input,readonly",
    '--mount', "type=bind,source=$outputRoot,target=/output",
    '--entrypoint', '/bin/sh', $image, '-lc', $containerRun
) -Description 'P1-T02 offline DRAGON lumpSS run'
$runEndedUtc = [DateTimeOffset]::UtcNow.ToString('o')
Assert-AllowedContainerStderr -Stderr $run.Stderr -Description 'P1-T02 offline DRAGON lumpSS run'

$resultPath = Join-Path $outputRoot 'lumpSS.result'
Assert-Condition (Test-Path -LiteralPath $resultPath -PathType Leaf) `
    "DRAGON did not produce the required result file: $resultPath"
$resultItem = Get-Item -LiteralPath $resultPath -ErrorAction Stop
Assert-Condition ($resultItem.Length -gt 0) 'DRAGON produced an empty result file.'
$resultText = [IO.File]::ReadAllText($resultPath)
$dragonStderrPath = Join-Path $outputRoot 'lumpSS.stderr'
Assert-Condition (Test-Path -LiteralPath $dragonStderrPath -PathType Leaf) `
    "DRAGON did not produce the required stderr file: $dragonStderrPath"
$dragonStderrItem = Get-Item -LiteralPath $dragonStderrPath -ErrorAction Stop
$dragonStderr = [IO.File]::ReadAllText($dragonStderrPath)
Assert-AllowedContainerStderr -Stderr $dragonStderr -Description 'P1-T02 DRAGON program stderr'

foreach ($marker in $case.execution.required_result_markers) {
    Assert-Condition ($resultText.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) `
        "DRAGON result is missing required completion marker: $marker"
}

$failurePatterns = @(
    '(?im)\b(?:x?abort|test\s+fail(?:ure|ed)|error\s+code|kernel\s+error)\b',
    '(?im)\b(?:non[- ]?converg(?:ence|ed|ing)?|not\s+converg(?:ed|ing)?)\b',
    '(?i)(?<![A-Za-z0-9_-])nan(?![A-Za-z0-9_-])',
    '(?i)(?<![A-Za-z0-9_-])[+-]?(?:inf|infinity)(?![A-Za-z0-9_-])'
)
foreach ($pattern in $failurePatterns) {
    Assert-Condition (-not [regex]::IsMatch($resultText, $pattern)) `
        "DRAGON result contains a fatal diagnostic matching: $pattern"
}

$kinfMatches = [regex]::Matches(
    $resultText,
    '(?im)FINAL\s+KINF\s*=\s*(?<value>[+-]?[0-9]+(?:\.[0-9]+)?E[+-][0-9]+)')
$actualKinf = @($kinfMatches | ForEach-Object { $_.Groups['value'].Value.ToUpperInvariant() })
$expectedKinf = @($case.execution.required_final_kinf)
Assert-Condition ($actualKinf.Count -eq $expectedKinf.Count) `
    "Expected $($expectedKinf.Count) FINAL KINF values, found $($actualKinf.Count)."
for ($index = 0; $index -lt $expectedKinf.Count; $index++) {
    Assert-Condition ($actualKinf[$index] -eq $expectedKinf[$index]) `
        "FINAL KINF mismatch at step $($index + 1): expected $($expectedKinf[$index]), found $($actualKinf[$index])."
}

$finalExternalConvergence = [regex]::Matches(
    $resultText,
    '(?im)EXTERNAL\s+CONVERGENCE\s+REACHED\s+AFTER\s+\d+\s+ITERATIONS\.')
Assert-Condition ($finalExternalConvergence.Count -eq $expectedKinf.Count) `
    "Expected $($expectedKinf.Count) final external-convergence messages, found $($finalExternalConvergence.Count)."
$successfulAssertions = [regex]::Matches($resultText, '(?im)\bTEST\s+SUCCESSFUL\b')
Assert-Condition ($successfulAssertions.Count -eq $expectedKinf.Count) `
    "Expected $($expectedKinf.Count) TEST SUCCESSFUL markers, found $($successfulAssertions.Count)."

$runRecordPath = Join-Path $metadataRoot 'P1-T02-lumpSS-run-record.json'
$runRecord = [ordered]@{
    format = 'candu.reference-dragon5-smoke-run/v1'
    task_id = 'P1-T02'
    case_descriptor = [ordered]@{
        path = 'reference/dragon5/P1-T02-lumpSS.case.json'
        sha256 = Get-FileSha256 -Path $caseDescriptorPath
    }
    version5_source = [ordered]@{
        path = $version5Root
        commit = $sourceCommit
        tree = $sourceTree
        staged_files = @($case.version5_source.staged_files | ForEach-Object {
            [ordered]@{
                source_path = $_.source_path
                staged_name = $_.staged_name
                git_blob = $_.git_blob
                canonical_lf_sha256 = $_.canonical_lf_sha256
                canonical_lf_size_bytes = $_.canonical_lf_size_bytes
            }
        })
    }
    nuclear_data = [ordered]@{
        source_path = $libraryRoot
        commit = $libraryCommit
        tree = $libraryTree
        asset_path = $assetPath
        asset_cache_path = $assetCachePath
        retrieval_method = 'official GitLab immutable raw URL download'
        upstream_url = $compatibilityPin.upstream_url
        retrieved_at_utc = $retrievedAt.ToString('o')
        lfs_oid_sha256 = $compatibilityPin.lfs_oid_sha256
        compressed_sha256 = $assetSha256
        compressed_size_bytes = $assetItem.Length
        decompressed_sha256 = $compatibilityPin.decompressed_sha256
        decompressed_size_bytes = $compatibilityPin.decompressed_size_bytes
    }
    execution_environment = [ordered]@{
        docker_version = $dockerVersion.Stdout
        image = $image
        image_id = $imageInfo.Id
        image_repo_digests = @($imageInfo.RepoDigests)
        platform = 'linux/amd64'
        offline_network = 'none'
        image_pull_policy = 'never'
        omp_num_threads = 1
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
        dragon_stderr_path = $dragonStderrPath
        dragon_stderr_sha256 = Get-FileSha256 -Path $dragonStderrPath
        dragon_stderr_size_bytes = $dragonStderrItem.Length
        dragon_stderr = $dragonStderr
        result_hash_policy = $case.execution.raw_result_policy
        final_kinf = $actualKinf
        final_external_convergence_count = $finalExternalConvergence.Count
        successful_assertion_count = $successfulAssertions.Count
        completion_markers = @($case.execution.required_result_markers)
    }
}
[IO.File]::WriteAllText(
    $runRecordPath,
    ($runRecord | ConvertTo-Json -Depth 12),
    (New-Object Text.UTF8Encoding($false)))

Write-Output "PASS: P1-T02 DRAGON lumpSS smoke case completed. External record: $runRecordPath"
