[CmdletBinding()]
param(
    [string]$PythonCommand = 'python'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool]$Condition,

        [Parameter(Mandatory)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-ByteEqual {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Actual,

        [Parameter(Mandatory)]
        [byte[]]$Expected,

        [Parameter(Mandatory)]
        [string]$Label
    )

    Assert-Condition ($Actual.Length -eq $Expected.Length) "$Label byte length differs."
    for ($index = 0; $index -lt $Actual.Length; $index++) {
        Assert-Condition ($Actual[$index] -eq $Expected[$index]) "$Label differs at byte $index."
    }
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$parserPath = Join-Path $repositoryRoot 'tools\PhysicsData\Export-P1-T05-Dragon.py'
$descriptorPath = Join-Path $repositoryRoot 'reference\dragon5\P1-T02-lumpSS.case.json'
$fixturePath = Join-Path $repositoryRoot 'tests\fixtures\reference\dragon5\P1-T05-lumpSS-synthetic.listing'
$expectedPath = Join-Path $repositoryRoot 'tests\fixtures\reference\dragon5\P1-T05-lumpSS-synthetic.expected.json'
$schemaPath = Join-Path $repositoryRoot 'data\schema\reference-compact-export-v1.schema.json'
foreach ($path in @($parserPath, $descriptorPath, $fixturePath, $expectedPath, $schemaPath)) {
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) "Missing P1-T05 test input: $path"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("candu-p1-t05-" + [Guid]::NewGuid().ToString('N'))
Assert-Condition (-not (Test-Path -LiteralPath $tempRoot)) "Temporary root already exists: $tempRoot"
New-Item -ItemType Directory -Path $tempRoot -ErrorAction Stop | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)

function Invoke-Parser {
    param(
        [Parameter(Mandatory)]
        [string]$InputPath,

        [Parameter(Mandatory)]
        [string]$OutputPath,

        [string]$CaseDescriptorPath = $descriptorPath
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $PythonCommand $parserPath --input $InputPath --output $OutputPath --descriptor $CaseDescriptorPath 2>&1 | Out-Null
        return $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Write-NegativeListing {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Text
    )

    $path = Join-Path $tempRoot ("$Name.listing")
    [IO.File]::WriteAllText($path, $Text, $utf8)
    return $path
}

function Assert-RejectedListing {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Text
    )

    $inputPath = Write-NegativeListing -Name $Name -Text $Text
    $outputPath = Join-Path $tempRoot ("$Name.json")
    $exitCode = Invoke-Parser -InputPath $inputPath -OutputPath $outputPath
    Assert-Condition ($exitCode -ne 0) "Negative parser case unexpectedly succeeded: $Name"
    Assert-Condition (-not (Test-Path -LiteralPath $outputPath)) "Negative parser case wrote output: $Name"
}

function Assert-RejectedBytes {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [byte[]]$Bytes
    )

    $inputPath = Join-Path $tempRoot ("$Name.listing")
    $outputPath = Join-Path $tempRoot ("$Name.json")
    [IO.File]::WriteAllBytes($inputPath, $Bytes)
    $exitCode = Invoke-Parser -InputPath $inputPath -OutputPath $outputPath
    Assert-Condition ($exitCode -ne 0) "Negative byte parser case unexpectedly succeeded: $Name"
    Assert-Condition (-not (Test-Path -LiteralPath $outputPath)) "Negative byte parser case wrote output: $Name"
}

