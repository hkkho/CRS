#Requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $DemoPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-unity-demo\ReactorGameDemo.exe')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedDemoPath = [System.IO.Path]::GetFullPath($DemoPath)
if (-not (Test-Path -LiteralPath $resolvedDemoPath -PathType Leaf)) {
    throw "The Unity demo executable was not found: $resolvedDemoPath"
}

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testPlayerNames = @('PlayerWithTests', 'P10T04DesktopSmoke')
$stalePlayers = @()
foreach ($candidate in @(Get-Process -Name $testPlayerNames -ErrorAction SilentlyContinue)) {
    $candidatePath = $null
    try {
        $candidatePath = $candidate.Path
    }
    catch {
        # A path that cannot be read is not safe to classify as project-owned.
        continue
    }

    if ([string]::IsNullOrWhiteSpace($candidatePath)) {
        continue
    }

    $path = [System.IO.Path]::GetFullPath([string]$candidatePath)
    if ($path.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        $path -match '(?i)\\candu-[^\\]+\\') {
        $stalePlayers += $candidate
    }
}

foreach ($stalePlayer in $stalePlayers) {
    $stalePath = [string]$stalePlayer.ExecutablePath
    Stop-Process -Id ([int]$stalePlayer.ProcessId) -ErrorAction Stop
    Write-Output "Stopped stale Candu Unity Test Runner player: $stalePath"
}

$demoProcess = Start-Process -FilePath $resolvedDemoPath -WorkingDirectory (Split-Path -Parent $resolvedDemoPath) -PassThru
Write-Output "Started offline Unity graphical demo: $resolvedDemoPath (PID $($demoProcess.Id))"
