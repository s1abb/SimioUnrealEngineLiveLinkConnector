# Development Plan

**Purpose:** Current project status and phased task tracking.  
**Audience:** Project lead, contributors checking progress  
**Scope:** Temporal status information, phase-based checklists, current blockers  
**Not Included:** Implementation guides (see layer dev docs), architecture rationale (see Architecture.md)

---

## Current Phase: [Phase Name]

**Status:** [In Progress / Blocked / Complete]  
**Objective:** [One sentence goal]  
**Started:** [Date]  
**Blockers:** [Any blocking issues]

---

## Phase 1: Foundation Layer

**Objective:** Establish core P/Invoke surface and coordinate conversion utilities.

**Prerequisites:** None

**Deliverables:**
- [ ] Types.cs - P/Invoke marshaling structs
- [ ] UnrealLiveLinkNative.cs - C API declarations  
- [ ] CoordinateConverter.cs - Simio ↔ Unreal conversion
- [ ] LiveLinkObjectUpdater.cs - Per-object wrapper
- [ ] LiveLinkManager.cs - Singleton coordinator

**Acceptance Criteria:**
- [ ] Coordinate conversion tests passing (target: 18 tests)
- [ ] Types validated via Marshal.SizeOf
- [ ] Mock DLL integration successful
- [ ] No compilation warnings

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Phase 2: Simio Integration

**Objective:** Implement Simio Element and Steps for object lifecycle.

**Prerequisites:** Phase 1 complete

### Element Implementation
- [ ] SimioUnrealEngineLiveLinkElementDefinition.cs - Schema with 7 properties
- [ ] SimioUnrealEngineLiveLinkElement.cs - Lifecycle management
- [ ] TraceInformation added to Initialize/Shutdown

**Acceptance Criteria:**
- [ ] Element deploys to Simio without errors
- [ ] All 7 properties visible in Simio UI
- [ ] Element reads properties correctly
- [ ] TraceInformation provides user feedback

### Steps Implementation
- [ ] CreateObjectStep - Transform + optional properties registration
- [ ] CreateObjectStepDefinition - Schema and factory
- [ ] SetObjectPositionOrientationStep - High-frequency transform updates
- [ ] SetObjectPositionOrientationStepDefinition - Schema and factory
- [ ] TransmitValuesStep - Data subjects with repeating group
- [ ] TransmitValuesStepDefinition - Schema and factory
- [ ] DestroyObjectStep - Cleanup and unregister
- [ ] DestroyObjectStepDefinition - Schema and factory

**Acceptance Criteria:**
- [ ] All 4 step types compile and execute
- [ ] Steps visible in Simio Processes tab
- [ ] TraceInformation provides appropriate feedback
- [ ] High-frequency steps have loop protection (1 sec throttle)
- [ ] Integration tests pass with mock DLL

**Current Gaps:** [List any incomplete items]

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Phase 3: Enhanced Configuration & Utils

**Objective:** Robust property validation and configuration management.

**Prerequisites:** Phase 2 Element complete

**Deliverables:**
- [ ] LiveLinkConfiguration class with validation
- [ ] Utils/PropertyValidation.cs - Path/network/UE validation
- [ ] Utils/PathUtils.cs - Path handling utilities
- [ ] Utils/NetworkUtils.cs - Network validation
- [ ] Utils/UnrealEngineDetection.cs - UE installation validation
- [ ] Unit tests for Utils infrastructure (target: 23 tests)

**Acceptance Criteria:**
- [ ] Element constructor validates all 7 properties
- [ ] Invalid configurations report clear, actionable errors
- [ ] Utils tests passing (23 tests)
- [ ] Path normalization handles edge cases
- [ ] Network validation prevents invalid endpoints

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Phase 4: Mock Native Layer

**Objective:** Develop mock DLL for managed layer testing.

**Prerequisites:** Phase 1 P/Invoke surface defined

**Deliverables:**
- [ ] MockLiveLink.cpp - Complete API implementation
- [ ] MockLiveLink.h - Header with logging macros
- [ ] BuildMockDLL.ps1 - Build automation
- [ ] ValidateDLL.ps1 - P/Invoke validation script

