# DeployDLLToSimio.ps1
# Deploy both managed and native DLLs to Simio for Unreal Engine LiveLink testing
# This deploys the REAL Unreal Engine native DLL (~29 MB) for full LiveLink connectivity
# For development/testing without UE, use: DeployMockDLLToSimio.ps1

param(
    [string]$Configuration = "Release",
    [string]$UEPath = "",
    [switch]$Force = $false,
    [switch]$Verbose = $false
)

# Get script directory and project paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = Split-Path -Parent $ScriptDir
$ManagedDLLSource = Join-Path $ProjectRoot "src\Managed\bin\$Configuration\net48\SimioUnrealEngineLiveLinkConnector.dll"
$NativeDLLSource = Join-Path $ProjectRoot "lib\native\win-x64\UnrealLiveLink.Native.dll"

Write-Host "=== Deploy Managed + Native DLLs to Simio ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host ""

# Auto-detect UE installation if not provided
if ([string]::IsNullOrEmpty($UEPath)) {
    Write-Host "Auto-detecting Unreal Engine installation..." -ForegroundColor Yellow
    
    # Priority 1: Check common binary installation
    $UEBinaryPath = "C:\UE\UE_5.6"
    if (Test-Path $UEBinaryPath -PathType Container) {
        $UEPath = $UEBinaryPath
        Write-Host "Found UE Binary installation: $UEPath" -ForegroundColor Green
    }
    
    # Priority 2: Check source build
    if ([string]::IsNullOrEmpty($UEPath)) {
        $UESourcePath = "C:\UE\UE_5.6_Source"
        if (Test-Path $UESourcePath -PathType Container) {
            $UEPath = $UESourcePath
            Write-Host "Found UE Source installation: $UEPath" -ForegroundColor Green
        }
    }
    
    # Priority 3: Check Epic Games installations
    if ([string]::IsNullOrEmpty($UEPath)) {
        $EpicGamesPath = "C:\Program Files\Epic Games"
        if (Test-Path $EpicGamesPath) {
            $UEInstallations = Get-ChildItem $EpicGamesPath -Directory | 
                Where-Object { Test-Path "$($_.FullName)\Engine\Binaries\Win64\UnrealEditor.exe" }
            
            if ($UEInstallations.Count -gt 0) {
                $UEPath = $UEInstallations[0].FullName
                Write-Host "Found UE installation in Epic Games: $UEPath" -ForegroundColor Green
            }
        }
    }
    
    # Error if nothing found
    if ([string]::IsNullOrEmpty($UEPath)) {
        Write-Error "No Unreal Engine installation found. Please specify -UEPath parameter."
        Write-Host ""
        Write-Host "Example: .\build\DeployToUnreal.ps1 -UEPath `"C:\UE\UE_5.6`"" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "UE Path: $UEPath" -ForegroundColor White
Write-Host ""

# Determine UE installation type
$UEEditorPath = Join-Path $UEPath "Engine\Binaries\Win64\UnrealEditor.exe"
$IsSourceBuild = (Test-Path (Join-Path $UEPath "GenerateProjectFiles.bat"))

if (!(Test-Path $UEEditorPath)) {
    Write-Error "UnrealEditor.exe not found at: $UEEditorPath"
    Write-Host "Please verify the UE installation path." -ForegroundColor Red
    exit 1
}

Write-Host "UE Installation Type: $(if ($IsSourceBuild) { 'Source Build' } else { 'Binary' })" -ForegroundColor Cyan

# Verify source DLLs exist
if (!(Test-Path $ManagedDLLSource)) {
    Write-Error "Managed DLL not found: $ManagedDLLSource"
    Write-Host "Please run build\BuildManaged.ps1 first to build the managed DLL." -ForegroundColor Red
    exit 1
}

$ManagedDLLInfo = Get-Item $ManagedDLLSource
$ManagedDLLSizeKB = [math]::Round($ManagedDLLInfo.Length / 1KB, 2)

Write-Host "Managed DLL found: $ManagedDLLSource" -ForegroundColor Green
Write-Host "  Size: $ManagedDLLSizeKB KB" -ForegroundColor White
Write-Host "  Modified: $($ManagedDLLInfo.LastWriteTime)" -ForegroundColor White
Write-Host ""

# Verify native DLL exists and is the real one (not mock)
if (!(Test-Path $NativeDLLSource)) {
    Write-Error "Native DLL not found: $NativeDLLSource"
    Write-Host "Please run build\BuildNative.ps1 first to build the native DLL." -ForegroundColor Red
    exit 1
}

$SourceDLLInfo = Get-Item $NativeDLLSource
$SourceDLLSizeMB = [math]::Round($SourceDLLInfo.Length / 1MB, 2)

if ($SourceDLLSizeMB -lt 1) {
    Write-Error "Native DLL appears to be the MOCK version ($SourceDLLSizeMB MB)"
    Write-Host "The real Unreal Engine DLL should be ~29 MB." -ForegroundColor Red
    Write-Host "Please run build\BuildNative.ps1 to build the real native DLL." -ForegroundColor Yellow
    exit 1
}

Write-Host "Source DLL found: $NativeDLLSource" -ForegroundColor Green
Write-Host "  Size: $SourceDLLSizeMB MB" -ForegroundColor White
Write-Host "  Modified: $($SourceDLLInfo.LastWriteTime)" -ForegroundColor White
Write-Host ""

# Deployment targets (DLL goes to Simio, not UE, but we verify UE is accessible)
Write-Host "=== Deployment Information ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANT NOTE:" -ForegroundColor Yellow
Write-Host "The native DLL runs in Simio's process and connects to UE via UDP." -ForegroundColor White
Write-Host "Deployment target is Simio UserExtensions, not Unreal Engine." -ForegroundColor White
Write-Host ""
Write-Host "This script will:" -ForegroundColor Cyan
Write-Host "  1. Verify Unreal Engine is accessible at: $UEPath" -ForegroundColor White
Write-Host "  2. Deploy BOTH managed and native DLLs to Simio UserExtensions" -ForegroundColor White
Write-Host "  3. Provide instructions for testing with UE" -ForegroundColor White
Write-Host ""

# Target directory for deployment (Simio UserExtensions)
$SimioUserExtensionsDir = "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector"
$TargetManagedDLL = Join-Path $SimioUserExtensionsDir "SimioUnrealEngineLiveLinkConnector.dll"
$TargetNativeDLL = Join-Path $SimioUserExtensionsDir "UnrealLiveLink.Native.dll"

# Check if running as administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Check if we need admin rights
$NeedsAdmin = $false
try {
    if (!(Test-Path $SimioUserExtensionsDir)) {
        $NeedsAdmin = $true
    } else {
        $TestFile = Join-Path $SimioUserExtensionsDir "test_write_access.tmp"
        "test" | Out-File -FilePath $TestFile -ErrorAction Stop
        Remove-Item $TestFile -Force -ErrorAction SilentlyContinue
    }
} catch {
    $NeedsAdmin = $true
}

# If we need admin rights and don't have them, restart with elevation
if ($NeedsAdmin -and !(Test-Administrator)) {
    Write-Host "Administrator rights required for deployment to Simio directory." -ForegroundColor Yellow
    Write-Host "Requesting elevation..." -ForegroundColor Yellow
    Write-Host ""
    
    $Arguments = @(
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$($MyInvocation.MyCommand.Definition)`""
    )
    
    if (![string]::IsNullOrEmpty($UEPath)) { $Arguments += @("-UEPath", "`"$UEPath`"") }
    if ($Force) { $Arguments += "-Force" }
    if ($Verbose) { $Arguments += "-Verbose" }
    
    try {
        Start-Process -FilePath "powershell.exe" -ArgumentList $Arguments -Verb RunAs -Wait
        Write-Host "Deployment completed with administrator rights." -ForegroundColor Green
        exit 0
    } catch {
        Write-Error "Failed to restart with administrator rights: $($_.Exception.Message)"
        exit 1
    }
}

# We have the rights we need, proceed with deployment
try {
    # Create target directory if it doesn't exist
    if (!(Test-Path $SimioUserExtensionsDir)) {
        Write-Host "Creating Simio UserExtensions directory..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $SimioUserExtensionsDir -Force | Out-Null
        Write-Host "Directory created: $SimioUserExtensionsDir" -ForegroundColor Green
        Write-Host ""
    }

    # Check if target exists and handle overwrite
    if (((Test-Path $TargetManagedDLL) -or (Test-Path $TargetNativeDLL)) -and !$Force) {
        Write-Host "Existing DLLs found:" -ForegroundColor Yellow
        
        if (Test-Path $TargetManagedDLL) {
            $ExistingManaged = Get-Item $TargetManagedDLL
            Write-Host "  Managed: $([math]::Round($ExistingManaged.Length / 1KB, 2)) KB, modified $($ExistingManaged.LastWriteTime)" -ForegroundColor White
        }
        
        if (Test-Path $TargetNativeDLL) {
            $ExistingNative = Get-Item $TargetNativeDLL
            $ExistingSizeMB = [math]::Round($ExistingNative.Length / 1MB, 2)
            Write-Host "  Native: $ExistingSizeMB MB ($(if ($ExistingSizeMB -lt 1) { 'MOCK' } else { 'REAL UE' })), modified $($ExistingNative.LastWriteTime)" -ForegroundColor $(if ($ExistingSizeMB -lt 1) { 'Red' } else { 'Green' })
        }
        Write-Host ""
        
        $Response = Read-Host "Overwrite with new DLLs? (Y/n)"
        if ($Response -eq 'n' -or $Response -eq 'N') {
            Write-Host "Deployment cancelled by user." -ForegroundColor Yellow
            exit 0
        }
    }

    # Copy the managed DLL
    Write-Host "Deploying managed DLL ($ManagedDLLSizeKB KB)..." -ForegroundColor Yellow
    Copy-Item -Path $ManagedDLLSource -Destination $TargetManagedDLL -Force
    Write-Host "  ✅ Managed DLL deployed" -ForegroundColor Green

    # Copy the real native DLL
    Write-Host "Deploying native DLL ($SourceDLLSizeMB MB)..." -ForegroundColor Yellow
    Copy-Item -Path $NativeDLLSource -Destination $TargetNativeDLL -Force
    Write-Host "  ✅ Native DLL deployed" -ForegroundColor Green
    
    # Copy required UE runtime dependencies
    Write-Host "Copying required Unreal Engine runtime dependencies..." -ForegroundColor Yellow
    $UEBinariesDir = Join-Path $UEPath "Engine\Binaries\Win64"
    $RequiredDependencies = @(
        "tbbmalloc.dll"      # Intel TBB allocator (required by UE core)
        # Add more as needed based on actual runtime errors
    )
    
    $DependenciesCopied = 0
    $DependenciesMissing = 0
    
    foreach ($dll in $RequiredDependencies) {
        $SourcePath = Join-Path $UEBinariesDir $dll
        $DestPath = Join-Path $SimioUserExtensionsDir $dll
        
        if (Test-Path $SourcePath) {
            try {
                Copy-Item -Path $SourcePath -Destination $DestPath -Force -ErrorAction Stop
                $DllSize = [math]::Round((Get-Item $DestPath).Length / 1MB, 2)
                Write-Host "  ✅ Copied $dll ($DllSize MB)" -ForegroundColor Green
                $DependenciesCopied++
            } catch {
                Write-Host "  ❌ Failed to copy $dll : $($_.Exception.Message)" -ForegroundColor Red
                $DependenciesMissing++
            }
        } else {
            Write-Host "  ⚠️ Not found in UE: $dll" -ForegroundColor Yellow
            $DependenciesMissing++
        }
    }
    
    Write-Host "  Dependencies copied: $DependenciesCopied, Missing: $DependenciesMissing" -ForegroundColor White
    Write-Host ""
    
    # Verify deployment
    $DeployedDLL = Get-Item $TargetNativeDLL
    $DeployedSizeMB = [math]::Round($DeployedDLL.Length / 1MB, 2)
    
    if ($DeployedSizeMB -eq $SourceDLLSizeMB) {
        Write-Host "✅ Successfully deployed native DLL!" -ForegroundColor Green
    } else {
        Write-Warning "Size mismatch! Source: $SourceDLLSizeMB MB, Deployed: $DeployedSizeMB MB"
    }
    
    Write-Host ""
    Write-Host "=== Deployment Summary ===" -ForegroundColor Cyan
    Write-Host "Status: SUCCESS" -ForegroundColor Green
    Write-Host "Deployed DLL: $TargetNativeDLL" -ForegroundColor White
    Write-Host "  Size: $DeployedSizeMB MB (Real UE DLL)" -ForegroundColor Green
    Write-Host "  Modified: $($DeployedDLL.LastWriteTime)" -ForegroundColor White
    Write-Host ""
    Write-Host "Unreal Engine Path: $UEPath" -ForegroundColor White
    Write-Host "  Editor: $UEEditorPath" -ForegroundColor White
    Write-Host ""
    
    Write-Host "=== Testing Instructions ===" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Start Unreal Engine:" -ForegroundColor Yellow
    Write-Host "   - Open/Create any UE project" -ForegroundColor White
    Write-Host "   - Window → Virtual Production → Live Link" -ForegroundColor White
    Write-Host "   - Keep LiveLink window open" -ForegroundColor White
    Write-Host ""
    Write-Host "2. Run Simio Simulation:" -ForegroundColor Yellow
    Write-Host "   - Open: tests\Simio.Tests\Model.spfx" -ForegroundColor White
    Write-Host "   - Verify Element properties:" -ForegroundColor White
    Write-Host "     • Source Name: SimioSimulation" -ForegroundColor Gray
    Write-Host "     • LiveLink Host: localhost" -ForegroundColor Gray
    Write-Host "     • LiveLink Port: 11111" -ForegroundColor Gray
    Write-Host "     • Unreal Engine Path: $UEPath" -ForegroundColor Gray
    Write-Host "   - Press Play/Run to start simulation" -ForegroundColor White
    Write-Host ""
    Write-Host "3. Expected Results:" -ForegroundColor Yellow
    Write-Host "   ✅ Source 'SimioSimulation' appears in LiveLink window" -ForegroundColor Green
    Write-Host "   ✅ Subjects appear as entities are created" -ForegroundColor Green
    Write-Host "   ✅ Transform data streams in real-time" -ForegroundColor Green
    Write-Host "   ✅ Green status indicators" -ForegroundColor Green
    Write-Host ""
    Write-Host "4. View Logs:" -ForegroundColor Yellow
    Write-Host "   - UE: Window → Developer Tools → Output Log" -ForegroundColor White
    Write-Host "   - Filter: LogUnrealLiveLinkNative" -ForegroundColor Gray
    Write-Host "   - Simio: Trace window for connector messages" -ForegroundColor White
    Write-Host ""
    Write-Host "5. Troubleshooting:" -ForegroundColor Yellow
    Write-Host "   - No source appears: Check Simio Element is configured" -ForegroundColor White
    Write-Host "   - Connection errors: Verify port 11111 not blocked" -ForegroundColor White
    Write-Host "   - No subjects: Ensure simulation is running (not paused)" -ForegroundColor White
    Write-Host ""
    Write-Host "🎉 Ready for real Unreal Engine testing!" -ForegroundColor Green
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "=== Deployment Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    
    if (!(Test-Administrator)) {
        Write-Host "Tip: Try running PowerShell as Administrator" -ForegroundColor Yellow
    }
    
    exit 1
}
