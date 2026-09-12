#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$PythonCommand = 'python',
    [string]$PowerShellCommand = 'powershell'
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

function Assert-ByteEqual {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Actual,

        [Parameter(Mandatory)]
        [byte[]]$Expected,

        [Parameter(Mandatory)]
        [string]$Label
    )

    Assert-Condition ($Actual.Length -eq $Expected.Length) "$Label byte length differs."
    for ($index = 0; $index -lt $Actual.Length; $index++) {
        Assert-Condition ($Actual[$index] -eq $Expected[$index]) "$Label differs at byte $index."
    }
}

function Get-FileSha256 {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash.ToLowerInvariant()
}

function Write-TestJson {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [object]$Document
    )

    $json = $Document | ConvertTo-Json -Depth 20
    $json = ($json -replace "`r`n", "`n") -replace "`r", "`n"
    [IO.File]::WriteAllText($Path, $json + "`n", (New-Object Text.UTF8Encoding($false)))
}

function Invoke-Workflow {
    param(
        [Parameter(Mandatory)]
        [string]$WorkflowPath,

        [Parameter(Mandatory)]
        [string]$DragonRecordPath,

        [Parameter(Mandatory)]
        [string]$DonjonRecordPath,

        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    $oldErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $PowerShellCommand -NoProfile -File $WorkflowPath `
            -DragonRunRecordPath $DragonRecordPath `
            -DonjonRunRecordPath $DonjonRecordPath `
            -ArtifactsPath $OutputPath `
            -PythonCommand $PythonCommand 2>&1 | Out-Null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $oldErrorActionPreference
    }
}

function New-RunRecord {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('dragon5', 'donjon5')]
        [string]$Program,

        [Parameter(Mandatory)]
        [string]$ListingPath,

        [Parameter(Mandatory)]
        [string]$StderrPath,

        [Parameter(Mandatory)]
        [string]$DescriptorPath,

        [Parameter(Mandatory)]
        [string]$RunnerPath,

        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    $descriptor = Get-Content -Raw -LiteralPath $DescriptorPath | ConvertFrom-Json
    $descriptorSha256 = Get-FileSha256 -Path $DescriptorPath
    $listingItem = Get-Item -LiteralPath $ListingPath
    $stderrItem = Get-Item -LiteralPath $StderrPath
    $runnerSha256 = Get-FileSha256 -Path $RunnerPath
    $run = [ordered]@{
        started_utc = '2026-08-07T12:00:00.0000000+00:00'
        ended_utc = '2026-08-07T12:01:00.0000000+00:00'
        result_path = $ListingPath
        result_sha256 = Get-FileSha256 -Path $ListingPath
        result_size_bytes = [Int64]$listingItem.Length
    }
    if ($Program -eq 'dragon5') {
        $run['dragon_stderr_path'] = $StderrPath
        $run['dragon_stderr_sha256'] = Get-FileSha256 -Path $StderrPath
        $run['dragon_stderr_size_bytes'] = [Int64]$stderrItem.Length
        $format = 'candu.reference-dragon5-smoke-run/v1'
        $taskId = 'P1-T02'
        $descriptorRelativePath = 'reference/dragon5/P1-T02-lumpSS.case.json'
    }
    else {
        $run['donjon_stderr_path'] = $StderrPath
        $run['donjon_stderr_sha256'] = Get-FileSha256 -Path $StderrPath
        $run['donjon_stderr_size_bytes'] = [Int64]$stderrItem.Length
        $format = 'candu.reference-donjon5-smoke-run/v1'
        $taskId = 'P1-T03'
        $descriptorRelativePath = 'reference/donjon5/P1-T03-AFA_180_310_type1_dual.case.json'
    }
    $record = [ordered]@{
        format = $format
        task_id = $taskId
        case_descriptor = [ordered]@{
            path = $descriptorRelativePath
            sha256 = $descriptorSha256
        }
        version5_source = [ordered]@{
            commit = [string]$descriptor.version5_source.commit
            tree = [string]$descriptor.version5_source.tree
        }
        execution_environment = [ordered]@{
            image = [string]$descriptor.execution.container_image
            offline_network = 'none'
            program_sha256 = [string]$descriptor.execution.program_sha256
            runner_sha256 = $runnerSha256
        }
        run = $run
    }
    Write-TestJson -Path $OutputPath -Document $record
}

