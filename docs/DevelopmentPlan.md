# Development Plan

**Purpose:** Current project status and phased task tracking.  
**Audience:** Project lead, contributors checking progress  
**Scope:** Temporal status information, phase-based checklists, current blockers  
**Not Included:** Implementation guides (see layer dev docs), architecture rationale (see Architecture.md)

**Last Updated:** October 17, 2025

---

## Current Phase: Phase 6.2 - Type Definitions (C API Structs)

**Status:** In Progress  
**Objective:** Define ULL_Transform and return codes matching managed layer exactly  
**Started:** October 17, 2025  
**Blockers:** None

**Current Sub-Phase Progress:**
- ✅ Phase 6.1: UBT Project Setup (COMPLETE)
- 🔄 Phase 6.2: Type Definitions (IN PROGRESS)
- 📋 Phase 6.3-6.11: Pending

---

## Phase 1: Foundation Layer ✅ COMPLETE

**Objective:** Establish core P/Invoke surface and coordinate conversion utilities.

**Prerequisites:** None

**Deliverables:**
- [x] Types.cs - P/Invoke marshaling structs (including LiveLinkConfiguration)
- [x] UnrealLiveLinkNative.cs - C API declarations  
- [x] CoordinateConverter.cs - Simio ↔ Unreal conversion
- [x] LiveLinkObjectUpdater.cs - Per-object wrapper
- [x] LiveLinkManager.cs - Singleton coordinator

**Acceptance Criteria:**
- [x] Coordinate conversion tests passing (18 tests)
- [x] Types validated via Marshal.SizeOf (5 tests)
- [x] Mock DLL integration successful
- [x] No compilation warnings

**Status:** ✅ Complete  
**Completion Date:** Pre-October 2025

---

## Phase 2: Simio Integration ✅ COMPLETE

**Objective:** Implement Simio Element and Steps for object lifecycle.

**Prerequisites:** Phase 1 complete

### Element Implementation
- [x] SimioUnrealEngineLiveLinkElementDefinition.cs - Schema with **8 properties**
- [x] SimioUnrealEngineLiveLinkElement.cs - Lifecycle management
- [x] TraceInformation added to Initialize/Shutdown

**8 Properties Implemented:**
1. SourceName (LiveLink Connection)
2. LiveLinkHost (LiveLink Connection)
3. LiveLinkPort (LiveLink Connection)
4. ConnectionTimeout (LiveLink Connection)
5. RetryAttempts (LiveLink Connection)
6. EnableLogging (Logging)
7. LogFilePath (Logging)
8. UnrealEnginePath (Unreal Engine)

**Acceptance Criteria:**
- [x] Element deploys to Simio without errors
- [x] All 8 properties visible in Simio UI
- [x] Element reads properties correctly
- [x] TraceInformation provides user feedback

### Steps Implementation
- [x] CreateObjectStep - Transform + optional properties registration
- [x] CreateObjectStepDefinition - Schema and factory
- [x] SetObjectPositionOrientationStep - High-frequency transform updates
- [x] SetObjectPositionOrientationStepDefinition - Schema and factory
- [x] TransmitValuesStep - Data subjects with repeating group
- [x] TransmitValuesStepDefinition - Schema and factory
- [x] DestroyObjectStep - Cleanup and unregister
- [x] DestroyObjectStepDefinition - Schema and factory

**Status:** ✅ Complete  
**Completion Date:** Pre-October 2025

---

## Phase 3: Enhanced Configuration & Utils ✅ COMPLETE

**Objective:** Robust property validation and configuration management.

**Prerequisites:** Phase 2 Element complete

**Deliverables:**
- [x] LiveLinkConfiguration class with validation
- [x] Utils/PropertyValidation.cs - Path/network/UE validation
- [x] Utils/PathUtils.cs - Path handling utilities
- [x] Utils/NetworkUtils.cs - Network validation
- [x] Utils/UnrealEngineDetection.cs - UE installation validation
- [x] Unit tests for Utils infrastructure (3 tests completed, 23 planned)

