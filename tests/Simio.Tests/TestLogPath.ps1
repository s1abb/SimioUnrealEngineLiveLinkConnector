# Simple test to verify the updated mock DLL logging
Write-Host "=== Mock DLL Log Path Verification Test ===" -ForegroundColor Green

$logPath = "tests\Simio.Tests\SimioUnrealLiveLink_Mock.log"

# Check current log contents (before test)
Write-Host "`nCurrent log file size:" -ForegroundColor Yellow
if (Test-Path $logPath) {
    $currentSize = (Get-Item $logPath).Length
    Write-Host "  $logPath - $currentSize bytes"
} else {
    Write-Host "  Log file does not exist yet"
}

Write-Host "`nTo test the log clearing functionality:" -ForegroundColor Cyan
Write-Host "1. Run a Simio simulation with the extension"
Write-Host "2. Check that the log file is cleared at ULL_Initialize"
Write-Host "3. Verify new log entries are written to: $logPath"

Write-Host "`nLog monitoring command:" -ForegroundColor Green
Write-Host "  Get-Content '$logPath' -Wait -Tail 10"

Write-Host "`nUpdated features:" -ForegroundColor Magenta
Write-Host "  ✅ Log path moved from C:\temp\ to tests\Simio.Tests\"
Write-Host "  ✅ Log file automatically cleared on each ULL_Initialize call"
Write-Host "  ✅ Added to .gitignore to prevent accidental commits"