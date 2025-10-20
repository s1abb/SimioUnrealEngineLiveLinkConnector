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
- Unreal Engine 5.6 Source (for native layer development) - Install location: `C:\UE\UE_5.6_Source\`
  - **Note:** Binary installation at `C:\UE\UE_5.6` insufficient for native builds
  - Source build required for UnrealBuildTool compilation

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

### Native Layer (Real Implementation) ✅ AVAILABLE
**Purpose:** Build Unreal Engine native DLL with stub functions

```powershell
# Build native UE DLL (WORKING - requires UE 5.6 source)
.\build\BuildNative.ps1

# Prerequisites:
# - UE 5.6 source installation at C:\UE\UE_5.6_Source
# - Visual Studio 2022 Build Tools
# - Completed Setup.bat and GenerateProjectFiles.bat
```

**Output:** `C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll` (25MB DLL)

**Known Issue:** BuildNative.ps1 currently expects `.exe` output but Target.cs now produces `.dll`
- **Workaround:** Manually copy DLL from UE build output to `lib\native\win-x64\`
- **Command:** `Copy-Item "C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll" "lib\native\win-x64\"`

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
- 32/33 tests passing (97% pass rate)
- 1 known failure (native DLL architecture detection - does not affect functionality)

---

### Integration Tests ✅ AVAILABLE
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
- `DLL` - DLL loading and availability (2 tests)
- `Lifecycle` - Initialize, Shutdown, Version, Connection (8 tests)
- `TransformSubjects` - Transform subject operations (6 tests)
- `DataSubjects` - Data-only subject operations (3 tests)
- `Marshaling` - Struct marshaling and binary compatibility (3 tests)
- `ErrorHandling` - Return codes and null parameter handling (2 tests)
- `Performance` - High-frequency updates @ 60Hz simulation (1 test)

**Expected Results:**
- 25/25 tests passing (100% pass rate)
- Execution time: ~2-5 seconds
- Validates all 12 C API functions

**Key Validations:**
- ✅ Binary compatibility (80-byte ULL_Transform struct)
- ✅ All 12 C API functions callable from C#
- ✅ Proper error codes (ULL_OK=0, ULL_ERROR=-1, etc.)
- ✅ Null parameter safety
- ✅ High-frequency update stability

**Prerequisites:**
- Native DLL must be built and copied to test output directory
- For stub DLL testing: Use UnrealLiveLinkNative.dll from UE build
- For mock DLL testing: Use mock DLL (not recommended for integration tests)

---

### Run All Tests
```powershell
# Run both unit and integration tests
dotnet test tests/Unit.Tests/ tests/Integration.Tests/

# Expected: 57/58 tests passing (98% overall)
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

### Native Build Validation ✅ AVAILABLE
**Purpose:** Verify UBT compilation and DLL output

```powershell
# Build native DLL
.\build\BuildNative.ps1

# Expected output:
# ✅ UBT build completed
# Output: C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll
# Note: Manual copy required to lib\native\win-x64\

# Verify DLL exports
dumpbin /EXPORTS C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll

# Expected: 12 exported functions
# ULL_Initialize, ULL_Shutdown, ULL_GetVersion, ULL_IsConnected
# ULL_RegisterObject, ULL_UpdateObject, etc.
```

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
- `UnrealLiveLink.Native.dll` (mock or real)
- `System.Drawing.Common.dll` (dependency)

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

**Unit Tests: "1 test failing (native DLL architecture)"**
- Expected: Known unrelated issue with native DLL architecture detection
- Does not affect functionality
- Current pass rate: 32/33 (97%)

**Integration Tests: "DLL not found"**
- Native DLL not copied to test output directory
- **Solution:** Manually copy from UE build output:
  ```powershell
  Copy-Item "C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll" "tests\Integration.Tests\bin\Debug\net48\"
  ```

**Integration Tests: "Wrong DLL being tested (console output visible)"**
- Mock DLL (75KB) loaded instead of stub DLL (25MB)
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

### Native Build Issues ✅ TROUBLESHOOTING AVAILABLE

**"UE installation not found"**
- Verify UE 5.6 source installation at `C:\UE\UE_5.6_Source`
- Run Setup.bat and GenerateProjectFiles.bat if not done
- Binary UE installation insufficient - source required

**"UnrealBuildTool not found"**
- Ensure Setup.bat completed successfully
- Check `C:\UE\UE_5.6_Source\Engine\Binaries\DotNET\UnrealBuildTool\UnrealBuildTool.exe` exists
- Re-run GenerateProjectFiles.bat if needed

**"Compilation errors"**
- Check UBT log file path shown in output
- Verify Visual Studio 2022 Build Tools installed
- Ensure proper UE project structure (Target.cs, Build.cs files)

**"Build succeeds but no DLL in lib folder"**
- Known issue: BuildNative.ps1 expects .exe output but Target.cs produces .dll
- **Solution:** Manually copy DLL:
  ```powershell
  Copy-Item "C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll" "lib\native\win-x64\"
  ```
- Build script update needed to handle DLL output

**"Integration tests fail with stub DLL"**
- Expected: IsConnected returns ULL_NOT_CONNECTED (-2)
- Stub functions log but don't implement LiveLink yet
- Subjects won't appear in Unreal Editor
- This is correct behavior for current implementation phase

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
.\build\BuildMockDLL.ps1
.\build\BuildNative.ps1
# Manual copy required:
Copy-Item "C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll" "lib\native\win-x64\"
.\build\BuildManaged.ps1
dotnet test tests/Unit.Tests/ tests/Integration.Tests/
```

