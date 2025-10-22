# Simple P/Invoke test for Mock DLL
Write-Host "=== Simple Mock DLL P/Invoke Test ===" -ForegroundColor Cyan

# Check DLL exists
$DllPath = "lib\native\win-x64\UnrealLiveLink.Native.dll"
if (-not (Test-Path $DllPath)) {
    Write-Host "❌ Mock DLL not found at: $DllPath" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Mock DLL found: $DllPath" -ForegroundColor Green

# Create simple P/Invoke test
$TestCode = @'
using System;
using System.Runtime.InteropServices;

class Program {
    [DllImport("UnrealLiveLink.Native.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int ULL_Initialize(string sourceName);
    
    [DllImport("UnrealLiveLink.Native.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int ULL_IsConnected();
    
    [DllImport("UnrealLiveLink.Native.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int ULL_Shutdown();
    
    static int Main() {
        Console.WriteLine("=== Mock DLL P/Invoke Test ===");
        
        try {
            Console.WriteLine("Calling ULL_Initialize...");
            int result = ULL_Initialize("MockTest");
            Console.WriteLine($"ULL_Initialize result: {result}");
            
            Console.WriteLine("Calling ULL_IsConnected...");
            result = ULL_IsConnected();
            Console.WriteLine($"ULL_IsConnected result: {result}");
            
            Console.WriteLine("Calling ULL_Shutdown...");
            result = ULL_Shutdown();
            Console.WriteLine($"ULL_Shutdown result: {result}");
            
            Console.WriteLine("✅ All P/Invoke calls succeeded!");
            return 0;
            
        } catch (Exception ex) {
            Console.WriteLine($"❌ P/Invoke test failed: {ex.Message}");
            return 1;
        }
    }
}
'@

# Create temporary test directory
$TempTestDir = Join-Path $env:TEMP "MockDllTest_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $TempTestDir -Force | Out-Null

try {
    # Write test code
    $TestFile = Join-Path $TempTestDir "MockDllTest.cs"
    Set-Content -Path $TestFile -Value $TestCode -Encoding UTF8
    
    # Copy DLL to test directory
    $TestDllPath = Join-Path $TempTestDir "UnrealLiveLink.Native.dll"
    Copy-Item $DllPath $TestDllPath -Force
    
    # Compile and run test
    Write-Host "Compiling P/Invoke test..." -ForegroundColor Yellow
    Push-Location $TempTestDir
    
    # Try to find C# compiler
    $CscPath = Get-Command "csc.exe" -ErrorAction SilentlyContinue
    if ($CscPath) {
        $CompileOutput = & csc.exe MockDllTest.cs 2>&1
        $CompileExitCode = $LASTEXITCODE
        
        if ($CompileExitCode -eq 0) {
            Write-Host "Running P/Invoke test..." -ForegroundColor Yellow
            $TestOutput = & .\MockDllTest.exe 2>&1
            $TestExitCode = $LASTEXITCODE
            
            Write-Host "`nP/Invoke Test Output:" -ForegroundColor Yellow
            $TestOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            
            if ($TestExitCode -eq 0) {
                Write-Host "`n🎉 P/INVOKE TEST PASSED!" -ForegroundColor Green
            } else {
                Write-Host "`n❌ P/Invoke test failed with exit code: $TestExitCode" -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "`n❌ Compilation failed:" -ForegroundColor Red
            $CompileOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
            exit 1
        }
    } else {
        Write-Host "`n❌ C# compiler not found. Please install .NET SDK." -ForegroundColor Red
        exit 1
    }
    
    Pop-Location
    
} finally {
    # Cleanup
    if (Test-Path $TempTestDir) {
        Remove-Item $TempTestDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}