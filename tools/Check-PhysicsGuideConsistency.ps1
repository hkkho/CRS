$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$guidePath = Join-Path $repoRoot 'docs/physics/candu-nuclear-diffusion-student-guide.md'
$pdfPath = Join-Path $repoRoot 'output/pdf/candu-nuclear-diffusion-student-guide.pdf'
$privatePdfPath = Join-Path $repoRoot 'tmp/private-physics-download-site/public/candu-nuclear-diffusion-student-guide.pdf'
$planPath = Join-Path $repoRoot 'docs/Implementation_plan.md'
$scopePath = Join-Path $repoRoot 'docs/PROJECT_SCOPE.md'
$builderPath = Join-Path $repoRoot 'tools/build_physics_guide_pdf.py'

foreach ($path in @($guidePath, $pdfPath, $privatePdfPath, $planPath, $scopePath, $builderPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required guide artifact is missing: $path"
    }
}

$guide = Get-Content -LiteralPath $guidePath -Raw -Encoding utf8
$plan = Get-Content -LiteralPath $planPath -Raw -Encoding utf8
$scope = Get-Content -LiteralPath $scopePath -Raw -Encoding utf8

foreach ($maintenanceTerm in @(
    'docs/physics/candu-nuclear-diffusion-student-guide.md',
    'tools/build_physics_guide_pdf.py',
    'tools/Check-PhysicsGuideConsistency.ps1'
)) {
    if (-not $plan.Contains($maintenanceTerm)) {
        throw "Implementation plan is missing the living-guide maintenance marker: $maintenanceTerm"
    }
}

foreach ($scopeTerm in @('P4-T05', 'P4-T06-R3', 'P4-T06-G4H', 'G4-R3', 'G4-R6', 'P5-T01', 'P5-T02', 'P5-T03', 'P5-T04', 'P5-T05', 'P5-T06', 'P5-T07', 'P5-T08', 'P5-T09', 'P5-T10', 'P5-T16', 'P6-T01', 'P6-T07', 'G4', 'G5', 'G6', 'FORCED CLOSED / WAIVED')) {
    if (-not $scope.Contains($scopeTerm)) {
        throw "Project scope is missing the required guide-status marker: $scopeTerm"
    }
}

$requiredGuideTerms = @(
    '# Nuclear Diffusion Theory in a CANDU Reactor',
    '**Guide version:** 2.7',
    '**Status:** Living documentation',
    '**A bounded static-solver foundation is implemented in Core.**',
    'P4-T01:',
    'P4-T02:',
    'P4-T03:',
    'P4-T04',
    'P4-T05',
    'P4-T08',
    'P4-T06-R3',
    'P5-T01:',
    'P5-T02:',
    'P5-T03:',
    'P5-T04:',
    'P5-T05:',
    'P5-T06:',
    'P5-T07:',
    'P5-T08:',
    'P5-T09:',
    'Synthetic case coverage',
    'Model geometry and ReducedModel visualizations',
    'Channel-plane cross-section',
    'G4-R6',
    'P4-T06-G4H',
    'P5-T10 is complete for the G4-R6-approved ReducedModel',
    'Phase 5/G5',
    'G5 is',
    'P6-T01 through P6-T07',
    'Phase 6/G6 is a',
    '`CONDITIONAL PASS` for the bounded synthetic/test-only Core/ReducedModel scope',
    'Current test results and test-runner limitation',
    'Core 154/154 PASS; Golden 19/19 PASS',
    'Test-FullHeadlessSuite.ps1',
    'G2 is FORCED CLOSED / WAIVED',
    '## 5. The static two-group model: specified and bounded implementation',
    '## 10. How this physics feeds the game',
    '## 13. Living-document maintenance and consistency checks',
    'Check-PhysicsGuideConsistency.ps1',
    'S1-R02',
    'S5-R03',
    'S6-R05',
    'reduced-model-interpolation-boundary-v1'
)
foreach ($term in $requiredGuideTerms) {
    if (-not $guide.Contains($term)) {
        throw "Guide is missing required consistency marker: $term"
    }
}

if ($guide -match '[\u2010-\u2015\u2212]') {
    throw 'Guide contains a non-ASCII dash; use ASCII hyphens for PDF portability.'
}

$localLinkPattern = '\[[^\]]+\]\(([^)]+)\)'
foreach ($match in [regex]::Matches($guide, $localLinkPattern)) {
    $target = $match.Groups[1].Value.Trim('<>')
    if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*://' -or $target.StartsWith('#')) {
        continue
    }
    $relative = $target.Split('#')[0]
    $resolved = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $guidePath -Parent) $relative))
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Guide link target is missing: $target -> $resolved"
    }
}

$p2T01Spec = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/spec/topology-indexing-units-boundaries-v1.md') -Raw -Encoding utf8
$p2T02Spec = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/spec/two-group-solver-normalization-convergence-v1.md') -Raw -Encoding utf8
if (-not $p2T01Spec.Contains('**Status:** frozen P2-T01 implementation input.')) {
    throw 'P2-T01 status changed; update the guide.'
}
if (-not $p2T02Spec.Contains('**Status:** frozen P2-T02 implementation input.')) {
    throw 'P2-T02 status changed; update the guide.'
}

