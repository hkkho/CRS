param(
    [string]$ArtifactsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-p4-t06-r5-focused')
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$source = Join-Path $repoRoot 'data\packs\p4-t06-r4-synthetic-input-v1.json'
$pack = Join-Path $repoRoot 'data\packs\p4-t06-r4-reduced-candidate-v1.json'
$manifest = Join-Path $repoRoot 'data\packs\p4-t06-r4-reduced-candidate-v1.manifest.json'
$comparisonProject = Join-Path $repoRoot 'tools\SpatialComparison\ReactorSim.SpatialComparison.csproj'
$physicsProject = Join-Path $repoRoot 'tools\PhysicsData\ReactorSim.PhysicsData.csproj'
$output = Join-Path $ArtifactsPath 'p4-t06-r5-candidate-snapshots-v1.json'
$repeatOutput = Join-Path $ArtifactsPath 'p4-t06-r5-candidate-snapshots-v1-repeat.json'

if (Test-Path -LiteralPath $ArtifactsPath) {
    Remove-Item -LiteralPath $ArtifactsPath -Recurse -Force
}
New-Item -ItemType Directory -Path $ArtifactsPath -Force | Out-Null

$dotnetWorkingDirectory = Split-Path -Parent $repoRoot
Push-Location $dotnetWorkingDirectory
try {
    dotnet build $physicsProject --configuration Release --nologo
    dotnet run --project $physicsProject --configuration Release --no-build -- validate $pack $manifest $source
    dotnet build $comparisonProject --configuration Release --nologo
    dotnet run --project $comparisonProject --configuration Release --no-build -- generate $source $pack $manifest $output
    dotnet run --project $comparisonProject --configuration Release --no-build -- validate $source $pack $manifest $output
    dotnet run --project $comparisonProject --configuration Release --no-build -- generate $source $pack $manifest $repeatOutput
}
finally {
    Pop-Location
}

$firstBytes = [System.IO.File]::ReadAllBytes($output)
$secondBytes = [System.IO.File]::ReadAllBytes($repeatOutput)
if (-not [System.Linq.Enumerable]::SequenceEqual($firstBytes, $secondBytes)) {
    throw 'R5 deterministic regeneration did not produce byte-identical evidence.'
}

$tamperedManifest = Join-Path $ArtifactsPath 'tampered-manifest.json'
$tamperedManifestObject = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
$tamperedManifestObject.tables[0].row_count = $tamperedManifestObject.tables[0].row_count + 1
$tamperedManifestObject | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $tamperedManifest -Encoding utf8
Push-Location $dotnetWorkingDirectory
try {
    dotnet run --project $comparisonProject --configuration Release --no-build -- validate $source $pack $tamperedManifest $output
    if ($LASTEXITCODE -eq 0) {
        throw 'R5 accepted a manifest with a mismatched per-table row count.'
    }
}
finally {
    Pop-Location
}

$tamperedSource = Join-Path $ArtifactsPath 'tampered-source.json'
$tamperedSourceObject = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
$tamperedSourceObject.source_provenance = 'C:\private\source.json'
$tamperedSourceObject | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $tamperedSource -Encoding utf8
Push-Location $dotnetWorkingDirectory
try {
    dotnet run --project $comparisonProject --configuration Release --no-build -- validate $tamperedSource $pack $manifest $output
    if ($LASTEXITCODE -eq 0) {
        throw 'R5 accepted a host-local source provenance path.'
    }
}
finally {
    Pop-Location
}

$document = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
if ($document.status -ne 'candidate' -or $document.evidence_class -ne 'synthetic') {
    throw 'R5 output is not marked candidate synthetic evidence.'
}
if (($document.cases | Where-Object { $_.status -eq 'CandidateSnapshot' }).Count -ne 3) {
    throw 'R5 did not produce three converged candidate snapshots.'
}
if (($document.cases | Where-Object { $_.status -eq 'Nonconverged' }).Count -ne 1) {
    throw 'R5 did not preserve the mixed-burnup nonconverged case with its truthful status.'
}
if (($document.cases | Where-Object { $_.status -eq 'NotCovered' }).Count -ne 2) {
    throw 'R5 did not preserve the deferred RRS and poison cases as NotCovered.'
}
if (($document.records | Where-Object { $_.status -ne 'Deferred' }).Count -ne 0) {
    throw 'R5 emitted a comparison record without deferred gate status.'
}
if ($document.records.Count -ne 57) {
    throw 'R5 did not emit the expected typed-scope record count.'
}
if (($document.records | Select-Object -ExpandProperty observable_id -Unique).Count -ne $document.records.Count) {
    throw 'R5 emitted duplicate ObservableId values.'
}
if (($document.cases | Where-Object { $_.status -eq 'CandidateSnapshot' -and ($_.snapshot.comparison.exact_bitwise_equal -ne $true) }).Count -ne 0) {
    throw 'R5 reduced-pack/source-contract comparisons were not exact.'
}
if (($document.records | Where-Object {
        [string]::IsNullOrWhiteSpace($_.value.payload_schema_id) -or
        $null -eq $_.value.component_order_spec -or
        $_.state_binding.snapshot_digest -eq 'NotApplicable' -or
        $_.state_binding.coefficient_digest -eq $_.input_digest
    }).Count -ne 0) {
    throw 'R5 emitted a record without canonical payload binding or usable solve/snapshot identity.'
}
if (($document.records | Where-Object {
        $_.quantity_id -eq 'spatial.total_power' -and
        ($_.scope.kind -ne 'Global' -or $_.scope.key.key_kind -ne 'NodeSet' -or $_.scope.key.node_set.Count -ne 3)
    }).Count -ne 0 -or
    ($document.records | Where-Object {
        $_.quantity_id -eq 'spatial.flux' -and
        ($_.scope.kind -ne 'Vector' -or $_.scope.key.vector_kind -ne 'NodeGroup' -or $_.scope.key.node_groups.Count -ne 6)
    }).Count -ne 0 -or
    ($document.records | Where-Object {
        $_.quantity_id -eq 'spatial.coefficient_identity' -and
        ($_.scope.kind -ne 'Lookup' -or $_.scope.key.owner_kind -ne 'NodeGroup' -or
         [string]::IsNullOrWhiteSpace($_.scope.key.table_id) -or $null -eq $_.scope.key.bracket)
    }).Count -ne 0) {
    throw 'R5 emitted a free-form or incomplete typed P2-T05 scope.'
}
if (($document.records | Where-Object {
        $_.quantity_id -eq 'determinism.repeat_equal' -and
        ($_.scope.kind -ne 'RunPair' -or $null -eq $_.repeat_binding -or
         [string]::IsNullOrWhiteSpace($_.repeat_binding.evidence_digest) -or
         [string]::IsNullOrWhiteSpace($_.repeat_binding.evidence_bytes_hex))
    }).Count -ne 0) {
    throw 'R5 repeat evidence is not independently bound to a typed run pair.'
}
$fluxRecords = @($document.records | Where-Object { $_.quantity_id -eq 'spatial.flux' })
if ($fluxRecords.Count -ne 3) {
    throw 'R5 did not emit one vector value binding per converged candidate snapshot.'
}
foreach ($record in $fluxRecords) {
    $spec = $record.value.component_order_spec
    if ($spec.order_kind_ordinal -ne 0 -or
        $spec.component_key_schema_id -ne 'NodeGroupKeyV1' -or
        $spec.comparator_ordinal -ne 0 -or
        $spec.tie_break_schema_id -ne 'ChannelPositionGroupV1') {
        throw 'R5 vector ComponentOrderSpec does not match the complete P2-T05 profile.'
    }
}

$previousOrderKey = $null
foreach ($record in @($document.records)) {
    $order = $record.order_key
    $orderKey = '{0:D2}|{1:R}|{2:D20}|{3:D5}|{4:D20}|{5}|{6:D2}' -f `
        $order.validation_domain_rank,
        $order.simulation_time_seconds,
        $order.core_state_version,
        $order.event_rank_sort,
        $order.sequence_sort,
        $order.event_id_or_not_applicable,
        $order.scope_kind_rank
    if ($null -ne $previousOrderKey -and [string]::CompareOrdinal($previousOrderKey, $orderKey) -gt 0) {
        throw 'R5 records are not sorted by the P2-T05 rank tuple.'
    }
    $previousOrderKey = $orderKey
}

Write-Output ('P4_T06_R5_FOCUSED_PASS snapshots=3 nonconverged=1 not_covered=2 records=' + $document.records.Count)
