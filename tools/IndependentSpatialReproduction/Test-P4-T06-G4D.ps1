[CmdletBinding()]
param(
    [string]$DotnetPath = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'ReactorSim.IndependentSpatialReproduction.csproj'
$source = Join-Path $PSScriptRoot '..\..\data\packs\p4-t06-r4-synthetic-input-v1.json'
$pack = Join-Path $PSScriptRoot '..\..\data\packs\p4-t06-r4-reduced-candidate-v1.json'
$reducedManifest = Join-Path $PSScriptRoot '..\..\data\packs\p4-t06-r4-reduced-candidate-v1.manifest.json'
$benchmark = Join-Path $PSScriptRoot '..\..\benchmarks\P4-T08-static-solver-benchmark.json'

$projectText = Get-Content -Raw -LiteralPath $project
if ($projectText -match 'ProjectReference|ReactorSim\.Core|SpatialComparison') {
    throw 'G4D independence boundary failed: runtime or candidate project reference found.'
}

& $DotnetPath build $project --configuration Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'G4D build failed.'
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('p4-t06-g4d-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    $artifact = Join-Path $tempRoot 'reproduction.json'
    $manifest = Join-Path $tempRoot 'reproduction.manifest.json'
    $artifactRepeat = Join-Path $tempRoot 'reproduction-repeat.json'
    $manifestRepeat = Join-Path $tempRoot 'reproduction-repeat.manifest.json'

    & $DotnetPath run --no-build --project $project --configuration Release -- generate $source $pack $reducedManifest $benchmark $artifact $manifest
    if ($LASTEXITCODE -ne 0) {
        throw 'G4D generation failed.'
    }
    & $DotnetPath run --no-build --project $project --configuration Release -- validate $source $pack $reducedManifest $benchmark $artifact $manifest
    if ($LASTEXITCODE -ne 0) {
        throw 'G4D validation failed.'
    }
    & $DotnetPath run --no-build --project $project --configuration Release -- generate $source $pack $reducedManifest $benchmark $artifactRepeat $manifestRepeat
    if ($LASTEXITCODE -ne 0) {
        throw 'G4D repeat generation failed.'
    }

    $artifactBytes = [System.IO.File]::ReadAllBytes($artifact)
    $artifactRepeatBytes = [System.IO.File]::ReadAllBytes($artifactRepeat)
    $manifestBytes = [System.IO.File]::ReadAllBytes($manifest)
    $manifestRepeatBytes = [System.IO.File]::ReadAllBytes($manifestRepeat)
    if (-not [System.Security.Cryptography.CryptographicOperations]::FixedTimeEquals($artifactBytes, $artifactRepeatBytes)) {
        throw 'G4D artifact bytes changed across repeat generation.'
    }
    if (-not [System.Security.Cryptography.CryptographicOperations]::FixedTimeEquals($manifestBytes, $manifestRepeatBytes)) {
        throw 'G4D manifest bytes changed across repeat generation.'
    }

    $document = Get-Content -Raw -LiteralPath $artifact | ConvertFrom-Json
    $manifestDocument = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json
    if ($document.status -ne 'candidate' -or $document.cases.Count -ne 3 -or $document.records.Count -ne 57) {
        throw 'G4D output shape or candidate boundary failed.'
    }
    if (@($document.cases | Where-Object { $_.status -ne 'CandidateSnapshot' -or -not $_.repeat_equal }).Count -ne 0) {
        throw 'G4D admitted case determinism/status check failed.'
    }
    if (@($document.records | Where-Object { $_.status -ne 'Deferred' -or $_.evidence_approval -ne 'Candidate' }).Count -ne 0) {
        throw 'G4D record status boundary failed.'
    }

    $hashBindings = @(
        @{ Artifact = 'source'; Path = $source; DocumentField = 'source_sha256'; ManifestField = 'source_sha256' },
        @{ Artifact = 'pack'; Path = $pack; DocumentField = 'pack_sha256'; ManifestField = 'pack_sha256' },
        @{ Artifact = 'reduced-manifest'; Path = $reducedManifest; DocumentField = 'reduced_manifest_sha256'; ManifestField = 'reduced_manifest_sha256' },
        @{ Artifact = 'benchmark'; Path = $benchmark; DocumentField = 'benchmark_manifest_sha256'; ManifestField = 'benchmark_manifest_sha256' }
    )
    foreach ($binding in $hashBindings) {
        $boundHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $binding.Path).Hash.ToLowerInvariant()
        if ($document.($binding.DocumentField) -ne $boundHash -or $manifestDocument.($binding.ManifestField) -ne $boundHash) {
            throw ('G4D hash binding failed for ' + $binding.Artifact + '.')
        }
    }

    $expectedProfileCounts = @{
        'P2-T05-determinism-repeat-equal-v1' = 3
        'P2-T05-spatial-coefficient-id-v1' = 18
        'P2-T05-spatial-convergence-v1' = 3
        'P2-T05-spatial-fission-source-v1' = 9
        'P2-T05-spatial-flux-v1' = 3
        'P2-T05-spatial-iteration-count-v1' = 3
        'P2-T05-spatial-k-v1' = 3
        'P2-T05-spatial-normalization-scale-v1' = 3
        'P2-T05-spatial-power-v1' = 9
        'P2-T05-spatial-total-power-v1' = 3
    }
    $actualProfileCounts = @{}
    foreach ($group in ($document.records | Group-Object -Property tolerance_profile_id)) {
        $actualProfileCounts[$group.Name] = $group.Count
    }
    if ($actualProfileCounts.Count -ne $expectedProfileCounts.Count) {
        throw 'G4D profile distribution shape failed.'
    }
    foreach ($profile in $expectedProfileCounts.Keys) {
        if (-not $actualProfileCounts.ContainsKey($profile) -or $actualProfileCounts[$profile] -ne $expectedProfileCounts[$profile]) {
            throw ('G4D profile distribution failed for ' + $profile + '.')
        }
    }

    $scopeRanks = @{
        'Global' = 0
        'Entity' = 1
        'Vector' = 2
        'Solve' = 6
        'RunPair' = 10
        'Lookup' = 15
    }
    $previousOrderKey = $null
    foreach ($record in $document.records) {
        if ($null -eq $record.order_key -or
            $record.order_key.validation_domain -ne 'Runtime' -or
            $record.order_key.validation_domain_rank -ne 1 -or
            $record.order_key.component_key -ne 'not_applicable' -or
            $record.order_key.scope_kind -ne $record.scope.kind -or
            $record.order_key.scope_kind_rank -ne $scopeRanks[$record.scope.kind] -or
            $record.order_key.quantity_id -ne $record.quantity_id -or
            $record.order_key.observable_id -ne $record.observable_id) {
            throw ('G4D P2-T05 order key failed for ' + $record.quantity_id + '.')
        }
        $currentOrderKey = @(
            $record.order_key.validation_domain_rank.ToString('D2')
            $record.order_key.simulation_time_seconds.ToString('R', [System.Globalization.CultureInfo]::InvariantCulture)
            $record.order_key.core_state_version.ToString('D20')
            $record.order_key.event_rank_sort.ToString('D2')
            $record.order_key.sequence_sort.ToString('D20')
            $record.order_key.scope_kind_rank.ToString('D2')
            ($record.order_key.scope_key | ConvertTo-Json -Compress -Depth 20)
            $record.order_key.quantity_id
            $record.order_key.component_key
            $record.order_key.observable_id
        ) -join '|'
        if ($null -ne $previousOrderKey -and [StringComparer]::Ordinal.Compare($previousOrderKey, $currentOrderKey) -gt 0) {
            throw ('G4D record order is not deterministic/canonical at ' + $record.quantity_id + '.')
        }
        $previousOrderKey = $currentOrderKey
        if ($record.input_digest -ne $record.state_binding.input_digest -or
            [string]::IsNullOrWhiteSpace($record.state_binding.topology_fixture_id) -or
            [string]::IsNullOrWhiteSpace($record.state_binding.data_pack_artifact_id) -or
            [string]::IsNullOrWhiteSpace($record.state_binding.benchmark_manifest_sha256) -or
            $record.state_binding.core_state_version -ne '0' -or
            $record.state_binding.spatial_state_version -ne '0' -or
            [string]::IsNullOrWhiteSpace($record.state_binding.spatial_solve_id) -or
            [string]::IsNullOrWhiteSpace($record.state_binding.power_snapshot_id) -or
            $record.state_binding.power_snapshot_version -ne '0' -or
            $record.state_binding.topology_version -ne $document.topology_fixture_id -or
            $record.state_binding.data_pack_version -ne $document.pack_artifact_id -or
            [string]::IsNullOrWhiteSpace($record.state_binding.coefficient_digest) -or
            [string]::IsNullOrWhiteSpace($record.state_binding.snapshot_digest)) {
            throw ('G4D complete state binding failed for ' + $record.quantity_id + '.')
        }
        switch ($record.scope.kind) {
            'Global' {
                if ($record.scope.key.key_kind -ne 'NodeSet' -or $record.scope.key.node_set.Count -ne 3) { throw 'G4D Global typed scope failed.' }
            }
            'Entity' {
                if ($record.scope.key.entity_kind -ne 'Node' -or $null -eq $record.scope.key.node) { throw 'G4D Entity typed scope failed.' }
            }
            'Vector' {
                if ($record.scope.key.vector_kind -ne 'NodeGroup' -or $record.scope.key.node_groups.Count -ne 6) { throw 'G4D Vector typed scope failed.' }
                if ($record.value.component_order_spec.order_kind_ordinal -ne 0 -or
                    $record.value.component_order_spec.component_key_schema_id -ne 'NodeGroupKeyV1' -or
                    $record.value.component_order_spec.comparator_ordinal -ne 0 -or
                    $record.value.component_order_spec.tie_break_schema_id -ne 'ChannelPositionGroupV1') { throw 'G4D vector component order failed.' }
            }
            'Solve' {
                if ($null -eq $record.scope.key.solve_id -or $record.scope.key.group_index -ne 'NotApplicable') { throw 'G4D Solve typed scope failed.' }
            }
            'Lookup' {
                if ($record.scope.key.owner_kind -ne 'NodeGroup' -or
                    $null -eq $record.scope.key.node_group -or
                    $null -eq $record.scope.key.table_id -or
                    $null -eq $record.scope.key.bracket -or
                    $record.scope.key.bracket.result_status -ne 'InRange') { throw 'G4D Lookup typed scope failed.' }
            }
            'RunPair' {
                if ($null -eq $record.scope.key.run_id_a -or $null -eq $record.scope.key.run_id_b) { throw 'G4D RunPair typed scope failed.' }
            }
            default { throw ('G4D unexpected scope kind ' + $record.scope.kind + '.') }
        }
        if ($record.scope.kind -ne 'Vector' -and $record.value.component_order_spec -ne 'NotApplicable') {
            throw ('G4D non-vector component order failed for ' + $record.quantity_id + '.')
        }
    }

    $mutations = @(
        @{ Label = 'source'; Input = $source; Replacement = '"absorption_group1_per_m": 0.41' },
        @{ Label = 'pack'; Input = $pack; Replacement = '"absorption_group1_per_m": 0.41' },
        @{ Label = 'reduced-manifest'; Input = $reducedManifest; Replacement = '"approval_status": "candidate-mutated"' },
        @{ Label = 'benchmark'; Input = $benchmark; Replacement = '"target_power_w": 0.91' }
    )
    foreach ($mutation in $mutations) {
        $mutatedPath = Join-Path $tempRoot ('mutated-' + $mutation.Label + '.json')
        $mutatedText = Get-Content -Raw -LiteralPath $mutation.Input
        if ($mutation.Label -eq 'source' -or $mutation.Label -eq 'pack') {
            $mutatedText = $mutatedText.Replace('"absorption_group1_per_m": 0.4', $mutation.Replacement)
        }
        elseif ($mutation.Label -eq 'reduced-manifest') {
            $mutatedText = $mutatedText.Replace('"approval_status": "candidate"', $mutation.Replacement)
        }
        else {
            $mutatedText = $mutatedText.Replace('"target_power_w": 0.9', $mutation.Replacement)
        }
        if ($mutatedText -eq (Get-Content -Raw -LiteralPath $mutation.Input)) {
            throw ('G4D mutation fixture was not changed for ' + $mutation.Label + '.')
        }
        [System.IO.File]::WriteAllText($mutatedPath, $mutatedText, [System.Text.UTF8Encoding]::new($false))
        $sourceArgument = if ($mutation.Label -eq 'source') { $mutatedPath } else { $source }
        $packArgument = if ($mutation.Label -eq 'pack') { $mutatedPath } else { $pack }
        $manifestArgument = if ($mutation.Label -eq 'reduced-manifest') { $mutatedPath } else { $reducedManifest }
        $benchmarkArgument = if ($mutation.Label -eq 'benchmark') { $mutatedPath } else { $benchmark }
        & $DotnetPath run --no-build --project $project --configuration Release -- generate $sourceArgument $packArgument $manifestArgument $benchmarkArgument (Join-Path $tempRoot ('invalid-' + $mutation.Label + '.json')) (Join-Path $tempRoot ('invalid-' + $mutation.Label + '.manifest.json'))
        if ($LASTEXITCODE -eq 0) {
            throw ('G4D accepted a hash-mismatched ' + $mutation.Label + ' fixture.')
        }
    }

    $artifactHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifact).Hash.ToLowerInvariant()
    $manifestHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifest).Hash.ToLowerInvariant()
    Write-Output ('P4_T06_G4D_TEST_PASS cases=' + $document.cases.Count + ' records=' + $document.records.Count + ' artifact_sha256=' + $artifactHash + ' manifest_sha256=' + $manifestHash + ' typed_schema=true profile_distribution=true mutations_rejected=4')
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
