#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Project,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $FullyQualifiedName,

    [ValidateNotNullOrEmpty()]
    [string] $ArtifactsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-focused-test-artifacts')
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

        $testResults = @()
        $resultNodes = $document.SelectNodes(
            '/*[local-name() = "TestRun"]/*[local-name() = "Results"]/*[local-name() = "UnitTestResult"]')
        foreach ($resultNode in $resultNodes) {
            $testName = $resultNode.GetAttribute('testName')
            $outcome = $resultNode.GetAttribute('outcome')
            if ([string]::IsNullOrWhiteSpace($testName) -or
                [string]::IsNullOrWhiteSpace($outcome)) {
                throw "TRX result '$($trxFile.FullName)' contains a test result without a name or outcome."
            }

            $testResults += [PSCustomObject]@{
                TestName = $testName
                Outcome = $outcome
                TrxPath = $trxFile.FullName
            }
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
            TestResults = @($testResults)
        }
    }

    return $runs
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'tests'))
$projectCandidate = if ([System.IO.Path]::IsPathRooted($Project)) {
    $Project
}
else {
    Join-Path $repositoryRoot $Project
}
$projectPath = (Resolve-Path -LiteralPath $projectCandidate).Path
$testsPrefix = $testsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

if (-not $projectPath.StartsWith($testsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Focused tests must select a project under '$testsRoot'."
}

if ([System.IO.Path]::GetExtension($projectPath) -ne '.csproj') {
    throw "Focused tests require a .csproj path."
}

if ($FullyQualifiedName -notmatch '^[A-Za-z_][A-Za-z0-9_.+`]*$') {
    throw 'FullyQualifiedName must be one exact test name; filter operators and wildcards are not allowed.'
}

$resolvedArtifactsPath = [System.IO.Path]::GetFullPath($ArtifactsPath)
$resultsDirectory = New-TrxResultsDirectory -Prefix 'candu-focused-trx'
& dotnet test $projectPath `
    --artifacts-path $resolvedArtifactsPath `
    --filter "FullyQualifiedName=$FullyQualifiedName" `
    --results-directory $resultsDirectory `
    --logger 'trx' `
    --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test exited with code $LASTEXITCODE."
}

$runs = @(Read-TrxRuns -ResultsDirectory $resultsDirectory)
[Int64] $total = 0
[Int64] $executed = 0
[Int64] $passed = 0
[Int64] $failed = 0
[Int64] $errors = 0
[Int64] $timeouts = 0
[Int64] $aborted = 0
[Int64] $inconclusive = 0
[Int64] $passedButRunAborted = 0
[Int64] $notRunnable = 0
[Int64] $notExecuted = 0
[Int64] $disconnected = 0
[Int64] $warnings = 0
$testResults = @()

foreach ($run in $runs) {
    $total += $run.Total
    $executed += $run.Executed
    $passed += $run.Passed
    $failed += $run.Failed
    $errors += $run.Error
    $timeouts += $run.Timeout
    $aborted += $run.Aborted
    $inconclusive += $run.Inconclusive
    $passedButRunAborted += $run.PassedButRunAborted
    $notRunnable += $run.NotRunnable
    $notExecuted += $run.NotExecuted
    $disconnected += $run.Disconnected
    $warnings += $run.Warning
    $testResults += @($run.TestResults)
}

$counterSummary = @($runs | ForEach-Object {
        '{0}: total={1}, executed={2}, passed={3}, failed={4}' -f `
            $_.TrxPath, $_.Total, $_.Executed, $_.Passed, $_.Failed
    }) -join '; '
Write-Output "TRX results directory: $resultsDirectory"
Write-Output "TRX counters: $counterSummary"

if ($total -ne 1 -or $executed -ne 1 -or $passed -ne 1 -or
    $failed -ne 0 -or $errors -ne 0 -or $timeouts -ne 0 -or
    $aborted -ne 0 -or $inconclusive -ne 0 -or
    $passedButRunAborted -ne 0 -or $notRunnable -ne 0 -or
    $notExecuted -ne 0 -or $disconnected -ne 0 -or $warnings -ne 0) {
    throw "Focused test '$FullyQualifiedName' must execute and pass exactly once. " +
        "TRX totals were total=$total, executed=$executed, passed=$passed, failed=$failed."
}

if ($testResults.Count -ne 1 -or
    $testResults[0].TestName -cne $FullyQualifiedName -or
    $testResults[0].Outcome -cne 'Passed') {
    throw "Focused test '$FullyQualifiedName' did not produce exactly one passed TRX result for that exact name."
}