**Acceptance Criteria:**
- [x] Element constructor validates all 8 properties
- [x] Invalid configurations report clear, actionable errors
- [x] Utils tests passing (3/23 tests - technical debt noted)
- [x] Path normalization handles edge cases
- [x] Network validation prevents invalid endpoints

**Status:** ✅ Complete (with noted test coverage gap)  
**Completion Date:** Pre-October 2025  
**Technical Debt:** Utils test coverage at 13% (3/23) - deferred to future iteration

---

## Phase 4: Mock Native Layer ✅ COMPLETE

**Objective:** Develop mock DLL for managed layer testing.

**Prerequisites:** Phase 1 P/Invoke surface defined

**Deliverables:**
- [x] MockLiveLink.cpp - Complete API implementation (437 lines)
- [x] MockLiveLink.h - Header with API declarations
- [x] BuildMockDLL.ps1 - Build automation
- [x] ValidateDLL.ps1 - P/Invoke validation script

**Acceptance Criteria:**
- [x] Mock implements all 12 native functions
- [x] Comprehensive logging to file
- [x] Integration tests pass with mock
- [x] ValidateDLL.ps1 succeeds with PowerShell P/Invoke test
- [x] E2E validation in Simio environment successful

**Status:** ✅ Complete  
**Completion Date:** Pre-October 2025  
**Validation:** Mock DLL successfully tested in Simio (see tests/Simio.Tests/SimioUnrealLiveLink_Mock.log)

---

## Phase 5: Testing & Hardening ✅ FUNCTIONALLY COMPLETE

**Objective:** Comprehensive test coverage for all components.

**Prerequisites:** Phases 1-4 core deliverables complete

### Test Coverage Goals
**Current:** 47 tests passing  
**Target:** 80-90 tests  
**Coverage:** 59%

### Test Suites
- [x] TypesTests.cs (5 tests) - ULL_Transform validation
- [x] CoordinateConverterTests.cs (18 tests) - Coordinate conversion
- [x] LiveLinkConfigurationTests.cs (4 tests) - Configuration validation
- [x] LiveLinkManagerTests.cs (9 tests) - Singleton and initialization
- [x] LiveLinkObjectUpdaterTests.cs (4 tests) - Per-object wrapper
- [x] SimioIntegrationTests.cs (4 tests) - Element/Step instantiation
- [x] UtilsTests.cs (3 tests) - PathUtils, NetworkUtils (partial coverage)
- [ ] Enhanced UtilsTests (20 additional tests) - Deferred
- [ ] StepTraceInformationTests (integration-level) - Deferred

**Acceptance Criteria:**
- [x] Core functionality fully tested (47 tests)
- [x] No failing tests in CI
- [x] System validated in real Simio environment
- [ ] 80+ unit tests passing (59% complete)
- [ ] Code coverage >80% for Utils and Configuration (deferred)

**Status:** ✅ Functionally Complete / 🔶 Test Coverage Gap Noted  
**Completion Date:** Pre-October 2025  
**Technical Debt:** 33 additional tests planned for comprehensive coverage (deferred to future iteration)

**Rationale for Proceeding to Phase 6:**
- All core functionality working and validated in production environment
- Mock DLL proven reliable through E2E testing
- Additional test coverage represents polish rather than blocking issues

---

## Phase 6: Native Layer Implementation 🔄 IN PROGRESS

**Objective:** Implement native DLL with real Unreal Engine LiveLink integration via Message Bus.

**Prerequisites:** Managed layer complete (Phases 1-5) ✅

**Architecture Validation:** ✅ CONFIRMED VIABLE by UnrealLiveLinkCInterface reference project
- Build method: UBT-compiled standalone program (NOT plugin)
- Third-party integration: P/Invoke to UBT DLL → LiveLink Message Bus → Unreal Editor
- Performance: Proven to handle "thousands of floats @ 60Hz" (our case is 6x lighter)

**Environment Setup:** ✅ COMPLETE
- UE 5.6 source installed at C:\UE\UE_5.6_Source (20.7GB)
- BuildNative.ps1 automation working
- Visual Studio Build Tools configured

### Sub-Phase 6.1: UBT Project Setup ✅ COMPLETE

**Objective:** Create UBT Program project structure and verify compilation.

