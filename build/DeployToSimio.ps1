# DeployToSimio.ps1
# Deploy the Simio Unreal Engine LiveLink Connector to Simio UserExtensions directory

param(
    [string]$Configuration = "Release",
    [switch]$Force = $false,
    [switch]$Verbose = $false
)

# Get script directory and project paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ProjectRoot = Split-Path -Parent $ScriptDir
$OutputDir = Join-Path $ProjectRoot "src\Managed\bin\$Configuration\net48"

# Simio installation paths
$SimioUserExtensionsDir = "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector"
$TargetDLL = Join-Path $SimioUserExtensionsDir "SimioUnrealEngineLiveLinkConnector.dll"
$TargetNativeDLL = Join-Path $SimioUserExtensionsDir "UnrealLiveLink.Native.dll"

# Source files
$SourceDLL = Join-Path $OutputDir "SimioUnrealEngineLiveLinkConnector.dll"
$SourceNativeDLL = Join-Path $OutputDir "UnrealLiveLink.Native.dll"

Write-Host "=== Deploy Simio Unreal Engine LiveLink Connector ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Source: $OutputDir" -ForegroundColor Yellow  
Write-Host "Target: $SimioUserExtensionsDir" -ForegroundColor Yellow
Write-Host ""

# Check if running as administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Verify source files exist
if (!(Test-Path $SourceDLL)) {
    Write-Error "Source DLL not found: $SourceDLL"
    Write-Host "Please run build\BuildManaged.ps1 first to build the project." -ForegroundColor Red
    exit 1
}

Write-Host "Source DLL found: $SourceDLL" -ForegroundColor Green

# Check for native DLL (optional)
$HasNativeDLL = Test-Path $SourceNativeDLL
if ($HasNativeDLL) {
    Write-Host "Native DLL found: $SourceNativeDLL" -ForegroundColor Green
} else {
    Write-Host "WARNING: Native DLL not found (will deploy managed layer only): $SourceNativeDLL" -ForegroundColor Yellow
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

    # Copy native DLL if available
    if ($HasNativeDLL) {
        Write-Host "Copying native DLL..." -ForegroundColor Yellow
        Copy-Item -Path $SourceNativeDLL -Destination $TargetNativeDLL -Force
        Write-Host "Deployed: UnrealLiveLink.Native.dll" -ForegroundColor Green
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
    Write-Host "Main DLL: SimioUnrealEngineLiveLinkConnector.dll" -ForegroundColor White
    if ($HasNativeDLL) {
        Write-Host "Native DLL: UnrealLiveLink.Native.dll" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Launch Simio application" -ForegroundColor White
    Write-Host "2. Verify 'UnrealEngineLiveLinkConnector' element appears in Elements toolbox" -ForegroundColor White
    Write-Host "3. Verify 'CreateObject' and 'SetObjectPositionOrientation' steps appear in Steps toolbox" -ForegroundColor White
    Write-Host "4. Report back with validation results" -ForegroundColor White

} catch {
    Write-Host ""
    Write-Host "=== Deployment Failed ===" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if (!(Test-Administrator)) {
        Write-Host "Tip: Try running PowerShell as Administrator" -ForegroundColor Yellow
    }
    
    exit 1
}