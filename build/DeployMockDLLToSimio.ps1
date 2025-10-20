# DeployMockDLLToSimio.ps1
# Deploy the Simio Unreal Engine LiveLink Connector with MOCK native DLL to Simio UserExtensions directory
# Use this for: Development, testing without Unreal Engine, CI/CD pipelines
# For real UE testing, use: DeployNativeDLLToSimio.ps1

param(
    [string]$Configuration = "Release",
    [switch]$Force = $false,
    [switch]$Verbose = $false
)

# Get script directory and project paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = Split-Path -Parent $ScriptDir
$OutputDir = Join-Path $ProjectRoot "src\Managed\bin\$Configuration\net48"
$MockDLLTemp = Join-Path $ProjectRoot "build\temp\mock\UnrealLiveLink.Native.dll"

# Simio installation paths
$SimioUserExtensionsDir = "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector"
$TargetDLL = Join-Path $SimioUserExtensionsDir "SimioUnrealEngineLiveLinkConnector.dll"
$TargetNativeDLL = Join-Path $SimioUserExtensionsDir "UnrealLiveLink.Native.dll"

# Source files
$SourceDLL = Join-Path $OutputDir "SimioUnrealEngineLiveLinkConnector.dll"

Write-Host "=== Deploy Simio Connector with MOCK Native DLL ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Managed DLL Source: $OutputDir" -ForegroundColor Yellow
Write-Host "Mock DLL Source: lib\native\win-x64\" -ForegroundColor Yellow
Write-Host "Target: $SimioUserExtensionsDir" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  NOTE: This deploys the MOCK DLL (~75 KB)" -ForegroundColor Yellow
Write-Host "   For real UE testing, use: DeployNativeDLLToSimio.ps1" -ForegroundColor Yellow
Write-Host ""

# Check if running as administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Verify source files exist
if (!(Test-Path $SourceDLL)) {
    Write-Error "Managed DLL not found: $SourceDLL"
    Write-Host "Please run build\BuildManaged.ps1 first to build the project." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Managed DLL found: $SourceDLL" -ForegroundColor Green

# Always build fresh mock DLL to temp location
Write-Host ""
Write-Host "Building fresh MOCK DLL..." -ForegroundColor Yellow
& "$ScriptDir\BuildMockDLL.ps1" | Out-Null

# Check temp location first (fresh build)
if (Test-Path $MockDLLTemp) {
    $MockDLLSource = $MockDLLTemp
    Write-Host "✅ Using freshly built mock DLL" -ForegroundColor Green
} else {
    # Fallback to lib location (might be real UE DLL!)
    $MockDLLSource = Join-Path $ProjectRoot "lib\native\win-x64\UnrealLiveLink.Native.dll"
    if (!(Test-Path $MockDLLSource)) {
        Write-Error "Mock DLL not found after build attempt"
        exit 1
    }
    Write-Host "⚠️  Using DLL from lib\native\win-x64\ (might not be mock!)" -ForegroundColor Yellow
}

$MockDLLInfo = Get-Item $MockDLLSource
$MockDLLSizeKB = [math]::Round($MockDLLInfo.Length / 1KB, 2)

Write-Host "   Source: $MockDLLSource" -ForegroundColor White
Write-Host "   Size: $MockDLLSizeKB KB" -ForegroundColor White

# Verify it's actually the mock DLL (should be < 200 KB)
if ($MockDLLInfo.Length -gt 200KB) {
    Write-Error "DLL is too large ($MockDLLSizeKB KB) - this is NOT the mock DLL!"
    Write-Host "Mock DLL should be ~75 KB. Use DeployNativeDLLToSimio.ps1 instead." -ForegroundColor Yellow
    exit 1
}

# Check if we need admin rights
$NeedsAdmin = $false
try {
    # Test if we can write to the Simio directory
    $TestFile = Join-Path $SimioUserExtensionsDir "test_write_access.tmp"
    if (!(Test-Path $SimioUserExtensionsDir)) {
        # Directory doesn't exist, we'll need to create it
        $NeedsAdmin = $true
    } else {
        # Try to create a test file
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
    
    # Build arguments for elevated process
    $Arguments = @(
        "-ExecutionPolicy", "Bypass",
        "-File", "`"$($MyInvocation.MyCommand.Definition)`"",
        "-Configuration", $Configuration
    )
    
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
    }

    # Check if target files exist and handle overwrite
    if ((Test-Path $TargetDLL) -and !$Force) {
        Write-Host "Target DLL already exists: $TargetDLL" -ForegroundColor Yellow
        $Response = Read-Host "Overwrite existing file? (y/N)"
        if ($Response -ne 'y' -and $Response -ne 'Y') {
            Write-Host "Deployment cancelled by user." -ForegroundColor Yellow
            exit 0
        }
    }

    # Copy main DLL
    Write-Host "Copying main DLL..." -ForegroundColor Yellow
    Copy-Item -Path $SourceDLL -Destination $TargetDLL -Force
    Write-Host "Deployed: SimioUnrealEngineLiveLinkConnector.dll" -ForegroundColor Green

    # Copy MOCK native DLL
    Write-Host ""
    Write-Host "Copying MOCK native DLL to Simio..." -ForegroundColor Yellow
    Copy-Item -Path $MockDLLSource -Destination $TargetNativeDLL -Force
    
    $DeployedDLL = Get-Item $TargetNativeDLL
    $DeployedSizeKB = [math]::Round($DeployedDLL.Length / 1KB, 2)
    
    if ($DeployedDLL.Length -lt 200KB) {
        Write-Host "✅ Deployed: UnrealLiveLink.Native.dll ($DeployedSizeKB KB - MOCK)" -ForegroundColor Green
    } else {
        Write-Warning "Deployed DLL is larger than expected! ($DeployedSizeKB KB)"
    }

    # Copy any additional dependencies from output directory
    $AdditionalFiles = @(
        "System.Drawing.Common.dll"
    )

    foreach ($File in $AdditionalFiles) {
        $SourceFile = Join-Path $OutputDir $File
        $TargetFile = Join-Path $SimioUserExtensionsDir $File
        
        if (Test-Path $SourceFile) {
            Write-Host "Copying dependency: $File..." -ForegroundColor Yellow
            Copy-Item -Path $SourceFile -Destination $TargetFile -Force
            Write-Host "Deployed: $File" -ForegroundColor Green
        }
    }

    Write-Host ""
    Write-Host "=== Deployment Summary ===" -ForegroundColor Cyan
    Write-Host "Status: SUCCESS" -ForegroundColor Green
    Write-Host "Target Directory: $SimioUserExtensionsDir" -ForegroundColor White
    Write-Host "Managed DLL: SimioUnrealEngineLiveLinkConnector.dll" -ForegroundColor White
    Write-Host "Native DLL: UnrealLiveLink.Native.dll ($DeployedSizeKB KB - MOCK)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "⚠️  MOCK DLL DEPLOYED - For development/testing only!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Launch Simio application" -ForegroundColor White
    Write-Host "2. Test with mock functionality (no Unreal Engine required)" -ForegroundColor White
    Write-Host "3. For REAL Unreal Engine testing:" -ForegroundColor Yellow
    Write-Host "   Run: .\build\DeployNativeDLLToSimio.ps1" -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host ""
    Write-Host "=== Deployment Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if (!(Test-Administrator)) {
        Write-Host "Tip: Try running PowerShell as Administrator" -ForegroundColor Yellow
    }
    
    exit 1
}