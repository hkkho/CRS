param(
    [ValidateSet('candidate', 'approved')]
    [string]$Disposition = 'candidate'
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dotnetWorkingDirectory = Split-Path $root -Parent
$project = Join-Path $PSScriptRoot 'ReactorSim.ManufacturedSpatialAuthority.csproj'
$definition = Join-Path $root 'data\comparisons\p4-t06-g4i-manufactured-definition-v1.json'
$candidate = Join-Path $root 'data\comparisons\p4-t06-g4i-manufactured-authority-v1.json'
$candidateManifest = Join-Path $root 'data\comparisons\p4-t06-g4i-manufactured-authority-v1.manifest.json'
$approved = Join-Path $root 'data\golden\p4-t06-g4i-manufactured-authority-v1.json'
$approvedManifest = Join-Path $root 'data\golden\p4-t06-g4i-manufactured-authority-v1.manifest.json'

if ($Disposition -eq 'candidate') {
    $artifact = $candidate
    $manifest = $candidateManifest
}
else {
    $artifact = $approved
    $manifest = $approvedManifest
}

Push-Location $dotnetWorkingDirectory
try {
    dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $project --configuration Release --no-build -- generate `
        $definition $artifact $manifest $Disposition
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $project --configuration Release --no-build -- validate `
        $definition $artifact $manifest $Disposition
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

$document = Get-Content -Raw -LiteralPath $artifact | ConvertFrom-Json
$manifestDocument = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json
$expectedStatus = if ($Disposition -eq 'approved') { 'approved_golden' } else { 'candidate' }
$expectedApproval = if ($Disposition -eq 'approved') { 'Approved' } else { 'Candidate' }
$expectedComparison = if ($Disposition -eq 'approved') { 'Approved' } else { 'Deferred' }
$expectedTolerance = if ($Disposition -eq 'approved') { 'Approved' } else { 'Deferred' }
$expectedGolden = if ($Disposition -eq 'approved') { 'ApprovedGolden' } else { 'NoGolden' }

if ($document.status -ne $expectedStatus -or
    $document.evidence_approval -ne $expectedApproval -or
    $document.comparison_status -ne $expectedComparison -or
    $document.tolerance_status -ne $expectedTolerance -or
    $document.golden_status -ne $expectedGolden) {
    throw 'G4I disposition boundary changed.'
}

$artifactHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifact).Hash.ToLowerInvariant()
$definitionHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $definition).Hash.ToLowerInvariant()
if ($manifestDocument.artifact_sha256 -ne $artifactHash -or
    $manifestDocument.definition_sha256 -ne $definitionHash -or
    $document.definition_sha256 -ne $definitionHash) {
    throw 'G4I hash binding mismatch.'
}

foreach ($path in @($definition, $artifact, $manifest)) {
    if (@(Select-String -LiteralPath $path -Pattern '(?i)[A-Z]:[\\/]|\\\\[^\\/]+[\\/]').Count -ne 0) {
        throw "Host-local path leaked into $path."
    }
}

Write-Output (
    'P4_T06_G4I_TEST_PASS disposition=' + $Disposition +
    ' nodes=' + @($document.coefficients).Count +
    ' edges=' + @($document.geometry.edges).Count +
    ' boundaries=' + @($document.geometry.boundaries).Count +
    ' artifact_sha256=' + $artifactHash +
    ' definition_sha256=' + $definitionHash +
    ' repeat_equal=' + $document.generator_checks.independent_repeat_equal)
