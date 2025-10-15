# Test Structure

This folder contains all tests for the SimioUnrealEngineLiveLinkConnector project, organized by scope and test level.

## Test Hierarchy

### Unit.Tests/ ✅ **Active**
**Purpose:** Component-level unit tests for individual C# classes  
**Scope:** Tests individual classes in isolation with mocked dependencies  
**Framework:** MSTest with .NET Framework 4.8  
**Status:** Implemented - 97% pass rate (32/33 tests)

**Current Tests:**
- `CoordinateConverterTests.cs` - Mathematical conversion validation
- `LiveLinkManagerTests.cs` - Singleton management and object registry

### Integration.Tests/ 🚧 **Planned - Phase 3**
**Purpose:** Managed ↔ Native layer integration testing  
**Scope:** P/Invoke marshaling, native library integration, cross-boundary error handling  
**Framework:** MSTest + native UnrealLiveLink.Native.dll  
**Dependencies:** Compiled native library

### E2E.Tests/ 🚧 **Planned - Phase 4-5**
**Purpose:** Full workflow validation from Simio to Unreal Engine  
**Scope:** Complete system integration, performance testing, real-world scenarios  
**Framework:** MSTest + Unreal Engine automation  
**Dependencies:** Deployed Simio connector + running Unreal Engine

### Native.Tests/ 🚧 **Planned - Phase 3**
**Purpose:** C++ unit tests for the native layer  
**Scope:** C++ class testing, LiveLink API integration, memory management  
**Framework:** Google Test or Catch2  
**Dependencies:** Unreal Engine development libraries

## Running Tests

### Current (Phase 1)
```powershell
# Run unit tests
dotnet test tests/Unit.Tests/Unit.Tests.csproj

# Or via Visual Studio Test Explorer
# Build -> Run Tests
```

### Future (Phase 3+)
```powershell
# Run all managed tests
dotnet test tests/Unit.Tests/ tests/Integration.Tests/

# Run native tests (build-dependent)
# Details TBD based on chosen C++ test framework

# Run full suite including E2E
# Script TBD - will orchestrate Simio + Unreal + tests
```

## Test Development Guidelines

### Unit Tests
- **Fast execution** - No external dependencies
- **Isolated** - Mock all external services
- **Deterministic** - Same input always produces same result
- **Comprehensive** - Cover edge cases and error conditions

### Integration Tests
- **Real dependencies** - Use actual native library
- **Boundary validation** - Focus on interface contracts
- **Error handling** - Test failure modes and recovery
- **Resource cleanup** - Ensure proper disposal of native resources

### E2E Tests
- **Realistic scenarios** - Based on actual Simio usage patterns
- **Performance validation** - Measure against SLA requirements
- **Environment setup** - Document prerequisites clearly
- **Stability focus** - Designed for CI/CD reliability

## CI/CD Integration

### Current
- Unit tests run on every commit
- Integrated with build pipeline
- Test results reported in build status

### Planned
- **Phase 3:** Add integration tests to PR validation
- **Phase 4:** Add E2E tests to nightly builds
- **Phase 5:** Performance regression testing

## Coverage Goals

- **Unit Tests:** >95% line coverage for business logic
- **Integration Tests:** 100% P/Invoke interface coverage
- **E2E Tests:** All major workflow scenarios
- **Native Tests:** >90% C++ code coverage