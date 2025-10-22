# Enhanced DLL validation script
Write-Host "=== Enhanced DLL Validation ===" -ForegroundColor Cyan

$DllPath = "lib\native\win-x64\UnrealLiveLink.Native.dll"
if (-not (Test-Path $DllPath)) {
    Write-Host "❌ DLL not found at: $DllPath" -ForegroundColor Red
    exit 1
}

Write-Host "✅ DLL found: $DllPath" -ForegroundColor Green

# Get DLL info
$DllInfo = Get-Item $DllPath
Write-Host "DLL Size: $($DllInfo.Length) bytes" -ForegroundColor Gray
Write-Host "DLL Modified: $($DllInfo.LastWriteTime)" -ForegroundColor Gray

Write-Host "`nTesting P/Invoke with isolated process..." -ForegroundColor Yellow

try {
    # Get absolute path to DLL
    $AbsoluteDllPath = (Resolve-Path $DllPath).Path.Replace('\', '\\')
    
    # Create C# test code that doesn't require copying DLL
    $TestCode = @"
using System;
using System.Runtime.InteropServices;
using System.IO;

class Program {
    // Use absolute path to avoid copying DLL
    [DllImport("$AbsoluteDllPath", CallingConvention = CallingConvention.Cdecl)]
    static extern int ULL_Initialize(string providerName);
    
    [DllImport("$AbsoluteDllPath", CallingConvention = CallingConvention.Cdecl)]
    static extern int ULL_GetVersion();
    
    [DllImport("$AbsoluteDllPath", CallingConvention = CallingConvention.Cdecl)]
    static extern int ULL_IsConnected();
    
    [DllImport("$AbsoluteDllPath", CallingConvention = CallingConvention.Cdecl)]
    static extern void ULL_Shutdown();
    
    static int Main() {
        try {
            Console.WriteLine("[TEST] Calling ULL_GetVersion...");
            int version = ULL_GetVersion();
            Console.WriteLine("[TEST] ULL_GetVersion result: " + version);
            
            Console.WriteLine("[TEST] Calling ULL_Initialize...");
            int initResult = ULL_Initialize("IsolatedTest");
            Console.WriteLine("[TEST] ULL_Initialize result: " + initResult);
            
            Console.WriteLine("[TEST] Calling ULL_IsConnected...");
            int connResult = ULL_IsConnected();
            Console.WriteLine("[TEST] ULL_IsConnected result: " + connResult);
            
            Console.WriteLine("[TEST] Calling ULL_Shutdown...");
            ULL_Shutdown();
            Console.WriteLine("[TEST] ULL_Shutdown completed");
            
            Console.WriteLine("[TEST] All P/Invoke calls succeeded!");
            return 0;
        } catch (Exception ex) {
            Console.WriteLine("[ERROR] P/Invoke test failed: " + ex.Message);
            return 1;
        }
    }
}
"@

    # Create temporary directory for compilation
    $TempDir = Join-Path $env:TEMP "DLLValidation_$(Get-Date -Format 'yyyyMMddHHmmss')"
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    
    try {
        $TestFile = Join-Path $TempDir "DLLTest.cs"
        Set-Content -Path $TestFile -Value $TestCode -Encoding UTF8
        
        Push-Location $TempDir
        
        # Try using C# compiler if available in PATH
        $CompileSuccess = $false
        $CscCommand = Get-Command "csc.exe" -ErrorAction SilentlyContinue
        
        if ($CscCommand) {
            Write-Host "Compiling with Visual Studio C# compiler..." -ForegroundColor Gray
            
            try {
                # Compile directly using csc.exe
                $CompileResult = & csc.exe /reference:System.dll DLLTest.cs 2>&1
                if ($LASTEXITCODE -eq 0 -and (Test-Path "DLLTest.exe")) {
                    $CompileSuccess = $true
                } else {
                    Write-Host "Compilation failed:" -ForegroundColor Red
                    $CompileResult | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
                }
            } catch {
                Write-Host "Compilation error: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        
        if ($CompileSuccess) {
            Write-Host "Running isolated P/Invoke test..." -ForegroundColor Gray
            
            # Run the test in separate process
            $TestOutput = & .\DLLTest.exe 2>&1
            $TestExitCode = $LASTEXITCODE
            
            # Display results
            Write-Host "`nTest Output:" -ForegroundColor Yellow
            $TestOutput | ForEach-Object { 
                if ($_ -like "[MOCK]*") {
                    Write-Host "  $_" -ForegroundColor Green
                } elseif ($_ -like "[TEST]*") {
                    Write-Host "  $_" -ForegroundColor Cyan
                } elseif ($_ -like "[ERROR]*") {
                    Write-Host "  $_" -ForegroundColor Red
                } else {
                    Write-Host "  $_" -ForegroundColor Gray
                }
            }
            
            if ($TestExitCode -eq 0) {
                Write-Host "`n🎉 ISOLATED P/INVOKE TEST PASSED!" -ForegroundColor Green
                Write-Host "✅ Mock DLL working perfectly with no stray files!" -ForegroundColor Green
            } else {
                Write-Host "`n❌ P/Invoke test failed with exit code: $TestExitCode" -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "`n⚠️  Compilation failed - falling back to PowerShell Add-Type method" -ForegroundColor Yellow
            Write-Host "This may create a temporary DLL copy that gets cleaned up" -ForegroundColor Yellow
            
            Pop-Location
            
            # If VS compilation fails, we skip the fallback to avoid DLL conflicts
            Write-Host "❌ Visual Studio C# compiler not available" -ForegroundColor Red
            Write-Host "Please run: .\build\SetupVSEnvironment.ps1" -ForegroundColor Yellow
            Write-Host "This will add csc.exe to your PATH for clean P/Invoke testing" -ForegroundColor Yellow
            exit 1

        }
        
    } finally {
        Pop-Location -ErrorAction SilentlyContinue
        
        # Clean up temp directory
        if (Test-Path $TempDir) {
            Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    
} catch {
    Write-Host "`n❌ Enhanced validation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}