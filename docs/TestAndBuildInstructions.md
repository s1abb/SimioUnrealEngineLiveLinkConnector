# Test & Build Instructions

**Purpose:** Build, test, and deploy workflows - the "how to run" reference.  
**Audience:** Developers building and testing the connector  
**Scope:** Step-by-step commands, troubleshooting, tool configuration  
**Not Included:** Implementation details (see layer dev docs), architecture (see Architecture.md)

---

## Prerequisites

### Required Software
- Windows 10/11
- Visual Studio 2019+ or Build Tools for Visual Studio
- .NET Framework 4.8 Developer Pack
- PowerShell 5.1+

### Optional Software
- Simio (for Simio UI testing) - Install location: `C:\Program Files\Simio LLC\Simio\`
- Unreal Engine 5.6 (for native layer development)
  - **Auto-detection priority:** Source build (`C:\UE\UE_5.6_Source`) → Binary (`C:\UE\UE_5.6`)
  - **Note:** Source build required for native DLL compilation with UnrealBuildTool
  - Binary installation insufficient for BuildNative.ps1

### Environment Setup
```powershell
# Verify .NET Framework
dotnet --info

# Verify Visual Studio installation
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest
```

---

## Quick Start (Managed Layer Only)

### One-Time Setup
```powershell
# Navigate to repo
cd C:\repos\SimioUnrealEngineLiveLinkConnector

# Setup VS environment (once per PowerShell session)
.\build\SetupVSEnvironment.ps1
```

### Build & Test Workflow
```powershell
# 1. Build mock native DLL
.\build\BuildMockDLL.ps1

# 2. Build managed layer
.\build\BuildManaged.ps1
# OR: dotnet build src/Managed/SimioUnrealEngineLiveLinkConnector.csproj --configuration Release

# 3. Run unit tests
dotnet test tests/Unit.Tests/Unit.Tests.csproj

# 4. Run integration tests
dotnet test tests/Integration.Tests/Integration.Tests.csproj
```

### Deploy to Simio (Optional)
```powershell
# Requires Administrator privileges
.\build\DeployToSimio.ps1
```

---

## Build Commands Reference

### Mock Native DLL
**Purpose:** Build mock DLL for managed layer testing (no Unreal required)

```powershell
.\build\BuildMockDLL.ps1
```

**Output:** `lib/native/win-x64/UnrealLiveLink.Native.dll` (mock implementation, ~75KB)

### Managed Layer
**Purpose:** Build C# Simio extension

```powershell
# Using build script (recommended)
.\build\BuildManaged.ps1

# Using dotnet CLI
dotnet build src/Managed/SimioUnrealEngineLiveLinkConnector.csproj --configuration Release

# Clean build
.\build\CleanupBuildArtifacts.ps1
.\build\BuildManaged.ps1
```

**Output:** `src/Managed/bin/Release/net48/SimioUnrealEngineLiveLinkConnector.dll`

### Native Layer (Real Implementation)
**Purpose:** Build Unreal Engine native DLL with full LiveLink integration

```powershell
# Build native UE DLL (requires UE 5.6 source)
.\build\BuildNative.ps1

# Prerequisites:
# - UE 5.6 source installation (auto-detected: C:\UE\UE_5.6_Source or C:\UE\UE_5.6)
# - Visual Studio 2022 Build Tools
# - Completed Setup.bat and GenerateProjectFiles.bat (source builds only)
```

**Output:** 
- Primary: `lib\native\win-x64\UnrealLiveLink.Native.dll` (28.5 MB) - **Automatically copied by script**
- Source: `C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll`
- Additional: `.pdb`, `.exp`, `.lib` files also copied

**Build Performance:**
- **Duration:** ~2 minutes (116 seconds typical)
- **Modules:** 71 UE modules compiled
- **Incremental builds:** < 5 seconds when no changes

**Build Configuration:** See [NativeLayerDevelopment.md](NativeLayerDevelopment.md) for complete build settings

---

## Test Commands Reference

### Unit Tests
**Purpose:** Fast tests covering coordinate conversion, utils, manager logic

```powershell
# Run all unit tests
dotnet test tests/Unit.Tests/Unit.Tests.csproj