**Deliverables:**
- [x] Project structure in src/Native/UnrealLiveLink.Native/
- [x] UnrealLiveLinkNative.Target.cs - Build target configuration
- [x] UnrealLiveLinkNative.Build.cs - Module dependencies
- [x] BuildNative.ps1 - Automated build script with UE detection
- [x] WinMain entry point with UE Core integration

**Build Output:**
- [x] UnrealLiveLinkNative.exe (25MB) - Compiles successfully
- [x] Build time: 3-5 seconds (incremental)
- [x] Full UBT integration working

**Acceptance Criteria:**
- [x] Project compiles without errors
- [x] BuildNative.ps1 automation functional
- [x] Executable generated in lib/native/win-x64/
- [x] No Unreal Editor launch required for build

**Status:** ✅ Complete  
**Completion Date:** October 17, 2025

---

### Sub-Phase 6.2: Type Definitions (C API Structs) 🔄 IN PROGRESS

**Objective:** Define ULL_Transform and return codes matching managed layer exactly.

**Deliverables:**
- [ ] UnrealLiveLinkTypes.h - C-compatible type definitions
  - [ ] ULL_Transform struct (80 bytes, 10 doubles)
  - [ ] Return code constants (ULL_OK=0, ULL_ERROR=-1, etc.)
  - [ ] API version constant (ULL_API_VERSION=1)
  - [ ] Static assertions for struct size validation

**Requirements:**
- Must match MockLiveLink.h structure exactly
- Must match C# Types.cs marshaling expectations
- Return codes must be NEGATIVE for errors (managed layer expects this)
- Pure C types (no C++ classes in public API)

**Acceptance Criteria:**
- [ ] sizeof(ULL_Transform) == 80 bytes (compile-time verified)
- [ ] Header compiles standalone (no Unreal dependencies)
- [ ] Static assertions pass
- [ ] Struct layout matches C# ULL_Transform

**Status:** 🔄 In Progress  
**Started:** October 17, 2025

---

### Sub-Phase 6.3: C API Export Layer (Stub Functions) 📋 PLANNED

**Objective:** Export all 12 functions with stub implementations (logging only, no LiveLink yet).

**Deliverables:**
- [ ] UnrealLiveLinkAPI.h - Function declarations with extern "C"
- [ ] UnrealLiveLinkAPI.cpp - Stub implementations
  - [ ] All 12 functions implemented
  - [ ] UE_LOG calls for debugging
  - [ ] Parameter validation (null checks, bounds)
  - [ ] Return success codes

**Acceptance Criteria:**
- [ ] DLL exports 12 functions (verify with dumpbin)
- [ ] Function names match P/Invoke declarations
- [ ] Callable from managed layer without crashes
- [ ] Logs visible in Unreal output log
- [ ] Returns ULL_OK for Initialize, version 1 for GetVersion

**Status:** 📋 Planned

---

### Sub-Phase 6.4: LiveLinkBridge Singleton 📋 PLANNED

**Objective:** Create C++ bridge class that tracks state (no LiveLink APIs yet).

**Deliverables:**
- [ ] LiveLinkBridge.h - Class interface
- [ ] LiveLinkBridge.cpp - State tracking implementation
  - [ ] Singleton pattern
  - [ ] Subject registry (TMap)
  - [ ] Thread safety (FCriticalSection)
  - [ ] Initialize/Shutdown lifecycle

**Acceptance Criteria:**
- [ ] Singleton returns same instance
- [ ] Tracks registered subjects in maps
- [ ] Thread-safe with FScopeLock
- [ ] Shutdown clears all subjects
- [ ] Still no actual LiveLink (just state tracking)

**Status:** 📋 Planned

---

### Sub-Phase 6.5: ILiveLinkProvider Creation 📋 PLANNED

**Objective:** Create real ILiveLinkProvider and register with LiveLink Message Bus.

**Deliverables:**
- [ ] LiveLink client acquisition via IModularFeatures
- [ ] Message Bus source creation
- [ ] Basic frame submission (identity transform test)

