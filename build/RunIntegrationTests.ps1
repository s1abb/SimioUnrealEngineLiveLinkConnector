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

# Kill any stray testhost processes that might lock the DLL
Write-Host "`nCleaning up test processes..." -ForegroundColor Yellow
$TestHostProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like "*testhost*" }
if ($TestHostProcesses) {
    $TestHostProcesses | ForEach-Object {
        Write-Host "  Killing $($_.ProcessName) process (PID: $($_.Id))" -ForegroundColor Gray
        try {
            taskkill /F /PID $_.Id 2>$null | Out-Null
        } catch {
            # Ignore errors if process already exited
        }
    }
    Start-Sleep -Seconds 2  # Give Windows time to release file locks
    Write-Host "✅ Test processes cleaned up" -ForegroundColor Green
} else {
    Write-Host "  No testhost processes found" -ForegroundColor Gray
}

# Build test project (unless -NoBuild specified)
if (-not $NoBuild) {
    Write-Host "`nBuilding integration test project..." -ForegroundColor Yellow
    
    $BuildArgs = @(
        "build",
        $TestProject,
        "-c", $Configuration,
        "--no-incremental",
        "/p:CopyNativeReferences=false"  # Disable automatic DLL copy - we'll handle it ourselves
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

# Copy native DLL to test output directory
Write-Host "`nCopying native DLL to test output..." -ForegroundColor Yellow
$TestOutputDir = Join-Path $ProjectRoot "tests\Integration.Tests\bin\$Configuration\net48"

if (-not (Test-Path $TestOutputDir)) {
    Write-Host "❌ Test output directory not found: $TestOutputDir" -ForegroundColor Red
    Write-Host "   Build may have failed or used different configuration" -ForegroundColor Yellow
    exit 1
}

try {
    Copy-Item -Path $NativeDll -Destination $TestOutputDir -Force -ErrorAction Stop
    if (Test-Path (Join-Path $TestOutputDir "UnrealLiveLink.Native.dll")) {
        $CopiedDll = Get-Item (Join-Path $TestOutputDir "UnrealLiveLink.Native.dll")
        Write-Host "✅ Native DLL copied: $($CopiedDll.Length) bytes" -ForegroundColor Green
    }
} catch {
    # DLL might be locked by previous test run - check if existing DLL is current
    $ExistingDll = Join-Path $TestOutputDir "UnrealLiveLink.Native.dll"
    if (Test-Path $ExistingDll) {
        $ExistingInfo = Get-Item $ExistingDll
        if ($ExistingInfo.LastWriteTime -ge $DllInfo.LastWriteTime) {
            Write-Host "⚠️ Could not copy DLL (file locked), but existing DLL is current" -ForegroundColor Yellow
            Write-Host "   Existing: $($ExistingInfo.Length) bytes, modified $($ExistingInfo.LastWriteTime)" -ForegroundColor Gray
            Write-Host "   Source:   $($DllInfo.Length) bytes, modified $($DllInfo.LastWriteTime)" -ForegroundColor Gray
        } else {
            Write-Host "❌ DLL is locked and outdated - tests may fail" -ForegroundColor Red
            Write-Host "   Kill all testhost processes and retry: taskkill /F /IM testhost.exe" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "❌ Failed to copy DLL and no existing DLL found" -ForegroundColor Red
        exit 1
    }
}

# Add UE binaries to PATH for runtime dependency resolution
Write-Host "`nConfiguring environment for Unreal Engine dependencies..." -ForegroundColor Yellow
$UEBinPath = "C:\UE\UE_5.6_Source\Engine\Binaries\Win64"
if (Test-Path $UEBinPath) {
    $env:PATH = "$UEBinPath;$env:PATH"
    Write-Host "✅ Added UE binaries to PATH: $UEBinPath" -ForegroundColor Green
} else {
    Write-Host "⚠️ UE binaries directory not found: $UEBinPath" -ForegroundColor Yellow
    Write-Host "   Tests may fail if UE dependencies are not available" -ForegroundColor Yellow
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
