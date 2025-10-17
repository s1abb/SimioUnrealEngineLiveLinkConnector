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
- Unreal Engine 5.1+ (for native layer development and E2E testing)

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

**Output:** `lib/native/win-x64/UnrealLiveLink.Native.dll` (mock implementation)

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
**Purpose:** Build Unreal Engine plugin (future - Phase 6)

```powershell
# Not yet implemented - see NativeLayerDevelopment.md for future steps
.\build\BuildNative.ps1
```

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
- Current: 33+ tests passing
- Target: 80+ tests passing (after Phase 5)

### Integration Tests
**Purpose:** End-to-end tests with mock DLL

```powershell
# Run integration tests (requires mock DLL built)
.\build\TestIntegration.ps1
```

### Mock DLL Validation
**Purpose:** Verify P/Invoke compatibility

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

### Test Issues

**"1 test failing (native DLL architecture)"**
- Expected: Known unrelated issue with native DLL architecture detection
- Does not affect functionality
- Current pass rate: 32/33 (97%)

**"FileLoadException: System.Drawing.Common"**
- Verify `App.config` present in test output folder
- Check binding redirect points to version 6.0.0

**Tests skip Simio integration tests**
- Simio DLLs not found in test output
- Verify Simio installed or provide custom path

### Deployment Issues

**"Access Denied" during DeployToSimio.ps1**
- Run PowerShell as Administrator
- Close Simio before deploying

**Extension not visible in Simio**
- Check Windows Event Viewer for loading errors
- Verify DLLs copied to UserExtensions folder
- Check Simio version compatibility (15.x, 16.x)

### Mock DLL Issues

**ValidateDLL.ps1 fails**
- Ensure BuildMockDLL.ps1 ran successfully
- Check `lib/native/win-x64/UnrealLiveLink.Native.dll` exists
- Verify 64-bit PowerShell (not 32-bit)

---

## Advanced Usage

### Building Everything
```powershell
# One-liner: Setup, build mock, build managed, run tests
.\build\SetupVSEnvironment.ps1; .\build\BuildMockDLL.ps1; .\build\BuildManaged.ps1; dotnet test tests/Unit.Tests/Unit.Tests.csproj
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

## Mock DLL vs Real Native DLL

### Mock DLL Features
- ✅ Complete API coverage (11 functions)
- ✅ Console logging for debugging
- ✅ State tracking (registered objects, properties)
- ✅ No Unreal Engine dependency
- ✅ Fast build (seconds)
- ❌ No actual LiveLink integration

### When to Use Mock vs Real
**Use Mock DLL:**
- Managed layer development
- Unit/integration testing
- CI/CD pipelines
- Quick iteration

**Use Real Native DLL:**
- Native layer development (Phase 6+)
- End-to-end integration testing
- Production deployment
- Performance profiling

---

## CI/CD Notes

### GitHub Actions Workflow (Planned - Phase 8)
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
      - name: Run Tests
        run: dotnet test tests/Unit.Tests/Unit.Tests.csproj
```

### Self-Hosted Runner Considerations
- Required for Simio-dependent integration tests
- Must have Simio installed
- Can run full deployment validation

---

## Test Development Guidelines

### Unit Test Best Practices
- ✅ Focus on context-independent logic
- ✅ Use real Simio DLLs when available (copied post-build)
- ❌ Do NOT create custom Simio interface implementations
- ❌ Do NOT test Simio API behavior (assume it works)

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

### Debug Logging
**Mock DLL:** Logs to console (visible in test output)  
**Managed Layer:** Uses Simio TraceInformation (visible in Simio Trace window)  
**Deployment:** Check Event Viewer → Application logs

---

## Related Documentation
- **Architecture:** [Architecture.md](Architecture.md)
- **Implementation:** [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md), [NativeLayerDevelopment.md](NativeLayerDevelopment.md)
- **Status:** [DevelopmentPlan.md](DevelopmentPlan.md)