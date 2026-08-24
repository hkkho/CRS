#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DragonRunRecordPath,

    [Parameter(Mandatory)]
    [string]$DonjonRunRecordPath,

    [Parameter(Mandatory)]
    [string]$ArtifactsPath,

    [string]$PythonCommand = 'python'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

trap {
    Write-Error $_
    exit 1
}

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
        "$Description is inside the Git repository: $Path"

    if (Test-Path -LiteralPath $Path) {
        $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
        Assert-Condition (-not (Test-PathInside -CandidatePath $resolved -ParentPath $RepositoryRoot)) `
            "$Description resolves inside the Git repository: $Path"
    }
    else {
        $parent = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))
        Assert-Condition (Test-Path -LiteralPath $parent -PathType Container) `
            "$Description parent does not exist: $parent"
        $resolvedParent = (Resolve-Path -LiteralPath $parent -ErrorAction Stop).ProviderPath
        Assert-Condition (-not (Test-PathInside -CandidatePath $resolvedParent -ParentPath $RepositoryRoot)) `
            "$Description parent resolves inside the Git repository: $parent"
    }
}

function Get-NewExternalDirectoryPath {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-OutsideRepository -Path $fullPath -Description 'Artifacts path' -RepositoryRoot $RepositoryRoot
    Assert-Condition (-not (Test-Path -LiteralPath $fullPath)) `
        "Artifacts path already exists; choose a new external directory: $fullPath"
    return $fullPath
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

function Get-RequiredProperty {
    param(
        [Parameter(Mandatory)]
        [object]$Object,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Description
    )

    $property = $Object.PSObject.Properties[$Name]
    Assert-Condition ($null -ne $property) "$Description is missing property '$Name'."
    return $property.Value
}

function Read-JsonDocument {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description
    )

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) `
        "$Description does not exist: $Path"
    [byte[]]$bytes = [IO.File]::ReadAllBytes($Path)
    Assert-Condition (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xef -and $bytes[1] -eq 0xbb -and $bytes[2] -eq 0xbf)) `
        "$Description has a UTF-8 BOM: $Path"
    try {
        $text = [Text.Encoding]::UTF8.GetString($bytes)
        return $text | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "$Description is not valid JSON: $Path. $($_.Exception.Message)"
    }
}

