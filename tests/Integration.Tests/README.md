# Integration Tests

## Purpose
Tests that validate the interaction between the managed C# layer and the native C++ layer through P/Invoke.

## Status
✅ **Active** - Implemented for Sub-Phase 6.3 validation

## Scope
- **P/Invoke marshaling** - Verify data structures are correctly marshaled between managed and native code
- **Native library integration** - Test that UnrealLiveLink.Native.dll functions work correctly
- **Error handling** - Validate native error codes are properly handled by managed wrappers
- **Memory management** - Ensure no memory leaks in P/Invoke boundaries
- **Lifecycle operations** - Test Initialize, Shutdown, GetVersion, IsConnected
- **Subject operations** - Test Register, Update, Remove for both transform and data subjects
- **Struct marshaling** - Verify ULL_Transform binary compatibility (80 bytes)

## Requirements
- Requires compiled UnrealLiveLink.Native.dll in lib/native/win-x64/
- Uses actual native library (not mocked)
- Native layer must be built before running tests (see build/BuildNative.ps1)

## Test Framework
- MSTest with .NET Framework 4.8
- Project reference to main SimioUnrealEngineLiveLinkConnector project
- Native library automatically copied to test output directory

## Running Tests

### Command Line
```powershell
# Build native layer first (if not already built)
.\build\BuildNative.ps1

# Run integration tests (builds and runs tests)
.\build\RunIntegrationTests.ps1

# Run without rebuilding
.\build\RunIntegrationTests.ps1 -NoBuild

# Run with Release configuration
.\build\RunIntegrationTests.ps1 -Configuration Release

# Verbose output
.\build\RunIntegrationTests.ps1 -Verbose
```

### Visual Studio
1. Build the native layer: Run `build\BuildNative.ps1` from PowerShell
2. Open solution: `SimioUnrealEngineLiveLinkConnector.sln`
3. Build solution: `Build > Build Solution` (Ctrl+Shift+B)
4. Open Test Explorer: `Test > Test Explorer`
5. Run tests: `Run All` or select specific tests

### Manual dotnet CLI
```powershell
cd tests\Integration.Tests
dotnet build
dotnet test
```

## Test Categories

Tests are organized by category for selective execution:

- `Integration` - All integration tests
- `DLL` - DLL loading and availability
- `Lifecycle` - Initialize, Shutdown, Version, Connection
- `TransformSubjects` - 3D object operations
- `DataSubjects` - Data-only subject operations
- `Marshaling` - Struct size and layout validation
- `ErrorHandling` - Null parameters, invalid states
- `Performance` - High-frequency update tests

### Run Specific Categories
```powershell
# Run only lifecycle tests
dotnet test --filter "TestCategory=Lifecycle"

# Run only marshaling tests
dotnet test --filter "TestCategory=Marshaling"

# Run lifecycle and error handling
dotnet test --filter "TestCategory=Lifecycle|TestCategory=ErrorHandling"
```

## Test Results

### Expected Behavior (Sub-Phase 6.3 - Stub Functions)
- ✅ All tests should pass without exceptions
- ✅ DLL loads successfully
- ✅ Initialize returns ULL_OK (0)
- ✅ GetVersion returns 1 (API version)
- ✅ IsConnected returns ULL_NOT_CONNECTED (-2) - no LiveLink integration yet
- ✅ Subject operations complete without errors
- ✅ Struct marshaling correct (80 bytes)
- ✅ High-frequency updates work without blocking

### Known Limitations (Sub-Phase 6.3)
- ❌ IsConnected returns NOT_CONNECTED (no actual LiveLink yet - expected)
- ❌ Subjects don't appear in Unreal Editor (no LiveLink integration - expected)
- ❌ Log file location not yet validated (may require Unreal Engine running)

## Test Implementation

### Test File
`NativeIntegrationTests.cs` - Main integration test file with 27 tests

### Test Structure
```csharp
[TestClass]
public class NativeIntegrationTests
{
    [ClassInitialize]  // Verify DLL exists before running tests
    [ClassCleanup]     // Cleanup: call Shutdown if initialized
    [TestInitialize]   // Per-test setup
    [TestCleanup]      // Per-test cleanup
}
```

### Test Sections
1. **DLL Loading & Availability** (2 tests)
2. **Lifecycle Functions** (8 tests)
3. **Transform Subject Operations** (6 tests)
4. **Data Subject Operations** (3 tests)
5. **Struct Marshaling** (3 tests)
6. **Error Handling** (2 tests)
7. **High-Frequency Updates** (1 test)

**Total: 25 tests**

## Development Notes

### State Management
The native layer (Sub-Phase 6.3) maintains global state:
- Tests must handle "already initialized" scenarios
- Some tests explicitly call Shutdown to reset state
- Test isolation is maintained through careful setup/teardown

### Future Enhancements (Sub-Phase 6.5+)
Once LiveLink integration is implemented:
- Add tests for actual Unreal Engine connection
- Verify subjects appear in LiveLink window
- Test property values are transmitted correctly
- Add performance benchmarks (latency, throughput)
- Test multi-threaded scenarios

## Troubleshooting

### DLL Not Found
```
Error: Native DLL not found at: lib\native\win-x64\UnrealLiveLink.Native.dll
Solution: Run .\build\BuildNative.ps1 to build the native layer
```

### Build Errors
```
Error: Project reference issues
Solution: Restore NuGet packages with `dotnet restore`
```

### Test Failures
```
Error: Tests fail after native code changes
Solution: Rebuild native layer and test project:
  .\build\BuildNative.ps1
  dotnet build tests\Integration.Tests\Integration.Tests.csproj
```

## CI/CD Integration

### Pull Request Validation
```yaml
steps:
  - name: Build Native Layer
    run: .\build\BuildNative.ps1
  
  - name: Run Integration Tests
    run: .\build\RunIntegrationTests.ps1 -Configuration Release
```

### Reporting
Test results are automatically reported in:
- Console output (detailed logging)
- Test result files (TRX format)
- CI/CD pipeline status

## Implementation History

- **October 17, 2025** - Initial implementation for Sub-Phase 6.3 validation
- Created Integration.Tests project with MSTest framework
- Implemented 25 integration tests covering all P/Invoke functions
- Added build script (RunIntegrationTests.ps1) for automation
- Verified all tests pass with stub function implementation