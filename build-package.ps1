function Exec
{
    [CmdletBinding()]
    param(
        [Parameter(Position=0,Mandatory=1)][scriptblock]$cmd,
        [Parameter(Position=1,Mandatory=0)][string]$errorMessage = ($msgs.error_bad_command -f $cmd)
    )
    & $cmd
    if ($lastexitcode -ne 0) {
        throw ("Exec: " + $errorMessage)
    }
}

$build_dir = $PSScriptRoot
$build_artifacts_dir = "$build_dir\artifacts"
$lamar_project_dir = "$build_dir\src\Lamar"
$di_project_dir = "$build_dir\src\Lamar.Microsoft.DependencyInjection"
$scriptName = $MyInvocation.MyCommand.Name

Write-Host "Creating BuildArtifacts directory" -ForegroundColor Green
if (Test-Path $build_artifacts_dir) {
	rd $build_artifacts_dir -rec -force | out-null
}    
mkdir $build_artifacts_dir | out-null

if ($env:BUILD_SUFFIX -ne "") {
   $suffix = "/p:VersionSuffix=""$env:BUILD_SUFFIX"""
}

Write-Host "Creating BuildArtifacts" -ForegroundColor Green
Exec { dotnet restore }

Set-Location "$lamar_project_dir"	
Exec { dotnet pack --configuration release --output $build_artifacts_dir $suffix} 

Set-Location "$di_project_dir"
Exec { dotnet pack --configuration release --output $build_artifacts_dir $suffix} 