function Resolve-ExternalFile {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    Assert-Condition (Test-Path -LiteralPath $Path -PathType Leaf) `
        "$Description is not a regular file: $Path"
    $resolved = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
    Assert-OutsideRepository -Path $resolved -Description $Description -RepositoryRoot $RepositoryRoot
    return $resolved
}

function ConvertTo-UtcZulu {
    param(
        [Parameter(Mandatory)]
        [string]$Value,

        [Parameter(Mandatory)]
        [string]$Description
    )

    try {
        $parsed = [DateTimeOffset]::Parse(
            $Value,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
        return $parsed.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", [Globalization.CultureInfo]::InvariantCulture)
    }
    catch {
        throw "$Description is not a valid timestamp: $Value"
    }
}

function Write-NewJsonDocument {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [object]$Document
    )

    $json = $Document | ConvertTo-Json -Depth 20
    $json = ($json -replace "`r`n", "`n") -replace "`r", "`n"
    $bytes = [Text.Encoding]::UTF8.GetBytes($json + "`n")
    $stream = $null
    try {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $stream.Write($bytes, 0, $bytes.Length)
    }
    catch {
        if (Test-Path -LiteralPath $Path) {
            Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        }
        throw "Could not create JSON output without overwrite: $Path. $($_.Exception.Message)"
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Get-CaseConfiguration {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('dragon5', 'donjon5')]
        [string]$Program,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    if ($Program -eq 'dragon5') {
        return [pscustomobject]@{
            Program = 'dragon5'
            TaskId = 'P1-T02'
            RunFormat = 'candu.reference-dragon5-smoke-run/v1'
            DescriptorRelativePath = 'reference/dragon5/P1-T02-lumpSS.case.json'
            DescriptorPath = Join-Path $RepositoryRoot 'reference\dragon5\P1-T02-lumpSS.case.json'
            ParserPath = Join-Path $RepositoryRoot 'tools\PhysicsData\Export-P1-T05-Dragon.py'
            RunnerRelativePath = 'tools/PhysicsData/Run-P1-T02-DragonLumpSS.ps1'
            RunnerPath = Join-Path $RepositoryRoot 'tools\PhysicsData\Run-P1-T02-DragonLumpSS.ps1'
            ResultPathProperty = 'result_path'
            ResultHashProperty = 'result_sha256'
            ResultSizeProperty = 'result_size_bytes'
            StderrPathProperty = 'dragon_stderr_path'
            StderrHashProperty = 'dragon_stderr_sha256'
            StderrSizeProperty = 'dragon_stderr_size_bytes'
            ManifestName = 'dragon5.reference-raw-run.json'
            CompactName = 'dragon5.compact.json'
            RepeatCompactName = 'dragon5.compact.repeat.json'
        }
    }

    return [pscustomobject]@{
        Program = 'donjon5'
        TaskId = 'P1-T03'
        RunFormat = 'candu.reference-donjon5-smoke-run/v1'
        DescriptorRelativePath = 'reference/donjon5/P1-T03-AFA_180_310_type1_dual.case.json'
        DescriptorPath = Join-Path $RepositoryRoot 'reference\donjon5\P1-T03-AFA_180_310_type1_dual.case.json'
        ParserPath = Join-Path $RepositoryRoot 'tools\PhysicsData\Export-P1-T06-Donjon.py'
        RunnerRelativePath = 'tools/PhysicsData/Run-P1-T03-DonjonAfa180310.ps1'
        RunnerPath = Join-Path $RepositoryRoot 'tools\PhysicsData\Run-P1-T03-DonjonAfa180310.ps1'
        ResultPathProperty = 'result_path'
        ResultHashProperty = 'result_sha256'
        ResultSizeProperty = 'result_size_bytes'
        StderrPathProperty = 'donjon_stderr_path'
        StderrHashProperty = 'donjon_stderr_sha256'
        StderrSizeProperty = 'donjon_stderr_size_bytes'
        ManifestName = 'donjon5.reference-raw-run.json'
        CompactName = 'donjon5.compact.json'
        RepeatCompactName = 'donjon5.compact.repeat.json'
    }
}

function Get-DescriptorInfo {
    param(
        [Parameter(Mandatory)]
        [object]$Configuration
    )

    Assert-Condition (Test-Path -LiteralPath $Configuration.DescriptorPath -PathType Leaf) `
        "Missing $($Configuration.Program) case descriptor: $($Configuration.DescriptorPath)"
    $descriptor = Read-JsonDocument -Path $Configuration.DescriptorPath -Description "$($Configuration.Program) case descriptor"
    $descriptorBytes = [IO.File]::ReadAllBytes($Configuration.DescriptorPath)
    $descriptorSha256 = Get-Sha256HexFromBytes -Bytes $descriptorBytes
    Assert-Condition ([string]$descriptor.format -match '^candu\.reference-.*-smoke-case/v1$') `
        "Unsupported $($Configuration.Program) descriptor format: $($descriptor.format)"
    Assert-Condition ([string]$descriptor.task_id -eq $Configuration.TaskId) `
        "$($Configuration.Program) descriptor task_id is not $($Configuration.TaskId)."
    Assert-Condition ([string]$descriptor.version5_source.ref -eq 'v5.1.0') `
        "$($Configuration.Program) descriptor must pin Version5 v5.1.0."
    return [pscustomobject]@{
        Document = $descriptor
        Sha256 = $descriptorSha256
    }
}

function Get-RunInfo {
    param(
        [Parameter(Mandatory)]
        [object]$Configuration,

        [Parameter(Mandatory)]
        [string]$RunRecordPath,

        [Parameter(Mandatory)]
        [string]$RepositoryRoot
    )

    $recordPath = Resolve-ExternalFile -Path $RunRecordPath -Description "$($Configuration.Program) run record" -RepositoryRoot $RepositoryRoot
    $record = Read-JsonDocument -Path $recordPath -Description "$($Configuration.Program) run record"
    Assert-Condition ([string]$record.format -eq $Configuration.RunFormat) `
        "$($Configuration.Program) run record has an unexpected format: $($record.format)"
    Assert-Condition ([string]$record.task_id -eq $Configuration.TaskId) `
        "$($Configuration.Program) run record task_id is not $($Configuration.TaskId)."

    $descriptorInfo = Get-DescriptorInfo -Configuration $Configuration
    $caseDescriptor = Get-RequiredProperty -Object $record -Name 'case_descriptor' -Description "$($Configuration.Program) run record"
    Assert-Condition ([string]$caseDescriptor.path -eq $Configuration.DescriptorRelativePath) `
        "$($Configuration.Program) run record points at an unexpected descriptor."
    Assert-Condition ([string]$caseDescriptor.sha256 -eq $descriptorInfo.Sha256) `
        "$($Configuration.Program) run record descriptor SHA-256 does not match the repository descriptor."

    $source = Get-RequiredProperty -Object $record -Name 'version5_source' -Description "$($Configuration.Program) run record"
    Assert-Condition ([string]$source.commit -eq [string]$descriptorInfo.Document.version5_source.commit) `
        "$($Configuration.Program) run record Version5 commit does not match its descriptor."
    Assert-Condition ([string]$source.tree -eq [string]$descriptorInfo.Document.version5_source.tree) `
        "$($Configuration.Program) run record Version5 tree does not match its descriptor."

    $environment = Get-RequiredProperty -Object $record -Name 'execution_environment' -Description "$($Configuration.Program) run record"
    Assert-Condition ([string]$environment.offline_network -eq 'none') `
        "$($Configuration.Program) run record is not marked offline."
    Assert-Condition ([string]$environment.image -eq [string]$descriptorInfo.Document.execution.container_image) `
        "$($Configuration.Program) run record container image does not match its descriptor."
    Assert-Condition ([string]$environment.program_sha256 -eq [string]$descriptorInfo.Document.execution.program_sha256) `
        "$($Configuration.Program) run record executable SHA-256 does not match its descriptor."
    Assert-Condition (Test-Path -LiteralPath $Configuration.RunnerPath -PathType Leaf) `
        "Missing $($Configuration.Program) runner: $($Configuration.RunnerPath)"
    $runnerSha256 = Get-FileSha256 -Path $Configuration.RunnerPath
    Assert-Condition ([string]$environment.runner_sha256 -eq $runnerSha256) `
        "$($Configuration.Program) run record runner SHA-256 is stale or does not identify the pinned runner."

    $run = Get-RequiredProperty -Object $record -Name 'run' -Description "$($Configuration.Program) run record"
    $startedUtc = ConvertTo-UtcZulu -Value ([string](Get-RequiredProperty -Object $run -Name 'started_utc' -Description "$($Configuration.Program) run record.run")) -Description "$($Configuration.Program) run start"
    $finishedUtc = ConvertTo-UtcZulu -Value ([string](Get-RequiredProperty -Object $run -Name 'ended_utc' -Description "$($Configuration.Program) run record.run")) -Description "$($Configuration.Program) run end"
    $started = [DateTimeOffset]::Parse($startedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    $finished = [DateTimeOffset]::Parse($finishedUtc, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
    Assert-Condition ($finished -ge $started) "$($Configuration.Program) run finished before it started."

    $listingRecordedPath = [string](Get-RequiredProperty -Object $run -Name $Configuration.ResultPathProperty -Description "$($Configuration.Program) run record.run")
    $stderrRecordedPath = [string](Get-RequiredProperty -Object $run -Name $Configuration.StderrPathProperty -Description "$($Configuration.Program) run record.run")
    $listingPath = Resolve-ExternalFile -Path $listingRecordedPath -Description "$($Configuration.Program) raw listing" -RepositoryRoot $RepositoryRoot
    $stderrPath = Resolve-ExternalFile -Path $stderrRecordedPath -Description "$($Configuration.Program) program stderr" -RepositoryRoot $RepositoryRoot

    $listingItem = Get-Item -LiteralPath $listingPath -ErrorAction Stop
    $stderrItem = Get-Item -LiteralPath $stderrPath -ErrorAction Stop
    $listingSha256 = Get-FileSha256 -Path $listingPath
    $stderrSha256 = Get-FileSha256 -Path $stderrPath
    Assert-Condition ([string]$run.($Configuration.ResultHashProperty) -eq $listingSha256) `
        "$($Configuration.Program) run record listing SHA-256 does not match the retained listing."
    Assert-Condition ([Int64]$run.($Configuration.ResultSizeProperty) -eq [Int64]$listingItem.Length) `
        "$($Configuration.Program) run record listing size does not match the retained listing."
    Assert-Condition ([string]$run.($Configuration.StderrHashProperty) -eq $stderrSha256) `
        "$($Configuration.Program) run record stderr SHA-256 does not match the retained stderr."
    Assert-Condition ([Int64]$run.($Configuration.StderrSizeProperty) -eq [Int64]$stderrItem.Length) `
        "$($Configuration.Program) run record stderr size does not match the retained stderr."

    return [pscustomobject]@{
        Configuration = $Configuration
        Record = $record
        Descriptor = $descriptorInfo.Document
        DescriptorSha256 = $descriptorInfo.Sha256
        ListingPath = $listingPath
        ListingSha256 = $listingSha256
        ListingSize = [Int64]$listingItem.Length
        StderrPath = $stderrPath
        StderrSha256 = $stderrSha256
        StderrSize = [Int64]$stderrItem.Length
        RunnerSha256 = $runnerSha256
        StartedUtc = $startedUtc
        FinishedUtc = $finishedUtc
    }
}

function Invoke-Parser {
    param(
        [Parameter(Mandatory)]
        [object]$RunInfo,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [Parameter(Mandatory)]
        [string]$PythonCommand
    )

    Assert-Condition (Test-Path -LiteralPath $RunInfo.Configuration.ParserPath -PathType Leaf) `
        "Missing $($RunInfo.Configuration.Program) parser: $($RunInfo.Configuration.ParserPath)"
    $parserOutput = & $PythonCommand $RunInfo.Configuration.ParserPath `
        '--input' $RunInfo.ListingPath `
        '--output' $OutputPath `
        '--descriptor' $RunInfo.Configuration.DescriptorPath 2>&1
    $exitCode = $LASTEXITCODE
    $captured = @($parserOutput) -join [Environment]::NewLine
    Assert-Condition ($exitCode -eq 0) `
        "$($RunInfo.Configuration.Program) compact exporter failed with exit code $($exitCode): $captured"
    Assert-Condition ([string]::IsNullOrWhiteSpace($captured)) `
        "$($RunInfo.Configuration.Program) compact exporter emitted unexpected output: $captured"
    Assert-Condition (Test-Path -LiteralPath $OutputPath -PathType Leaf) `
        "$($RunInfo.Configuration.Program) compact exporter did not create: $OutputPath"
}

function Compare-Bytes {
    param(
        [Parameter(Mandatory)]
        [string]$FirstPath,

        [Parameter(Mandatory)]
        [string]$SecondPath,

        [Parameter(Mandatory)]
        [string]$Description
    )

    [byte[]]$first = [IO.File]::ReadAllBytes($FirstPath)
    [byte[]]$second = [IO.File]::ReadAllBytes($SecondPath)
    Assert-Condition ($first.Length -eq $second.Length) "$Description byte lengths differ."
    for ($index = 0; $index -lt $first.Length; $index++) {
        Assert-Condition ($first[$index] -eq $second[$index]) "$Description differs at byte $index."
    }
}

function Get-ManifestArtifact {
    param(
        [Parameter(Mandatory)]
        [string]$ArtifactId,

        [Parameter(Mandatory)]
        [ValidateSet('listing', 'stderr', 'compact-export')]
        [string]$Role,

        [Parameter(Mandatory)]
        [string]$MediaType,

        [Parameter(Mandatory)]
        [string]$Path
    )

    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    $sha256 = Get-FileSha256 -Path $Path
    return [ordered]@{
        artifact_id = $ArtifactId
        role = $Role
        media_type = $MediaType
        byte_length = [Int64]$item.Length
        sha256 = $sha256
        retention = 'external-private'
        publication = 'not-approved'
        locator_id = "opaque:$ArtifactId-$($sha256.Substring(0, 16))"
    }
}

function Copy-CompactDiagnostics {
    param(
        [Parameter(Mandatory)]
        [object]$Compact
    )

    $warningCodes = @($Compact.diagnostics.warning_codes | ForEach-Object {
        [ordered]@{
            code = [string]$_.code
            count = [Int64]$_.count
        }
    })
    $assertions = @($Compact.diagnostics.assertions | ForEach-Object {
        [ordered]@{
            assertion_id = [string]$_.assertion_id
            passed = [bool]$_.passed
            count = [Int64]$_.count
        }
    })
    $measures = @($Compact.diagnostics.convergence | ForEach-Object {
        [ordered]@{
            quantity_id = [string]$_.quantity_id
            value = $_.value
            source_lexeme = [string]$_.source_lexeme
        }
    })
    return [ordered]@{
        status = 'succeeded'
        normal_end = [bool]$Compact.diagnostics.normal_end
        warning_codes = $warningCodes
        error_codes = @()
        assertions = $assertions
        convergence = [ordered]@{
            status = [string]$Compact.diagnostics.convergence_status
            measures = $measures
        }
    }
}

function New-RawManifest {
    param(
        [Parameter(Mandatory)]
        [object]$RunInfo,

        [Parameter(Mandatory)]
        [object]$Compact,

        [Parameter(Mandatory)]
        [string]$CompactPath
    )

    Assert-Condition ([string]$Compact.approval_status -eq 'candidate_reference') `
        "$($RunInfo.Configuration.Program) compact export is not a candidate reference."
    Assert-Condition ([string]$Compact.retention -eq 'external-private') `
        "$($RunInfo.Configuration.Program) compact export has an unexpected retention policy."
    Assert-Condition ([string]$Compact.publication -eq 'not-approved') `
        "$($RunInfo.Configuration.Program) compact export has an unexpected publication policy."
    Assert-Condition ([string]$Compact.case.case_id -eq [string]$RunInfo.Descriptor.case_id) `
        "$($RunInfo.Configuration.Program) compact export case ID does not match its descriptor."

    $compactSha256 = Get-FileSha256 -Path $CompactPath
    $invocationId = "p1-t07-$($RunInfo.Configuration.Program)-$($RunInfo.ListingSha256.Substring(0, 16))"
    $tool = [ordered]@{
        program = [string]$Compact.tool_provenance.program
        version = [string]$Compact.tool_provenance.version
        source_commit = [string]$Compact.tool_provenance.source_commit
        source_tree = [string]$Compact.tool_provenance.source_tree
        executable_sha256 = [string]$Compact.tool_provenance.executable_sha256
        container_image_digest = [string]$Compact.tool_provenance.container_image_digest
    }
    $artifacts = @(
        (Get-ManifestArtifact -ArtifactId 'raw-listing' -Role 'listing' -MediaType 'text/plain' -Path $RunInfo.ListingPath),
        (Get-ManifestArtifact -ArtifactId 'program-stderr' -Role 'stderr' -MediaType 'text/plain' -Path $RunInfo.StderrPath),
        (Get-ManifestArtifact -ArtifactId 'compact-export' -Role 'compact-export' -MediaType 'application/json' -Path $CompactPath)
    )
    return [ordered]@{
        format = 'reactorsim.reference-raw-run/v1'
        approval_status = 'retained_private'
        case = [ordered]@{
            case_id = [string]$Compact.case.case_id
            descriptor_path = $RunInfo.Configuration.DescriptorRelativePath
            descriptor_sha256 = $RunInfo.DescriptorSha256
        }
        tool = $tool
        execution = [ordered]@{
            runner_path = $RunInfo.Configuration.RunnerRelativePath
            runner_sha256 = $RunInfo.RunnerSha256
            started_utc = $RunInfo.StartedUtc
            finished_utc = $RunInfo.FinishedUtc
            invocation_id = $invocationId
            network_mode = 'none'
        }
        artifacts = $artifacts
        outcome = Copy-CompactDiagnostics -Compact $Compact
    }
}

function Test-WorkflowJsonArtifacts {
    param(
        [Parameter(Mandatory)]
        [string]$RawSchemaPath,

        [Parameter(Mandatory)]
        [string]$CompactSchemaPath,

        [Parameter(Mandatory)]
        [string[]]$ManifestPaths,

        [Parameter(Mandatory)]
        [string[]]$CompactPaths,

        [Parameter(Mandatory)]
        [string]$PythonCommand
    )

    $validator = @'
import json
import math
import sys
from decimal import Decimal, InvalidOperation
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker


def reject_constant(value):
    raise ValueError(f"non-standard JSON number {value!r}")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON object name {key!r}")
        result[key] = value
    return result


def read_json(path):
    data = path.read_bytes()
    if data.startswith(b"\xef\xbb\xbf") or b"\r" in data:
        raise AssertionError(f"{path}: UTF-8 BOM/CR is forbidden")
    if not data.endswith(b"\n") or data.endswith(b"\n\n"):
        raise AssertionError(f"{path}: expected exactly one terminal LF")
    value = json.loads(
        data.decode("utf-8", errors="strict"),
        parse_constant=reject_constant,
        object_pairs_hook=unique_object,
    )
    return value, data


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, allow_nan=False,
                       indent=2, separators=(",", ": ")) + "\n").encode("utf-8")


