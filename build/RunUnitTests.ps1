# RunUnitTests.ps1
# Build and run unit tests for the managed layer

param(
    [string]$Configuration = "Debug",
    [switch]$NoBuild,
    [switch]$Verbose,
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"

Write-Host "=== Unit Tests Runner ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$TestProject = Join-Path $ProjectRoot "tests\Unit.Tests\Unit.Tests.csproj"

# Verify prerequisites
Write-Host "`nVerifying prerequisites..." -ForegroundColor Yellow

if (-not (Test-Path $TestProject)) {
    Write-Host "❌ Unit test project not found: $TestProject" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Unit test project found" -ForegroundColor Green

# Build test project (unless -NoBuild specified)
if (-not $NoBuild) {
    Write-Host "`nBuilding unit test project..." -ForegroundColor Yellow
    
    $BuildArgs = @(
        "build",
        $TestProject,
        "-c", $Configuration,
        "--no-incremental"
    )
    
    if ($Verbose) {
        $BuildArgs += "-v", "detailed"
    }
    
    & dotnet @BuildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
    
    Write-Host "✅ Build completed successfully" -ForegroundColor Green
} else {
    Write-Host "`nSkipping build (NoBuild flag set)" -ForegroundColor Yellow
}

# Run tests
Write-Host "`nRunning unit tests..." -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan

# Build dotnet test command arguments
$TestArgs = @(
    "test",
    $TestProject,
    "-c", $Configuration,
    "--no-build"
)

if ($Verbose) {
    $TestArgs += "-v", "detailed"
}

if ($Filter) {
    $TestArgs += "--filter", $Filter
    Write-Host "Filter: $Filter" -ForegroundColor Yellow
}

& dotnet @TestArgs

$TestExitCode = $LASTEXITCODE

Write-Host "================================================================" -ForegroundColor Cyan

if ($TestExitCode -eq 0) {
    Write-Host "`n🎉 All unit tests passed!" -ForegroundColor Green
} else {
    Write-Host "`n❌ Some unit tests failed (exit code: $TestExitCode)" -ForegroundColor Red
}

Write-Host "`nTest Summary:" -ForegroundColor Yellow
Write-Host "  Project: Unit.Tests" -ForegroundColor White
Write-Host "  Configuration: $Configuration" -ForegroundColor White
if ($Filter) {
    Write-Host "  Filter: $Filter" -ForegroundColor White
}

Write-Host "`nTest Categories Available:" -ForegroundColor Cyan
Write-Host "  --filter `"FullyQualifiedName~CoordinateConverter`"  (Coordinate conversion tests)" -ForegroundColor Gray
Write-Host "  --filter `"FullyQualifiedName~LiveLinkManager`"       (Manager tests)" -ForegroundColor Gray
Write-Host "  --filter `"FullyQualifiedName~LiveLinkConfiguration`" (Configuration tests)" -ForegroundColor Gray
Write-Host "  --filter `"FullyQualifiedName~Utils`"                 (Utility tests)" -ForegroundColor Gray

exit $TestExitCode
