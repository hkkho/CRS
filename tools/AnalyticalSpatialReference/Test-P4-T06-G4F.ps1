$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$dotnetWorkingDirectory = Split-Path $root -Parent
$project = Join-Path $PSScriptRoot 'ReactorSim.AnalyticalSpatialReference.csproj'
$source = Join-Path $root 'data\packs\p4-t06-r4-synthetic-input-v1.json'
$pack = Join-Path $root 'data\packs\p4-t06-r4-reduced-candidate-v1.json'
$packManifest = Join-Path $root 'data\packs\p4-t06-r4-reduced-candidate-v1.manifest.json'
$benchmark = Join-Path $root 'benchmarks\P4-T08-static-solver-benchmark.json'
$candidate = Join-Path $root 'data\comparisons\p4-t06-g4d-independent-reproduction-v1.json'
$candidateManifest = Join-Path $root 'data\comparisons\p4-t06-g4d-independent-reproduction-v1.manifest.json'
$output = Join-Path $root 'data\comparisons\p4-t06-g4f-analytical-reference-v1.json'
$outputManifest = Join-Path $root 'data\comparisons\p4-t06-g4f-analytical-reference-v1.manifest.json'

Push-Location $dotnetWorkingDirectory
try {
    dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $project --configuration Release --no-build -- generate `
        $source $pack $packManifest $benchmark $candidate $candidateManifest $output $outputManifest
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run --project $project --configuration Release --no-build -- validate `
        $source $pack $packManifest $benchmark $candidate $candidateManifest $output $outputManifest
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

$doc = Get-Content -Raw $output | ConvertFrom-Json
$manifest = Get-Content -Raw $outputManifest | ConvertFrom-Json
if ($doc.status -ne 'candidate' -or $doc.evidence_approval -ne 'Candidate' -or
    $doc.comparison_status -ne 'Deferred' -or $doc.tolerance_status -ne 'Deferred' -or
    $doc.golden_status -ne 'NoGolden') { throw 'G4F candidate/deferred boundary changed' }
if (@($doc.cases).Count -ne 3) { throw 'G4F case count changed' }
if (@($doc.cases | Where-Object { $_.candidate_comparison.comparison_disposition -ne 'DiagnosticOnly' }).Count -ne 0) {
    throw 'G4F comparison disposition changed'
}
if ($manifest.artifact_sha256 -ne ((Get-FileHash -Algorithm SHA256 -LiteralPath $output).Hash.ToLowerInvariant())) {
    throw 'G4F manifest does not bind artifact hash'
}
$paths = @($output, $outputManifest)
foreach ($path in $paths) {
    if (@(Select-String -LiteralPath $path -Pattern '(?i)[A-Z]:[\\/]|\\\\[^\\/]+[\\/]|(^|[\r\n"=,:(\[\s])/[^\s/]').Count -ne 0) {
        throw "Host-local path leaked into $path"
    }
}
Write-Output ('P4_T06_G4F_TEST_PASS cases=' + @($doc.cases).Count +
    ' artifact_sha256=' + $manifest.artifact_sha256 +
    ' comparison=DiagnosticOnly tolerance=Deferred golden=NoGolden')
