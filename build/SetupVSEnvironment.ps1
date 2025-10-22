# Setup Visual Studio Developer Environment for PowerShell session
# This adds Visual Studio tools (including csc.exe) to the current PATH

param(
    [switch]$Persistent = $false  # Add to user PATH permanently
)

Write-Host "=== Visual Studio Developer Environment Setup ===" -ForegroundColor Cyan

# Find Visual Studio 2022 installation
$VSPaths = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional", 
    "C:\Program Files\Microsoft Visual Studio\2022\Community",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools",
    "C:\Program Files\Microsoft Visual Studio\2022\BuildTools"
)

$VSPath = $null
foreach ($Path in $VSPaths) {
    if (Test-Path $Path) {
        $VSPath = $Path
        Write-Host "Found Visual Studio at: $VSPath" -ForegroundColor Green
        break
    }
}

if (-not $VSPath) {
    Write-Host "Visual Studio 2022 not found in standard locations" -ForegroundColor Red
    Write-Host "Please install Visual Studio 2022 or Visual Studio Build Tools" -ForegroundColor Yellow
    exit 1
}

# Setup environment using vcvars64.bat
$VCVarsPath = Join-Path $VSPath "VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path $VCVarsPath)) {
    Write-Host "vcvars64.bat not found at: $VCVarsPath" -ForegroundColor Red
    exit 1
}

Write-Host "Setting up Visual Studio environment..." -ForegroundColor Yellow

# Create temporary batch file to capture environment
$TempBat = Join-Path $env:TEMP "capture_vs_env.bat"
$TempEnv = Join-Path $env:TEMP "vs_environment.txt"

# Batch file that sets up VS environment and outputs all environment variables
$BatchContent = @"
@echo off
call "$VCVarsPath" >nul 2>&1
set > "$TempEnv"
"@

Set-Content -Path $TempBat -Value $BatchContent -Encoding ASCII

# Execute and capture environment
& cmd.exe /c "`"$TempBat`"" | Out-Null

if (Test-Path $TempEnv) {
    Write-Host "Processing Visual Studio environment variables..." -ForegroundColor Gray
    
    # Read environment variables from VS setup
    $VSEnvVars = @{}
    Get-Content $TempEnv | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            $VSEnvVars[$matches[1]] = $matches[2]
        }
    }
    
    # Key paths to add to current session
    $KeyVars = @('PATH', 'LIB', 'LIBPATH', 'INCLUDE')
    
    foreach ($VarName in $KeyVars) {
        if ($VSEnvVars.ContainsKey($VarName)) {
            $NewValue = $VSEnvVars[$VarName]
            [Environment]::SetEnvironmentVariable($VarName, $NewValue, 'Process')
            
            if ($VarName -eq 'PATH') {
                Write-Host "Updated PATH with Visual Studio tools" -ForegroundColor Green
                
                # Show key tools now available
                $ImportantTools = @('csc.exe', 'cl.exe', 'link.exe', 'msbuild.exe')
                foreach ($Tool in $ImportantTools) {
                    $ToolPath = Get-Command $Tool -ErrorAction SilentlyContinue
                    if ($ToolPath) {
                        Write-Host "  -> $Tool available at: $($ToolPath.Source)" -ForegroundColor Gray
                    }
                }
            }
        }
    }
    
    # Optionally add to user PATH permanently  
    if ($Persistent) {
        Write-Host "`nAdding Visual Studio paths to user PATH permanently..." -ForegroundColor Yellow
        
        $CurrentUserPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
        $VSBinPath = Split-Path (Get-Command 'csc.exe' -ErrorAction SilentlyContinue).Source -Parent
        
        if ($VSBinPath -and $CurrentUserPath -notlike "*$VSBinPath*") {
            $NewUserPath = $CurrentUserPath + ';' + $VSBinPath
            [Environment]::SetEnvironmentVariable('PATH', $NewUserPath, 'User')
            Write-Host "Added to user PATH: $VSBinPath" -ForegroundColor Green
            Write-Host "Restart PowerShell to see permanent changes" -ForegroundColor Yellow
        } else {
            Write-Host "Visual Studio tools already in user PATH" -ForegroundColor Green
        }
    }
    
    Write-Host "`nVisual Studio Developer Environment Ready!" -ForegroundColor Green
    Write-Host "You can now use: csc.exe, cl.exe, msbuild.exe, etc." -ForegroundColor Cyan
    Write-Host "This setup is active for the current PowerShell session." -ForegroundColor Gray
    
    if (-not $Persistent) {
        Write-Host "`nTo make permanent, run: .\build\SetupVSEnvironment.ps1 -Persistent" -ForegroundColor Yellow
    }
    
} else {
    Write-Host "Failed to capture Visual Studio environment" -ForegroundColor Red
    exit 1
}

# Cleanup
Remove-Item $TempBat -ErrorAction SilentlyContinue  
Remove-Item $TempEnv -ErrorAction SilentlyContinue