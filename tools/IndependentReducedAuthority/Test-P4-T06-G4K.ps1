$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$program = Join-Path $PSScriptRoot 'Program.py'
$profileAudit = Join-Path $PSScriptRoot 'ProfileDigestAudit.py'
$definition = Join-Path $root 'data\comparisons\p4-t06-g4j-representative-definition-v1.json'
$pack = Join-Path $root 'data\packs\p4-t06-r4-reduced-candidate-v1.json'
$authorityDefinition = Join-Path $root 'data\comparisons\p4-t06-g4k-independent-authority-definition-v1.json'
$artifact = Join-Path $root 'data\comparisons\p4-t06-g4k-independent-authority-v1.json'
$manifest = Join-Path $root 'data\comparisons\p4-t06-g4k-independent-authority-v1.manifest.json'
$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $program).Hash.ToLowerInvariant()

Push-Location $root
try {
    & python $program generate $definition $pack $authorityDefinition $artifact $manifest $sourceHash
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & python $program validate $definition $pack $authorityDefinition $artifact $manifest $sourceHash
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & python $profileAudit $artifact
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $alteredSourceHash = '0' * 64
    $priorErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $rejectionOutput = & python $program validate $definition $pack $authorityDefinition $artifact $manifest $alteredSourceHash 2>&1
    $rejectionExitCode = $LASTEXITCODE
    $ErrorActionPreference = $priorErrorActionPreference
    if ($rejectionExitCode -eq 0) {
        throw 'G4K altered source-snapshot hash was accepted.'
    }
    Write-Output ('P4_T06_G4K_HASH_BINDING_REJECTION_PASS exit_code=' + $rejectionExitCode)
}
finally {
    Pop-Location
}

$document = Get-Content -Raw -LiteralPath $artifact | ConvertFrom-Json
$manifestDocument = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json
$artifactHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifact).Hash.ToLowerInvariant()
$definitionHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $definition).Hash.ToLowerInvariant()
$packHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $pack).Hash.ToLowerInvariant()
$authorityDefinitionHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $authorityDefinition).Hash.ToLowerInvariant()

if ($document.status -ne 'candidate' -or
    $document.evidence_approval -ne 'Candidate' -or
    $document.comparison_status -ne 'Deferred' -or
    $document.tolerance_status -ne 'Deferred' -or
    $document.golden_status -ne 'NoGolden' -or
    $document.coverage_class -ne 'RepresentativeReducedModel' -or
    $document.validation_domain -ne 'ReducedModel') {
    throw 'G4K candidate boundary changed.'
}

if ($manifestDocument.artifact_sha256 -ne $artifactHash -or
    $manifestDocument.definition_sha256 -ne $definitionHash -or
    $manifestDocument.source_pack_sha256 -ne $packHash -or
    $manifestDocument.independent_definition_sha256 -ne $authorityDefinitionHash -or
    $manifestDocument.disposition -ne 'candidate') {
    throw 'G4K manifest/hash binding mismatch.'
}

foreach ($path in @($authorityDefinition, $artifact, $manifest)) {
    if (@(Select-String -LiteralPath $path -Pattern '(?i)[A-Z]:[\\/]|\\\\[^\\/]+[\\/]|file://' ).Count -ne 0) {
        throw "Host-local path leaked into $path."
    }
}

if (@($document.scenarios).Count -ne 5 -or
    @($document.geometry.nodes).Count -ne 48 -or
    @($document.geometry.edges).Count -ne 92 -or
    @($document.geometry.boundaries).Count -ne 104) {
    throw 'G4K geometry or scenario coverage changed.'
}

Write-Output (
    'P4_T06_G4K_TEST_PASS scenarios=' + @($document.scenarios).Count +
    ' nodes=' + @($document.geometry.nodes).Count +
    ' edges=' + @($document.geometry.edges).Count +
    ' boundaries=' + @($document.geometry.boundaries).Count +
    ' artifact_sha256=' + $artifactHash +
    ' definition_sha256=' + $definitionHash +
    ' source_pack_sha256=' + $packHash +
    ' independent_definition_sha256=' + $authorityDefinitionHash +
    ' source_snapshot_sha256=' + $sourceHash +
    ' repeat_equal=True')
