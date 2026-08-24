#Requires -Version 5.1

[CmdletBinding()]
param(
    [switch] $ConfirmFullSuite,

    [ValidateNotNullOrEmpty()]
    [string] $ArtifactsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-full-test-artifacts')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-TrxResultsDirectory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Prefix
    )

    $resultsDirectory = Join-Path ([System.IO.Path]::GetTempPath()) (
        "$Prefix-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $resultsDirectory -ErrorAction Stop |
        Out-Null
    return $resultsDirectory
}

function Get-TrxCounterValue {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlElement] $Counters,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $TrxPath
    )

    if (-not $Counters.HasAttribute($Name)) {
        throw "TRX result '$TrxPath' is missing the '$Name' counter."
    }

    $rawValue = $Counters.GetAttribute($Name)
    [Int64] $value = 0
    if (-not [Int64]::TryParse(
            $rawValue,
            [Globalization.NumberStyles]::Integer,
            [Globalization.CultureInfo]::InvariantCulture,
            [ref] $value) -or $value -lt 0) {
        throw "TRX result '$TrxPath' has an invalid '$Name' counter: '$rawValue'."
    }

    return $value
}

function Read-TrxRuns {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string] $ResultsDirectory
    )

    $trxFiles = @(Get-ChildItem -LiteralPath $ResultsDirectory -Recurse -File `
            -Filter '*.trx' -ErrorAction Stop)
    if ($trxFiles.Count -eq 0) {
        throw "VSTest did not produce any TRX results in '$ResultsDirectory'."
    }

    $counterNames = @(
        'total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted',
        'inconclusive', 'passedButRunAborted', 'notRunnable', 'notExecuted',
        'disconnected', 'warning'
    )
    $runs = @()

    foreach ($trxFile in $trxFiles) {
        try {
            $document = New-Object System.Xml.XmlDocument
            $document.LoadXml((Get-Content -Raw -LiteralPath $trxFile.FullName))
        }
        catch {
            throw "Unable to parse TRX result '$($trxFile.FullName)': $($_.Exception.Message)"
        }

        $counters = $document.SelectSingleNode(
            '/*[local-name() = "TestRun"]/*[local-name() = "ResultSummary"]/*[local-name() = "Counters"]')
        if ($null -eq $counters) {
            throw "TRX result '$($trxFile.FullName)' does not contain test counters."
        }

        $counterValues = @{}
        foreach ($counterName in $counterNames) {
            $counterValues[$counterName] = Get-TrxCounterValue `
                -Counters $counters `
                -Name $counterName `
                -TrxPath $trxFile.FullName
        }

        $runs += [PSCustomObject]@{
            TrxPath = $trxFile.FullName
            Total = $counterValues['total']
            Executed = $counterValues['executed']
            Passed = $counterValues['passed']
            Failed = $counterValues['failed']
            Error = $counterValues['error']
            Timeout = $counterValues['timeout']
            Aborted = $counterValues['aborted']
            Inconclusive = $counterValues['inconclusive']
            PassedButRunAborted = $counterValues['passedButRunAborted']
            NotRunnable = $counterValues['notRunnable']
            NotExecuted = $counterValues['notExecuted']
            Disconnected = $counterValues['disconnected']
            Warning = $counterValues['warning']
        }
    }

    return $runs
}

if (-not $ConfirmFullSuite) {
    throw 'The full suite is gated. Re-run with -ConfirmFullSuite only when a task, gate, or risk trigger requires T3.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedArtifactsPath = [System.IO.Path]::GetFullPath($ArtifactsPath)
$resultsDirectory = New-TrxResultsDirectory -Prefix 'candu-full-headless-trx'
Push-Location -LiteralPath $repositoryRoot
try {
    & dotnet test ReactorSim.sln `
        --artifacts-path $resolvedArtifactsPath `
        --results-directory $resultsDirectory `
        --logger 'trx'
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test exited with code $LASTEXITCODE."
    }

    $runs = @(Read-TrxRuns -ResultsDirectory $resultsDirectory)
    [Int64] $executed = 0
    foreach ($run in $runs) {
        $executed += $run.Executed
    }

    $counterSummary = @($runs | ForEach-Object {
            '{0}: total={1}, executed={2}, passed={3}, failed={4}' -f `
                $_.TrxPath, $_.Total, $_.Executed, $_.Passed, $_.Failed
        }) -join '; '
    Write-Output "TRX results directory: $resultsDirectory"
    Write-Output "TRX counters: $counterSummary"

    if ($executed -lt 1) {
        throw "The full headless suite executed zero tests according to TRX results in '$resultsDirectory'."
    }
}
finally {
    Pop-Location
}
