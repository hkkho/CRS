[CmdletBinding()]
param([switch]$Stage)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$packRoot = Split-Path -Parent $PSScriptRoot
$canonicalPack = Join-Path $packRoot 'data/packs/candu6-two-group-diffusion-pack-v1.json'
$embeddedPack = Join-Path $packRoot 'src/ReactorSim.Core/EmbeddedData/candu6-two-group-diffusion-pack-v1.json'
if ($Stage) { Copy-Item -LiteralPath $canonicalPack -Destination $embeddedPack -Force }
$canonicalHash = (Get-FileHash -LiteralPath $canonicalPack -Algorithm SHA256).Hash
$embeddedHash = (Get-FileHash -LiteralPath $embeddedPack -Algorithm SHA256).Hash
if ($canonicalHash -ne $embeddedHash) {
    throw 'Embedded diffusion pack differs from canonical data/packs. Run this script with -Stage.'
}
Write-Output "Canonical diffusion pack matches embedded runtime: $canonicalHash"
