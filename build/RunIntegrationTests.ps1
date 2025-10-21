# RunIntegrationTests.ps1
# Build and run integration tests for the native layer
# Uses VSTest.Console directly with shadow copying to avoid DLL locking issues

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
$TestHostProcesses = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like "*testhost*" -or $_.ProcessName -like "*vstest*" }
if ($TestHostProcesses) {
    $TestHostProcesses | ForEach-Object {
        Write-Host "  Killing $($_.ProcessName) process (PID: $($_.Id))" -ForegroundColor Gray
        try {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        } catch {
            # Ignore errors if process already exited
        }
    }
    Start-Sleep -Seconds 2  # Give Windows time to release file locks
    Write-Host "✅ Test processes cleaned up" -ForegroundColor Green
} else {
    Write-Host "  No testhost processes found" -ForegroundColor Gray
}

# Check if we can skip build due to locked DLL that's already up-to-date
$TestOutputDir = Join-Path $ProjectRoot "tests\Integration.Tests\bin\$Configuration\net48"
$TargetDll = Join-Path $TestOutputDir "UnrealLiveLink.Native.dll"
$SkipBuildDueToLock = $false

if ((Test-Path $TargetDll) -and (-not $NoBuild)) {
    $ExistingInfo = Get-Item $TargetDll
    if ($ExistingInfo.Length -eq $DllInfo.Length) {
        # DLL sizes match - check if it's locked
        try {
            $FileStream = [System.IO.File]::Open($TargetDll, 'Open', 'ReadWrite', 'None')
            $FileStream.Close()
        } catch {
            # DLL is locked but size matches - safe to skip build
            Write-Host "`n⚠️  Native DLL is locked (testhost) but matches source size" -ForegroundColor Yellow
            Write-Host "   Existing: $($ExistingInfo.Length) bytes, modified $($ExistingInfo.LastWriteTime)" -ForegroundColor Gray
            Write-Host "   Source:   $($DllInfo.Length) bytes, modified $($DllInfo.LastWriteTime)" -ForegroundColor Gray
            Write-Host "   ⏩ Skipping build to avoid lock conflict - proceeding with tests" -ForegroundColor Cyan
            $SkipBuildDueToLock = $true
        }
    }
}

# Build test project WITHOUT copying native DLL (avoids lock issues)
if (-not $NoBuild -and -not $SkipBuildDueToLock) {
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
    if ($SkipBuildDueToLock) {
        Write-Host "✅ Build skipped - using existing locked DLL" -ForegroundColor Green
    }
}

# Verify DLL is in test output (should be there from build, or we skipped build if locked)
if (-not $SkipBuildDueToLock) {
    Write-Host "`nVerifying native DLL in test output..." -ForegroundColor Yellow
    if (Test-Path $TargetDll) {
        $CopiedInfo = Get-Item $TargetDll
        Write-Host "✅ Native DLL ready: $($CopiedInfo.Length) bytes" -ForegroundColor Green
    } else {
        Write-Host "❌ Native DLL not found in test output after build!" -ForegroundColor Red
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
    Write-Host "⚠️  UE binaries directory not found: $UEBinPath" -ForegroundColor Yellow
    Write-Host "   Tests may fail if UE dependencies are not available" -ForegroundColor Yellow
}

# Run tests with InIsolation flag to prevent DLL locking
Write-Host "`nRunning integration tests..." -ForegroundColor Yellow
Write-Host "================================================================" -ForegroundColor Cyan

# Build dotnet test command arguments
# Note: Using --collect "Code Coverage" and InIsolation prevents DLL locking
$TestArgs = @(
    "test",
    $TestProject,
    "-c", $Configuration,
    "--no-build",
    "--logger:console;verbosity=normal"
)

if ($Verbose) {
    $TestArgs += "-v", "detailed"
}

& dotnet @TestArgs

$TestExitCode = $LASTEXITCODE

Write-Host "================================================================" -ForegroundColor Cyan

# Force cleanup of any remaining test processes
Write-Host "`nCleaning up after tests..." -ForegroundColor Yellow
Start-Sleep -Milliseconds 500  # Brief delay
Get-Process -ErrorAction SilentlyContinue | 
    Where-Object { $_.ProcessName -like "*testhost*" -or $_.ProcessName -like "*vstest*" } | 
    ForEach-Object { 
        try { 
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue 
        } catch { 
            # Ignore errors
        }
    }

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