def scalar_agreement(value, label):
    if isinstance(value, dict):
        if "value" in value and "source_lexeme" in value:
            try:
                numeric = Decimal(str(value["value"]))
                lexeme = Decimal(value["source_lexeme"])
            except (InvalidOperation, ValueError) as error:
                raise AssertionError(f"{label}: invalid scalar evidence: {error}") from error
            if not numeric.is_finite() or not lexeme.is_finite() or numeric != lexeme:
                raise AssertionError(f"{label}: value/source_lexeme mismatch")
        for key, child in value.items():
            scalar_agreement(child, f"{label}/{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            scalar_agreement(child, f"{label}/{index}")


raw_schema, _ = read_json(Path(sys.argv[1]))
compact_schema, _ = read_json(Path(sys.argv[2]))
raw_validator = Draft202012Validator(raw_schema, format_checker=FormatChecker())
compact_validator = Draft202012Validator(compact_schema, format_checker=FormatChecker())

manifest_paths = [Path(item) for item in sys.argv[3:5]]
compact_paths = [Path(item) for item in sys.argv[5:]]
for index, path in enumerate(compact_paths):
    document, data = read_json(path)
    errors = list(compact_validator.iter_errors(document))
    if errors:
        raise AssertionError(f"{path}: compact schema rejected output: {errors[0].message}")
    if canonical(document) != data:
        raise AssertionError(f"{path}: compact bytes do not match the P1-T04 deterministic rendering")
    scalar_agreement(document, str(path))

for index, path in enumerate(manifest_paths):
    document, data = read_json(path)
    errors = list(raw_validator.iter_errors(document))
    if errors:
        raise AssertionError(f"{path}: raw schema rejected output: {errors[0].message}")
    scalar_agreement(document, str(path))
    ids = [item["artifact_id"] for item in document["artifacts"]]
    if len(ids) != len(set(ids)):
        raise AssertionError(f"{path}: duplicate artifact identifiers")
    roles = [item["role"] for item in document["artifacts"]]
    if "listing" not in roles or "stderr" not in roles or "compact-export" not in roles:
        raise AssertionError(f"{path}: missing listing, stderr, or compact-export association")
    for artifact in document["artifacts"]:
        if artifact["retention"] != "external-private" or artifact["publication"] != "not-approved":
            raise AssertionError(f"{path}: artifact policy is not external-private/not-approved")

print("P1-T07 compact and raw-manifest schema validation passed: 4 compact exports and 2 retained-private manifests.")
'@

    $arguments = @($RawSchemaPath, $CompactSchemaPath) + $ManifestPaths + $CompactPaths
    $output = $validator | & $PythonCommand - $arguments 2>&1
    $exitCode = $LASTEXITCODE
    $captured = @($output) -join [Environment]::NewLine
    Assert-Condition ($exitCode -eq 0) "P1-T07 JSON artifact validation failed with exit code $($exitCode): $captured"
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($captured)) 'P1-T07 JSON artifact validator emitted no result.'
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..') -ErrorAction Stop).ProviderPath
$artifactsRoot = Get-NewExternalDirectoryPath -Path $ArtifactsPath -RepositoryRoot $repositoryRoot
$dragonConfiguration = Get-CaseConfiguration -Program 'dragon5' -RepositoryRoot $repositoryRoot
$donjonConfiguration = Get-CaseConfiguration -Program 'donjon5' -RepositoryRoot $repositoryRoot
$dragonRunInfo = Get-RunInfo -Configuration $dragonConfiguration -RunRecordPath $DragonRunRecordPath -RepositoryRoot $repositoryRoot
$donjonRunInfo = Get-RunInfo -Configuration $donjonConfiguration -RunRecordPath $DonjonRunRecordPath -RepositoryRoot $repositoryRoot

