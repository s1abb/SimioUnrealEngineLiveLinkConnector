# Build Native UnrealLiveLink.Native DLL using Unreal Build Tool
param(
    [string]$Configuration = "Development",
    [string]$Platform = "Win64",
    [string]$UEPath = ""
)

Write-Host "=== Building Native UnrealLiveLink DLL with UBT ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"

# Auto-detect UE installation if not provided
if ([string]::IsNullOrEmpty($UEPath)) {
    # Priority 1: Source build installation
    $UESourcePath = "C:\UE\UE_5.6_Source"
    if (Test-Path $UESourcePath -PathType Container) {
        $GenerateProjectFiles = Join-Path $UESourcePath "GenerateProjectFiles.bat"
        if (Test-Path $GenerateProjectFiles) {
            $UEPath = $UESourcePath
            Write-Host "Auto-detected UE Source installation: $UEPath" -ForegroundColor Cyan
        }
    }
    
    # Priority 2: Binary installation fallback
    if ([string]::IsNullOrEmpty($UEPath)) {
        $UEBinaryPath = "C:\UE\UE_5.6"
        if (Test-Path $UEBinaryPath -PathType Container) {
            $UEPath = $UEBinaryPath
            Write-Host "Auto-detected UE Binary installation: $UEPath" -ForegroundColor Yellow
        }
    }
    
    # Fallback: Error if nothing found
    if ([string]::IsNullOrEmpty($UEPath)) {
        Write-Error "No UE installation found. Please specify -UEPath parameter."
        exit 1
    }
}

Write-Host "UE Path: $UEPath"

# Setup paths
$RepoRoot = Split-Path $PSScriptRoot -Parent
$NativeSourceDir = Join-Path $RepoRoot "src\Native\UnrealLiveLink.Native"
$UEProgramsDir = Join-Path $UEPath "Engine\Source\Programs"
$UETargetDir = Join-Path $UEProgramsDir "UnrealLiveLinkNative"
$UEBinariesDir = Join-Path $UEPath "Engine\Binaries\$Platform"
$OutputExe = Join-Path $UEBinariesDir "UnrealLiveLinkNative.exe"
$RepoOutputDir = Join-Path $RepoRoot "lib\native\win-x64"

# Verify prerequisites
if (-not (Test-Path $UEPath)) {
    Write-Error "Unreal Engine not found at: $UEPath"
    exit 1
}

if (-not (Test-Path $NativeSourceDir)) {
    Write-Error "Native source directory not found at: $NativeSourceDir"
    exit 1
}

$UBTPath = Join-Path $UEPath "Engine\Binaries\DotNET\UnrealBuildTool\UnrealBuildTool.exe"
if (-not (Test-Path $UBTPath)) {
    Write-Error "UnrealBuildTool not found at: $UBTPath"
    exit 1
}

# Check if this is a source build or binary build
$IsSourceBuild = Test-Path (Join-Path $UEPath "GenerateProjectFiles.bat")
Write-Host "UE Installation Type: $(if ($IsSourceBuild) { 'Source Build' } else { 'Binary Build' })" -ForegroundColor Yellow

Write-Host "✅ Prerequisites verified" -ForegroundColor Green

# Step 1: Copy source to UE Programs directory
Write-Host "Copying source to UE Programs directory..." -ForegroundColor Yellow

# Ensure Programs directory exists
if (-not (Test-Path $UEProgramsDir)) {
    Write-Host "Creating UE Programs directory: $UEProgramsDir"
    New-Item -ItemType Directory -Path $UEProgramsDir -Force | Out-Null
}

if (Test-Path $UETargetDir) {
    Write-Host "Removing existing directory: $UETargetDir"
    Remove-Item -Recurse -Force $UETargetDir
}

Copy-Item -Recurse $NativeSourceDir $UETargetDir
Write-Host "✅ Source copied to: $UETargetDir" -ForegroundColor Green

