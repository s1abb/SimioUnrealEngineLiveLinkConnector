Proposed Additional Unit Tests for Phase 4 Enhancements
Current Test Coverage Analysis:
✅ 33/37 tests passing (89% success rate)
✅ Types & Coordinate Conversion: Comprehensive coverage (18 tests)
✅ LiveLinkManager: Basic functionality covered (9 tests)
✅ P/Invoke Integration: UnrealLiveLinkNative tested (6 tests)
❌ Missing Coverage: Phase 4 enhancements (Utils, LiveLinkConfiguration, Enhanced Element)
🎯 Priority 1: Utils Infrastructure Tests
New Test File: UtilsTests.cs

[TestClass]
public class UtilsTests
{
    // PropertyValidation Tests (8 tests)
    [TestMethod] public void PropertyValidation_NormalizeFilePath_ValidPath_ShouldReturnAbsolutePath()
    [TestMethod] public void PropertyValidation_NormalizeFilePath_RelativePath_ShouldConvertToAbsolute()
    [TestMethod] public void PropertyValidation_NormalizeFilePath_InvalidPath_ShouldReturnEmpty()
    [TestMethod] public void PropertyValidation_ValidateUnrealEnginePath_ValidInstallation_ShouldReturnTrue()
    [TestMethod] public void PropertyValidation_ValidateUnrealEnginePath_InvalidPath_ShouldReturnFalse()
    [TestMethod] public void PropertyValidation_ValidateNetworkEndpoint_ValidHostPort_ShouldReturnTrue()
    [TestMethod] public void PropertyValidation_ValidateNetworkEndpoint_InvalidFormat_ShouldReturnFalse()
    [TestMethod] public void PropertyValidation_ValidatePositiveTimeout_ValidValues_ShouldPass()

    // PathUtils Tests (6 tests)  
    [TestMethod] public void PathUtils_SafePathCombine_ValidPaths_ShouldCombineCorrectly()
    [TestMethod] public void PathUtils_SafePathCombine_InvalidChars_ShouldSanitize()
    [TestMethod] public void PathUtils_EnsureDirectoryExists_NonExistentPath_ShouldCreateDirectory()
    [TestMethod] public void PathUtils_GetSafeFileName_InvalidChars_ShouldReplaceChars()
    [TestMethod] public void PathUtils_IsValidLogPath_ValidPath_ShouldReturnTrue()
    [TestMethod] public void PathUtils_IsValidLogPath_InvalidPath_ShouldReturnFalse()

    // NetworkUtils Tests (4 tests)
    [TestMethod] public void NetworkUtils_IsValidHostname_ValidNames_ShouldReturnTrue()
    [TestMethod] public void NetworkUtils_IsValidHostname_InvalidNames_ShouldReturnFalse()
    [TestMethod] public void NetworkUtils_IsValidPort_ValidPorts_ShouldReturnTrue()
    [TestMethod] public void NetworkUtils_IsValidPort_InvalidPorts_ShouldReturnFalse()

    // UnrealEngineDetection Tests (5 tests) - WITHOUT auto-detection
    [TestMethod] public void UnrealEngineDetection_ValidateInstallation_ValidPath_ShouldReturnTrue()
    [TestMethod] public void UnrealEngineDetection_ValidateInstallation_MissingExecutable_ShouldReturnFalse()
    [TestMethod] public void UnrealEngineDetection_ValidateInstallation_InvalidPath_ShouldReturnFalse()
    [TestMethod] public void UnrealEngineDetection_GetEngineVersion_ValidInstall_ShouldReturnVersion()
    [TestMethod] public void UnrealEngineDetection_GetEngineVersion_InvalidPath_ShouldReturnEmpty()
}

Priority 2: LiveLinkConfiguration Tests
New Test File: LiveLinkConfigurationTests.cs

[TestClass]
public class LiveLinkConfigurationTests
{
    // Construction and Defaults (3 tests)
    [TestMethod] public void LiveLinkConfiguration_Constructor_ShouldSetDefaults()
    [TestMethod] public void LiveLinkConfiguration_CreateValidated_ValidData_ShouldCreateConfig()
    [TestMethod] public void LiveLinkConfiguration_CreateValidated_InvalidData_ShouldReturnNull()

