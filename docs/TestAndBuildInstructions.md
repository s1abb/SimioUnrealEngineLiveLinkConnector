# Test & Build Instructions (KISS)

This file describes the minimal steps to build, test and deploy the connector locally. Keep it simple: use the provided scripts when possible.

Prerequisites
- Windows machine with Visual Studio (Build Tools) or full Visual Studio installed.
- .NET Framework 4.8 developer pack.
- PowerShell (used by scripts).
- Simio installed (optional for local Simio UI testing) at: `C:\Program Files\Simio LLC\Simio`.

Quick build & test (recommended)
1. Open PowerShell and change to the repo root:

```powershell
cd C:\repos\SimioUnrealEngineLiveLinkConnector
```

2. Prepare VS build tools (first time only in this session):

```powershell
.\build\SetupVSEnvironment.ps1
```

3. Build the mock native DLL (used for unit/integration tests):

```powershell
.\build\BuildMockDLL.ps1
```

4. Build the managed layer (produces the extension DLL):

```powershell
.\build\BuildManaged.ps1
# OR: dotnet build src/Managed/SimioUnrealEngineLiveLinkConnector.csproj --configuration Release
```

5. Run unit tests:

```powershell
dotnet test tests/Unit.Tests/Unit.Tests.csproj
```

6. (Optional) Deploy to Simio for manual UI testing (requires admin):

```powershell
.\build\DeployToSimio.ps1
```

Notes and gotchas
- Tests and build scripts are the canonical workflow — prefer scripts to manual steps.
- The unit tests require Simio runtime assemblies at test runtime. The test project includes a post-build target that will copy `SimioAPI.Extensions.dll` and `SimioAPI.dll` from `C:\Program Files\Simio LLC\Simio` into the test output if those files exist. To override the source path (CI or custom location), pass `-p:SimioInstallDir="D:\path\to\Simio"` to `dotnet test`.
- If you see a System.Drawing.Common version mismatch when building, tests include an `App.config` binding redirect to the build-chosen version; it is safe to ignore the MSB3277 warnings if tests run.
- Use the mock native DLL for development when Unreal Engine isn't available.


Unit Test Best Practices
- Utility unit tests should avoid requiring Simio context objects (`IExecutionContext`, `IExecutionInformation`).
- Do not create custom dummy implementations of Simio interfaces for unit tests; these interfaces require many members and are best handled by real Simio objects or integration tests.
- For context-dependent logic, use integration tests or real Simio objects as test context.
- Follow the patterns in existing test files: focus on context-independent logic and managed code.

Useful commands
- Build everything with scripts:

```powershell
.\build\SetupVSEnvironment.ps1; .\build\BuildMockDLL.ps1; .\build\BuildManaged.ps1
```

- Run tests (override Simio path):

```powershell
dotnet test tests/Unit.Tests/Unit.Tests.csproj -p:SimioInstallDir="D:\Simio"
```

That's it — simple build/test/deploy steps.

- **Access Denied**: Run PowerShell as Administrator manually
- **Simio Not Found**: Verify Simio installed at `C:\Program Files\Simio LLC\Simio\`

#### Test Failures  
- **Expected**: 1 test fails (native DLL architecture issue - unrelated to Simio)
- **Current Status**: 32/33 tests passing (97% success rate)
- **New Tests**: Simio integration tests may fail outside Simio environment (expected)

#### Missing Dependencies
```powershell
# Restore NuGet packages
dotnet restore SimioUnrealEngineLiveLinkConnector.sln
```

### System.Drawing.Common Warnings
**Expected warnings** during build about version conflicts - these are resolved automatically by selecting version 6.0.0 for .NET Framework 4.8 compatibility.

### Getting Help
1. Check build output for specific error messages
2. Verify prerequisites are installed  
3. For Simio validation issues, check Windows Event Viewer for extension loading errors
4. For mock DLL issues, check console output for `[MOCK]` log messages
5. Consult phase-specific documentation in `docs/` folder

---

## Mock DLL Testing Guide ✅ **NEW**

### Quick Validation
```powershell
# 1. Build mock DLL
.\build\BuildMockDLL.ps1

# 2. Test basic P/Invoke (should show [MOCK] output)
.\build\ValidateDLL.ps1

# Expected output:
# [MOCK] ULL_GetVersion
# [MOCK] ULL_Initialize(providerName='PowerShellTest')  
# [MOCK] ULL_IsConnected(result=CONNECTED)
# [MOCK] ULL_Shutdown
# 🎉 POWERSHELL P/INVOKE TEST PASSED!
```

### Mock DLL Features
- **Complete API Coverage**: All 11 functions from UnrealLiveLinkNative.cs
- **Function Logging**: Every call logged with parameters for debugging
- **State Management**: Tracks registered objects and properties
- **Error Simulation**: Validates error handling paths
- **No Dependencies**: No Unreal Engine required for testing

### Mock vs Real Implementation
| Feature | Mock DLL | Real Unreal DLL |
|---------|----------|-----------------|
| **P/Invoke Testing** | ✅ Available | 🚧 Phase 3 |
| **Simio Integration** | ✅ Working | 🚧 Phase 3 |
| **Development Speed** | ✅ Instant | ⏳ Requires UE setup |
| **Function Coverage** | ✅ 100% | 🚧 Phase 3 |
| **Live Debugging** | ✅ Console logs | 🚧 UE logs |
| **Actual LiveLink** | ❌ Mock responses | ✅ Real Unreal |

### When to Use Mock vs Real
- **Use Mock** for: Simio integration testing, P/Invoke validation, rapid development
- **Use Real** for: Actual Unreal Engine integration, production deployment, performance testing

The mock implementation provides a complete development and testing environment while the real Unreal Engine implementation is being developed.
