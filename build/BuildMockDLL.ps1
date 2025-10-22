# Build Mock UnrealLiveLink.Native DLL using Visual Studio compiler directly
param(
    [string]$Configuration = "Release"
)

Write-Host "=== Building Mock UnrealLiveLink.Native DLL (Simple Build) ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration"

# Setup paths
$RepoRoot = Split-Path $PSScriptRoot -Parent
$MockSrcDir = Join-Path $RepoRoot "src\Native\Mock"
$OutputDir = Join-Path $RepoRoot "lib\native\win-x64"
$BuildDir = Join-Path $RepoRoot "build\temp\mock"

# Ensure directories exist
New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Find Visual Studio 2022 Build Tools
$VSPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"
if (-not (Test-Path $VSPath)) {
    $VSPath = "C:\Program Files\Microsoft Visual Studio\2022\BuildTools"
}
if (-not (Test-Path $VSPath)) {
    Write-Error "Visual Studio 2022 Build Tools not found"
    exit 1
}

# Setup Visual Studio environment
$VCVarsPath = Join-Path $VSPath "VC\Auxiliary\Build\vcvars64.bat"
if (-not (Test-Path $VCVarsPath)) {
    Write-Error "vcvars64.bat not found at: $VCVarsPath"
    exit 1
}

Write-Host "Using Visual Studio at: $VSPath"
Write-Host "Setting up build environment..."

# Create a temporary batch file to setup environment and compile
$TempBat = Join-Path $BuildDir "build.bat"
$MockCpp = Join-Path $MockSrcDir "MockLiveLink.cpp"
$OutputDll = Join-Path $OutputDir "UnrealLiveLink.Native.dll"
$OutputLib = Join-Path $OutputDir "UnrealLiveLink.Native.lib"

$BatchContent = @"
@echo off
call "$VCVarsPath"
echo Compiling MockLiveLink.cpp...
cl.exe /LD /O2 /MD /DNDEBUG /EHsc "$MockCpp" /Fe:"$OutputDll" /implib:"$OutputLib"
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b 1
)
echo Build completed successfully!
exit /b 0
"@

Write-Output $BatchContent | Out-File -FilePath $TempBat -Encoding ASCII

# Execute the build
Write-Host "Compiling..."
$Process = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "`"$TempBat`"" -WorkingDirectory $BuildDir -Wait -PassThru -NoNewWindow

if ($Process.ExitCode -eq 0) {
    Write-Host "Build SUCCESS!" -ForegroundColor Green
    
    if (Test-Path $OutputDll) {
        $FileInfo = Get-Item $OutputDll
        Write-Host "DLL created: $OutputDll" -ForegroundColor Green
        Write-Host "Size: $($FileInfo.Length) bytes"
        Write-Host "Modified: $($FileInfo.LastWriteTime)"
    }
    
    if (Test-Path $OutputLib) {
        $LibInfo = Get-Item $OutputLib
        Write-Host "Import library created: $OutputLib" -ForegroundColor Green
        Write-Host "Size: $($LibInfo.Length) bytes"
    }
} else {
    Write-Host "Build FAILED with exit code: $($Process.ExitCode)" -ForegroundColor Red
    exit 1
}

# Cleanup
Remove-Item $TempBat -ErrorAction SilentlyContinue

# Explicit success exit
Write-Host "Mock DLL build completed successfully!" -ForegroundColor Green
exit 0