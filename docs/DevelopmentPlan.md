# Phase 6: Native Layer Implementation Plan

**Status:** 🔄 IN PROGRESS  
**Progress:** 3/11 sub-phases complete (27%)

---

## Executive Summary

**Objective:** Implement real native DLL with Unreal Engine LiveLink Message Bus integration, replacing the mock DLL used for managed layer development.

**Current Status:**
- ✅ **Sub-Phases 6.1-6.3 COMPLETE:** UBT setup, type definitions, and C API export layer
- 🔄 **Sub-Phase 6.4 NEXT:** LiveLinkBridge singleton state management
- 📋 **Sub-Phases 6.5-6.11 PLANNED:** LiveLink integration, optimization, and deployment

**Key Achievements:**
- UE 5.6 source build environment established (20.7GB)
- Working BuildNative.ps1 automation (3-5 second builds)
- DLL output with 12 exported stub functions (75KB)
- Binary-compatible type definitions verified
- Full parameter validation and logging implemented

**Architecture Validation:** Approach confirmed viable based on production reference project (UnrealLiveLinkCInterface) handling "thousands of floats @ 60Hz" - our use case is 6x lighter (30,000 values/sec vs 180,000 values/sec).

---

## Goal & Scope

**Goal:** Implement the 12-function C API as a standalone DLL using Unreal Build Tool, providing real LiveLink integration.

**Scope:** ONLY implement what's specified in Architecture.md and NativeLayerDevelopment.md. No additions.

**Architecture:** Standalone C++ DLL built with UBT that exports C functions and uses Unreal's LiveLink APIs internally.

---

## Completed Sub-Phases

### ✅ Sub-Phase 6.1: UBT Project Setup
**Status:** COMPLETE

**Deliverables:**
- UE 5.6 source installation (C:\UE\UE_5.6_Source)
- Complete UBT project structure
- BuildNative.ps1 automation script
- Initial executable output (25MB UnrealLiveLinkNative.exe)
- Proper UE Program target configuration

**Key Files:**
- `UnrealLiveLinkNative.Target.cs` - UE Program target config
- `UnrealLiveLinkNative.Build.cs` - Module dependencies
- `build\BuildNative.ps1` - Automated build script

**Completion Report:** See Sub-Phase6.1-CompletionReport.md

---

### ✅ Sub-Phase 6.2: Type Definitions
**Status:** COMPLETE

**Deliverables:**
- `UnrealLiveLink.Types.h` - Complete type definitions
- ULL_Transform struct (80 bytes, verified)
- Return code constants (ULL_OK=0, errors negative)
- API version constant (ULL_API_VERSION=1)
- Compile-time validation with static_assert
- TypesValidation.cpp for additional checks

**Key Validations:**
- sizeof(ULL_Transform) == 80 bytes ✅
- Field offsets verified (position @0, rotation @24, scale @56)
- Pure C compatibility maintained
- Matches C# marshaling exactly

**Completion Report:** See Sub-Phase6.2-CompletionReport.md

---

### ✅ Sub-Phase 6.3: C API Export Layer (Stub Functions)
**Status:** COMPLETE

**Deliverables:**
- `UnrealLiveLinkAPI.h` - All 12 function declarations
- `UnrealLiveLinkAPI.cpp` - Stub implementations with logging
- DLL output configuration (bShouldCompileAsDLL = true)
- Complete parameter validation
- Comprehensive UE_LOG integration

**Build Results:**
- Output: UnrealLiveLink.Native.dll (75,264 bytes)
- Exports: 12/12 functions verified with dumpbin
- Calling convention: __cdecl (extern "C")
- Build time: ~5 seconds incremental

**Functions Implemented:**
- Lifecycle (4): Initialize, Shutdown, GetVersion, IsConnected
- Transform Subjects (5): RegisterObject, RegisterObjectWithProperties, UpdateObject, UpdateObjectWithProperties, RemoveObject
- Data Subjects (3): RegisterDataSubject, UpdateDataSubject, RemoveDataSubject

