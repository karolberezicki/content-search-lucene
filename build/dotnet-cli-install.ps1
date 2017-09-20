param([string] $version)

$scriptDir = Split-Path $MyInvocation.MyCommand.Path -parent

if ([string]::IsNullOrEmpty($version))
{
	$version = &(Join-Path $scriptDir get-sdkversion.ps1)
}

Write-Host "Installing .NET Core CLI, version: $version"
& (Join-Path $scriptDir dotnet-install.ps1) -Version $version -Architecture x64 -InstallDir (Join-Path $scriptDir "dotnet")
if($LASTEXITCODE -ne 0) { throw "Failed" }