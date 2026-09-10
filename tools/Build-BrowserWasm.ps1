[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$CommitSha = $env:GITHUB_SHA,
    [string]$TargetPath,
    [string]$StagingPath,
    [switch]$RunAOTCompilation,
    [ValidateSet('default', 'true', 'false')]
    [string]$WasmEnableSIMD = 'default'
)

$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectPath = Join-Path $repoRoot 'src/ReactorSim.BrowserHost/ReactorSim.BrowserHost.csproj'
$webRoot = Join-Path $repoRoot 'web/candu-playtest'
$defaultTargetPath = Join-Path $webRoot 'public/wasm'
$defaultStagingPath = Join-Path $repoRoot 'tmp/browser-wasm-publish'
$tmpRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'tmp'))

function Resolve-ManagedPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $candidate = if ([IO.Path]::IsPathRooted($Path)) {
        $Path
    } else {
        Join-Path $repoRoot $Path
    }
    $resolved = [IO.Path]::GetFullPath($candidate)
    $repoPrefix = $repoRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($resolved.Equals($repoRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not $resolved.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must be a directory below the repository root: $resolved"
    }
    return $resolved
}

function Test-StrictChildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Parent
    )

    $parentPrefix = $Parent.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    return $Path.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase)
}

$targetPath = Resolve-ManagedPath -Path $(if ([string]::IsNullOrWhiteSpace($TargetPath)) { $defaultTargetPath } else { $TargetPath }) -Label 'TargetPath'
$stagingPath = Resolve-ManagedPath -Path $(if ([string]::IsNullOrWhiteSpace($StagingPath)) { $defaultStagingPath } else { $StagingPath }) -Label 'StagingPath'

$defaultTargetPath = [IO.Path]::GetFullPath($defaultTargetPath)
if (-not $targetPath.Equals($defaultTargetPath, [StringComparison]::OrdinalIgnoreCase) -and
    -not (Test-StrictChildPath -Path $targetPath -Parent $tmpRoot)) {
    throw "TargetPath must be exactly $defaultTargetPath or a strict child of ${tmpRoot}: $targetPath"
}
if (-not (Test-StrictChildPath -Path $stagingPath -Parent $tmpRoot)) {
    throw "StagingPath must be a strict child of ${tmpRoot}: $stagingPath"
}

$pathsOverlap = $targetPath.Equals($stagingPath, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-StrictChildPath -Path $targetPath -Parent $stagingPath) -or
    (Test-StrictChildPath -Path $stagingPath -Parent $targetPath)
if ($pathsOverlap) {
    throw "TargetPath and StagingPath must not contain one another: TargetPath=$targetPath; StagingPath=$stagingPath"
}

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Browser bridge project was not found: $projectPath"
}

if (Test-Path -LiteralPath $stagingPath) {
    # This directory is disposable publish output owned by this script.
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stagingPath, $targetPath | Out-Null

$publishArguments = @(
    $projectPath,
    '--configuration', $Configuration,
    '--runtime', 'browser-wasm',
    '--output', $stagingPath,
    '-p:WasmEnableThreads=false'
)
if ($RunAOTCompilation) {
    $publishArguments += '-p:RunAOTCompilation=true'
}
if ($WasmEnableSIMD -ne 'default') {
    $publishArguments += "-p:WasmEnableSIMD=$WasmEnableSIMD"
}

dotnet publish @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Browser WASM publish failed with exit code $LASTEXITCODE."
}

$publishedWebRoot = Join-Path $stagingPath 'wwwroot'
if (-not (Test-Path -LiteralPath (Join-Path $stagingPath 'main.mjs') -PathType Leaf) -or
    -not (Test-Path -LiteralPath $publishedWebRoot -PathType Container)) {
    throw "Browser WASM publish did not produce the expected main.mjs and wwwroot outputs."
}

# Keep the checked-in .gitkeep, but never let stale generated assets survive a
# later publish when the SDK changes a content hash.
Get-ChildItem -LiteralPath $targetPath -Force |
    Where-Object { $_.Name -ne '.gitkeep' } |
    Remove-Item -Recurse -Force

function Copy-PublishedFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,
        [string]$RelativeBase = ''
        ,
        [switch]$Recurse
    )

    Get-ChildItem -LiteralPath $SourceRoot -Recurse:$Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($SourceRoot.Length).TrimStart('\', '/')
        $combinedRelativePath = if ([string]::IsNullOrEmpty($RelativeBase)) {
            $relativePath
        } else {
            Join-Path $RelativeBase $relativePath
        }
        $destinationPath = Join-Path $targetPath $combinedRelativePath
        $destinationDirectory = Split-Path -Parent $destinationPath
        New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
    }
}

# The WebAssembly SDK places the custom main module and runtime config at the
# publish root while the boot/runtime assets live below wwwroot.
Copy-PublishedFiles -SourceRoot $stagingPath -RelativeBase ''
Copy-PublishedFiles -SourceRoot $publishedWebRoot -RelativeBase '' -Recurse

if ([string]::IsNullOrWhiteSpace($CommitSha)) {
    try {
        $CommitSha = (git -C $repoRoot rev-parse --verify HEAD 2>$null).Trim()
    } catch {
        $CommitSha = ''
    }
}

if ([string]::IsNullOrWhiteSpace($CommitSha)) {
    $CommitSha = 'unknown'
}

$buildInfo = [ordered]@{
    gitCommitSha      = $CommitSha
    configuration     = $Configuration
    targetFramework   = 'net10.0'
    runtimeIdentifier = 'browser-wasm'
    runAotCompilation  = [bool]$RunAOTCompilation
    wasmEnableSIMD     = $WasmEnableSIMD
    wasmEnableThreads  = $false
}
$buildInfo | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $targetPath 'build-info.json') -Encoding utf8

$mainModulePath = Join-Path $targetPath 'main.mjs'
$wasmFiles = @(Get-ChildItem -LiteralPath $targetPath -Recurse -File -Filter '*.wasm')
if (-not (Test-Path -LiteralPath $mainModulePath -PathType Leaf) -or $wasmFiles.Count -eq 0) {
    throw "Browser WASM staging did not produce main.mjs and at least one .wasm payload."
}

Write-Host "Browser WASM staged at $targetPath"