# Run with verbose output
dotnet test tests/Unit.Tests/Unit.Tests.csproj --logger "console;verbosity=detailed"

# Run specific test
dotnet test tests/Unit.Tests/Unit.Tests.csproj --filter "FullyQualifiedName~CoordinateConverter"

# Override Simio installation path
dotnet test tests/Unit.Tests/Unit.Tests.csproj -p:SimioInstallDir="D:\Simio"
```

**Expected Results:**
- All tests passing (see test output for current count)
- Known issues logged in test output (do not affect functionality)

---

### Integration Tests
**Purpose:** Validate P/Invoke interface between managed and native layers

```powershell
# Run all integration tests
dotnet test tests/Integration.Tests/Integration.Tests.csproj

# Run with verbose output
dotnet test tests/Integration.Tests/Integration.Tests.csproj --logger "console;verbosity=detailed"

# Run specific category
dotnet test tests/Integration.Tests/Integration.Tests.csproj --filter "TestCategory=Lifecycle"
dotnet test tests/Integration.Tests/Integration.Tests.csproj --filter "TestCategory=Marshaling"
dotnet test tests/Integration.Tests/Integration.Tests.csproj --filter "TestCategory=Performance"
```

**Test Categories:**
- `DLL` - DLL loading and availability
- `Lifecycle` - Initialize, Shutdown, Version, Connection
- `TransformSubjects` - Transform subject operations
- `DataSubjects` - Data-only subject operations
- `Marshaling` - Struct marshaling and binary compatibility
- `ErrorHandling` - Return codes and null parameter handling
- `Performance` - High-frequency updates @ 60Hz simulation

**Expected Results:**
- All tests passing (see test output for current count)
- Execution time: ~2-5 seconds
- Validates all 12 C API functions

**Prerequisites:**
- Native DLL must be built first: Run `.\build\BuildNative.ps1`
- DLL automatically copied to `lib\native\win-x64\` and test output directories
- **Recommended:** Use real UE DLL (28.5 MB) for full validation
- **Not recommended:** Mock DLL for integration tests (limited validation)

---

### Run All Tests
```powershell
# Run both unit and integration tests
dotnet test tests/Unit.Tests/ tests/Integration.Tests/
```

---

### Mock DLL Validation
**Purpose:** Verify P/Invoke compatibility with mock implementation

```powershell
# Quick validation with PowerShell P/Invoke
.\build\ValidateDLL.ps1

# Expected output:
# [MOCK] ULL_GetVersion
# [MOCK] ULL_Initialize(providerName='PowerShellTest')
# [MOCK] ULL_IsConnected(result=CONNECTED)
# [MOCK] ULL_Shutdown
# 🎉 POWERSHELL P/INVOKE TEST PASSED!
```

---

### Native Build Validation
**Purpose:** Verify UBT compilation and DLL output

```powershell
# Build native DLL
.\build\BuildNative.ps1

# Expected output:
# ✅ UBT build completed
# ✅ Copied DLL and PDB to: lib\native\win-x64
# 🎉 BUILD SUCCESS!
# Output File: lib\native\win-x64\UnrealLiveLink.Native.dll
# Size: ~28.5 MB

# Verify DLL exports
dumpbin /EXPORTS lib\native\win-x64\UnrealLiveLink.Native.dll

# Expected: 12 exported functions
# ULL_Initialize, ULL_Shutdown, ULL_GetVersion, ULL_IsConnected
# ULL_RegisterTransformSubject, ULL_UpdateTransformSubject, etc.
```

**Build Configuration:** See [NativeLayerDevelopment.md](NativeLayerDevelopment.md) for complete build settings

---

## Deployment

### Deploy to Simio UserExtensions
**Purpose:** Install extension for manual Simio testing

```powershell
# Requires Administrator privileges
.\build\DeployToSimio.ps1