    // Validation Tests (8 tests)
    [TestMethod] public void LiveLinkConfiguration_Validate_AllValid_ShouldReturnNoErrors()
    [TestMethod] public void LiveLinkConfiguration_Validate_EmptySourceName_ShouldReturnError()
    [TestMethod] public void LiveLinkConfiguration_Validate_InvalidHost_ShouldReturnError()
    [TestMethod] public void LiveLinkConfiguration_Validate_InvalidPort_ShouldReturnError()
    [TestMethod] public void LiveLinkConfiguration_Validate_InvalidTimeout_ShouldReturnError()
    [TestMethod] public void LiveLinkConfiguration_Validate_InvalidRetryAttempts_ShouldReturnError()
    [TestMethod] public void LiveLinkConfiguration_Validate_InvalidLogPath_ShouldReturnError()
    [TestMethod] public void LiveLinkConfiguration_Validate_InvalidUEPath_ShouldReturnError()

    // Property Boundary Tests (4 tests)
    [TestMethod] public void LiveLinkConfiguration_Port_BoundaryValues_ShouldValidateCorrectly()
    [TestMethod] public void LiveLinkConfiguration_Timeout_BoundaryValues_ShouldValidateCorrectly()
    [TestMethod] public void LiveLinkConfiguration_RetryAttempts_BoundaryValues_ShouldValidateCorrectly()
    [TestMethod] public void LiveLinkConfiguration_ToString_ShouldProvideReadableOutput()
}

Priority 3: Enhanced LiveLinkManager Tests
Enhancement to existing LiveLinkManagerTests.cs

// New Configuration-based Tests (6 tests to add)
[TestMethod] public void LiveLinkManager_InitializeWithConfiguration_ValidConfig_ShouldSucceed()
[TestMethod] public void LiveLinkManager_InitializeWithConfiguration_InvalidConfig_ShouldThrow()
[TestMethod] public void LiveLinkManager_InitializeWithConfiguration_NullConfig_ShouldThrow()
[TestMethod] public void LiveLinkManager_Configuration_AfterInitialize_ShouldReturnConfig()
[TestMethod] public void LiveLinkManager_Initialize_BackwardCompatibility_ShouldWork()
[TestMethod] public void LiveLinkManager_Shutdown_WithConfiguration_ShouldClearConfig()

Priority 4: Element Property Reading Tests
// Priority 4: Element Property Reading Tests REMOVED FROM SCOPE
# Development Plan

## Overview
Phased, checklist-style plan for the Simio → Unreal LiveLink connector. All day/week counters removed; everything is organized by phase and actionable checklists.

## Phase 1 — Foundation (completed)
- [x] Core data types and marshaling (`UnrealIntegration/Types.cs`) implemented and unit-tested.
- [x] Coordinate conversion utilities (`CoordinateConverter.cs`) implemented and tested.
- [x] P/Invoke surface (`UnrealLiveLinkNative.cs`) declared with lifecycle and transform/data APIs.
- [x] High-level managed API (`LiveLinkManager.cs`, `LiveLinkObjectUpdater.cs`) implemented with thread-safety and lazy registration.

## Phase 2 — Simio Integration (completed)
- [x] Element definition and schema implemented (`SimioUnrealEngineLiveLinkElementDefinition.cs`).
- [x] Element implementation reads properties and initializes configuration (`SimioUnrealEngineLiveLinkElement.cs`).
- [x] Steps implemented: CreateObject, SetObjectPositionOrientation, DestroyObject, TransmitValues.
- [x] Build and deploy automation for Simio UserExtensions available (`BuildManaged.ps1`, `DeployToSimio.ps1`).

## Phase 3 — Native (mock) & Integration (mock completed)
- [x] Mock native DLL built and used for integration tests (`lib/native/win-x64/UnrealLiveLink.Native.dll`).
- [x] Mock supports the required C API surface for P/Invoke and simulates LiveLink behavior.
- [x] Integration tests with the mock validate Initialize → Create → Update → Destroy → Shutdown flows.

## Phase 4 — Essential Properties & Configuration (completed)
- [x] Element exposes essential properties: SourceName, EnableLogging (ExpressionProperty, default True), LogFilePath, UnrealEnginePath (user-specified), LiveLinkHost, LiveLinkPort, ConnectionTimeout, RetryAttempts.
- [x] `LiveLinkConfiguration` implemented with validation helpers and `CreateValidated()`.
- [x] PropertyValidation, PathUtils, NetworkUtils implemented under `src/Managed/Utils/`.
- [x] TraceInformation added with loop-protection; shutdown trace no longer overwrites Simio CSV traces.

