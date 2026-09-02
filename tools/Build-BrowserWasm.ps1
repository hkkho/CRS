[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src/ReactorSim.BrowserHost/ReactorSim.BrowserHost.csproj'
$webRoot = Join-Path $repoRoot 'web/candu-playtest'
$targetPath = Join-Path $webRoot 'public/wasm'
$stagingPath = Join-Path $repoRoot 'tmp/browser-wasm-publish'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Browser bridge project was not found: $projectPath"
}

if (Test-Path -LiteralPath $stagingPath) {
    # This directory is disposable publish output owned by this script.
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stagingPath, $targetPath | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime browser-wasm `
    --output $stagingPath
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

Write-Host "Browser WASM staged at $targetPath"
