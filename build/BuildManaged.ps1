# BuildManaged.ps1 - Build script for Simio Unreal Engine LiveLink Connector
param(
    [string]$Configuration = "Release"
)

Write-Host "=== Building Simio Unreal Engine LiveLink Connector ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Build the managed project
Write-Host "Building managed layer..." -ForegroundColor Yellow
dotnet build src/Managed/SimioUnrealEngineLiveLinkConnector.csproj --configuration $Configuration

if ($LASTEXITCODE -eq 0) {
    Write-Host "BUILD SUCCESS!" -ForegroundColor Green
    Write-Host "Output: src\Managed\bin\$Configuration\net48\SimioUnrealEngineLiveLinkConnector.dll" -ForegroundColor White
    Write-Host ""
    Write-Host "Next step: Run .\build\DeployToSimio.ps1 to deploy to Simio" -ForegroundColor Cyan
} else {
    Write-Host "BUILD FAILED!" -ForegroundColor Red
    exit 1
}