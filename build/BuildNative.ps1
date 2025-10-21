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
$OutputDll = Join-Path $UEBinariesDir "UnrealLiveLinkNative.dll"
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
            Write-Host "⚠️  GenerateProjectFiles.bat exited with code $($Process.ExitCode)" -ForegroundColor Yellow
            Write-Host "   This is usually due to warnings about optional modules (SwarmInterface, etc.)" -ForegroundColor Gray
            Write-Host "   Continuing with build if our target files were generated..." -ForegroundColor Gray
        } else {
            Write-Host "✅ Project files generated" -ForegroundColor Green
        }
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
    Write-Host "This might be expected with binary UE build." -ForegroundColor Yellow
    Write-Host "The project structure is now in place for manual building or source UE installation." -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ UBT build completed" -ForegroundColor Green

# Step 4: Verify output and copy to repository
Write-Host "Verifying build output..." -ForegroundColor Yellow

# Check for DLL first, then fallback to EXE
$OutputFile = $null
$OutputType = $null
$TargetRepoName = $null

if (Test-Path $OutputDll) {
    $OutputFile = $OutputDll
    $OutputType = "DLL"
    $TargetRepoName = "UnrealLiveLink.Native.dll"
    Write-Host "Found DLL output: $OutputDll" -ForegroundColor Green
} elseif (Test-Path $OutputExe) {
    $OutputFile = $OutputExe
    $OutputType = "EXE"
    $TargetRepoName = "UnrealLiveLinkNative.exe"
    Write-Host "Found EXE output: $OutputExe" -ForegroundColor Yellow
} else {
    Write-Error "No build output found. Expected DLL or EXE at:"
    Write-Host "  DLL: $OutputDll" -ForegroundColor Yellow
    Write-Host "  EXE: $OutputExe" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Contents of binaries directory:" -ForegroundColor Yellow
    if (Test-Path $UEBinariesDir) {
        Get-ChildItem $UEBinariesDir -Filter "UnrealLiveLink*" | ForEach-Object { 
            Write-Host "  $($_.Name) ($($_.Length) bytes)" 
        }
    } else {
        Write-Host "  Binaries directory does not exist: $UEBinariesDir" -ForegroundColor Red
    }
    exit 1
}

# Create repository output directory
New-Item -ItemType Directory -Path $RepoOutputDir -Force | Out-Null

# Copy output file to repository with correct name
$TargetRepoPath = Join-Path $RepoOutputDir $TargetRepoName
Copy-Item $OutputFile $TargetRepoPath -Force

# Copy PDB if available
$OutputPdb = $OutputFile -replace '\.(exe|dll)$', '.pdb'
if (Test-Path $OutputPdb) {
    $TargetPdbName = $TargetRepoName -replace '\.(exe|dll)$', '.pdb'
    $TargetPdbPath = Join-Path $RepoOutputDir $TargetPdbName
    Copy-Item $OutputPdb $TargetPdbPath -Force
    Write-Host "✅ Copied $OutputType and PDB to: $RepoOutputDir" -ForegroundColor Green
} else {
    Write-Host "✅ Copied $OutputType to: $RepoOutputDir (PDB not found)" -ForegroundColor Green
}

# Copy additional DLL artifacts (export and import libraries)
if ($OutputType -eq "DLL") {
    $OutputExp = $OutputFile -replace '\.dll$', '.exp'
    $OutputLib = $OutputFile -replace '\.dll$', '.lib'
    
    if (Test-Path $OutputExp) {
        $TargetExpPath = Join-Path $RepoOutputDir "UnrealLiveLink.Native.exp"
        Copy-Item $OutputExp $TargetExpPath -Force
        Write-Host "  Copied export library (.exp)" -ForegroundColor Gray
    }
    
    if (Test-Path $OutputLib) {
        $TargetLibPath = Join-Path $RepoOutputDir "UnrealLiveLink.Native.lib"
        Copy-Item $OutputLib $TargetLibPath -Force
        Write-Host "  Copied import library (.lib)" -ForegroundColor Gray
    }
}

# Display build results
$OutputInfo = Get-Item $TargetRepoPath
Write-Host ""
Write-Host "🎉 BUILD SUCCESS!" -ForegroundColor Green
Write-Host "Output Type: $OutputType" -ForegroundColor Green
Write-Host "Output File: $($OutputInfo.FullName)" -ForegroundColor Green
Write-Host "Size: $($OutputInfo.Length) bytes ($([math]::Round($OutputInfo.Length / 1MB, 2)) MB)"
Write-Host "Modified: $($OutputInfo.LastWriteTime)"

if ($OutputType -eq "DLL") {
    Write-Host ""
    Write-Host "Native DLL build completed successfully!" -ForegroundColor Green
    Write-Host "Ready for integration testing and Sub-Phase 6.4" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "Native executable build completed successfully!" -ForegroundColor Green
    Write-Host "Ready for Sub-Phase 6.2: Type Definitions" -ForegroundColor Cyan
}

exit 0