## Phase 5 — Testing & Hardening (current)
Goal: Increase confidence in property handling and reduce regressions by adding focused unit tests and a small set of integration tests that use the mock DLL.

### Test Coverage & Priorities
- [ ] `tests/Unit.Tests/UtilsTests.cs` — PathUtils, PropertyValidation, NetworkUtils
- [ ] `tests/Unit.Tests/LiveLinkConfigurationTests.cs` — Defaults, CreateValidated, Validate edge-cases, ToString
    - [ ] ~~`tests/Unit.Tests/ElementPropertyTests.cs` — ExpressionProperty handling, defaults, validation reporting~~ (removed from scope)
- [ ] `tests/Unit.Tests/LiveLinkManagerTests.cs` — Initialize/Shutdown with valid/invalid configs, config available after init
- [ ] `tests/Unit.Tests/StepTraceInformationTests.cs` — Loop protection, message format, shutdown behavior

### Coverage Impact
- Current tests: 37 passing
- Proposed additional: ~50 focused tests
- Expected total: 80–90 after Phase 4 coverage

### Implementation Priority
- [ ] UtilsTests — highest priority, core validation infrastructure
- [ ] LiveLinkConfigurationTests — central configuration system
- [ ] Enhanced LiveLinkManagerTests — new configuration features
- [ ] StepTraceInformationTests — integration-level, completes coverage

### Expected Benefits
- Reliability: Catch edge cases in property validation
- Confidence: Full coverage of Phase 4 enhancements
- Regression Protection: Prevent future bugs in Utils and Configuration
- Documentation: Tests serve as usage examples
- Phase 5 Readiness: Solid foundation for native layer development

## Phase 6 — Native Implementation (next)
Goal: Replace the mock DLL with a real Unreal Engine LiveLink plugin exposing a minimal C API used by the managed layer via P/Invoke.

### Native Implementation Checklist
- [ ] Add UBT target & build config (`UnrealLiveLink.Native.Target.cs`, `UnrealLiveLink.Native.Build.cs`)
- [ ] Add public headers (`UnrealLiveLink.Native.h`, `UnrealLiveLink.Types.h`)
- [ ] Implement LiveLinkBridge singleton (provider creation, subject registry, name caching)
- [ ] Implement C exports (`ULL_Initialize`, `ULL_Shutdown`, `ULL_IsConnected`, `ULL_RegisterObject`, `ULL_UpdateObject`, `ULL_RemoveObject`, `ULL_RegisterDataSubject`, `ULL_UpdateDataSubject`)
- [ ] Validate marshaling (native test harness or console app)
- [ ] Build & integration test (UBT build, verify exports, run managed P/Invoke tests, replace mock DLL, run end-to-end checks)

### Risks & Mitigations
- Unreal is heavy: use mock for CI; reserve real-UE runs for developer machines or a self-hosted runner
- Marshaling mismatches: protect with explicit struct layout tests and a small native test harness

## Phase 7 — End-to-End Testing & Integration
Goal: Validate the complete managed and native layers in Simio and Unreal.

### Essential Properties / Integration Tests
- [ ] Test element creation with all properties in Simio UI
- [ ] Validate property defaults and user input handling
- [ ] Test file path normalization (relative/absolute)
- [ ] Test Unreal Engine path validation (user-specified)
- [ ] Test network endpoint validation and error reporting
- [ ] Validate mock DLL logging and error simulation for retry logic

### Complete Integration / System Tests
- [ ] Replace mock DLL locally with native plugin and validate P/Invoke
- [ ] Validate Initialize → Create → Update → Destroy → Shutdown flows with Unreal Editor LiveLink
- [ ] Verify transforms show in Unreal actors/components
- [ ] Validate data subjects and property reading in Blueprints
- [ ] Performance sanity check (many objects @ moderate frequency)
- [ ] Manual Simio model test with sample model exercising steps

## CI & Packaging
Checklist
- [ ] Add GitHub Actions: restore → build → run unit tests (skip Simio-dependent tests unless artifacts available)
- [ ] Add packaging script to produce deployable ZIP for Simio UserExtensions

## Immediate Choices
- [ ] Implement `tests/Unit.Tests/UtilsTests.cs` and run `dotnet test` locally
- [ ] Add a GitHub Actions workflow that runs the existing unit tests and reports results (skipping integration tests that need Simio)