**Acceptance Criteria:**
- [ ] LiveLink source visible in Unreal Editor LiveLink window
- [ ] Source shows "Connected" status (green indicator)
- [ ] Test subject receives frames
- [ ] No crashes or errors in log

**Status:** 📋 Planned

---

### Sub-Phase 6.6: Transform Subject Registration & Updates 📋 PLANNED

**Objective:** Implement dynamic subject registration and real-time transform streaming.

**Deliverables:**
- [ ] Subject creation on registration
- [ ] FLiveLinkFrameDataStruct construction
- [ ] Frame timestamp and metadata
- [ ] Auto-registration logic (match mock behavior)

**Acceptance Criteria:**
- [ ] Subjects appear dynamically in LiveLink window
- [ ] Subject names match Simio entity names
- [ ] Actors bound to subjects move in viewport
- [ ] Position updates at 30-60 Hz
- [ ] Multiple concurrent subjects work

**Status:** 📋 Planned

---

### Sub-Phase 6.7: Coordinate Conversion Validation 📋 PLANNED

**Objective:** Verify coordinate system handling between managed and native layers.

**Deliverables:**
- [ ] Coordinate validation tests
- [ ] CoordinateHelpers.h (if conversion needed)

**Acceptance Criteria:**
- [ ] Object at Simio origin appears at Unreal origin
- [ ] Rotations match expected orientation
- [ ] No mirroring or axis flips

**Status:** 📋 Planned

---

### Sub-Phase 6.8: String/FName Caching 📋 PLANNED

**Objective:** Optimize string conversions with FName caching.

**Deliverables:**
- [ ] TMap<FString, FName> cache in LiveLinkBridge
- [ ] GetCachedName() helper method

**Acceptance Criteria:**
- [ ] Subject name converted to FName only once
- [ ] Subsequent calls use cached FName
- [ ] No string allocations in hot path
- [ ] Cache cleared on shutdown

**Status:** 📋 Planned

---

### Sub-Phase 6.9: Properties & Data Subjects 📋 PLANNED

**Objective:** Implement property streaming for transform+property and data-only subjects.

**Deliverables:**
- [ ] RegisterObjectWithProperties implementation
- [ ] UpdateObjectWithProperties implementation
- [ ] RegisterDataSubject implementation
- [ ] UpdateDataSubject implementation
- [ ] RemoveDataSubject implementation

**Acceptance Criteria:**
- [ ] Properties visible in LiveLink window
- [ ] Blueprint can read property values via "Get LiveLink Property Value"
- [ ] Property values update in real-time
- [ ] Property count validation works
- [ ] Data-only subjects work without transforms

**Status:** 📋 Planned

---

### Sub-Phase 6.10: Error Handling & Validation 📋 PLANNED

**Objective:** Production-ready error checking and validation.

**Deliverables:**
- [ ] Comprehensive parameter validation
- [ ] UE_LOG error messages
- [ ] Null pointer guards
- [ ] Thread safety verification

**Acceptance Criteria:**
- [ ] Invalid parameters return ULL_ERROR with log message
- [ ] Null pointers handled gracefully
- [ ] Memory stable over long runs
- [ ] Thread-safe under concurrent access
- [ ] Clear error messages in log

**Status:** 📋 Planned

---

### Sub-Phase 6.11: Dependency Analysis & Deployment 📋 PLANNED

**Objective:** Identify DLL dependencies and create deployment package.

**Reference:** UnrealLiveLinkCInterface warns: "copying just the dll may not be enough. There may be other dependencies such as tbbmalloc.dll"

**Deliverables:**
- [ ] Dependencies.exe analysis of UnrealLiveLinkNative.dll
- [ ] Identification of required Unreal DLLs
- [ ] Deployment package creation
- [ ] Testing on clean machine without UE

**Acceptance Criteria:**
- [ ] All dependencies identified
- [ ] DLLs copied to deployment folder
- [ ] Works on machine without Unreal Engine installed
- [ ] Simio can load and call functions

**Status:** 📋 Planned

---

### Phase 6 Overall Acceptance Criteria