# Step 2: Generate project files (if source build)
if ($IsSourceBuild) {
    Write-Host "Generating UE project files..." -ForegroundColor Yellow
    
    $GenerateProjectFilesScript = Join-Path $UEPath "GenerateProjectFiles.bat"
    Push-Location $UEPath
    try {
        $Process = Start-Process -FilePath $GenerateProjectFilesScript -Wait -PassThru -NoNewWindow
        if ($Process.ExitCode -ne 0) {
            Write-Error "GenerateProjectFiles.bat failed with exit code: $($Process.ExitCode)"
            exit 1
        }
        Write-Host "✅ Project files generated" -ForegroundColor Green
    } finally {
        Pop-Location
    }
} else {
    Write-Host "⚠️ Binary UE build - skipping project generation" -ForegroundColor Yellow
}

# Step 3: Build with UBT
Write-Host "Building with UnrealBuildTool..." -ForegroundColor Yellow

$UBTArgs = @(
    "UnrealLiveLinkNative"
    $Platform
    $Configuration
)

Write-Host "UBT Command: $UBTPath $($UBTArgs -join ' ')"

$Process = Start-Process -FilePath $UBTPath -ArgumentList $UBTArgs -Wait -PassThru -NoNewWindow
if ($Process.ExitCode -ne 0) {
    Write-Host "❌ UBT build failed with exit code: $($Process.ExitCode)" -ForegroundColor Red
    
    # Check UBT log for more details
    $UBTLogPath = "$env:LOCALAPPDATA\UnrealBuildTool\Log.txt"
    if (Test-Path $UBTLogPath) {
        Write-Host ""
        Write-Host "Last few lines from UBT log:" -ForegroundColor Yellow
        Get-Content $UBTLogPath -Tail 10 | ForEach-Object { Write-Host "  $_" }
    }
    
    Write-Host ""
    Write-Host "This might be expected for Sub-Phase 6.1 with binary UE build." -ForegroundColor Yellow
    Write-Host "The project structure is now in place for manual building or source UE installation." -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ UBT build completed" -ForegroundColor Green

# Step 4: Verify output and copy to repository
Write-Host "Verifying build output..." -ForegroundColor Yellow

if (-not (Test-Path $OutputExe)) {
    Write-Error "Expected executable not found at: $OutputExe"
    Write-Host "Contents of binaries directory:"
    if (Test-Path $UEBinariesDir) {
        Get-ChildItem $UEBinariesDir -Filter "UnrealLiveLink*" | ForEach-Object { Write-Host "  $($_.Name)" }
    } else {
        Write-Host "  Binaries directory does not exist: $UEBinariesDir"
    }
    exit 1
}

# Create repository output directory
New-Item -ItemType Directory -Path $RepoOutputDir -Force | Out-Null

# Copy executable and PDB to repository
Copy-Item $OutputExe $RepoOutputDir -Force
$OutputPdb = $OutputExe -replace '\.exe$', '.pdb'
if (Test-Path $OutputPdb) {
    Copy-Item $OutputPdb $RepoOutputDir -Force
    Write-Host "✅ Copied executable and PDB to: $RepoOutputDir" -ForegroundColor Green
} else {
    Write-Host "✅ Copied executable to: $RepoOutputDir (PDB not found)" -ForegroundColor Green
}

# Display build results
$ExeInfo = Get-Item (Join-Path $RepoOutputDir "UnrealLiveLinkNative.exe")
Write-Host ""
Write-Host "🎉 BUILD SUCCESS!" -ForegroundColor Green
Write-Host "Output Executable: $($ExeInfo.FullName)" -ForegroundColor Green
Write-Host "Size: $($ExeInfo.Length) bytes"
Write-Host "Modified: $($ExeInfo.LastWriteTime)"

Write-Host ""
Write-Host "Native DLL build completed successfully!" -ForegroundColor Green
Write-Host "Ready for Sub-Phase 6.2: Type Definitions" -ForegroundColor Cyan

exit 0
