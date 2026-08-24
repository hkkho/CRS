#Requires -Version 5.1

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repositoryRoot
try {
    & dotnet format ReactorSim.sln whitespace --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
