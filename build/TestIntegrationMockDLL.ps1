# TestIntegrationMockDLL.ps1
# Simple test to validate P/Invoke integration between managed layer and mock DLL
# NOTE: For comprehensive integration tests with the real native DLL, use RunIntegrationTests.ps1
Write-Host "=== Testing Managed Layer with Mock DLL ===" -ForegroundColor Cyan

# Ensure DLL is available
$DllPath = "lib\native\win-x64\UnrealLiveLink.Native.dll"
if (-not (Test-Path $DllPath)) {
    Write-Host "❌ Mock DLL not found at: $DllPath" -ForegroundColor Red
    exit 1
}

$ManagedDll = "src\Managed\bin\Release\net48\SimioUnrealEngineLiveLinkConnector.dll"
if (-not (Test-Path $ManagedDll)) {
    Write-Host "❌ Managed DLL not found at: $ManagedDll" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Mock DLL found: $DllPath" -ForegroundColor Green
Write-Host "✅ Managed DLL found: $ManagedDll" -ForegroundColor Green

# Create simple test executable
$TestCode = @'
using System;
using System.IO;
using System.Reflection;

class Program {
    static int Main() {
        try {
            Console.WriteLine("=== Managed Layer + Mock DLL Integration Test ===");
            
            // Load the managed assembly
            string managedDllPath = Path.Combine(Environment.CurrentDirectory, @"src\Managed\bin\Release\net48\SimioUnrealEngineLiveLinkConnector.dll");
            if (!File.Exists(managedDllPath)) {
                Console.WriteLine($"❌ Managed DLL not found: {managedDllPath}");
                return 1;
            }
            
            Assembly assembly = Assembly.LoadFrom(managedDllPath);
            
            // Get the UnrealLiveLinkNative class
            Type nativeType = assembly.GetType("SimioUnrealEngineLiveLinkConnector.UnrealIntegration.UnrealLiveLinkNative");
            if (nativeType == null) {
                Console.WriteLine("❌ UnrealLiveLinkNative type not found");
                return 1;
            }
            
            Console.WriteLine("✅ Loaded UnrealLiveLinkNative type");
            
            // Test basic P/Invoke calls
            Console.WriteLine("\nTesting P/Invoke calls...");
            
            // Test ULL_Initialize
            MethodInfo initMethod = nativeType.GetMethod("ULL_Initialize");
            int result = (int)initMethod.Invoke(null, new object[] { "MockIntegrationTest" });
            Console.WriteLine($"ULL_Initialize result: {result}");
            
            // Test ULL_GetVersion 
            MethodInfo versionMethod = nativeType.GetMethod("ULL_GetVersion");
            int version = (int)versionMethod.Invoke(null, null);
            Console.WriteLine($"ULL_GetVersion result: {version}");
            
            // Test ULL_IsConnected
            MethodInfo connectedMethod = nativeType.GetMethod("ULL_IsConnected");
            int connected = (int)connectedMethod.Invoke(null, null);
            Console.WriteLine($"ULL_IsConnected result: {connected}");
            
            // Test ULL_Shutdown
            MethodInfo shutdownMethod = nativeType.GetMethod("ULL_Shutdown");
            shutdownMethod.Invoke(null, null);
            Console.WriteLine("ULL_Shutdown called");
            
            Console.WriteLine("\n🎉 All P/Invoke calls succeeded!");
            Console.WriteLine("✅ Mock DLL integration working correctly!");
            
            return 0;
            
        } catch (Exception ex) {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}
'@

# Create temporary test directory
$TempTestDir = Join-Path $env:TEMP "ManagedMockTest_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -ItemType Directory -Path $TempTestDir -Force | Out-Null

try {
    Write-Host "Creating integration test..." -ForegroundColor Yellow
    
    # Write test code
    $TestFile = Join-Path $TempTestDir "ManagedMockTest.cs"
    Set-Content -Path $TestFile -Value $TestCode -Encoding UTF8
    
    # Copy DLLs to test directory
    $TestDllPath = Join-Path $TempTestDir "UnrealLiveLink.Native.dll"
    Copy-Item $DllPath $TestDllPath -Force
    
    # Copy managed DLL and dependencies
    $ManagedTestDir = Join-Path $TempTestDir "src\Managed\bin\Release\net48"
    New-Item -ItemType Directory -Path $ManagedTestDir -Force | Out-Null
    Copy-Item "src\Managed\bin\Release\net48\*" $ManagedTestDir -Recurse -Force
    
    Write-Host "Compiling integration test..." -ForegroundColor Yellow
    Push-Location $TempTestDir
    
    # Find .NET Framework references
    $FrameworkDir = (Get-ChildItem "C:\Program Files*\Reference Assemblies\Microsoft\Framework\.NETFramework" | Sort-Object Name -Descending | Select-Object -First 1).FullName
    if ($FrameworkDir) {
        $NetFrameworkRefs = Join-Path $FrameworkDir "v4.*" | Get-ChildItem | Sort-Object Name -Descending | Select-Object -First 1
    }
    
    # Compile with csc if available
    $CscPath = Get-Command "csc.exe" -ErrorAction SilentlyContinue
    if ($CscPath) {
        $CompileCmd = "csc.exe"
        if ($NetFrameworkRefs) {
            $CompileCmd += " /reference:System.dll /reference:System.Core.dll /reference:$($NetFrameworkRefs.FullName)\System.dll"
        }
        $CompileCmd += " ManagedMockTest.cs"
        
        Write-Host "Compile command: $CompileCmd" -ForegroundColor Gray
        $CompileOutput = Invoke-Expression $CompileCmd 2>&1
        $CompileExitCode = $LASTEXITCODE
        
        if ($CompileExitCode -eq 0) {
            Write-Host "Running integration test..." -ForegroundColor Yellow
            $TestOutput = & .\ManagedMockTest.exe 2>&1
            $TestExitCode = $LASTEXITCODE
            
            Write-Host "`nIntegration Test Output:" -ForegroundColor Yellow
            $TestOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            
            if ($TestExitCode -eq 0) {
                Write-Host "`n🎉 INTEGRATION TEST PASSED!" -ForegroundColor Green
                Write-Host "✅ Mock DLL + Managed Layer integration working!" -ForegroundColor Green
            } else {
                Write-Host "`n❌ Integration test failed with exit code: $TestExitCode" -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Host "`n❌ Compilation failed:" -ForegroundColor Red
            $CompileOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
            exit 1
        }
    } else {
        Write-Host "`n❌ C# compiler not found. Cannot run integration test." -ForegroundColor Yellow
        Write-Host "Mock DLL is ready, but can't validate P/Invoke without compiler." -ForegroundColor Yellow
    }
    
} finally {
    Pop-Location -ErrorAction SilentlyContinue
    # Cleanup
    if (Test-Path $TempTestDir) {
        Remove-Item $TempTestDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}