# Custom Simio installation path
.\build\DeployToSimio.ps1 -SimioPath "D:\Simio"
```

**Target Location:** `%PROGRAMFILES%\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\`

**Deployed Files:**
- `SimioUnrealEngineLiveLinkConnector.dll` (managed)
- `UnrealLiveLink.Native.dll` (28.5 MB native DLL)
- `System.Drawing.Common.dll` (dependency)

**PATH Requirement:** 
- Deployment script automatically adds `C:\UE\UE_5.6_Source\Engine\Binaries\Win64` to system PATH
- This allows native DLL to find UE dependencies at runtime (no redistribution needed)

### Verify Deployment
1. Launch Simio
2. Check Windows Event Viewer → Application logs for extension loading errors
3. Create new model → Check if "UnrealEngineConnector" Element appears in toolbox
4. Check if 4 steps appear (CreateObject, SetPosition, TransmitValues, DestroyObject)

---

## Troubleshooting

### Build Issues

**"MSBuild not found"**
```powershell
# Re-run VS environment setup
.\build\SetupVSEnvironment.ps1

# Verify MSBuild available
msbuild -version
```

**"Simio DLLs not found"**
- Simio not installed: Tests will skip Simio-dependent tests
- Custom path: Use `-p:SimioInstallDir="path"` when running tests

**System.Drawing.Common warnings (MSB3277)**
- Expected during build - App.config binding redirect resolves at runtime
- Safe to ignore if tests pass

---

### Test Issues

**Integration Tests: "DLL not found"**
- Native DLL not built yet
- **Solution:** Build the native DLL first:
  ```powershell
  .\build\BuildNative.ps1
  ```
- Script automatically copies DLL to test output directories

**Integration Tests: "Wrong DLL being tested"**
- Mock DLL (75KB) loaded instead of real DLL (28.5 MB)
- **Verify:** Check DLL size in test output directory
- **Solution:** Ensure correct DLL copied before running tests

**"FileLoadException: System.Drawing.Common"**
- Verify `App.config` present in test output folder
- Check binding redirect points to version 6.0.0

**Tests skip Simio integration tests**
- Simio DLLs not found in test output
- Verify Simio installed or provide custom path

---

### Deployment Issues

**"Access Denied" during DeployToSimio.ps1**
- Run PowerShell as Administrator
- Close Simio before deploying

**Extension not visible in Simio**
- Check Windows Event Viewer for loading errors
- Verify DLLs copied to UserExtensions folder
- Check Simio version compatibility (15.x, 16.x)

---

### Mock DLL Issues

**ValidateDLL.ps1 fails**
- Ensure BuildMockDLL.ps1 ran successfully
- Check `lib/native/win-x64/UnrealLiveLink.Native.dll` exists
- Verify 64-bit PowerShell (not 32-bit)

---

### Native Build Issues

**"UE installation not found"**
- Verify UE 5.6 source installation at `C:\UE\UE_5.6_Source` or `C:\UE\UE_5.6`
- Run Setup.bat and GenerateProjectFiles.bat if not done (source builds)
- Binary UE installation insufficient for native builds - source required

**"UnrealBuildTool not found"**
- Ensure Setup.bat completed successfully
- Check `C:\UE\UE_5.6_Source\Engine\Binaries\DotNET\UnrealBuildTool\UnrealBuildTool.exe` exists
- Re-run GenerateProjectFiles.bat if needed

**"Compilation errors"**
- Check UBT log file path shown in output
- Verify Visual Studio 2022 Build Tools installed
- See [NativeLayerDevelopment.md](NativeLayerDevelopment.md) for troubleshooting common build configuration errors

**"Build takes too long"**
- First build: ~2 minutes (71 modules) is normal
- If build takes 10+ minutes: Configuration issue - see NativeLayerDevelopment.md
- Incremental builds: < 5 seconds when no changes

---

## Advanced Usage

### Building Everything
```powershell
# Complete build: Setup, mock DLL, managed layer, tests
.\build\SetupVSEnvironment.ps1
.\build\BuildMockDLL.ps1
.\build\BuildManaged.ps1
dotnet test tests/Unit.Tests/Unit.Tests.csproj
dotnet test tests/Integration.Tests/Integration.Tests.csproj

