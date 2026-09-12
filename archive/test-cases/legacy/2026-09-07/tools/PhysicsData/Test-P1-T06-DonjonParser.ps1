[CmdletBinding()]
param(
    [string]$PythonCommand = 'python',
    [string]$PrivateListingPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-ByteEqual([byte[]]$Actual, [byte[]]$Expected, [string]$Label) {
    Assert-Condition ($Actual.Length -eq $Expected.Length) "$Label length differs."
    for ($i = 0; $i -lt $Actual.Length; $i++) {
        Assert-Condition ($Actual[$i] -eq $Expected[$i]) "$Label differs at byte $i."
    }
}

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$parser = Join-Path $root 'tools\PhysicsData\Export-P1-T06-Donjon.py'
$descriptor = Join-Path $root 'reference\donjon5\P1-T03-AFA_180_310_type1_dual.case.json'
$fixture = Join-Path $root 'tests\fixtures\reference\donjon5\P1-T06-AFA_180_310_type1_dual-synthetic.listing'
$expected = Join-Path $root 'tests\fixtures\reference\donjon5\P1-T06-AFA_180_310_type1_dual-synthetic.expected.json'
$schema = Join-Path $root 'data\schema\reference-compact-export-v1.schema.json'
foreach ($path in @($parser, $descriptor, $fixture, $expected, $schema)) {
    Assert-Condition (Test-Path -LiteralPath $path -PathType Leaf) "Missing input: $path"
}
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('candu-p1-t06-' + [Guid]::NewGuid().ToString('N'))
Assert-Condition (-not (Test-Path -LiteralPath $tempRoot)) "Temporary path exists: $tempRoot"
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)

function Invoke-Parser([string]$InputPath, [string]$OutputPath, [string]$DescriptorPath = $descriptor) {
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $PythonCommand $parser --input $InputPath --output $OutputPath --descriptor $DescriptorPath 2>&1 | Out-Null
        return $LASTEXITCODE
    } finally { $ErrorActionPreference = $old }
}

function Assert-Rejected([string]$Name, [string]$Text) {
    $input = Join-Path $tempRoot "$Name.listing"
    $output = Join-Path $tempRoot "$Name.json"
    [IO.File]::WriteAllText($input, $Text, $utf8)
    Assert-Condition ((Invoke-Parser $input $output) -ne 0) "Negative case succeeded: $Name"
    Assert-Condition (-not (Test-Path -LiteralPath $output)) "Negative case wrote output: $Name"
}

function Assert-RejectedBytes([string]$Name, [byte[]]$Bytes) {
    $input = Join-Path $tempRoot "$Name.listing"
    $output = Join-Path $tempRoot "$Name.json"
    [IO.File]::WriteAllBytes($input, $Bytes)
    Assert-Condition ((Invoke-Parser $input $output) -ne 0) "Negative byte case succeeded: $Name"
    Assert-Condition (-not (Test-Path -LiteralPath $output)) "Negative byte case wrote output: $Name"
}