try {
    $validText = [IO.File]::ReadAllText($fixturePath, $utf8)
    $firstOutput = Join-Path $tempRoot 'first.json'
    $secondOutput = Join-Path $tempRoot 'second.json'
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath $firstOutput) -eq 0) 'Valid synthetic listing did not parse.'
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath $secondOutput) -eq 0) 'Repeated valid synthetic listing did not parse.'
    Assert-ByteEqual -Actual ([IO.File]::ReadAllBytes($firstOutput)) -Expected ([IO.File]::ReadAllBytes($expectedPath)) -Label 'Expected compact fixture equality'
    Assert-ByteEqual -Actual ([IO.File]::ReadAllBytes($firstOutput)) -Expected ([IO.File]::ReadAllBytes($secondOutput)) -Label 'Deterministic repeat compact bytes'

    $beforeOverwrite = [IO.File]::ReadAllBytes($firstOutput)
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath $firstOutput) -ne 0) 'Parser overwrote an existing output.'
    Assert-ByteEqual -Actual ([IO.File]::ReadAllBytes($firstOutput)) -Expected $beforeOverwrite -Label 'Overwrite refusal preserved output'

    $repoOutput = Join-Path $repositoryRoot 'P1-T05-parser-must-not-write.json'
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath $repoOutput) -ne 0) 'Parser accepted a repository output path.'
    Assert-Condition (-not (Test-Path -LiteralPath $repoOutput)) 'Repository output path was created.'
    $traversalOutput = Join-Path $repositoryRoot 'tests\..\P1-T05-parser-traversal.json'
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath $traversalOutput) -ne 0) 'Parser accepted a traversal path into the repository.'
    Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'P1-T05-parser-traversal.json'))) 'Traversal output path was created.'
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath (Join-Path $tempRoot 'ads.json:stream')) -ne 0) 'Parser accepted a Windows ADS-like output path.'
    $reservedOutput = Join-Path $tempRoot 'NUL.json'
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath $reservedOutput) -ne 0) 'Parser accepted a reserved Windows device basename.'
    Assert-Condition (-not (Test-Path -LiteralPath $reservedOutput)) 'Reserved Windows device output path was created.'
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath (Join-Path $tempRoot 'COM1.trace.json')) -ne 0) 'Parser accepted a reserved Windows device basename with extra extensions.'

    $copiedDescriptor = Join-Path $tempRoot 'P1-T02-lumpSS.case.json'
    [IO.File]::Copy($descriptorPath, $copiedDescriptor)
    Assert-Condition ((Invoke-Parser -InputPath $fixturePath -OutputPath (Join-Path $tempRoot 'noncanonical-descriptor.json') -CaseDescriptorPath $copiedDescriptor) -ne 0) 'Parser accepted a noncanonical descriptor path.'

    $newline = "`n"
    $firstConvergence = 'FLU2DR: SYNTHETIC EXTERNAL CONVERGENCE          REACHED AFTER 3 ITERATIONS.'
    $firstKinf = '++ TRACKING SYNTHETIC FINAL KINF= 1.329496E+00 FINAL KEFF= 1.329496E+00 B2= 0.00000E+00 PRECISION= 1.329496E+00'
    $middleKinf = '++ TRACKING SYNTHETIC FINAL KINF= 1.314796E+00 FINAL KEFF= 1.314796E+00 B2= 0.00000E+00 PRECISION= 1.314796E+00'
    $secondAssertion = '>|TEST SUCCESSFUL; DELTA= 1.314796E+00 |>0028'
    $thirdConvergence = 'FLU2DR: SYNTHETIC EXTERNAL CONVERGENCE          REACHED AFTER 3 ITERATIONS.'
    $thirdAssertion = '>|TEST SUCCESSFUL; DELTA= 1.292124E+00 |>0028'
    $completion = '>|test lumpSS completed |>0241'
    $normalEnd = 'normal end of execution for dragon 5  Version 5.1.0'

    Assert-RejectedListing -Name 'missing-kinf' -Text $validText.Replace($middleKinf + $newline, '')
    Assert-RejectedListing -Name 'extra-kinf' -Text $validText.Replace($secondAssertion, $middleKinf + $newline + $secondAssertion)
    Assert-RejectedListing -Name 'duplicate-kinf-token' -Text $validText.Replace('++ TRACKING SYNTHETIC FINAL KINF=', '++ TRACKING SYNTHETIC FINAL KINF SYNTHETIC FINAL KINF=')
    Assert-RejectedListing -Name 'duplicate-convergence-token' -Text $validText.Replace('FLU2DR: SYNTHETIC EXTERNAL CONVERGENCE          REACHED', 'FLU2DR: SYNTHETIC EXTERNAL CONVERGENCE SYNTHETIC EXTERNAL CONVERGENCE          REACHED')
    Assert-RejectedListing -Name 'wrong-lexeme' -Text $validText.Replace('FINAL KINF= 1.329496E+00 FINAL KEFF=', 'FINAL KINF= 1.329496e+00 FINAL KEFF=')
    Assert-RejectedListing -Name 'wrong-order' -Text $validText.Replace($firstConvergence + $newline + $firstKinf, $firstKinf + $newline + $firstConvergence)
    Assert-RejectedListing -Name 'missing-assertion' -Text $validText.Replace($thirdAssertion + $newline, '')
    $thirdConvergenceOffset = $validText.LastIndexOf($thirdConvergence, [StringComparison]::Ordinal)
    Assert-Condition ($thirdConvergenceOffset -ge 0) 'Could not locate third synthetic convergence marker.'
    $missingThirdConvergence = $validText.Remove($thirdConvergenceOffset, $thirdConvergence.Length + $newline.Length)
    Assert-RejectedListing -Name 'missing-convergence' -Text $missingThirdConvergence
    Assert-RejectedListing -Name 'missing-completion' -Text $validText.Replace($completion + $newline, '')
    Assert-RejectedListing -Name 'echo-only-completion' -Text $validText.Replace($completion + $newline, 'ECHO "test lumpSS completed" ;' + $newline)
    Assert-RejectedListing -Name 'missing-normal-end' -Text $validText.Replace($normalEnd + $newline, '')
    Assert-RejectedListing -Name 'wrong-normal-end-version' -Text $validText.Replace('Version 5.1.0', 'Version 5.1.1')
    Assert-RejectedListing -Name 'fatal' -Text ($validText + 'FATAL ERROR' + $newline)
    Assert-RejectedListing -Name 'error' -Text ($validText + 'ERROR CODE 3' + $newline)
    Assert-RejectedListing -Name 'nan' -Text ($validText + 'NaN' + $newline)
    Assert-RejectedListing -Name 'infinity' -Text ($validText + 'Infinity' + $newline)
    Assert-RejectedListing -Name 'malformed-number' -Text $validText.Replace('FINAL KINF= 1.329496E+00 FINAL KEFF=', 'FINAL KINF= 1.329496E++00 FINAL KEFF=')
    Assert-RejectedListing -Name 'malformed-delta' -Text $validText.Replace('DELTA= 1.329496E+00 |>0028', 'DELTA= 1.329496E++00 |>0028')
    Assert-RejectedListing -Name 'nan-delta' -Text $validText.Replace('DELTA= 1.329496E+00 |>0028', 'DELTA= NaN |>0028')
    Assert-RejectedListing -Name 'malformed-b2' -Text $validText.Replace('B2= 0.00000E+00 PRECISION=', 'B2= 0.00000E++00 PRECISION=')
    Assert-RejectedListing -Name 'infinity-b2' -Text $validText.Replace('B2= 0.00000E+00 PRECISION=', 'B2= Infinity PRECISION=')
    Assert-RejectedListing -Name 'malformed-precision' -Text $validText.Replace('PRECISION= 1.329496E+00', 'PRECISION= 1.329496E++00')
    Assert-RejectedListing -Name 'infinity-precision' -Text $validText.Replace('PRECISION= 1.329496E+00', 'PRECISION= Infinity')
    Assert-RejectedListing -Name 'duplicate-completion' -Text ($validText + $completion + $newline)
    Assert-RejectedListing -Name 'malformed-runtime-completion' -Text ($validText + '>|test lumpSS completed |>9999 TRAILING' + $newline)

    Assert-RejectedBytes -Name 'invalid-utf8' -Bytes ([byte[]](0xff, 0x0a))
    [byte[]]$validBytes = [IO.File]::ReadAllBytes($fixturePath)
    Assert-RejectedBytes -Name 'utf8-bom' -Bytes ([byte[]](@(0xef, 0xbb, 0xbf) + $validBytes))
    Assert-RejectedBytes -Name 'nul' -Bytes ([byte[]]($validBytes + @(0x00)))
    Assert-RejectedBytes -Name 'crlf' -Bytes ($utf8.GetBytes($validText.Replace("`n", "`r`n")))
    Assert-RejectedBytes -Name 'bare-cr' -Bytes ($utf8.GetBytes($validText + "`r"))

    $schemaCheck = @'
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