**Key Features:**
- Null checks for all pointer parameters
- Bounds checks for array counts
- Update throttling (log every 60th call)
- Proper error code returns
- UTF8 to TCHAR conversion for logging

**Completion Report:** See Sub-Phase6.3-CompletionReport.md

---

## Planned Sub-Phases

### 📋 Sub-Phase 6.4: LiveLinkBridge Class (No LiveLink Integration)
**Status:** NEXT - Ready to implement

**Objective:** Create C++ bridge class that tracks state but doesn't use LiveLink APIs yet.

**Deliverables:**
- `LiveLinkBridge.h` - Singleton class declaration
- `LiveLinkBridge.cpp` - State tracking implementation
- Replace global namespace state with proper class
- Thread safety with FCriticalSection
- Subject registry with TMap

**Implementation Pattern:**
```cpp
class FLiveLinkBridge {
public:
    static FLiveLinkBridge& Get();  // Singleton
    
    // Lifecycle
    bool Initialize(const FString& ProviderName);
    void Shutdown();
    bool IsInitialized() const;
    
    // Transform/Data subject methods (stubs)
    
private:
    FLiveLinkBridge() = default;
    bool bInitialized = false;
    TMap<FName, TArray<FName>> TransformSubjects;
    TMap<FName, TArray<FName>> DataSubjects;
    FCriticalSection CriticalSection;
};
```

**Changes Required:**
- Modify UnrealLiveLinkAPI.cpp to call FLiveLinkBridge::Get() methods
- Remove global namespace StubState
- Add thread-safe operations with FScopeLock

**Success Criteria:**
- Singleton returns same instance
- Initialize sets internal state
- Register/Update/Remove track subjects in maps
- Thread-safe operations
- Shutdown clears all subjects
- Still no actual LiveLink (state only)

---

### 📋 Sub-Phase 6.5: ILiveLinkProvider Creation
**Status:** PLANNED

**Objective:** Create real LiveLink Message Bus source and register it with LiveLink system.

**Key Implementation:**
```cpp
bool FLiveLinkBridge::Initialize(const FString& ProviderName) {
    // Get LiveLink client
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    // Create Message Bus source
    TSharedPtr<ILiveLinkSource> Source = 
        MakeShared<FLiveLinkMessageBusSource>(FText::FromString(ProviderName));
    
    LiveLinkSource = Client->AddSource(Source);
    // ...
}
```

**Build Configuration:**
Add to UnrealLiveLinkNative.Build.cs:
```csharp
PublicDependencyModuleNames.AddRange(new string[] {
    "Core",
    "CoreUObject",
    "LiveLink",
    "LiveLinkInterface",
    "Messaging",
    "UdpMessaging"
});
```

**Success Criteria:**
- Compiles with LiveLink headers
- Initialize creates source handle successfully
- Launch Unreal Editor → LiveLink window shows source
- Source shows "Connected" status (green)
- No subjects yet (registered in 6.6)

---

### 📋 Sub-Phase 6.6: Transform Subject Registration & Updates
**Status:** PLANNED

**Objective:** Implement transform subject registration and frame updates.

**Key Implementations:**
- RegisterTransformSubject: Push FLiveLinkTransformStaticData
- UpdateTransformSubject: Push FLiveLinkTransformFrameData
- Auto-registration on first update (matches mock behavior)
- Convert ULL_Transform to FTransform

**Success Criteria:**
- Register subject from C# → appears in LiveLink window
- Update subject repeatedly → green status (receiving updates)
- Create Unreal actor with LiveLink component → transforms in viewport
- Position/rotation updates visible in real-time

---

### 📋 Sub-Phase 6.7: Coordinate Conversion Validation
**Status:** PLANNED

**Objective:** Verify coordinate system handling between managed layer and Unreal.

**Expected Behavior:**
- Managed layer converts Simio → Unreal before P/Invoke
- Native layer receives position in centimeters (Unreal units)
- Native layer performs direct pass-through: ULL_Transform → FTransform
- No additional coordinate conversion needed