**Acceptance Criteria:**
- [ ] Mock implements all 11 native functions
- [ ] Console logging for debugging
- [ ] Integration tests pass with mock
- [ ] ValidateDLL.ps1 succeeds with PowerShell P/Invoke test

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Phase 5: Testing & Hardening

**Objective:** Comprehensive test coverage for all components.

**Prerequisites:** Phases 1-4 core deliverables complete

### Test Coverage Goals
**Current:** [X tests passing]  
**Target:** 80-90 tests

### Test Suites
- [ ] UtilsTests.cs (23 tests) - PropertyValidation, PathUtils, NetworkUtils
- [ ] LiveLinkConfigurationTests.cs (15 tests) - Validation, defaults, edge cases
- [ ] Enhanced LiveLinkManagerTests (6 tests) - Configuration-based initialization
- [ ] StepTraceInformationTests (integration-level) - Loop protection, message format

**Acceptance Criteria:**
- [ ] 80+ unit tests passing
- [ ] No failing tests in CI
- [ ] Code coverage >80% for Utils and Configuration
- [ ] Integration tests validate full workflows

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Phase 6: Native Layer Implementation

**Objective:** Replace mock DLL with real Unreal Engine LiveLink plugin.

**Prerequisites:** Managed layer complete (Phases 1-5)

### Native Development Tasks
- [ ] Unreal plugin structure (descriptor, Build.cs, Target.cs)
- [ ] LiveLinkBridge singleton implementation
- [ ] ILiveLinkProvider integration
- [ ] C API export layer (11 functions)
- [ ] Coordinate conversion helpers
- [ ] String/FName conversion with caching
- [ ] Error handling and logging

### Testing Tasks
- [ ] Native test harness (C++ console app)
- [ ] Struct marshaling validation
- [ ] LiveLink subjects visible in Unreal Editor
- [ ] Integration with managed layer
- [ ] Performance profiling (100 objects @ 30Hz)

**Acceptance Criteria:**
- [ ] All 11 functions exported and callable
- [ ] dumpbin /EXPORTS shows correct signatures
- [ ] Subjects appear in Unreal LiveLink window
- [ ] Transform updates visible in Unreal actors
- [ ] Data subjects readable in Blueprints
- [ ] Performance targets met (<5ms per update)
- [ ] No memory leaks under stress testing

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Phase 7: End-to-End Integration

**Objective:** Validate complete system in Simio + Unreal.

**Prerequisites:** Phases 1-6 complete

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

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Phase 8: CI/CD & Packaging

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

**Status:** [Not Started / In Progress / Complete]  
**Completion Date:** [Date or TBD]

---

## Known Issues & Blockers

### Current Blockers
1. [Issue description] - **Impact:** [High/Medium/Low] - **Status:** [In Progress/Blocked/Resolved]

### Technical Debt
1. [Debt item] - **Priority:** [High/Medium/Low] - **Plan:** [Defer/Address/Monitor]

---

## Test Status Summary

**Last Updated:** [Date]

| Test Suite | Tests | Passing | Failing | Notes |
|------------|-------|---------|---------|-------|
| CoordinateConverter | 18 | 18 | 0 | ✅ Complete |
| LiveLinkManager | 9 | 9 | 0 | ✅ Complete |
| Types | 6 | 6 | 0 | ✅ Complete |
| Utils | 23 | TBD | TBD | 📋 Planned |
| Configuration | 15 | TBD | TBD | 📋 Planned |
| Integration (Mock) | 10 | TBD | TBD | 🚧 In Progress |
| **Total** | **81** | **33** | **0** | **41% coverage** |

---

## Related Documentation
- **Architecture:** [Architecture.md](Architecture.md)
- **Implementation guides:** [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md), [NativeLayerDevelopment.md](NativeLayerDevelopment.md)
- **Build/test:** [TestAndBuildInstructions.md](TestAndBuildInstructions.md)