def reject_constant(value):
    raise ValueError(f"non-standard numeric token {value!r}")

def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate object name {key!r}")
        result[key] = value
    return result

schema_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])
schema = json.loads(schema_path.read_text(encoding="utf-8"), parse_constant=reject_constant, object_pairs_hook=unique_object)
data = output_path.read_bytes()
if data.startswith(b"\xef\xbb\xbf") or b"\r" in data or not data.endswith(b"\n") or data.endswith(b"\n\n"):
    raise AssertionError("compact output violates the P1-T04 byte contract")
document = json.loads(data.decode("utf-8", errors="strict"), parse_constant=reject_constant, object_pairs_hook=unique_object)
errors = list(Draft202012Validator(schema).iter_errors(document))
if errors:
    raise AssertionError(errors[0].message)
'@
    $schemaCheck | & $PythonCommand - $schemaPath $firstOutput
    Assert-Condition ($LASTEXITCODE -eq 0) 'Existing jsonschema rejected the parser compact output.'

    Write-Output 'P1-T05 DRAGON parser tests passed: valid export, schema, deterministic bytes, strict parser negatives, and output safety.'
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).Path
        $systemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
        Assert-Condition ($resolvedTemp.StartsWith($systemTemp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) "Refusing to remove unexpected path: $resolvedTemp"
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
