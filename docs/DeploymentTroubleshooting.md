# Deployment Troubleshooting Guide

## Issue: Simio Crash with No Logs

### Symptom
- Simio crashes immediately when simulation starts
- No activity in Unreal Engine LiveLink window
- No error messages in UE Output Log
- No Simio trace messages

### Root Cause
**Missing Unreal Engine Runtime Dependencies**

The native DLL (`UnrealLiveLink.Native.dll`) requires additional runtime DLLs from Unreal Engine to function correctly. When these dependencies are missing, the DLL fails to load, causing Simio to crash before any logging can occur.

### Critical Dependencies

#### **tbbmalloc.dll** (Intel Threading Building Blocks)
- **Required**: YES - First dependency loaded
- **Size**: ~110 KB
- **Location**: `<UE_ROOT>/Engine/Binaries/Win64/tbbmalloc.dll`
- **Purpose**: Memory allocation library used by Unreal Engine core

#### Additional Potential Dependencies
Based on DLL dependency analysis, the following may also be required in some scenarios:
- `WinPixEventRuntime.dll` (delay-loaded)
- `dbghelp.dll` (delay-loaded)
- UE core modules (usually handled by Windows DLL search path)

### Solution

#### Automatic (Recommended)
Use the updated deployment script which now copies all required dependencies:

```powershell
.\build\DeployNativeDLLToSimio.ps1 -Force
```

This script will:
1. Deploy `UnrealLiveLink.Native.dll` (28.35 MB)
2. Copy `tbbmalloc.dll` from UE installation
3. Copy any other required runtime dependencies
4. Verify all files are present

#### Manual
If automatic deployment fails, manually copy dependencies:

```powershell
# Source
$UEBinaries = "C:\UE\UE_5.6\Engine\Binaries\Win64"

# Destination
$SimioExt = "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector"

# Copy required DLLs
Copy-Item "$UEBinaries\tbbmalloc.dll" $SimioExt -Force
```

### Verification

After deployment, verify all files are present:

```powershell
Get-ChildItem "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector" -Filter "*.dll"
```

Expected output:
```
Name                                   Size(MB)
----                                   --------
UnrealLiveLink.Native.dll                 28.35  ✅ Native UE DLL
tbbmalloc.dll                              0.11  ✅ UE Runtime
SimioUnrealEngineLiveLinkConnector.dll     0.05  ✅ Managed Layer
```

### Diagnostic Tools

#### Test DLL Loading Outside Simio
```powershell
# Create test harness
@"
using System;
using System.Runtime.InteropServices;

class TestNativeDLL
{
    [DllImport("UnrealLiveLink.Native.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ULL_GetVersion();

    [DllImport("UnrealLiveLink.Native.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ULL_Initialize([MarshalAs(UnmanagedType.LPStr)] string sourceName);

    static void Main()
    {
        try
        {
            Console.WriteLine("Getting version...");
            int version = ULL_GetVersion();
            Console.WriteLine($"Version: {version}");
            
            Console.WriteLine("Initializing...");
            int result = ULL_Initialize("TestSource");
            Console.WriteLine($"Result: {result}");
            Console.WriteLine("SUCCESS!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED: {ex.Message}");
        }
    }
}
"@ | Out-File -FilePath "TestNativeDLL.cs" -Encoding UTF8

# Compile and run
$env:Path = "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector;$env:Path"
csc /out:TestNativeDLL.exe TestNativeDLL.cs
.\TestNativeDLL.exe
```

#### Check DLL Dependencies
```powershell
# View required dependencies
dumpbin /dependents "UnrealLiveLink.Native.dll"

# Look for missing DLLs
# First dependency listed should be tbbmalloc.dll
```

### Prevention

#### Updated Build Process
The build process has been enhanced:

1. **BuildNative.ps1** - Builds the native DLL
2. **DeployNativeDLLToSimio.ps1** - Deploys DLL + dependencies
3. Automatic dependency detection and copying

#### Testing Checklist
Before reporting "it doesn't work":

✅ Verify all DLLs are present (especially `tbbmalloc.dll`)  
✅ Check DLL sizes match expected values  
✅ Test DLL loading with simple test harness  
✅ Check Windows Event Log for .NET errors  
✅ Verify Unreal Engine installation path is correct  

### Related Issues

#### Mock vs Native DLL Confusion
Previously, the deployment scripts were ambiguous about which DLL they deployed:
- **Mock DLL**: ~75 KB - For development without UE
- **Native DLL**: ~29 MB - For real UE integration

**Solution**: Scripts renamed for clarity:
- `DeployMockDLLToSimio.ps1` - Explicitly deploys mock
- `DeployNativeDLLToSimio.ps1` - Explicitly deploys native + dependencies
- `DeployToSimio.ps1` - Interactive menu to choose

### Technical Details

#### DLL Loading Order
When Simio loads the extension:
1. Loads `SimioUnrealEngineLiveLinkConnector.dll` (managed layer)
2. P/Invoke triggers load of `UnrealLiveLink.Native.dll`
3. Windows loader checks for `tbbmalloc.dll` ⚠️ CRITICAL
4. If missing → Instant crash, no error handling possible

#### Why No Error Messages?
- Crash occurs in Windows loader before managed exception handling
- No chance for managed code to catch or log
- Simio process terminates immediately
- No UE log because connection never established

### Summary

**The Problem**: Missing `tbbmalloc.dll` dependency  
**The Symptom**: Silent crash with no logs  
**The Solution**: Updated deployment script copies all dependencies  
**Prevention**: Always use `DeployNativeDLLToSimio.ps1` for UE testing  

### Last Updated
October 20, 2025 - After resolving deployment dependency issue
