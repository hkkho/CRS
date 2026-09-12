$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dotnetWorkingDirectory = Split-Path $root -Parent
$project = Join-Path $PSScriptRoot 'ReactorSim.RepresentativeReducedAuthority.csproj'
$definition = Join-Path $root 'data\comparisons\p4-t06-g4j-representative-definition-v1.json'
$pack = Join-Path $root 'data\packs\p4-t06-r4-reduced-candidate-v1.json'
$artifact = Join-Path $root 'data\comparisons\p4-t06-g4j-representative-authority-v1.json'
$manifest = Join-Path $root 'data\comparisons\p4-t06-g4j-representative-authority-v1.manifest.json'
$source = Join-Path $PSScriptRoot 'Program.cs'
$sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash.ToLowerInvariant()

Push-Location $dotnetWorkingDirectory
try {
    dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $project --configuration Release --no-build -- generate `
        $definition $pack $artifact $manifest $sourceHash
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $project --configuration Release --no-build -- validate `
        $definition $pack $artifact $manifest $sourceHash
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

$document = Get-Content -Raw -LiteralPath $artifact | ConvertFrom-Json
$manifestDocument = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json
$artifactHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifact).Hash.ToLowerInvariant()
$definitionHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $definition).Hash.ToLowerInvariant()
$packHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $pack).Hash.ToLowerInvariant()

if ($document.status -ne 'candidate' -or
    $document.evidence_approval -ne 'Candidate' -or
    $document.comparison_status -ne 'Deferred' -or
    $document.tolerance_status -ne 'Deferred' -or
    $document.golden_status -ne 'NoGolden' -or
    $document.coverage_class -ne 'RepresentativeReducedModel' -or
    $document.validation_domain -ne 'ReducedModel') {
    throw 'G4J candidate boundary changed.'
}

if ($document.definition_sha256 -ne $definitionHash -or
    $document.source_pack.sha256 -ne $packHash -or
    $manifestDocument.artifact_sha256 -ne $artifactHash -or
    $manifestDocument.definition_sha256 -ne $definitionHash -or
    $manifestDocument.source_pack_sha256 -ne $packHash -or
    $manifestDocument.disposition -ne 'candidate') {
    throw 'G4J hash binding mismatch.'
}

foreach ($path in @($definition, $artifact, $manifest)) {
    if (@(Select-String -LiteralPath $path -Pattern '(?i)[A-Z]:[\\/]|\\\\[^\\/]+[\\/]|file://' ).Count -ne 0) {
        throw "Host-local path leaked into $path."
    }
}

Write-Output (
    'P4_T06_G4J_TEST_PASS scenarios=' + @($document.scenarios).Count +
    ' nodes=' + @($document.geometry.nodes).Count +
    ' edges=' + @($document.geometry.edges).Count +
    ' boundaries=' + @($document.geometry.boundaries).Count +
    ' artifact_sha256=' + $artifactHash +
    ' definition_sha256=' + $definitionHash +
    ' pack_sha256=' + $packHash +
    ' repeat_equal=' + $document.generator_checks.independent_repeat_equal)