function Assert-ManifestArtifacts {
    param(
        [Parameter(Mandatory)]
        [string]$ManifestPath,

        [Parameter(Mandatory)]
        [string]$ListingPath,

        [Parameter(Mandatory)]
        [string]$StderrPath,

        [Parameter(Mandatory)]
        [string]$CompactPath
    )

    $manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
    Assert-Condition ($manifest.format -eq 'reactorsim.reference-raw-run/v1') "$ManifestPath format mismatch."
    Assert-Condition ($manifest.approval_status -eq 'retained_private') "$ManifestPath approval status mismatch."
    $artifacts = @($manifest.artifacts)
    Assert-Condition ($artifacts.Count -eq 3) "$ManifestPath must contain listing, stderr, and compact artifacts."
    $expected = @{
        'raw-listing' = $ListingPath
        'program-stderr' = $StderrPath
        'compact-export' = $CompactPath
    }
    foreach ($artifact in $artifacts) {
        Assert-Condition $expected.ContainsKey([string]$artifact.artifact_id) `
            "$ManifestPath contains an unexpected artifact ID: $($artifact.artifact_id)"
        $path = $expected[[string]$artifact.artifact_id]
        $item = Get-Item -LiteralPath $path
        Assert-Condition ([Int64]$artifact.byte_length -eq [Int64]$item.Length) `
            "$ManifestPath has the wrong byte length for $($artifact.artifact_id)."
        Assert-Condition ([string]$artifact.sha256 -eq (Get-FileSha256 -Path $path)) `
            "$ManifestPath has the wrong hash for $($artifact.artifact_id)."
        Assert-Condition ($artifact.retention -eq 'external-private' -and $artifact.publication -eq 'not-approved') `
            "$ManifestPath has an unsafe artifact policy."
    }
    $manifestText = Get-Content -Raw -LiteralPath $ManifestPath
    Assert-Condition ($manifestText -notmatch '[A-Za-z]:[\\/]') "$ManifestPath leaked an absolute local path."
    Assert-Condition ($manifestText -notmatch [regex]::Escape($ListingPath)) "$ManifestPath embedded the raw listing path."
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).ProviderPath
$workflowPath = Join-Path $repositoryRoot 'tools\PhysicsData\Run-P1-T07-ReferenceWorkflow.ps1'
$dragonParserPath = Join-Path $repositoryRoot 'tools\PhysicsData\Export-P1-T05-Dragon.py'
$donjonParserPath = Join-Path $repositoryRoot 'tools\PhysicsData\Export-P1-T06-Donjon.py'
$dragonDescriptorPath = Join-Path $repositoryRoot 'reference\dragon5\P1-T02-lumpSS.case.json'
$donjonDescriptorPath = Join-Path $repositoryRoot 'reference\donjon5\P1-T03-AFA_180_310_type1_dual.case.json'
$dragonRunnerPath = Join-Path $repositoryRoot 'tools\PhysicsData\Run-P1-T02-DragonLumpSS.ps1'
$donjonRunnerPath = Join-Path $repositoryRoot 'tools\PhysicsData\Run-P1-T03-DonjonAfa180310.ps1'
$dragonFixturePath = Join-Path $repositoryRoot 'tests\fixtures\reference\dragon5\P1-T05-lumpSS-synthetic.listing'
$donjonFixturePath = Join-Path $repositoryRoot 'tests\fixtures\reference\donjon5\P1-T06-AFA_180_310_type1_dual-synthetic.listing'
$dragonExpectedPath = Join-Path $repositoryRoot 'tests\fixtures\reference\dragon5\P1-T05-lumpSS-synthetic.expected.json'
$donjonExpectedPath = Join-Path $repositoryRoot 'tests\fixtures\reference\donjon5\P1-T06-AFA_180_310_type1_dual-synthetic.expected.json'
foreach ($path in @(
        $workflowPath, $dragonParserPath, $donjonParserPath,
        $dragonDescriptorPath, $donjonDescriptorPath,
        $dragonRunnerPath, $donjonRunnerPath,
        $dragonFixturePath, $donjonFixturePath,
        $dragonExpectedPath, $donjonExpectedPath)) {
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) "Missing P1-T07 focused input: $path"
}

