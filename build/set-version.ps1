param([string] $version)

if ([string]::IsNullOrEmpty($version))
{
	throw [System.ArgumentNullException] "version must be specified"
}

$fileName = "$PSScriptRoot\version.props"
[xml] $versionFile = Get-Content $fileName
$versionNode = $versionFile.SelectSingleNode("Project/PropertyGroup/VersionPrefix")
$versionNode.InnerText = $version
$versionFile.Save($fileName) 

Write-Host "Updated version to $version"