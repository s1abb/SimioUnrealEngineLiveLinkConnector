# CleanupBuildArtifacts.ps1
# Remove all build artifacts to enable clean rebuild

param(
    [switch]$Force,
    [switch]$IncludeNative
)

$ErrorActionPreference = "Continue"

Write-Host "=== Cleanup Build Artifacts ===" -ForegroundColor Cyan
Write-Host ""

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$DeletedCount = 0
$ErrorCount = 0

# Function to safely remove directory
function Remove-DirectorySafe {
    param(
        [string]$Path, 
        [string]$Description,
        [switch]$NonFatal  # If set, errors won't increment error count (e.g., for IDE-locked test files)
    )
    
    if (Test-Path $Path) {
        Write-Host "Removing $Description..." -ForegroundColor Yellow
        try {
            Remove-Item $Path -Recurse -Force -ErrorAction Stop
            Write-Host "  ✅ Removed: $Path" -ForegroundColor Green
            $script:DeletedCount++
        } catch {
            if ($NonFatal) {
                Write-Host "  ⚠️  Locked: $($_.Exception.Message) (safe to ignore - doesn't affect build)" -ForegroundColor Yellow
            } else {
                Write-Host "  ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
                $script:ErrorCount++
            }
        }
    } else {
        Write-Host "  ⏭️  Not found (skipping): $Path" -ForegroundColor Gray
    }
}

# Function to safely remove files in directory
function Remove-FilesSafe {
    param([string]$Path, [string]$Filter, [string]$Description)
    
    if (Test-Path $Path) {
        $Files = Get-ChildItem -Path $Path -Filter $Filter -File -ErrorAction SilentlyContinue
        if ($Files) {
            Write-Host "Removing $Description..." -ForegroundColor Yellow
            foreach ($File in $Files) {
                try {
                    Remove-Item $File.FullName -Force -ErrorAction Stop
                    Write-Host "  ✅ Removed: $($File.Name)" -ForegroundColor Green
                    $script:DeletedCount++
                } catch {
                    Write-Host "  ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
                    $script:ErrorCount++
                }
            }
        }
    }
}

Write-Host "Cleaning Managed Layer Artifacts..." -ForegroundColor Cyan
Write-Host "──────────────────────────────────────" -ForegroundColor Gray

# Managed layer build output
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "src\Managed\bin") -Description "Managed bin folder"
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "src\Managed\obj") -Description "Managed obj folder"

Write-Host ""
Write-Host "Cleaning Unit Test Artifacts..." -ForegroundColor Cyan
Write-Host "──────────────────────────────────────" -ForegroundColor Gray

# Unit tests - bin folder may be locked by VS Code C# extension
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "tests\Unit.Tests\bin") -Description "Unit Tests bin folder" -NonFatal
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "tests\Unit.Tests\obj") -Description "Unit Tests obj folder"
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "tests\Unit.Tests\TestResults") -Description "Unit Tests results"

Write-Host ""
Write-Host "Cleaning Integration Test Artifacts..." -ForegroundColor Cyan
Write-Host "──────────────────────────────────────" -ForegroundColor Gray

# Integration tests - bin folder may be locked by VS Code C# extension
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "tests\Integration.Tests\bin") -Description "Integration Tests bin folder" -NonFatal
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "tests\Integration.Tests\obj") -Description "Integration Tests obj folder"
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "tests\Integration.Tests\TestResults") -Description "Integration Tests results"

Write-Host ""
Write-Host "Cleaning Native Layer Artifacts..." -ForegroundColor Cyan
Write-Host "──────────────────────────────────────" -ForegroundColor Gray

