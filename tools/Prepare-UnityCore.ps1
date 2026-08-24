#Requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $ArtifactsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'candu-unity-core-artifacts')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coreProject = Join-Path $repositoryRoot 'src/ReactorSim.Core/ReactorSim.Core.csproj'
$toolchainPath = Join-Path $repositoryRoot 'unity/toolchain.json'
$unityManifestPath = Join-Path $repositoryRoot 'unity/ReactorGame/Packages/manifest.json'
$unityPackageLockPath = Join-Path $repositoryRoot 'unity/ReactorGame/Packages/packages-lock.json'
$pluginDirectory = Join-Path $repositoryRoot 'unity/ReactorGame/Assets/Plugins'
$pluginPath = Join-Path $pluginDirectory 'ReactorSim.Core.dll'
$manuallyCopiedSerializerPath = Join-Path $pluginDirectory 'Newtonsoft.Json.dll'
$resolvedArtifactsPath = [System.IO.Path]::GetFullPath($ArtifactsPath)

$toolchain = Get-Content -Raw -LiteralPath $toolchainPath | ConvertFrom-Json
$expectedNuGetPackageId = [string] $toolchain.serialization.nugetPackage.id
$expectedNuGetVersion = [string] $toolchain.serialization.nugetPackage.version
$expectedUnityPackageId = [string] $toolchain.serialization.unityPackage.id
$expectedUnityPackageVersion = [string] $toolchain.serialization.unityPackage.version
if ([string]::IsNullOrWhiteSpace($expectedNuGetPackageId) -or
    [string]::IsNullOrWhiteSpace($expectedNuGetVersion) -or
    [string]::IsNullOrWhiteSpace($expectedUnityPackageId) -or
    [string]::IsNullOrWhiteSpace($expectedUnityPackageVersion)) {
    throw 'unity/toolchain.json does not contain complete serialization package pins.'
}

[xml] $coreProjectDocument = Get-Content -Raw -LiteralPath $coreProject
$corePackageReference = $coreProjectDocument.SelectSingleNode(
    "/Project/ItemGroup/PackageReference[@Include='$expectedNuGetPackageId']")
$expectedNuGetVersionRange = "[$expectedNuGetVersion]"
if ($null -eq $corePackageReference -or
    $corePackageReference.Version -ne $expectedNuGetVersionRange) {
    throw "$expectedNuGetPackageId must be pinned exactly to $expectedNuGetVersionRange in Core."
}

$unityManifest = Get-Content -Raw -LiteralPath $unityManifestPath | ConvertFrom-Json
$manifestVersion = [string] $unityManifest.dependencies.($expectedUnityPackageId)
if ($manifestVersion -ne $expectedUnityPackageVersion) {
    throw "$expectedUnityPackageId must be pinned to $expectedUnityPackageVersion in the Unity manifest."
}

$unityPackageLock = Get-Content -Raw -LiteralPath $unityPackageLockPath | ConvertFrom-Json
$lockedUnityPackage = $unityPackageLock.dependencies.($expectedUnityPackageId)
if ($null -eq $lockedUnityPackage -or
    [string] $lockedUnityPackage.version -ne $expectedUnityPackageVersion -or
    [int] $lockedUnityPackage.depth -ne 0) {
    throw "$expectedUnityPackageId must be a depth-zero $expectedUnityPackageVersion Unity lock entry."
}

if (Test-Path -LiteralPath $manuallyCopiedSerializerPath) {
    throw "Remove $manuallyCopiedSerializerPath; the pinned Unity package owns the serializer dependency."
}

& dotnet build $coreProject `
    --configuration Release `
    --artifacts-path $resolvedArtifactsPath `
    --no-incremental
if ($LASTEXITCODE -ne 0) {
    throw "Core build exited with code $LASTEXITCODE."
}

$builtPluginPath = Join-Path $resolvedArtifactsPath 'bin/ReactorSim.Core/release/ReactorSim.Core.dll'
if (-not (Test-Path -LiteralPath $builtPluginPath -PathType Leaf)) {
    throw "Core build did not produce the expected plugin: $builtPluginPath"
}

Copy-Item -LiteralPath $builtPluginPath -Destination $pluginPath -Force
$pluginHash = Get-FileHash -Algorithm SHA256 -LiteralPath $pluginPath
Write-Output "Prepared $pluginPath"
Write-Output "SHA256 $($pluginHash.Hash)"
Write-Output "Serializer dependency: $expectedUnityPackageId $expectedUnityPackageVersion (Unity Package Manager)"
