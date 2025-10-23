# Configure UDP Messaging for Simio LiveLink Native DLL
# Creates the Engine.ini config file that the native DLL will read

Write-Host "=== Configure UDP Messaging for Simio LiveLink ===" -ForegroundColor Cyan
Write-Host ""

# Determine config path
$ConfigDir = Join-Path $env:LOCALAPPDATA "UnrealEngine\Common\Saved\Config\WindowsNoEditor"
$ConfigFile = Join-Path $ConfigDir "Engine.ini"

Write-Host "Config location: $ConfigFile" -ForegroundColor Yellow
Write-Host ""

# Create directory if it doesn't exist
if (-not (Test-Path $ConfigDir)) {
    Write-Host "Creating config directory structure..." -ForegroundColor Yellow
    try {
        New-Item -ItemType Directory -Path $ConfigDir -Force | Out-Null
        Write-Host "✅ Directory created successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Failed to create directory: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "✅ Config directory already exists" -ForegroundColor Green
}
Write-Host ""

# Config content for Simio side
$ConfigContent = @"
[/Script/UdpMessaging.UdpMessagingSettings]
EnableTransport=True
bAutoRepair=False
bStopServiceWhenAppDeactivates=False
UnicastEndpoint=0.0.0.0:0
MulticastEndpoint=230.0.0.1:6666
MulticastTimeToLive=0
EnableTunnel=True
TunnelUnicastEndpoint=127.0.0.1:9030
TunnelMulticastEndpoint=127.0.0.1:9031
"@

# Check if file exists
if (Test-Path $ConfigFile) {
    Write-Host "Config file already exists. Checking for UdpMessaging section..." -ForegroundColor Yellow
    
    $ExistingContent = Get-Content $ConfigFile -Raw -ErrorAction SilentlyContinue
    
    if ($ExistingContent -match "\[/Script/UdpMessaging\.UdpMessagingSettings\]") {
        Write-Host "⚠️  UdpMessaging section already exists!" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Current config:" -ForegroundColor Cyan
        Write-Host $ExistingContent -ForegroundColor White
        Write-Host ""
        $Response = Read-Host "Overwrite? (y/N)"
        
        if ($Response -ne "y" -and $Response -ne "Y") {
            Write-Host "Skipping configuration update." -ForegroundColor Yellow
            exit 0
        }
    }
}

# Write config
Write-Host "Writing configuration..." -ForegroundColor Yellow
try {
    $ConfigContent | Out-File -FilePath $ConfigFile -Encoding UTF8 -Force
    Write-Host "✅ Configuration written successfully!" -ForegroundColor Green
}
catch {
    Write-Host "❌ Failed to write config: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Configuration Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Config file: $ConfigFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "Simio Settings:" -ForegroundColor Cyan
Write-Host "  • Tunnel ENABLED (localhost tunnel)" -ForegroundColor White
Write-Host "  • Tunnel ports: 9030/9031" -ForegroundColor White
Write-Host "  • Auto port selection for unicast" -ForegroundColor White
Write-Host "  • Auto-repair disabled" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Configure Unreal side with matching tunnel settings:" -ForegroundColor White
Write-Host "   Edit → Project Settings → Plugins → UDP Messaging" -ForegroundColor Gray
Write-Host "   • Enable Tunnel: ON" -ForegroundColor Gray
Write-Host "   • Tunnel Unicast: 127.0.0.1:9030" -ForegroundColor Gray
Write-Host "   • Tunnel Multicast: 127.0.0.1:9031" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Restart both Simio and Unreal Editor" -ForegroundColor White
Write-Host ""