**Functional Requirements:**
- [ ] All 12 functions implemented and working
- [ ] Subjects stream from Simio to Unreal in real-time
- [ ] Transforms accurate (matches Simio positions/rotations)
- [ ] Properties stream correctly to Blueprints
- [ ] Data subjects work without transforms
- [ ] Drop-in replacement for mock DLL (no C# changes)

**Performance Requirements:**
- [ ] 100 objects @ 30 Hz sustained (per Architecture.md)
- [ ] < 5ms update latency
- [ ] Memory stable (< 100MB for 50 objects)
- [ ] No frame drops over 10-minute test

**Quality Requirements:**
- [ ] No crashes or exceptions
- [ ] Comprehensive error logging
- [ ] Parameter validation on all functions
- [ ] Thread-safe for concurrent calls

**Integration Requirements:**
- [ ] Existing Simio test models pass
- [ ] All managed layer tests still pass
- [ ] No changes required to C# code
- [ ] Compatible with UE 5.3+

**Status:** 🔄 In Progress (10% complete - Sub-Phase 6.1 done)  
**Estimated Completion:** 1.5 weeks remaining

---

## Phase 7: End-to-End Integration 📋 PLANNED

**Objective:** Validate complete system in Simio + Unreal.

**Prerequisites:** Phase 6 complete

### Integration Testing
- [ ] Deploy managed + native DLLs to Simio
- [ ] Create test Simio model exercising all 4 steps
- [ ] Configure Unreal project with LiveLink
- [ ] Verify object creation and movement in Unreal
- [ ] Test data subject property reads in Blueprints
- [ ] Performance validation with realistic model

### Documentation
- [ ] Update SimioInstructions.md with deployment steps
- [ ] Update UnrealSetup.md with LiveLink configuration
- [ ] Create example Simio models
- [ ] Create example Unreal Blueprint demonstrations

**Acceptance Criteria:**
- [ ] Full workflow works end-to-end
- [ ] Performance acceptable in real scenarios
- [ ] User documentation complete and tested
- [ ] Example projects validate all features

**Status:** 📋 Planned  
**Estimated Start:** After Phase 6 complete

---

## Phase 8: CI/CD & Packaging 📋 PLANNED

**Objective:** Automated build, test, and release process.

**Prerequisites:** Phases 1-7 complete

### CI/CD Tasks
- [ ] GitHub Actions workflow - restore, build, test
- [ ] Consider self-hosted runner for Simio-dependent tests
- [ ] Automated deployment script for UserExtensions
- [ ] Version management strategy

### Packaging Tasks
- [ ] Create deployable ZIP for Simio UserExtensions
- [ ] Installer script (optional)
- [ ] Release notes template
- [ ] Binary signing (optional)

**Acceptance Criteria:**
- [ ] CI runs on every commit
- [ ] Test results visible in GitHub
- [ ] Release artifacts generated automatically
- [ ] Deployment process documented

**Status:** 📋 Planned  
**Estimated Start:** After Phase 7 complete

---

## Known Issues & Blockers

### Current Blockers
None

### Active Risks
1. **Dependency Size** - Priority: Medium
   - Real native DLL may require 50-200MB of Unreal dependencies
   - Impact: Larger deployment package than mock
   - Mitigation: Use Dependencies.exe to minimize required DLLs

### Technical Debt
1. **Utils Test Coverage** - Priority: Low - Plan: Defer to post-Phase 6
   - Current: 3/23 tests (13% coverage)
   - Impact: Low - all Utils functions validated through integration testing
   - Action: Document as future enhancement
   
2. **StepTraceInformation Tests** - Priority: Low - Plan: Defer to Phase 7
   - Integration-level testing deferred
   - Manual validation completed in Simio environment
   - Action: Formal test suite during E2E integration phase

3. **Mock Return Code Mismatch** - Priority: Low - Plan: Document
   - Mock uses positive error codes (1, 2)
   - Real implementation will use negative codes (-1, -2, -3) per managed layer
   - Impact: None (mock behavior documented, real will be correct)

---

## Test Status Summary

**Last Updated:** October 17, 2025 (Pre-Phase 6.2)

| Test Suite | Tests | Passing | Failing | Notes |
|------------|-------|---------|---------|-------|
| TypesTests | 5 | 5 | 0 | ✅ Complete |
| CoordinateConverter | 18 | 18 | 0 | ✅ Complete |
| LiveLinkConfiguration | 4 | 4 | 0 | ✅ Complete |
| LiveLinkManager | 9 | 9 | 0 | ✅ Complete |
| LiveLinkObjectUpdater | 4 | 4 | 0 | ✅ Complete |
| SimioIntegration | 4 | 4 | 0 | ✅ Complete |
| Utils | 3 | 3 | 0 | 🔶 Partial (13%) |
| Enhanced Utils | 20 | 0 | 0 | 📋 Deferred |
| Integration (Mock) | 0 | 0 | 0 | ✅ Manual E2E Complete |
| **Total** | **47** | **47** | **0** | **59% of target coverage** |

**Validation Status:**
- ✅ All implemented tests passing (100% pass rate)
- ✅ Mock DLL validated in real Simio environment
- ✅ Complete workflow tested: Initialize → Create → Update → Destroy → Shutdown
- ✅ Multi-entity concurrent object handling verified
- ✅ System stable and production-ready for managed layer

---

## Development Milestones

| Phase | Status | Start Date | Completion Date |
|-------|--------|------------|-----------------|
| Phase 1: Foundation Layer | ✅ Complete | - | Pre-Oct 2025 |
| Phase 2: Simio Integration | ✅ Complete | - | Pre-Oct 2025 |
| Phase 3: Configuration & Utils | ✅ Complete | - | Pre-Oct 2025 |
| Phase 4: Mock Native Layer | ✅ Complete | - | Pre-Oct 2025 |
| Phase 5: Testing & Hardening | ✅ Functionally Complete | - | Pre-Oct 2025 |
| **Phase 6: Native Layer** | **🔄 In Progress (10%)** | **Oct 17, 2025** | **Est: Oct 31, 2025** |
| → Sub-Phase 6.1: UBT Setup | ✅ Complete | Oct 17 | Oct 17, 2025 |
| → Sub-Phase 6.2: Type Defs | 🔄 In Progress | Oct 17 | TBD |
| → Sub-Phases 6.3-6.11 | 📋 Planned | TBD | TBD |
| Phase 7: E2E Integration | 📋 Planned | TBD | TBD |
| Phase 8: CI/CD & Packaging | 📋 Planned | TBD | TBD |

---

## Performance Validation (Reference Project)

**Proven Viable Architecture:**
- Reference project (UnrealLiveLinkCInterface) successfully handles "thousands of floats @ 60Hz"
- Our use case: 100 objects @ 30Hz × 10 doubles = 30,000 values/sec
- **Performance margin: 6x lighter than proven reference**
- **Conclusion: Performance will NOT be a bottleneck**

**Middleware Overhead:**
- Reference project confirms one extra memory copy per frame is acceptable
- Third-party → UBT DLL → LiveLink Message Bus architecture is production-proven

---

## Build Environment Summary

**Current Setup:**
- **UE Version:** 5.6 source build
- **UE Location:** C:\UE\UE_5.6_Source (20.7GB)
- **Build Tools:** Visual Studio 2022 Build Tools
- **Build Script:** BuildNative.ps1 (working)
- **Output:** lib/native/win-x64/UnrealLiveLinkNative.exe (25MB)
- **Build Time:** 3-5 seconds (incremental)

**Mock DLL (Still Available):**
- **Location:** lib/native/win-x64/UnrealLiveLink.Native.dll (mock)
- **Build Script:** BuildMockDLL.ps1
- **Purpose:** Managed layer development and testing
- **Status:** Fully functional, 437 lines

---

## Related Documentation

- **Architecture:** [Architecture.md](Architecture.md) - System design and technical decisions
- **Implementation Guides:** 
  - [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md) - C# implementation patterns
  - [NativeLayerDevelopment.md](NativeLayerDevelopment.md) - C++ implementation details and current status
- **Build/Test:** [TestAndBuildInstructions.md](TestAndBuildInstructions.md) - Build commands and testing procedures
- **Phase 6 Detailed Plan:** [Phase 6 Native Implementation Plan](artifact) - Sub-phase breakdown with technical details