foreach ($taskId in @(
    'P2-T01', 'P2-T02', 'P2-T03', 'P2-T04', 'P2-T05',
    'P3-T01', 'P3-T02', 'P3-T03', 'P3-T04', 'P3-T05',
    'P4-T01', 'P4-T02', 'P4-T03', 'P4-T04', 'P4-T05', 'P4-T06-R2D', 'P4-T06-R3', 'P4-T06-R4', 'P4-T06-R5', 'P4-T06-G4G', 'P5-T01', 'P5-T02', 'P5-T03', 'P5-T04', 'P5-T05', 'P5-T06', 'P5-T07', 'P5-T08', 'P5-T09', 'P5-T10', 'P5-T16', 'P6-T01', 'P6-T02', 'P6-T03', 'P6-T04', 'P6-T05', 'P6-T06', 'P6-T07'
)) {
    $reportPath = Join-Path $repoRoot "docs/tasks/$taskId.md"
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "Physics/status task report is missing: $reportPath"
    }
    $report = Get-Content -LiteralPath $reportPath -Raw -Encoding utf8
    if ($report -notmatch 'Status:\s+`?COMPLETE\b') {
        throw "$taskId is not marked COMPLETE; update the guide status section."
    }
}

$p6T02 = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/tasks/P6-T02.md') -Raw -Encoding utf8
if ($p6T02 -notmatch 'Reopened execution addendum' -or
    $p6T02 -notmatch 'Status:\s+`COMPLETE / SYNTHETIC TEST-ONLY`') {
    throw 'P6-T02 does not preserve its reopened synthetic/test-only controlling disposition.'
}

$g4R3TaskPath = Join-Path $repoRoot 'docs/tasks/G4-R3.md'
if (-not (Test-Path -LiteralPath $g4R3TaskPath -PathType Leaf)) {
    throw "G4-R3 task report is missing: $g4R3TaskPath"
}
$g4R3Task = Get-Content -LiteralPath $g4R3TaskPath -Raw -Encoding utf8
if ($g4R3Task -notmatch 'Status: BLOCKED' -or $g4R3Task -notmatch 'no approved numeric tolerance') {
    throw 'G4-R3 task report no longer records the historical blocked disposition.'
}

$g3 = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/gates/G3.md') -Raw -Encoding utf8
if ($g3 -notmatch '## Result\s+PASS') {
    throw 'G3 is not recorded as PASS; update the guide status section.'
}

$g5 = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/gates/G5.md') -Raw -Encoding utf8
if ($g5 -notmatch '## Result\s+`PASS`' -or $g5 -notmatch 'approved `ReducedModel` / engine-neutral Core scope') {
    throw 'G5 is not recorded as the bounded ReducedModel/Core PASS; update the guide status section.'
}

$g6 = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/gates/G6.md') -Raw -Encoding utf8
if ($g6 -notmatch '## Result\s+`CONDITIONAL PASS`' -or $g6 -notmatch 'synthetic/test-only') {
    throw 'G6 is not recorded as the bounded synthetic/test-only conditional pass; update the guide status section.'
}

$coreFiles = @(
    'src/ReactorSim.Core/Domain/SpatialStencilContracts.cs',
    'src/ReactorSim.Core/Domain/SpatialOperatorContracts.cs',
    'src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs',
    'src/ReactorSim.Core/Domain/SpatialConvergenceContracts.cs',
    'src/ReactorSim.Core/Domain/RefuellingSchemeContracts.cs',
    'src/ReactorSim.Core/Domain/RefuellingShiftContracts.cs',
    'src/ReactorSim.Core/Domain/RefuellingEventContracts.cs',
    'src/ReactorSim.Core/Domain/BurnupTransitionContracts.cs',
    'src/ReactorSim.Core/Domain/BurnupCoefficientContracts.cs',
    'src/ReactorSim.Core/Domain/SpatialRecomputeContracts.cs',
    'src/ReactorSim.Core/Domain/Phase5InvariantContracts.cs',
    'src/ReactorSim.Core/Domain/Phase6LiquidZoneContracts.cs',
    'src/ReactorSim.Core/Domain/Phase6LiquidZoneInfluenceMapContracts.cs',
    'src/ReactorSim.Core/Domain/Phase6AdjusterContracts.cs',
    'src/ReactorSim.Core/Domain/Phase6BulkPoisonContracts.cs',
    'src/ReactorSim.Core/Domain/Phase6RrsContracts.cs',
    'src/ReactorSim.Core/Domain/Phase6QueueTransitionContracts.cs'
)
foreach ($relativePath in $coreFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath) -PathType Leaf)) {
        throw "Implemented static-solver source is missing: $relativePath"
    }
}

$historyTestPath = Join-Path $repoRoot 'tests/ReactorSim.Core.Tests/P5T08DeterministicHistoryTests.cs'
if (-not (Test-Path -LiteralPath $historyTestPath -PathType Leaf)) {
    throw "Deterministic history test source is missing: $historyTestPath"
}

$guideTime = (Get-Item -LiteralPath $guidePath).LastWriteTimeUtc
$builderTime = (Get-Item -LiteralPath $builderPath).LastWriteTimeUtc
$pdfTime = (Get-Item -LiteralPath $pdfPath).LastWriteTimeUtc
if ($pdfTime -lt $guideTime -or $pdfTime -lt $builderTime) {
    throw "PDF is older than the Markdown source or PDF builder. Rebuild $pdfPath."
}

$outputHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $pdfPath).Hash
$privateHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $privatePdfPath).Hash
if ($outputHash -ne $privateHash) {
    throw 'The private-site PDF copy differs from the regenerated output PDF.'
}

$pageCountText = & python -c "from pypdf import PdfReader; print(len(PdfReader(r'$pdfPath').pages))" 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "pypdf page-count check failed: $pageCountText"
}
$pageCount = 0
if (-not [int]::TryParse(($pageCountText | Select-Object -Last 1), [ref]$pageCount)) {
    throw "pypdf did not report a page count: $pageCountText"
}
if ($pageCount -lt 1) {
    throw "PDF page count is invalid: $pageCount"
}

"PHYSICS_GUIDE_CONSISTENCY_PASS pages=$pageCount core_static_solver=present private_pdf_sync=PASS guide_sha256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $guidePath).Hash)"
