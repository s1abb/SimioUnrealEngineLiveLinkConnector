# RunIntegrationTests.ps1
# Build and run integration tests for the native layer

param(
    [string]$Configuration = "Debug",
    [switch]$NoBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "=== Integration Tests Runner ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow

# Paths
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$TestProject = Join-Path $ProjectRoot "tests\Integration.Tests\Integration.Tests.csproj"
$NativeDll = Join-Path $ProjectRoot "lib\native\win-x64\UnrealLiveLink.Native.dll"

# Verify prerequisites
Write-Host "`nVerifying prerequisites..." -ForegroundColor Yellow

if (-not (Test-Path $TestProject)) {
    Write-Host "❌ Integration test project not found: $TestProject" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $NativeDll)) {
    Write-Host "❌ Native DLL not found: $NativeDll" -ForegroundColor Red
    Write-Host "   Please build the native layer first using: .\build\BuildNative.ps1" -ForegroundColor Yellow
    exit 1
}

$DllInfo = Get-Item $NativeDll
Write-Host "✅ Native DLL found: $($DllInfo.Length) bytes, modified $($DllInfo.LastWriteTime)" -ForegroundColor Green

# Build test project (unless -NoBuild specified)
if (-not $NoBuild) {
    Write-Host "`nBuilding integration test project..." -ForegroundColor Yellow
    
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
Write-Host "`nRunning integration tests..." -ForegroundColor Yellow
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

& dotnet @TestArgs

$TestExitCode = $LASTEXITCODE

Write-Host "================================================================" -ForegroundColor Cyan

if ($TestExitCode -eq 0) {
    Write-Host "`n🎉 All integration tests passed!" -ForegroundColor Green
} else {
    Write-Host "`n❌ Some integration tests failed (exit code: $TestExitCode)" -ForegroundColor Red
}

Write-Host "`nTest Summary:" -ForegroundColor Yellow
Write-Host "  Project: Integration.Tests" -ForegroundColor White
Write-Host "  Configuration: $Configuration" -ForegroundColor White
Write-Host "  Native DLL: $($DllInfo.Name) ($($DllInfo.Length) bytes)" -ForegroundColor White

exit $TestExitCode