# Include native build (requires UE 5.6 source)
.\build\SetupVSEnvironment.ps1
.\build\BuildNative.ps1
.\build\BuildManaged.ps1
dotnet test tests/Unit.Tests/ tests/Integration.Tests/
```

### Clean Build
```powershell
# Remove all build artifacts
.\build\CleanupBuildArtifacts.ps1
.\build\CleanupBuildArtifacts.ps1 -IncludeNative

# Rebuild from scratch
.\build\BuildMockDLL.ps1
.\build\BuildManaged.ps1
```

### Custom Configurations
```powershell
# Build Debug configuration
dotnet build src/Managed/SimioUnrealEngineLiveLinkConnector.csproj --configuration Debug

# Test with custom Simio path
dotnet test tests/Unit.Tests/Unit.Tests.csproj -p:SimioInstallDir="D:\Custom\Simio"
```

---

## Mock DLL vs Real Native Build

### Mock DLL
- **Size:** 75KB
- **Build Time:** Seconds
- **Features:** API coverage, console logging, state tracking
- **Limitations:** No LiveLink integration, mock responses only
- **Use For:** Managed layer development, unit testing, quick iteration

### Native Real DLL
- **Size:** 28.5 MB
- **Build Time:** ~2 minutes (71 UE modules)
- **Features:** Full LiveLink integration, Message Bus connectivity, transform streaming
- **Requirements:** UE 5.6 source installation
- **Use For:** Integration testing, native development, production deployment

### When to Use Each

| Scenario | Mock DLL | Native Real DLL |
|----------|----------|-----------------|
| Managed layer development | ✅ Recommended | ⚠️ Slower builds |
| Unit testing | ✅ Recommended | ❌ Not needed |
| Integration testing | ❌ Limited | ✅ Required |
| Native layer development | ❌ N/A | ✅ Required |
| LiveLink testing | ❌ No functionality | ✅ Required |
| Production | ❌ Never | ✅ Always |

---

## Known Issues and Limitations

### Build System
- **Build time:** ~2 minutes (71 modules) for native DLL with UE source
- **Configuration:** See [NativeLayerDevelopment.md](NativeLayerDevelopment.md) for build configuration details

### Test Scripts
- **RunIntegrationTests.ps1:** Use `dotnet test tests/Integration.Tests/Integration.Tests.csproj` directly for more reliable execution

---

## Getting Help

### Common Resources
1. **Build errors:** Check build script output, verify prerequisites
2. **Test failures:** Review test output, check troubleshooting section
3. **Deployment issues:** Check Event Viewer, verify Administrator privileges
4. **Mock DLL problems:** Run ValidateDLL.ps1 for diagnostics
5. **Integration test issues:** Verify correct DLL copied to test output

### Debug Logging
**Mock DLL:** Logs to console (visible in test output)  
**Native Stub DLL:** Uses UE_LOG (output location TBD)  
**Managed Layer:** Uses Simio TraceInformation (visible in Simio Trace window)  
**Deployment:** Check Event Viewer → Application logs

### Documentation References
- **Architecture:** [Architecture.md](Architecture.md) - System design and component overview
- **Native Development:** [NativeLayerDevelopment.md](NativeLayerDevelopment.md) - Build configuration details
- **Managed Development:** [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md) - C# implementation guide
- **Development Status:** [DevelopmentPlan.md](DevelopmentPlan.md) - Current progress and roadmap
- **Test Structure:** [tests/README.md](../tests/README.md) - Test organization details