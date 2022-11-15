$scriptName = $MyInvocation.MyCommand.Name
$artifacts = "./artifacts"

if ([string]::IsNullOrEmpty($Env:NUGET_API_KEY)) {
    Write-Host "${scriptName}: NUGET_API_KEY is empty or not set. Skipped pushing package(s)."
} else {
    Get-ChildItem -Path $artifacts -Filter "*.nupkg" -Recurse  | ForEach-Object {
        Write-Host "$($scriptName): Pushing $($_.FullName)"
        dotnet nuget push $_.FullName --source "https://nuget.pkg.github.com/Kansys/index.json" --api-key $Env:NUGET_API_KEY --skip-duplicate
        if ($lastexitcode -ne 0) {
            throw ("Exec: " + $errorMessage)
        }
    }
}