try {
    $text = [IO.File]::ReadAllText($fixture, $utf8)
    $first = Join-Path $tempRoot 'first.json'
    $second = Join-Path $tempRoot 'second.json'
    Assert-Condition ((Invoke-Parser $fixture $first) -eq 0) 'Synthetic fixture failed.'
    Assert-Condition ((Invoke-Parser $fixture $second) -eq 0) 'Repeated fixture failed.'
    Assert-ByteEqual ([IO.File]::ReadAllBytes($first)) ([IO.File]::ReadAllBytes($expected)) 'Expected bytes'
    Assert-ByteEqual ([IO.File]::ReadAllBytes($first)) ([IO.File]::ReadAllBytes($second)) 'Repeat bytes'
    $before = [IO.File]::ReadAllBytes($first)
    Assert-Condition ((Invoke-Parser $fixture $first) -ne 0) 'Existing output was accepted.'
    Assert-ByteEqual ([IO.File]::ReadAllBytes($first)) $before 'Overwrite preservation'

    $repoOutput = Join-Path $root 'P1-T06-parser-must-not-write.json'
    Assert-Condition ((Invoke-Parser $fixture $repoOutput) -ne 0) 'Repository output accepted.'
    Assert-Condition (-not (Test-Path -LiteralPath $repoOutput)) 'Repository output created.'
    Assert-Condition ((Invoke-Parser $fixture (Join-Path $root 'tests\..\P1-T06-traversal.json')) -ne 0) 'Traversal accepted.'
    Assert-Condition ((Invoke-Parser $fixture (Join-Path $tempRoot 'ads.json:stream')) -ne 0) 'ADS path accepted.'
    Assert-Condition ((Invoke-Parser $fixture (Join-Path $tempRoot 'NUL.json')) -ne 0) 'Device name accepted.'
    Assert-Condition ((Invoke-Parser $fixture (Join-Path $tempRoot 'COM1.trace.json')) -ne 0) 'Multi-extension device accepted.'
    $copy = Join-Path $tempRoot 'descriptor.json'
    [IO.File]::Copy($descriptor, $copy)
    Assert-Condition ((Invoke-Parser $fixture (Join-Path $tempRoot 'copy.json') $copy) -ne 0) 'Copied descriptor accepted.'

    $nl = "`n"
    $warning = 'SPHAPX: WARNING -- Record MEDIA_VOLUME is missing in the Apex file. Volume set to 1.0'
    $row = '   15  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.0E+00  0.55E-07  0.82E-04  0'
    $factor = 'FLDDIR: EFFECTIVE MULTIPLICATION FACTOR = 9.9859273434E-01'
    $maxout = 'MAXOUT      200  (MAXIMUM NUMBER OF OUTER ITERATIONS)'
    $keff = 'EPSOUT 1.00E-04  (OUTER ITERATION KEFF EPSILON)'
    $flux = 'EPSOUT 1.00E-04  (OUTER ITERATION FLUX EPSILON)'
    $assertion = '>|TEST SUCCESSFUL; DELTA=  5.968865e-08 |>0028'
    $completion = '>|AFA_180_310_type1_dual completed |>0041'
    $normal = 'normal end of execution for donjon 5  Version 5.1.0'

    $warningOffset = $text.IndexOf($warning + $nl, [StringComparison]::Ordinal)
    Assert-Condition ($warningOffset -ge 0) 'Could not locate first warning.'
    Assert-Rejected 'missing-warning' $text.Remove($warningOffset, $warning.Length + $nl.Length)
    Assert-Rejected 'extra-warning' ($text + $warning + $nl)
    Assert-Rejected 'unknown-warning' $text.Replace($warning, 'OTHER: WARNING -- unexpected')
    Assert-Rejected 'missing-row' $text.Replace($row + $nl, '')
    Assert-Rejected 'duplicate-row' $text.Replace($row, $row + $nl + $row)
    Assert-Rejected 'wrong-iteration' $text.Replace('   15  0.0E+00', '   14  0.0E+00')
    Assert-Rejected 'wrong-dels' $text.Replace('0.55E-07  0.82E-04', '0.56E-07  0.82E-04')
    Assert-Rejected 'delt-above-epsout' $text.Replace('0.55E-07  0.82E-04', '0.55E-07  1.01E-04')
    Assert-Rejected 'missing-factor' $text.Replace($factor + $nl, '')
    Assert-Rejected 'duplicate-factor' $text.Replace($factor, $factor + $nl + $factor)
    Assert-Rejected 'wrong-factor-lexeme' $text.Replace('9.9859273434E-01', '9.9859273434e-01')
    Assert-Rejected 'missing-maxout' $text.Replace($maxout + $nl, '')
    Assert-Rejected 'wrong-maxout' $text.Replace('MAXOUT      200', 'MAXOUT      201')
    Assert-Rejected 'lowercase-duplicate-maxout' ($text + $maxout.ToLowerInvariant() + $nl)
    Assert-Rejected 'missing-keff-epsout' $text.Replace($keff + $nl, '')
    Assert-Rejected 'swapped-epsout' $text.Replace($keff + $nl + $flux, $flux + $nl + $keff)
    Assert-Rejected 'wrong-epsout' $text.Replace('EPSOUT 1.00E-04', 'EPSOUT 1.01E-04')
    Assert-Rejected 'lowercase-duplicate-epsout' ($text + $flux.ToLowerInvariant() + $nl)
    Assert-Rejected 'missing-assertion' $text.Replace($assertion + $nl, '')
    Assert-Rejected 'duplicate-assertion' $text.Replace($assertion, $assertion + $nl + $assertion)
    Assert-Rejected 'malformed-delta' $text.Replace('5.968865e-08', '5.968865e++08')
    Assert-Rejected 'missing-completion' $text.Replace($completion + $nl, '')
    Assert-Rejected 'echo-only-completion' $text.Replace($completion + $nl, '')
    Assert-Rejected 'malformed-completion' $text.Replace($completion, $completion + ' TRAILING')
    Assert-Rejected 'duplicate-completion' ($text + $completion + $nl)
    Assert-Rejected 'lowercase-duplicate-completion' ($text + $completion.ToLowerInvariant() + $nl)
    Assert-Rejected 'missing-normal' $text.Replace($normal + $nl, '')
    Assert-Rejected 'wrong-version' $text.Replace('Version 5.1.0', 'Version 5.1.1')
    Assert-Rejected 'wrong-order' $text.Replace($row + $nl + $factor, $factor + $nl + $row)
    Assert-Rejected 'fatal' ($text + 'FATAL ERROR' + $nl)
    Assert-Rejected 'nonconvergence' ($text + 'FLDDIR: CONVERGENCE FAILURE.' + $nl)
    Assert-Rejected 'nan' ($text + 'NaN' + $nl)
    Assert-Rejected 'infinity' ($text + 'Infinity' + $nl)

    [byte[]]$validBytes = [IO.File]::ReadAllBytes($fixture)
    Assert-RejectedBytes 'invalid-utf8' ([byte[]](0xff, 0x0a))
    Assert-RejectedBytes 'bom' ([byte[]](@(0xef, 0xbb, 0xbf) + $validBytes))
    Assert-RejectedBytes 'nul' ([byte[]]($validBytes + @(0x00)))
    Assert-RejectedBytes 'crlf' ($utf8.GetBytes($text.Replace("`n", "`r`n")))
    Assert-RejectedBytes 'bare-cr' ($utf8.GetBytes($text + "`r"))

    $schemaCheck = @'
import json, sys
from jsonschema import Draft202012Validator
def bad(value): raise ValueError(value)
def unique(pairs):
    d={}
    for k,v in pairs:
        if k in d: raise ValueError(k)
        d[k]=v
    return d
s=json.loads(open(sys.argv[1], encoding="utf-8").read(), parse_constant=bad, object_pairs_hook=unique)
b=open(sys.argv[2], "rb").read()
assert not b.startswith(b"\xef\xbb\xbf") and b"\r" not in b and b.endswith(b"\n") and not b.endswith(b"\n\n")
d=json.loads(b.decode("utf-8"), parse_constant=bad, object_pairs_hook=unique)
errors=list(Draft202012Validator(s).iter_errors(d))
if errors: raise AssertionError(errors[0].message)
'@
    $schemaCheck | & $PythonCommand - $schema $first
    Assert-Condition ($LASTEXITCODE -eq 0) 'Schema rejected compact output.'

    $privateChecked = $false
    if ($PrivateListingPath) {
        Assert-Condition (Test-Path -LiteralPath $PrivateListingPath -PathType Leaf) "Private listing does not exist: $PrivateListingPath"
        $privateOutput = Join-Path $tempRoot 'private.json'
        Assert-Condition ((Invoke-Parser $PrivateListingPath $privateOutput) -eq 0) 'Retained private listing failed.'
        Assert-ByteEqual ([IO.File]::ReadAllBytes($privateOutput)) ([IO.File]::ReadAllBytes($expected)) 'Private listing bytes'
        $privateChecked = $true
    }
    if ($privateChecked) {
        Write-Output 'P1-T06 DONJON parser tests passed: exact fixture and external-private export, schema, adversarial grammar, and output safety.'
    } else {
        Write-Output 'P1-T06 DONJON parser tests passed: exact synthetic export, schema, adversarial grammar, and output safety; private listing not requested.'
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolved = (Resolve-Path -LiteralPath $tempRoot).Path
        $systemTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
        Assert-Condition ($resolved.StartsWith($systemTemp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) "Unsafe cleanup path: $resolved"
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