$tokens = $null
$parseErrors = $null
[void][System.Management.Automation.Language.Parser]::ParseFile($workflowPath, [ref]$tokens, [ref]$parseErrors)
Assert-Condition ($parseErrors.Count -eq 0) "P1-T07 workflow PowerShell parse failed: $($parseErrors | Out-String)"

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('candu-p1-t07-' + [Guid]::NewGuid().ToString('N'))
Assert-Condition (-not (Test-Path -LiteralPath $tempRoot)) "Temporary path already exists: $tempRoot"
New-Item -ItemType Directory -Path $tempRoot -ErrorAction Stop | Out-Null
$inputRoot = Join-Path $tempRoot 'inputs'
$recordRoot = Join-Path $tempRoot 'records'
$workflowRoot1 = Join-Path $tempRoot 'workflow-1'
$workflowRoot2 = Join-Path $tempRoot 'workflow-2'
foreach ($directory in @($inputRoot, $recordRoot)) {
    New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
}

try {
    $pythonCacheRoot = Join-Path $tempRoot 'pycache'
    New-Item -ItemType Directory -Path $pythonCacheRoot -ErrorAction Stop | Out-Null
    $previousPythonCachePrefix = $env:PYTHONPYCACHEPREFIX
    try {
        $env:PYTHONPYCACHEPREFIX = $pythonCacheRoot
        & $PythonCommand -m py_compile $dragonParserPath $donjonParserPath
        Assert-Condition ($LASTEXITCODE -eq 0) 'P1-T05/P1-T06 parser Python compilation failed.'
    }
    finally {
        if ($null -eq $previousPythonCachePrefix) {
            Remove-Item Env:PYTHONPYCACHEPREFIX -ErrorAction SilentlyContinue
        }
        else {
            $env:PYTHONPYCACHEPREFIX = $previousPythonCachePrefix
        }
    }

    $dragonListingPath = Join-Path $inputRoot 'dragon.listing'
    $donjonListingPath = Join-Path $inputRoot 'donjon.listing'
    $dragonStderrPath = Join-Path $inputRoot 'dragon.stderr'
    $donjonStderrPath = Join-Path $inputRoot 'donjon.stderr'
    Copy-Item -LiteralPath $dragonFixturePath -Destination $dragonListingPath
    Copy-Item -LiteralPath $donjonFixturePath -Destination $donjonListingPath
    [IO.File]::WriteAllBytes($dragonStderrPath, [byte[]]@())
    [IO.File]::WriteAllBytes($donjonStderrPath, [byte[]]@())

    $dragonRecordPath = Join-Path $recordRoot 'dragon-run-record.json'
    $donjonRecordPath = Join-Path $recordRoot 'donjon-run-record.json'
    New-RunRecord -Program 'dragon5' -ListingPath $dragonListingPath -StderrPath $dragonStderrPath `
        -DescriptorPath $dragonDescriptorPath -RunnerPath $dragonRunnerPath -OutputPath $dragonRecordPath
    New-RunRecord -Program 'donjon5' -ListingPath $donjonListingPath -StderrPath $donjonStderrPath `
        -DescriptorPath $donjonDescriptorPath -RunnerPath $donjonRunnerPath -OutputPath $donjonRecordPath

    Assert-Condition ((Invoke-Workflow -WorkflowPath $workflowPath -DragonRecordPath $dragonRecordPath -DonjonRecordPath $donjonRecordPath -OutputPath $workflowRoot1) -eq 0) `
        'P1-T07 workflow failed for the first synthetic external run.'
    Assert-Condition ((Invoke-Workflow -WorkflowPath $workflowPath -DragonRecordPath $dragonRecordPath -DonjonRecordPath $donjonRecordPath -OutputPath $workflowRoot2) -eq 0) `
        'P1-T07 workflow failed for the repeated synthetic external run.'

    $dragonCompact1 = Join-Path $workflowRoot1 'compact\dragon5.compact.json'
    $donjonCompact1 = Join-Path $workflowRoot1 'compact\donjon5.compact.json'
    Assert-ByteEqual -Actual ([IO.File]::ReadAllBytes($dragonCompact1)) -Expected ([IO.File]::ReadAllBytes($dragonExpectedPath)) -Label 'DRAGON compact export fixture'
    Assert-ByteEqual -Actual ([IO.File]::ReadAllBytes($donjonCompact1)) -Expected ([IO.File]::ReadAllBytes($donjonExpectedPath)) -Label 'DONJON compact export fixture'

    $outputFiles = @(Get-ChildItem -LiteralPath $workflowRoot1 -Recurse -File | ForEach-Object {
        $_.FullName.Substring($workflowRoot1.Length + 1).Replace('\', '/')
    } | Sort-Object)
    $expectedOutputFiles = @(
        'compact/dragon5.compact.json',
        'compact/dragon5.compact.repeat.json',
        'compact/donjon5.compact.json',
        'compact/donjon5.compact.repeat.json',
        'manifests/dragon5.reference-raw-run.json',
        'manifests/donjon5.reference-raw-run.json',
        'metadata/P1-T07-compact-repeat-check.json'
    ) | Sort-Object
    Assert-Condition (($outputFiles -join '|') -eq ($expectedOutputFiles -join '|')) `
        "Unexpected P1-T07 workflow output files: $($outputFiles -join ', ')"
    foreach ($relativePath in $expectedOutputFiles) {
        $first = Join-Path $workflowRoot1 $relativePath.Replace('/', '\')
        $second = Join-Path $workflowRoot2 $relativePath.Replace('/', '\')
        Assert-ByteEqual -Actual ([IO.File]::ReadAllBytes($first)) -Expected ([IO.File]::ReadAllBytes($second)) `
            "Repeated workflow output $relativePath"
    }

    Assert-ManifestArtifacts `
        -ManifestPath (Join-Path $workflowRoot1 'manifests\dragon5.reference-raw-run.json') `
        -ListingPath $dragonListingPath -StderrPath $dragonStderrPath -CompactPath $dragonCompact1
    Assert-ManifestArtifacts `
        -ManifestPath (Join-Path $workflowRoot1 'manifests\donjon5.reference-raw-run.json') `
        -ListingPath $donjonListingPath -StderrPath $donjonStderrPath -CompactPath $donjonCompact1

    $repeatReport = Get-Content -Raw -LiteralPath (Join-Path $workflowRoot1 'metadata\P1-T07-compact-repeat-check.json') | ConvertFrom-Json
    Assert-Condition ($repeatReport.format -eq 'reactorsim.reference-compact-repeat-check/v1') 'Repeat report format mismatch.'
    Assert-Condition (@($repeatReport.parser_exports).Count -eq 2) 'Repeat report does not contain two parser exports.'
    foreach ($entry in @($repeatReport.parser_exports)) {
        Assert-Condition ([bool]$entry.byte_equal) "Repeat report contains a non-identical compact export for $($entry.program)."
    }

    $insideRecordPath = Join-Path $recordRoot 'dragon-inside-repository.json'
    New-RunRecord -Program 'dragon5' -ListingPath $dragonFixturePath -StderrPath $dragonStderrPath `
        -DescriptorPath $dragonDescriptorPath -RunnerPath $dragonRunnerPath -OutputPath $insideRecordPath
    $insideOutput = Join-Path $tempRoot 'must-not-run-inside-repository'
    Assert-Condition ((Invoke-Workflow -WorkflowPath $workflowPath -DragonRecordPath $insideRecordPath -DonjonRecordPath $donjonRecordPath -OutputPath $insideOutput) -ne 0) `
        'Workflow accepted a raw listing inside the repository.'
    Assert-Condition (-not (Test-Path -LiteralPath $insideOutput)) 'Rejected workflow created an output directory.'

    $repositoryOutput = Join-Path $repositoryRoot 'P1-T07-must-not-write'
    Assert-Condition (-not (Test-Path -LiteralPath $repositoryOutput)) "Unexpected pre-existing repository test path: $repositoryOutput"
    Assert-Condition ((Invoke-Workflow -WorkflowPath $workflowPath -DragonRecordPath $dragonRecordPath -DonjonRecordPath $donjonRecordPath -OutputPath $repositoryOutput) -ne 0) `
        'Workflow accepted an artifacts directory inside the repository.'
    Assert-Condition (-not (Test-Path -LiteralPath $repositoryOutput)) 'Rejected repository output path was created.'

    $existingOutput = Join-Path $tempRoot 'existing-output'
    New-Item -ItemType Directory -Path $existingOutput | Out-Null
    Assert-Condition ((Invoke-Workflow -WorkflowPath $workflowPath -DragonRecordPath $dragonRecordPath -DonjonRecordPath $donjonRecordPath -OutputPath $existingOutput) -ne 0) `
        'Workflow accepted an existing artifacts directory.'
    Assert-Condition (@(Get-ChildItem -LiteralPath $existingOutput -Force).Count -eq 0) 'Existing artifacts directory was modified.'

    Write-Output 'P1-T07 workflow tests passed: strict external inputs, repeated compact bytes, raw-manifest hashes/schema, output safety, and deterministic workflow evidence.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).ProviderPath
        $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
        Assert-Condition (Test-PathInside -CandidatePath $resolvedTemp -ParentPath $tempParent) "Refusing to remove unexpected test path: $resolvedTemp"
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