**Test Plan:**
- Place object at Simio origin (0,0,0) → appears at Unreal origin
- Place object at Simio (5m,0,0) → appears at Unreal (500cm,0,0)
- Verify rotations match expected orientation
- Confirm no mirroring or axis flips

**Success Criteria:**
- Objects appear at correct positions
- No coordinate system issues
- If needed, document any adjustments in managed layer

---

### 📋 Sub-Phase 6.8: String/FName Caching
**Status:** PLANNED

**Objective:** Cache FName conversions for performance optimization.

**Implementation:**
```cpp
class FLiveLinkBridge {
private:
    TMap<FString, FName> NameCache;
    
public:
    FName GetCachedName(const char* cString) {
        FString StringKey(cString);
        if (FName* Cached = NameCache.Find(StringKey)) {
            return *Cached;
        }
        FName NewName(cString);
        NameCache.Add(StringKey, NewName);
        return NewName;
    }
};
```

**Success Criteria:**
- First call creates FName and caches
- Subsequent calls use cached FName
- Cache cleared on Shutdown
- No performance regression

---

### 📋 Sub-Phase 6.9: Properties & Data Subjects
**Status:** PLANNED

**Objective:** Implement property streaming for transform+properties and data-only subjects.

**Key Features:**
- RegisterObjectWithProperties: Set PropertyNames in static data
- UpdateObjectWithProperties: Include PropertyValues in frame data
- Data subjects: Use ULiveLinkBasicRole (no transform)
- Property count validation

**Success Criteria:**
- Properties visible in LiveLink window
- Blueprint can read property values with "Get LiveLink Property Value"
- Property values update in real-time
- Data-only subjects work without transforms

---

### 📋 Sub-Phase 6.10: Error Handling & Validation
**Status:** PLANNED

**Objective:** Add comprehensive error checking and validation for production readiness.

**Enhancements:**
- Null parameter checks (already partially implemented)
- Invalid value checks (NaN, Inf)
- Initialization state checks
- Property count mismatch detection
- Helpful error messages in logs

**Success Criteria:**
- Null parameters handled gracefully
- Invalid values rejected with log messages
- Operations before Initialize fail gracefully
- No crashes under error conditions

---

### 📋 Sub-Phase 6.11: Dependency Analysis & Deployment Package
**Status:** PLANNED

**Objective:** Identify all DLL dependencies and create complete deployment package.

