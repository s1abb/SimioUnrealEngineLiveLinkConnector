# Reset LiveLink Message Bus
# Run this when Message Bus stops responding

Write-Host "Resetting LiveLink Message Bus..." -ForegroundColor Cyan

# Kill Simio processes
Write-Host "Stopping Simio processes..." -ForegroundColor Yellow
Get-Process -Name "Simio" -ErrorAction SilentlyContinue | Stop-Process -Force

# Kill Unreal Editor
Write-Host "Stopping Unreal Editor..." -ForegroundColor Yellow
Get-Process -Name "UnrealEditor" -ErrorAction SilentlyContinue | Stop-Process -Force

# Kill Epic Games Launcher (can hold port 6666)
Write-Host "Stopping Epic Games Launcher..." -ForegroundColor Yellow
Get-Process -Name "EpicGamesLauncher" -ErrorAction SilentlyContinue | Stop-Process -Force

# Wait for ports to release
Write-Host "Waiting for ports to release..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Check if ports are clear
Write-Host "`nChecking UDP port 6666..." -ForegroundColor Cyan
$port6666 = netstat -ano | findstr "6666"
if ($port6666) {
    Write-Host "WARNING: Port 6666 still in use:" -ForegroundColor Red
    Write-Host $port6666
} else {
    Write-Host "Port 6666 is clear!" -ForegroundColor Green
}

Write-Host "`nReset complete! You can now restart Unreal and Simio." -ForegroundColor Green
