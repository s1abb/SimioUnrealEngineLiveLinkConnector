# KillTestProcesses.ps1
# Forcefully kill all testhost and vstest processes

Write-Host "=== Killing Test Processes ===" -ForegroundColor Cyan

$processes = Get-Process -ErrorAction SilentlyContinue | Where-Object { 
    $_.ProcessName -like "*testhost*" -or 
    $_.ProcessName -like "*vstest*" -or
    $_.ProcessName -like "*testhost.exe*"
}

if ($processes) {
    Write-Host "Found $($processes.Count) test process(es) to kill:" -ForegroundColor Yellow
    $processes | ForEach-Object {
        Write-Host "  - $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Gray
        try {
            # Try PowerShell Stop-Process first
            Stop-Process -Id $_.Id -Force -ErrorAction Stop
            Write-Host "    ✅ Killed with Stop-Process" -ForegroundColor Green
        } catch {
            # If that fails, use taskkill which is more aggressive
            Write-Host "    ⚠️ Stop-Process failed, trying taskkill..." -ForegroundColor Yellow
            $result = & taskkill /F /T /PID $_.Id 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "    ✅ Killed with taskkill" -ForegroundColor Green
            } else {
                Write-Host "    ❌ taskkill failed: $result" -ForegroundColor Red
            }
        }
    }
    
    Write-Host "`nWaiting for processes to terminate..." -ForegroundColor Yellow
    Start-Sleep -Seconds 3
    
    # Verify
    $remaining = Get-Process -ErrorAction SilentlyContinue | Where-Object { 
        $_.ProcessName -like "*testhost*" -or 
        $_.ProcessName -like "*vstest*"
    }
    
    if ($remaining) {
        Write-Host "⚠️ Some processes still running - try running as Administrator" -ForegroundColor Red
        $remaining | ForEach-Object {
            Write-Host "  - $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Red
        }
    } else {
        Write-Host "✅ All test processes killed successfully" -ForegroundColor Green
    }
} else {
    Write-Host "✅ No test processes found" -ForegroundColor Green
}

Write-Host "`nYou can now run: .\build\RunIntegrationTests.ps1" -ForegroundColor Cyan