**Tools Required:**
- Dependencies.exe (https://github.com/lucasg/Dependencies)

**Process:**
1. Analyze UnrealLiveLinkNative.dll with Dependencies.exe
2. Identify required Unreal runtime DLLs
3. Copy all dependencies to deployment folder
4. Test on clean machine without UE installed

**Expected Dependencies:**
- tbbmalloc.dll
- UnrealEditor-Core.dll
- UnrealEditor-CoreUObject.dll
- UnrealEditor-LiveLink.dll
- UnrealEditor-LiveLinkInterface.dll
- UnrealEditor-Messaging.dll
- UnrealEditor-UdpMessaging.dll

**Package Size:** 50-200MB total

**Success Criteria:**
- All dependencies identified
- Works on machine without Unreal Engine
- No DllNotFoundException errors
- Complete deployment package ready

---

## Testing & Validation

### Build Verification
```powershell
# Build native DLL
.\build\BuildNative.ps1

# Verify exports
dumpbin /EXPORTS lib\native\win-x64\UnrealLiveLink.Native.dll
```

### Integration Testing
- Replace mock DLL with real native DLL in Simio
- Launch Unreal Editor with LiveLink window open
- Run Simio test model
- Verify subjects appear and update

### Manual Verification Checklist
- [ ] LiveLink source appears with correct name
- [ ] Subjects register dynamically
- [ ] Transforms update in real-time (30-60 Hz)
- [ ] Multiple objects work simultaneously
- [ ] Properties visible in LiveLink window
- [ ] Blueprint can read property values
- [ ] Objects removed when destroyed
- [ ] Clean shutdown (no leaks)

### Performance Testing
- [ ] 100 objects @ 30 Hz sustained
- [ ] Frame update time < 5ms average
- [ ] Memory stable over extended run
- [ ] No frame drops or stuttering

---

## Performance Targets

| Metric | Target | How to Measure |
|--------|--------|----------------|
| Initialization Time | < 2 seconds | From ULL_Initialize to IsConnected success |
| Update Latency | < 5 ms | Timestamp in Simio vs receipt in Unreal |
| Throughput | 100 objects @ 30Hz | Sustained without frame drops |
| Memory per Object | < 1 KB | Profile with Unreal Insights |
| Build Time (Incremental) | < 10 seconds | Time BuildNative.ps1 after code change |

**Baseline:** Reference project handles "thousands of floats @ 60Hz" successfully  
**Our Target:** 30,000 values/sec (6x lighter than reference)

---

## Success Criteria for Phase 6 Complete

**Functional Requirements:**
- ✅ All 12 functions implemented and working
- ✅ Subjects stream from Simio to Unreal in real-time
- ✅ Transforms accurate (matches Simio positions/rotations)
- ✅ Properties stream correctly to Blueprints
- ✅ Data subjects work without transforms
- ✅ Drop-in replacement for mock DLL (no C# changes)

**Performance Requirements:**
- ✅ 100 objects @ 30 Hz sustained
- ✅ < 5ms update latency
- ✅ Memory stable (< 100MB for 50 objects)
- ✅ No frame drops over extended test

**Quality Requirements:**
- ✅ No crashes or exceptions
- ✅ Comprehensive error logging
- ✅ Parameter validation on all functions
- ✅ Thread-safe for concurrent calls

**Integration Requirements:**
- ✅ Existing Simio test models pass
- ✅ All managed layer tests still pass (47/47)
- ✅ No changes required to C# code
- ✅ Compatible with UE 5.3+

---

## Common Issues & Solutions

### Build Issues

**Issue: Module not found**
```
Error: Unable to find module 'LiveLinkInterface'
```
**Solution:** Verify module in Build.cs PublicDependencyModuleNames

---

**Issue: WinMain signature error**
```
Error: Entry point signature incorrect
```
**Solution:** Use exact signature:
```cpp
int WINAPI WinMain(HINSTANCE hInst, HINSTANCE hPrev, LPSTR lpCmd, int nShow)
```

---

### Runtime Issues

**Issue: DLL not found from Simio**
```
DllNotFoundException: UnrealLiveLink.Native.dll
```
**Solutions:**
1. Copy DLL to same folder as managed DLL
2. Verify 64-bit DLL for 64-bit process
3. **CRITICAL:** Copy ALL dependencies (use Dependencies.exe)

---

**Issue: ULL_IsConnected returns NOT_CONNECTED (-2)**
**Checklist:**
- [ ] Unreal Editor running
- [ ] LiveLink window open
- [ ] Message Bus Source added
- [ ] Waited 1-2 seconds after Initialize
- [ ] Firewall allows UDP on port 6666

---

**Issue: Subjects not visible**
**Checklist:**
- [ ] Subject registered before first update
- [ ] Subject name valid and unique
- [ ] Provider name matches Initialize parameter
- [ ] Try manual refresh in LiveLink window
- [ ] Check UE_LOG output for errors

---

## Related Documentation

**Project Management:**
- [DevelopmentPlan.md](DevelopmentPlan.md) - Overall project status and milestones

**Technical Reference:**
- [NativeLayerDevelopment.md](NativeLayerDevelopment.md) - Technical patterns and API contract
- [Architecture.md](Architecture.md) - Overall system architecture

**Build & Test:**
- [TestAndBuildInstructions.md](TestAndBuildInstructions.md) - Build commands and procedures

**Reference:**
- `src/Native/Mock/MockLiveLink.cpp` - Working mock implementation