if ($IncludeNative) {
    # Native DLL output (only if explicitly requested)
    Remove-FilesSafe -Path (Join-Path $ProjectRoot "lib\native\win-x64") -Filter "*.dll" -Description "Native DLL files"
    Remove-FilesSafe -Path (Join-Path $ProjectRoot "lib\native\win-x64") -Filter "*.pdb" -Description "Native PDB files"
    Remove-FilesSafe -Path (Join-Path $ProjectRoot "lib\native\win-x64") -Filter "*.exp" -Description "Native export files"
    Remove-FilesSafe -Path (Join-Path $ProjectRoot "lib\native\win-x64") -Filter "*.lib" -Description "Native import libraries"
    Remove-FilesSafe -Path (Join-Path $ProjectRoot "lib\native\win-x64") -Filter "*.exe" -Description "Native executables"
    
    Write-Host ""
    Write-Host "Cleaning UBT Intermediate Files..." -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────" -ForegroundColor Gray
    
    # UBT copies source to Engine/Source/Programs - remove it for fresh build
    $UEProgramsDir = "C:\UE\UE_5.6_Source\Engine\Source\Programs\UnrealLiveLinkNative"
    Remove-DirectorySafe -Path $UEProgramsDir -Description "UBT Programs source copy"
    
    # UBT intermediate build files
    $UEIntermediateDir = "C:\UE\UE_5.6_Source\Engine\Intermediate\Build\Win64\x64\UnrealLiveLinkNative"
    Remove-DirectorySafe -Path $UEIntermediateDir -Description "UBT intermediate files"
    
    # UBT output binaries (DLL in Engine/Binaries)
    Write-Host "Removing UBT output binaries..." -ForegroundColor Yellow
    $UEBinariesPath = "C:\UE\UE_5.6_Source\Engine\Binaries\Win64"
    if (Test-Path $UEBinariesPath) {
        $BinFiles = Get-ChildItem -Path $UEBinariesPath -Filter "UnrealLiveLinkNative.*" -File -ErrorAction SilentlyContinue
        if ($BinFiles) {
            foreach ($File in $BinFiles) {
                try {
                    Remove-Item $File.FullName -Force -ErrorAction Stop
                    Write-Host "  ✅ Removed: $($File.Name)" -ForegroundColor Green
                    $script:DeletedCount++
                } catch {
                    Write-Host "  ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
                    $script:ErrorCount++
                }
            }
        } else {
            Write-Host "  ⏭️  No UBT binaries found (already clean)" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "  ⏭️  Skipping native DLL cleanup (use -IncludeNative to clean)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Cleaning Temporary Build Files..." -ForegroundColor Cyan
Write-Host "──────────────────────────────────────" -ForegroundColor Gray

# Temporary build directories
Remove-DirectorySafe -Path (Join-Path $ProjectRoot "build\temp") -Description "Build temp folder"

# Simio test logs (can be regenerated)
$SimioLogPath = Join-Path $ProjectRoot "tests\Simio.Tests\SimioUnrealLiveLink_Mock.log"
if (Test-Path $SimioLogPath) {
    Write-Host "Removing Simio test log..." -ForegroundColor Yellow
    try {
        Remove-Item $SimioLogPath -Force -ErrorAction Stop
        Write-Host "  ✅ Removed: SimioUnrealLiveLink_Mock.log" -ForegroundColor Green
        $DeletedCount++
    } catch {
        Write-Host "  ❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
        $ErrorCount++
    }
}

Write-Host ""
Write-Host "══════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Cleanup Summary:" -ForegroundColor Yellow
Write-Host "  Items removed: $DeletedCount" -ForegroundColor $(if ($DeletedCount -gt 0) { "Green" } else { "Gray" })
Write-Host "  Errors: $ErrorCount" -ForegroundColor $(if ($ErrorCount -gt 0) { "Red" } else { "Green" })

if ($ErrorCount -eq 0) {
    Write-Host ""
    Write-Host "✅ Cleanup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Ready for clean rebuild:" -ForegroundColor Cyan
    Write-Host "  1. .\build\SetupVSEnvironment.ps1" -ForegroundColor White
    Write-Host "  2. .\build\BuildMockDLL.ps1" -ForegroundColor White
    Write-Host "  3. .\build\BuildNative.ps1" -ForegroundColor White
    Write-Host "  4. .\build\BuildManaged.ps1" -ForegroundColor White
    Write-Host "  5. .\build\RunUnitTests.ps1" -ForegroundColor White
    Write-Host "  6. .\build\RunIntegrationTests.ps1" -ForegroundColor White
    exit 0
} else {
    Write-Host ""
    Write-Host "⚠️  Cleanup completed with errors" -ForegroundColor Yellow
    exit 1
}
