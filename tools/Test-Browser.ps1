[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$initialLocation = Get-Location
$dotnetRunner = Join-Path $PSScriptRoot 'Test-DotNet.ps1'
$webRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'web\candu-playtest'

try {
    & $dotnetRunner -Suite Browser
    if ($LASTEXITCODE -ne 0) {
        throw "Browser .NET tests failed (exit code $LASTEXITCODE)."
    }

    if (-not (Test-Path -LiteralPath $webRoot -PathType Container)) {
        throw "Browser project not found: $webRoot"
    }

    Set-Location -LiteralPath $webRoot

    & npm test
    if ($LASTEXITCODE -ne 0) {
        throw "Browser npm tests failed (exit code $LASTEXITCODE)."
    }

    & npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "Browser npm build failed (exit code $LASTEXITCODE)."
    }
}
finally {
    Set-Location -LiteralPath $initialLocation.Path
}
