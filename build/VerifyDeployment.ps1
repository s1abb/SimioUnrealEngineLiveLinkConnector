# VerifyDeployment.ps1
# Quick verification script to check if all required files are deployed correctly

param(
    [switch]$Detailed = $false
)

$SimioExtDir = "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector"

Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "         Simio Unreal LiveLink Deployment Verification" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Check if directory exists
if (!(Test-Path $SimioExtDir)) {
    Write-Host "❌ FAILED: Extension directory not found!" -ForegroundColor Red
    Write-Host "   Expected: $SimioExtDir" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Run: .\build\DeployNativeDLLToSimio.ps1 -Force" -ForegroundColor Cyan
    exit 1
}

Write-Host "✅ Extension directory found" -ForegroundColor Green
Write-Host "   Path: $SimioExtDir" -ForegroundColor Gray
Write-Host ""

# Define required files with expected characteristics
$RequiredFiles = @(
    @{
        Name = "UnrealLiveLink.Native.dll"
        MinSize = 20  # MB
        MaxSize = 35  # MB
        Type = "Native UE DLL"
        Critical = $true
    },
    @{
        Name = "tbbmalloc.dll"
        MinSize = 0.05  # MB
        MaxSize = 0.5   # MB
        Type = "UE Runtime Dependency"
        Critical = $true
    },
    @{
        Name = "SimioUnrealEngineLiveLinkConnector.dll"
        MinSize = 0.01  # MB
        MaxSize = 1     # MB
        Type = "Managed Layer"
        Critical = $true
    }
)

$AllGood = $true
$Warnings = @()

Write-Host "Checking required files..." -ForegroundColor Cyan
Write-Host ""

foreach ($file in $RequiredFiles) {
    $FilePath = Join-Path $SimioExtDir $file.Name
    $Status = "⚠️"
    $StatusColor = "Yellow"
    $Message = ""
    
    if (Test-Path $FilePath) {
        $FileInfo = Get-Item $FilePath
        $SizeMB = [math]::Round($FileInfo.Length / 1MB, 2)
        
        # Check size constraints
        if ($SizeMB -ge $file.MinSize -and $SizeMB -le $file.MaxSize) {
            $Status = "✅"
            $StatusColor = "Green"
            $Message = "OK ($SizeMB MB)"
        } else {
            $Status = "⚠️"
            $StatusColor = "Yellow"
            $Message = "SIZE MISMATCH ($SizeMB MB, expected: $($file.MinSize)-$($file.MaxSize) MB)"
            $Warnings += "$($file.Name): $Message"
            
            if ($file.Critical) {
                $AllGood = $false
            }
        }
        
        Write-Host "$Status $($file.Name.PadRight(45)) $Message" -ForegroundColor $StatusColor
        
        if ($Detailed) {
            Write-Host "   Type: $($file.Type)" -ForegroundColor Gray
            Write-Host "   Modified: $($FileInfo.LastWriteTime)" -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        $Status = "❌"
        $StatusColor = "Red"
        $Message = "MISSING"
        
        Write-Host "$Status $($file.Name.PadRight(45)) $Message" -ForegroundColor $StatusColor
        
        if ($file.Critical) {
            $AllGood = $false
        }
    }
}

Write-Host ""

# Additional files check (optional dependencies)
if ($Detailed) {
    Write-Host "Additional files in directory:" -ForegroundColor Cyan
    $AllFiles = Get-ChildItem $SimioExtDir -Filter "*.dll"
    $AdditionalFiles = $AllFiles | Where-Object { $_.Name -notin $RequiredFiles.Name }
    
    if ($AdditionalFiles.Count -gt 0) {
        foreach ($file in $AdditionalFiles) {
            $SizeMB = [math]::Round($file.Length / 1MB, 2)
            Write-Host "  INFO: $($file.Name) - $SizeMB MB" -ForegroundColor Gray
        }
    } else {
        Write-Host "  None" -ForegroundColor Gray
    }
    Write-Host ""
}

# Summary
Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan

if ($AllGood) {
    Write-Host "                    ✅ DEPLOYMENT OK!" -ForegroundColor Green
    Write-Host ""
    Write-Host "All critical files are present and correct." -ForegroundColor Green
    
    if ($Warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "⚠️  Non-critical warnings:" -ForegroundColor Yellow
        foreach ($warning in $Warnings) {
            Write-Host "   - $warning" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "Ready to test Simio → Unreal Engine LiveLink connection!" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Start Unreal Engine (Window → Virtual Production → Live Link)" -ForegroundColor White
    Write-Host "  2. Open: tests\Simio.Tests\Model.spfx" -ForegroundColor White
    Write-Host "  3. Run simulation" -ForegroundColor White
    Write-Host "  4. Watch for 'SimioSimulation' source in UE LiveLink window" -ForegroundColor White
    
    exit 0
} else {
    Write-Host "                    ❌ DEPLOYMENT FAILED!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Critical files are missing or incorrect." -ForegroundColor Red
    Write-Host ""
    Write-Host "🔧 Fix the deployment:" -ForegroundColor Yellow
    Write-Host "   .\build\DeployNativeDLLToSimio.ps1 -Force" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "📚 For troubleshooting help:" -ForegroundColor Yellow
    Write-Host "   See: docs\DeploymentTroubleshooting.md" -ForegroundColor Cyan
    
    exit 1
}

Write-Host "═══════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
