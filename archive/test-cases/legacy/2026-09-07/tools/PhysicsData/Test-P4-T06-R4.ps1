[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = Join-Path $repoRoot 'tools\PhysicsData\ReactorSim.PhysicsData.csproj'
$sourcePath = Join-Path $repoRoot 'data\packs\p4-t06-r4-synthetic-input-v1.json'
$packPath = Join-Path $repoRoot 'data\packs\p4-t06-r4-reduced-candidate-v1.json'
$manifestPath = Join-Path $repoRoot 'data\packs\p4-t06-r4-reduced-candidate-v1.manifest.json'
$artifactsFullPath = [IO.Path]::GetFullPath($ArtifactsPath)

New-Item -ItemType Directory -Path $artifactsFullPath -Force | Out-Null
$dotnetVersion = (& dotnet --version 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $dotnetVersion -ne '10.0.302') {
    throw "The pinned .NET SDK 10.0.302 must be available on PATH; detected '$dotnetVersion'."
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [int]$ExpectedExitCode
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = (& dotnet @Arguments 2>&1 | Out-String).TrimEnd()
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $exitCode = $LASTEXITCODE
    if ($output) {
        Write-Output $output
    }
    if ($exitCode -ne $ExpectedExitCode) {
        throw "dotnet command exited $exitCode; expected $ExpectedExitCode."
    }
    return $output
}

Write-Output 'R4 T0 build'
Invoke-CheckedCommand @('build', $projectPath, '--configuration', 'Release', '--nologo') 0 | Out-Null

$runPrefix = @('run', '--project', $projectPath, '--configuration', 'Release', '--no-build', '--')
Write-Output 'R4 T1 generate'
$generateOutput = Invoke-CheckedCommand ($runPrefix + @('generate', $sourcePath, $packPath, $manifestPath)) 0
if ($generateOutput -notmatch 'P4_T06_R4_GENERATE_PASS') {
    throw 'The converter did not report a generation pass marker.'
}

Write-Output 'R4 T1 validate with source'
$validateOutput = Invoke-CheckedCommand ($runPrefix + @('validate', $packPath, $manifestPath, $sourcePath)) 0
if ($validateOutput -notmatch 'P4_T06_R4_VALIDATE_PASS.*source_checked=true') {
    throw 'The validator did not report a source-checked pass marker.'
}

$reproPath = Join-Path $artifactsFullPath 'repro'
New-Item -ItemType Directory -Path $reproPath -Force | Out-Null
$reproPackPath = Join-Path $reproPath 'candidate.json'
$reproManifestPath = Join-Path $reproPath 'candidate.manifest.json'
Write-Output 'R4 T1 deterministic regeneration'
Invoke-CheckedCommand ($runPrefix + @('generate', $sourcePath, $reproPackPath, $reproManifestPath)) 0 | Out-Null
$packHash = (Get-FileHash -LiteralPath $packPath -Algorithm SHA256).Hash
$reproPackHash = (Get-FileHash -LiteralPath $reproPackPath -Algorithm SHA256).Hash
$manifestHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$reproManifestHash = (Get-FileHash -LiteralPath $reproManifestPath -Algorithm SHA256).Hash
if ($packHash -cne $reproPackHash -or $manifestHash -cne $reproManifestHash) {
    throw 'Independent regeneration did not produce byte-identical pack and manifest files.'
}

$packText = [IO.File]::ReadAllText($packPath)
$forbiddenFieldPattern = '"(node_key|bundle_id|volume_m3|edge_conductance|boundary_conductance|flow_direction|normalization|tolerance|golden)"'
if ([Text.RegularExpressions.Regex]::IsMatch($packText, $forbiddenFieldPattern)) {
    throw 'The candidate pack contains a topology, normalization, tolerance, or golden-data field.'
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$invalidBurnupPath = Join-Path $artifactsFullPath 'invalid-burnup.json'
$invalidBurnupText = ([IO.File]::ReadAllText($sourcePath)).Replace('"burnup_j_per_kg_hm": 50000.0', '"burnup_j_per_kg_hm": -1.0')
[IO.File]::WriteAllText($invalidBurnupPath, $invalidBurnupText, $utf8NoBom)
Write-Output 'R4 T1 reject invalid burnup'
$invalidOutput = Invoke-CheckedCommand ($runPrefix + @('generate', $invalidBurnupPath, (Join-Path $artifactsFullPath 'invalid.json'), (Join-Path $artifactsFullPath 'invalid.manifest.json'))) 2
if ($invalidOutput -notmatch 'Rows.Burnup.OrderInvalid') {
    throw 'Invalid burnup was not rejected with the expected diagnostic.'
}

$unknownFieldPath = Join-Path $artifactsFullPath 'unknown-field.json'
$unknownFieldText = ([IO.File]::ReadAllText($sourcePath)).Replace('  "tables": [', "  `"unexpected`": true,`n  `"tables`": [")
[IO.File]::WriteAllText($unknownFieldPath, $unknownFieldText, $utf8NoBom)
Write-Output 'R4 T1 reject unknown field'
$unknownOutput = Invoke-CheckedCommand ($runPrefix + @('generate', $unknownFieldPath, (Join-Path $artifactsFullPath 'unknown.json'), (Join-Path $artifactsFullPath 'unknown.manifest.json'))) 2
if ($unknownOutput -notmatch 'Json.Field.Unknown') {
    throw 'Unknown source fields were not rejected with the expected diagnostic.'
}

$badManifestPath = Join-Path $artifactsFullPath 'bad.manifest.json'
$badManifestText = [IO.File]::ReadAllText($manifestPath).Replace($packHash.ToLowerInvariant(), ('0' * 64))
[IO.File]::WriteAllText($badManifestPath, $badManifestText, $utf8NoBom)
Write-Output 'R4 T1 reject manifest digest mismatch'
$badManifestOutput = Invoke-CheckedCommand ($runPrefix + @('validate', $packPath, $badManifestPath)) 2
if ($badManifestOutput -notmatch 'Manifest.PackDigest.Mismatch') {
    throw 'A manifest pack-digest mismatch was not rejected with the expected diagnostic.'
}

$badEvidencePath = Join-Path $artifactsFullPath 'bad-evidence.manifest.json'
$badEvidenceText = [IO.File]::ReadAllText($manifestPath).Replace('"evidence_class": "synthetic"', '"evidence_class": "candidate_external"')
[IO.File]::WriteAllText($badEvidencePath, $badEvidenceText, $utf8NoBom)
Write-Output 'R4 T1 reject manifest evidence mismatch'
$badEvidenceOutput = Invoke-CheckedCommand ($runPrefix + @('validate', $packPath, $badEvidencePath)) 2
if ($badEvidenceOutput -notmatch 'Value.Unexpected') {
    throw 'A non-synthetic manifest evidence class was not rejected with the expected diagnostic.'
}

$badArtifactPath = Join-Path $artifactsFullPath 'bad-artifact.manifest.json'
$badArtifactText = [IO.File]::ReadAllText($manifestPath).Replace('"pack_artifact_id": "p4-t06-r4-reduced-candidate-v1"', '"pack_artifact_id": "different-candidate-v1"')
[IO.File]::WriteAllText($badArtifactPath, $badArtifactText, $utf8NoBom)
Write-Output 'R4 T1 reject manifest artifact mismatch'
$badArtifactOutput = Invoke-CheckedCommand ($runPrefix + @('validate', $packPath, $badArtifactPath)) 2
if ($badArtifactOutput -notmatch 'Manifest.Binding.Mismatch') {
    throw 'A manifest artifact mismatch was not rejected with the expected diagnostic.'
}

Write-Output "P4_T06_R4_FOCUSED_PASS pack_sha256=$($packHash.ToLowerInvariant()) manifest_sha256=$($manifestHash.ToLowerInvariant())"