### Clean Build
```powershell
# Remove all build artifacts
.\build\CleanupBuildArtifacts.ps1

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

### Mock DLL Features
- ✅ Complete API coverage (12 functions)
- ✅ Console logging for debugging
- ✅ State tracking (registered objects, properties)
- ✅ No Unreal Engine dependency
- ✅ Fast build (seconds)
- ✅ 75KB size
- ❌ No actual LiveLink integration
- ❌ Returns mock success for IsConnected

**Use Cases:**
- Managed layer development
- Unit testing (coordinate conversion, manager logic)
- CI/CD pipelines
- Quick iteration without UE dependencies

---

### Native Stub DLL Features ✅ AVAILABLE
- ✅ UBT compilation with UE 5.6 source
- ✅ 25MB native DLL with UE Core integration
- ✅ All 12 C API functions exported
- ✅ Proper UE logging (UE_LOG)
- ✅ Parameter validation (null checks, bounds checks)
- ✅ Correct return codes (negative for errors)
- ✅ 3-5 second incremental builds
- ✅ Binary compatible with C# P/Invoke
- 🔄 LiveLink integration (in development)
- ❌ Subjects don't appear in Unreal yet (stub phase)

**Use Cases:**
- Integration testing (P/Invoke validation)
- Native layer development
- LiveLink protocol implementation (future)
- Production deployment (when complete)

---

### When to Use Each

| Scenario | Mock DLL | Native Stub DLL |
|----------|----------|-----------------|
| Managed layer development | ✅ Recommended | ❌ Overkill |
| Unit testing | ✅ Recommended | ❌ Not needed |
| Integration testing | ❌ Limited value | ✅ Recommended |
| Native layer development | ❌ Not applicable | ✅ Required |
| CI/CD (current) | ✅ Fast feedback | 🔄 Future |
| Production | ❌ Never | ✅ When complete |

---

## Known Issues and Limitations

### Build System
- **Issue:** BuildNative.ps1 expects `.exe` output but Target.cs produces `.dll`
- **Impact:** Manual copy required after native build
- **Workaround:** Copy from `C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll` to `lib\native\win-x64\`
- **Fix Needed:** Update BuildNative.ps1 to detect and copy DLL

### Test Scripts
- **Issue:** RunIntegrationTests.ps1 has PowerShell escaping error
- **Impact:** Cannot run via script
- **Workaround:** Use `dotnet test tests/Integration.Tests/Integration.Tests.csproj` directly

### Current Stub Limitations (Expected)
- ✅ All P/Invoke functions callable
- ✅ Parameter validation works
- ✅ Return codes correct
- ❌ IsConnected returns ULL_NOT_CONNECTED (no actual connection yet)
- ❌ Subjects don't appear in Unreal Editor (stub phase)
- ❌ No LiveLink Message Bus integration (in development)
- ❌ Properties not transmitted (future implementation)

These limitations are expected for the current implementation phase.

---

## CI/CD Notes

### GitHub Actions Workflow (Planned)
```yaml
# .github/workflows/build-test.yml
name: Build and Test
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
      - name: Build Mock DLL
        run: .\build\BuildMockDLL.ps1
      - name: Build Managed
        run: .\build\BuildManaged.ps1
      - name: Run Unit Tests
        run: dotnet test tests/Unit.Tests/Unit.Tests.csproj
      - name: Run Integration Tests
        run: dotnet test tests/Integration.Tests/Integration.Tests.csproj
```

### Self-Hosted Runner Considerations
- Required for Simio-dependent integration tests
- Must have Simio installed
- Can run full deployment validation
- Native builds require UE 5.6 source installation

---

## Test Development Guidelines

### Unit Test Best Practices
- ✅ Focus on context-independent logic
- ✅ Use real Simio DLLs when available (copied post-build)
- ❌ Do NOT create custom Simio interface implementations
- ❌ Do NOT test Simio API behavior (assume it works)
- ✅ Mock external dependencies
- ✅ Fast execution (< 100ms per test)

### Integration Test Best Practices
- ✅ Test P/Invoke boundary contracts
- ✅ Validate struct marshaling and sizes
- ✅ Test error handling (null parameters, invalid values)
- ✅ Use real native DLL (not mock)
- ✅ Test high-frequency scenarios
- ✅ Verify return codes match expectations
- ❌ Do NOT test internal native implementation details

### Example Test Structure
```csharp
[TestClass]
public class CoordinateConverterTests
{
    [TestMethod]
    public void SimioPositionToUnreal_ZeroPosition_ReturnsOrigin()
    {
        // Arrange
        double simioX = 0, simioY = 0, simioZ = 0;
        
        // Act
        var (unrealX, unrealY, unrealZ) = CoordinateConverter.SimioPositionToUnreal(simioX, simioY, simioZ);
        
        // Assert
        Assert.AreEqual(0, unrealX);
        Assert.AreEqual(0, unrealY);
        Assert.AreEqual(0, unrealZ);
    }
}
```

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
- **Test Status:** [tests/README.md](tests/README.md)
- **Integration Test Results:** [IntegrationTestingComplete-Summary.md](IntegrationTestingComplete-Summary.md)
- **Native Development:** [NativeLayerDevelopment.md](NativeLayerDevelopment.md)
- **Development Status:** [Phase6DevelopmentPlan.md](Phase6DevelopmentPlan.md)

---

## Related Documentation
- **Architecture:** [Architecture.md](Architecture.md)
- **Implementation:** [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md), [NativeLayerDevelopment.md](NativeLayerDevelopment.md)
- **Development Status:** [Phase6DevelopmentPlan.md](Phase6DevelopmentPlan.md)
- **Test Structure:** [tests/README.md](tests/README.md)