$compactRoot = Join-Path $artifactsRoot 'compact'
$manifestRoot = Join-Path $artifactsRoot 'manifests'
$metadataRoot = Join-Path $artifactsRoot 'metadata'
foreach ($directory in @($compactRoot, $manifestRoot, $metadataRoot)) {
    New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
}

$processedCases = @()
foreach ($runInfo in @($dragonRunInfo, $donjonRunInfo)) {
    $firstCompactPath = Join-Path $compactRoot $runInfo.Configuration.CompactName
    $repeatCompactPath = Join-Path $compactRoot $runInfo.Configuration.RepeatCompactName
    Invoke-Parser -RunInfo $runInfo -OutputPath $firstCompactPath -PythonCommand $PythonCommand
    Invoke-Parser -RunInfo $runInfo -OutputPath $repeatCompactPath -PythonCommand $PythonCommand
    Compare-Bytes -FirstPath $firstCompactPath -SecondPath $repeatCompactPath `
        -Description "$($runInfo.Configuration.Program) repeated compact export"
    $compact = Read-JsonDocument -Path $firstCompactPath -Description "$($runInfo.Configuration.Program) compact export"
    $manifest = New-RawManifest -RunInfo $runInfo -Compact $compact -CompactPath $firstCompactPath
    $manifestPath = Join-Path $manifestRoot $runInfo.Configuration.ManifestName
    Write-NewJsonDocument -Path $manifestPath -Document $manifest
    $processedCases += [pscustomobject]@{
        RunInfo = $runInfo
        CompactPath = $firstCompactPath
        RepeatCompactPath = $repeatCompactPath
        Compact = $compact
        Manifest = $manifest
        ManifestPath = $manifestPath
    }
}

$rawSchemaPath = Join-Path $repositoryRoot 'data\schema\reference-raw-run-v1.schema.json'
$compactSchemaPath = Join-Path $repositoryRoot 'data\schema\reference-compact-export-v1.schema.json'
Test-WorkflowJsonArtifacts `
    -RawSchemaPath $rawSchemaPath `
    -CompactSchemaPath $compactSchemaPath `
    -ManifestPaths @($processedCases[0].ManifestPath, $processedCases[1].ManifestPath) `
    -CompactPaths @(
        $processedCases[0].CompactPath,
        $processedCases[0].RepeatCompactPath,
        $processedCases[1].CompactPath,
        $processedCases[1].RepeatCompactPath) `
    -PythonCommand $PythonCommand

$repeatEntries = @($processedCases | ForEach-Object {
    $firstSha256 = Get-FileSha256 -Path $_.CompactPath
    $repeatSha256 = Get-FileSha256 -Path $_.RepeatCompactPath
    [ordered]@{
        program = $_.RunInfo.Configuration.Program
        case_id = [string]$_.Compact.case.case_id
        compact_path = "compact/$($_.RunInfo.Configuration.CompactName)"
        compact_sha256 = $firstSha256
        repeat_compact_path = "compact/$($_.RunInfo.Configuration.RepeatCompactName)"
        repeat_compact_sha256 = $repeatSha256
        byte_length = [Int64](Get-Item -LiteralPath $_.CompactPath).Length
        byte_equal = ($firstSha256 -eq $repeatSha256)
        raw_manifest_path = "manifests/$($_.RunInfo.Configuration.ManifestName)"
        raw_manifest_sha256 = Get-FileSha256 -Path $_.ManifestPath
    }
})
$repeatReport = [ordered]@{
    format = 'reactorsim.reference-compact-repeat-check/v1'
    task_id = 'P1-T07'
    approval_status = 'unapproved_workflow_evidence'
    retention = 'external-private'
    publication = 'not-approved'
    parser_exports = $repeatEntries
}
$repeatReportPath = Join-Path $metadataRoot 'P1-T07-compact-repeat-check.json'
Write-NewJsonDocument -Path $repeatReportPath -Document $repeatReport

Write-Output 'PASS: P1-T07 DRAGON5 and DONJON5 parsers exported byte-identical compact evidence twice.'
Write-Output "External workflow artifacts: $artifactsRoot"
Write-Output "Repeat-hash report: $repeatReportPath"
