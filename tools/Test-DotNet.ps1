[CmdletBinding()]
param(
    [ValidateSet('Core', 'Game', 'Browser', 'All')]
    [string]$Suite = 'All',
    [switch]$EnableResearchExperiments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$projects = @{
    Core = Join-Path $repoRoot 'tests\ReactorSim.Core.Tests\ReactorSim.Core.Tests.csproj'
    Game = Join-Path $repoRoot 'tests\ReactorSim.Game.Tests\ReactorSim.Game.Tests.csproj'
    Browser = Join-Path $repoRoot 'tests\ReactorSim.Browser.Tests\ReactorSim.Browser.Tests.csproj'
}

$selectedSuites = if ($Suite -eq 'All') { @('Core', 'Game', 'Browser') } else { @($Suite) }

foreach ($selectedSuite in $selectedSuites) {
    $projectPath = $projects[$selectedSuite]
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Test project not found: $projectPath"
    }

    $resolvedProjectPath = (Resolve-Path -LiteralPath $projectPath).Path
    $testArguments = @($resolvedProjectPath, '--nologo', '--verbosity', 'minimal', "-p:EnableResearchExperiments=$($EnableResearchExperiments.IsPresent.ToString().ToLowerInvariant())")
    & dotnet test @testArguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "dotnet test failed for $selectedSuite (exit code $exitCode)."
    }
}
