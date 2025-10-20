# Test Structure

This folder contains all tests for the SimioUnrealEngineLiveLinkConnector project, organized by scope and test level.

## Test Hierarchy

### Unit.Tests/ ✅ **Current**
**Status:** 44/44 tests passing (100%)  
**Framework:** MSTest with .NET Framework 4.8  
**Focus:** Component-level tests for individual C# classes in isolation  

See [Unit.Tests/README.md](Unit.Tests/README.md) for detailed documentation.

---

### Integration.Tests/ ✅ **Current**
**Status:** 25/25 tests passing (100%)  
**Framework:** MSTest + native UnrealLiveLink.Native.dll  
**Focus:** Managed ↔ Native layer P/Invoke validation, marshaling, error handling  

See [Integration.Tests/README.md](Integration.Tests/README.md) for detailed documentation.

---

### E2E.Tests/ 🚧 **Future**
**Status:** Planned for Sub-Phase 6.6  
**Framework:** MSTest + Unreal Engine automation  
**Focus:** Full Simio → Unreal Engine workflow validation  

See [E2E.Tests/README.md](E2E.Tests/README.md) for planned scope.

---

### Native.Tests/ 🚧 **Future**
**Status:** Planned for post-Phase 6  
**Framework:** Google Test or Catch2  
**Focus:** C++ unit tests for native layer classes  

See [Native.Tests/README.md](Native.Tests/README.md) for planned scope.

---

### Simio.Tests/
**Status:** Manual test files and Simio models  
**Contents:** Test models for manual validation in Simio environment

---

## Running Tests

### Quick Start

**All Tests (Recommended):**
```powershell
# From repository root
build\RunUnitTests.ps1
build\RunIntegrationTests.ps1
```

**Unit Tests Only:**
```powershell
dotnet test tests\Unit.Tests\Unit.Tests.csproj
```

**Integration Tests Only:**
```powershell
dotnet test tests\Integration.Tests\Integration.Tests.csproj
```

**Selective Execution:**
```powershell
# By test category
dotnet test --filter "TestCategory=Lifecycle"
dotnet test --filter "TestCategory=Marshaling"
dotnet test --filter "TestCategory=Performance"
```

See individual test project READMEs for detailed running instructions and troubleshooting.

---

## Test Results Summary

**Current Status (as of Sub-Phase 6.4):**
```
Unit Tests:        44/44 passing (100%) ✅
Integration Tests: 25/25 passing (100%) ✅
-------------------------------------------
Total:             69/69 passing (100%) ✅
Execution Time:    ~7 seconds
Native DLL:        24.04 MB (LiveLinkBridge singleton)
```

---

## CI/CD Integration

- ✅ Unit tests run on every commit
- ✅ Integration tests run on every commit  
- ✅ Integrated with build pipeline
- ✅ Fast feedback loop (~7 seconds total)

**Future:**
- Native C++ unit tests in CI
- E2E tests in nightly builds
- Performance regression tracking

---

## Prerequisites

### For Current Tests
- Visual Studio 2022 or .NET Framework 4.8 SDK
- MSTest framework (installed via NuGet)
- UnrealLiveLink.Native.dll (auto-copied to test output)

### For Future Tests
- **E2E:** Unreal Engine 5.6+, Simio, deployed connector
- **Native:** Unreal Engine 5.6 source, C++ test framework, VS 2022 C++ workload

---

## Contributing

When adding tests:
1. Follow existing test organization and naming conventions
2. Use appropriate `[TestCategory]` attributes for filtering
3. Document test purpose and expected behavior
4. Include both positive and negative test cases
5. Ensure tests are deterministic and fast
6. Add to appropriate test project based on scope
7. Update relevant README files with new coverage areas

---

## Documentation

- **Integration Tests:** [Integration.Tests/README.md](Integration.Tests/README.md) - Complete P/Invoke validation details
- **E2E Tests:** [E2E.Tests/README.md](E2E.Tests/README.md) - Future workflow testing plans
- **Native Tests:** [Native.Tests/README.md](Native.Tests/README.md) - Future C++ testing plans
- **Build Scripts:** [../build/README.md](../build/README.md) - Build and test automation
- **Architecture:** [../docs/Architecture.md](../docs/Architecture.md) - System design
- **Development Plan:** [../docs/DevelopmentPlan.md](../docs/DevelopmentPlan.md) - Current phase status
