# Phase 6: Native Layer Implementation Plan

**Last Updated:** October 17, 2025  
**Status:** 🔄 IN PROGRESS (Sub-Phase 6.1 Complete, 6.2 In Progress)  
**Progress:** 1/11 sub-phases complete (~9%)  
**Estimated Completion:** Mid-November 2025

---

## Executive Summary

**Objective:** Implement real native DLL with Unreal Engine LiveLink Message Bus integration, replacing the mock DLL used for managed layer development.

**Current Status:**
- ✅ **Sub-Phase 6.1 COMPLETE:** UBT project setup, build automation, and compilation pipeline established
- 🔄 **Sub-Phase 6.2 IN PROGRESS:** Type definitions and C API contracts
- 📋 **Sub-Phases 6.3-6.11 PLANNED:** API implementation, LiveLink integration, optimization, and deployment

**Key Achievement:** Successfully established UE 5.6 source build environment (20.7GB) with working BuildNative.ps1 automation producing 25MB native executable in 3-5 second incremental builds.

**Architecture Validation:** Approach confirmed viable based on production reference project (UnrealLiveLinkCInterface) that successfully handles "thousands of floats @ 60Hz" - our use case is 6x lighter.

---

## Goal & Scope

**Goal:** Implement the 12-function C API as a standalone DLL using Unreal Build Tool, providing real LiveLink integration.

**Scope:** ONLY implement what's specified in Architecture.md and NativeLayerDevelopment.md. No additions.

**Architecture:** Standalone C++ DLL built with UBT that exports C functions and uses Unreal's LiveLink APIs internally.

## Performance Baseline (Reference Project Validation)

**Proven Viable:** UnrealLiveLinkCInterface successfully streams "a few thousand float values updating at 60Hz"

**Our Use Case Comparison:** 
- Reference: ~3,000+ floats @ 60Hz = ~180,000 values/sec
- Our target: 100 objects @ 30Hz × 10 doubles = 30,000 values/sec  
- **Conclusion:** Our use case is **6x lighter** than proven reference. Performance will NOT be an issue.

**Memory Copy Overhead:** One extra copy per frame is acceptable (per reference project experience).
**Architecture Confidence:** ✅ Approach proven viable in production.

---

## Prerequisites Verification

Before starting, confirm:
- [ ] Visual Studio 2022 Build Tools OR full IDE with C++ workload
- [ ] Unreal Engine 5.6 **source code** installed (path: `C:\UE\UE_5.6`)
- [ ] Windows 10/11 SDK (included with Build Tools)
- [ ] .NET SDK (for UBT itself - it's a C# tool)
- [ ] Mock DLL working and tested
- [ ] All managed layer tests passing (47/47)

## Architecture Validation

**Approach Confirmed:** Based on proven UnrealLiveLinkCInterface reference project that successfully implements:
- ✅ Standalone DLL compiled WITH UBT (not a plugin)
- ✅ C API via extern "C" functions 
- ✅ Third-party software loads DLL without UBT dependency
- ✅ LiveLink Message Bus integration
- ✅ Performance: "thousands of float values @ 60Hz" (our case: 100 objects @ 30Hz is 10x lighter)

**Why UBT vs Simple Build:**
- Mock DLL: Uses simple `cl.exe` (no Unreal dependencies)  
- Real implementation NEEDS UBT to link against:
  - `ILiveLinkClient` (requires LiveLinkInterface module)
  - `FLiveLinkMessageBusSource` (requires LiveLinkMessageBusFramework)
  - Unreal Core types (`FName`, `TMap`, `FCriticalSection`)

**Complexity Justification:**
- Mock: 437 lines, 2 files, global variables
- Real: ~800+ lines, 5+ files, singleton pattern
- Necessary because:
  - Mock simulates, Real integrates with LiveLink Message Bus
  - Need `ILiveLinkProvider` lifetime management  
  - Need thread-safe subject registry
  - Need FName caching for performance

---

## Sub-Phase 6.1: UBT Project Setup ✅ COMPLETED

**Objective:** Create UBT Program project structure and verify compilation.

**Reference:** Based on proven UnrealLiveLinkCInterface architecture pattern.

**Status:** ✅ **COMPLETED** - October 17, 2025

### Completion Summary

**🎉 Successfully Achieved:**
- ✅ UE 5.6 source installation setup (20.7GB download and configuration)
- ✅ Complete UBT project structure created
- ✅ Automated BuildNative.ps1 script with intelligent UE detection
- ✅ Working compilation pipeline: Source → UBT → 25MB executable
- ✅ Proper UE Program target with Core/CoreUObject dependencies
- ✅ Windows integration with WinMain entry point
- ✅ Required UE globals properly defined

**Build Results:**
```
Output: UnrealLiveLinkNative.exe (25,153,536 bytes)
Location: lib\native\win-x64\UnrealLiveLinkNative.exe
Status: Successfully compiled and tested
Build Time: ~3-5 seconds (incremental)
```

**Architecture Established:**
```
src/Native/UnrealLiveLink.Native/
├── UnrealLiveLinkNative.Target.cs    (UE Program target config)
├── UnrealLiveLinkNative.Build.cs     (Module dependencies)
├── Public/UnrealLiveLink.Native.h    (Header definitions)
└── Private/UnrealLiveLink.Native.cpp (WinMain entry point)
```

**Build System:**
- `build\BuildNative.ps1`: Automated UBT compilation with UE auto-detection
- Supports both source and binary UE installations (prioritizes source)
- Automatic project file generation and UBT execution
- Copies output to repository lib directory

### File Structure to Create

Create this structure in repository:
```
src/Native/UnrealLiveLink.Native/
├── Source/
│   └── UnrealLiveLinkNative/
│       ├── UnrealLiveLinkNative.Build.cs
│       ├── UnrealLiveLinkNative.cpp (minimal stub)
│       └── UnrealLiveLinkNative.h
└── UnrealLiveLinkNative.Target.cs
```

### Setup Process

**1. Copy to Unreal Engine Programs directory:**
```powershell
$UERoot = "C:\UE\UE_5.6"
$TargetDir = "$UERoot\Engine\Source\Programs\UnrealLiveLinkNative"

# Create directory and copy source
New-Item -ItemType Directory -Path $TargetDir -Force
Copy-Item -Recurse src/Native/UnrealLiveLink.Native/* $TargetDir
```

**2. Generate Visual Studio project files:**
```powershell
cd $UERoot
.\GenerateProjectFiles.bat
# This creates/updates UE5.sln
```

**3. Build using preferred method:**

**Option A: Visual Studio IDE** (easier for debugging)
- Open `UE5.sln` in Visual Studio
- Find "Programs > UnrealLiveLinkNative" project
- Build (Development configuration)

**Option B: MSBuild Command Line** (VS Code friendly, preferred)
```powershell
# Setup environment
.\build\SetupVSEnvironment.ps1

# Build specific project
msbuild UE5.sln /t:Programs\UnrealLiveLinkNative /p:Configuration=Development /p:Platform=Win64
```

**Option C: UBT Direct** (fastest for automation)
```powershell
.\Engine\Build\BatchFiles\Build.bat UnrealLiveLinkNative Win64 Development
```

**4. Verify output:**
```
C:\UE\UE_5.6\Engine\Binaries\Win64\UnrealLiveLinkNative.dll
C:\UE\UE_5.6\Engine\Binaries\Win64\UnrealLiveLinkNative.pdb
```

### .Target.cs File (Minimal Program Target)
```csharp
using UnrealBuildTool;
using System.Collections.Generic;

[SupportedPlatforms(UnrealPlatformClass.Desktop)]
public class UnrealLiveLinkNativeTarget : TargetRules
{
    public UnrealLiveLinkNativeTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Program;
        LinkType = TargetLinkType.Monolithic;
        LaunchModuleName = "UnrealLiveLinkNative";
        
        // Minimal program configuration (NOT a plugin)
        bBuildDeveloperTools = false;
        bCompileAgainstEngine = false;
        bCompileAgainstCoreUObject = true;
        bCompileWithPluginSupport = false;
        bIncludePluginsForTargetPlatforms = false;
        bBuildWithEditorOnlyData = false;
        
        // Enable logging for diagnostics
        bUseLoggingInShipping = true;
        
        GlobalDefinitions.Add("UE_TRACE_ENABLED=1");
    }
}
```

### Success Criteria ✅ ALL ACHIEVED
- ✅ Project structure created and properly configured  
- ✅ Compiles without errors using UBT
- ✅ Executable generated in `Engine\Binaries\Win64` (25MB UnrealLiveLinkNative.exe)
- ✅ No Unreal Editor launch required
- ✅ Automated build script (BuildNative.ps1) working
- ✅ UE source installation completed and functional

**Deliverable:** ✅ **COMPLETED** - Buildable UBT project outputting functional executable

**Key Achievement:** Full UE 5.6 source build environment established with working native compilation pipeline.

---

## Sub-Phase 6.2: Type Definitions (C API Structs) 🔄 IN PROGRESS

**Objective:** Define `ULL_Transform` and return codes matching the managed layer exactly.

**Status:** 🔄 **IN PROGRESS** - Started October 17, 2025

### File to Create

**`src/Native/UnrealLiveLink.Native/Source/UnrealLiveLinkNative/Public/UnrealLiveLinkTypes.h`**

This header must:
- Be pure C-compatible (no C++ classes)
- Have no Unreal Engine dependencies (standalone)
- Match C# marshaling layout exactly

### Return Code Constants

**CRITICAL:** Match managed layer expectations exactly.

From `UnrealLiveLinkNative.cs`:
```csharp
public const int ULL_OK = 0;
public const int ULL_ERROR = -1;
public const int ULL_NOT_CONNECTED = -2;
public const int ULL_NOT_INITIALIZED = -3;
public const int ULL_API_VERSION = 1;
```

**C++ Header Definition:**
```cpp
// Return codes (MUST be negative for errors)
#define ULL_OK                  0
#define ULL_ERROR              -1
#define ULL_NOT_CONNECTED      -2
#define ULL_NOT_INITIALIZED    -3

// API version for compatibility checking
#define ULL_API_VERSION         1
```

**Important:** Mock DLL uses positive error codes (1, 2) for simplicity, but managed layer expects NEGATIVE error codes. 
Real implementation MUST use negative values for errors as shown above.

### ULL_Transform Structure

**CRITICAL:** Must match C# layout exactly (verified in managed layer via `Marshal.SizeOf<ULL_Transform>()`)

```cpp
#pragma pack(push, 8)  // Ensure 8-byte alignment for doubles

typedef struct {
    double position[3];  // X, Y, Z in centimeters (Unreal coordinates)
    double rotation[4];  // Quaternion [X, Y, Z, W] (normalized)
    double scale[3];     // X, Y, Z scale factors
} ULL_Transform;

#pragma pack(pop)

// Compile-time validation
static_assert(sizeof(ULL_Transform) == 80, "ULL_Transform size must be 80 bytes to match C# marshaling");
static_assert(sizeof(double) == 8, "double must be 8 bytes");
```

**Memory Layout Verification:**
- Position: 3 × 8 bytes = 24 bytes
- Rotation: 4 × 8 bytes = 32 bytes  
- Scale: 3 × 8 bytes = 24 bytes
- **Total: 80 bytes** ✅

### Complete Header Template

```cpp
#pragma once

// Pure C API - no C++ types in this header
#ifdef __cplusplus
extern "C" {
#endif

// Return codes
#define ULL_OK                  0
#define ULL_ERROR              -1
#define ULL_NOT_CONNECTED      -2
#define ULL_NOT_INITIALIZED    -3

// API version
#define ULL_API_VERSION         1

// Transform structure (80 bytes total)
#pragma pack(push, 8)

typedef struct {
    double position[3];  // X, Y, Z in centimeters
    double rotation[4];  // Quaternion [X, Y, Z, W]
    double scale[3];     // X, Y, Z scale factors
} ULL_Transform;

#pragma pack(pop)

// Compile-time validation
static_assert(sizeof(ULL_Transform) == 80, "Size mismatch with C# marshaling");

#ifdef __cplusplus
}
#endif
```

### Success Criteria
- [ ] Header created in correct location
- [ ] `sizeof(ULL_Transform) == 80` (verified at compile time)
- [ ] Header compiles standalone (no Unreal dependencies)
- [ ] Pure C types (no C++ classes in struct)
- [ ] Return codes match managed layer (negative for errors)
- [ ] Can be included in both C and C++ files

**Deliverable:** Type header that matches managed layer exactly

---

## Sub-Phase 6.3: C API Export Layer (Stub Functions) 📋 PLANNED

**Objective:** Export all 12 functions with stub implementations (logging only, no LiveLink yet).

**Status:** 📋 **PLANNED**

**Reference:** Match `MockLiveLink.h` function signatures exactly.

### Files to Create/Modify

**1. `src/Native/UnrealLiveLink.Native/Source/UnrealLiveLinkNative/Public/UnrealLiveLinkAPI.h`**

Declares all 12 functions with `extern "C"` and `DLLEXPORT` macro.

**2. `src/Native/UnrealLiveLink.Native/Source/UnrealLiveLinkNative/Private/UnrealLiveLinkAPI.cpp`**

Implements all 12 functions as stubs that:
- Log function name and parameters using `UE_LOG`
- Validate parameters (null checks)
- Return success codes
- Do NOT implement LiveLink yet

### 12 Functions to Implement (Stubs Only)

**Lifecycle (4):**
```cpp
int ULL_Initialize(const char* providerName);
void ULL_Shutdown();
int ULL_GetVersion();
int ULL_IsConnected();
```

**Transform Subjects (5):**
```cpp
void ULL_RegisterObject(const char* subjectName);
void ULL_RegisterObjectWithProperties(const char* subjectName, const char** propertyNames, int propertyCount);
void ULL_UpdateObject(const char* subjectName, const ULL_Transform* transform);
void ULL_UpdateObjectWithProperties(const char* subjectName, const ULL_Transform* transform, const float* propertyValues, int propertyCount);
void ULL_RemoveObject(const char* subjectName);
```

**Data Subjects (3):**
```cpp
void ULL_RegisterDataSubject(const char* subjectName, const char** propertyNames, int propertyCount);
void ULL_UpdateDataSubject(const char* subjectName, const char** propertyNames, const float* propertyValues, int propertyCount);
void ULL_RemoveDataSubject(const char* subjectName);
```

**Build Configuration:** Update `.Build.cs` to export symbols:
```csharp
// In UnrealLiveLinkNative.Build.cs
PublicDependencyModuleNames.AddRange(new string[] {
    "Core",
    "CoreUObject"
    // LiveLink modules added in later sub-phases
});

// Define export macro for DLL
PublicDefinitions.Add("ULL_API=__declspec(dllexport)");
```

**Note:** Target type should be changed from TargetType.Program to a configuration that produces a DLL instead of an EXE. This will be updated based on UBT documentation research in Sub-Phase 6.3.

### Success Criteria
- ✅ DLL exports exactly 12 functions (verify with `dumpbin /EXPORTS`)
- ✅ Function names match P/Invoke declarations in C#
- ✅ Calling convention is `__cdecl` (default for extern "C")
- ✅ Can call from managed layer without DllNotFoundException
- ✅ Stub functions log to Unreal log file
- ✅ Returns ULL_OK for Initialize, version 1 for GetVersion

### Testing
Replace mock DLL with stub native DLL, run Simio test:
- Should initialize successfully
- Should log all function calls to Unreal log
- Won't show subjects in Unreal yet (no LiveLink implementation)

**Deliverable:** DLL with 12 exported stub functions callable from C#

---

## Sub-Phase 6.4: LiveLinkBridge Class (No LiveLink Integration) 📋 PLANNED

**Objective:** Create C++ bridge class that tracks state but doesn't use LiveLink APIs yet.

**Status:** 📋 **PLANNED**

**Reference:** Architecture.md specifies singleton pattern, subject registry, thread safety.

### Files to Create

**1. `src/Native/UnrealLiveLink.Native/Source/UnrealLiveLinkNative/Private/LiveLinkBridge.h`**

Class declaration:
```cpp
class FLiveLinkBridge {
public:
    static FLiveLinkBridge& Get();  // Singleton
    
    // Lifecycle
    bool Initialize(const FString& ProviderName);
    void Shutdown();
    bool IsInitialized() const;
    
    // Transform subjects (stubs for now)
    void RegisterTransformSubject(const FName& SubjectName);
    void RegisterTransformSubjectWithProperties(const FName& SubjectName, const TArray<FName>& PropertyNames);
    void UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform);
    void UpdateTransformSubjectWithProperties(const FName& SubjectName, const FTransform& Transform, const TArray<float>& PropertyValues);
    void RemoveTransformSubject(const FName& SubjectName);
    
    // Data subjects (stubs for now)
    void RegisterDataSubject(const FName& SubjectName, const TArray<FName>& PropertyNames);
    void UpdateDataSubject(const FName& SubjectName, const TArray<FName>& PropertyNames, const TArray<float>& PropertyValues);
    void RemoveDataSubject(const FName& SubjectName);
    
private:
    FLiveLinkBridge() = default;
    
    bool bInitialized = false;
    FString ProviderName;
    
    // Subject tracking
    TMap<FName, TArray<FName>> TransformSubjects;  // SubjectName -> PropertyNames
    TMap<FName, TArray<FName>> DataSubjects;       // SubjectName -> PropertyNames
    
    // Thread safety
    FCriticalSection CriticalSection;
};
```

**2. `src/Native/UnrealLiveLink.Native/Source/UnrealLiveLinkNative/Private/LiveLinkBridge.cpp`**

Implementation that:
- Implements singleton pattern
- Tracks registered subjects in maps
- Logs all operations (no LiveLink calls yet)
- Thread-safe with FCriticalSection

### Update API Functions

Modify `UnrealLiveLinkAPI.cpp` to call `FLiveLinkBridge::Get()` methods instead of just logging.

### Success Criteria
- ✅ Singleton returns same instance
- ✅ Initialize sets internal state
- ✅ Register/Update/Remove track subjects in maps
- ✅ Thread-safe (uses FScopeLock)
- ✅ Shutdown clears all subjects
- ✅ Still no actual LiveLink (just state tracking)

**Deliverable:** Bridge class managing state, no LiveLink yet

---

## Sub-Phase 6.5: ILiveLinkProvider Creation 📋 PLANNED

**Objective:** Create real LiveLink Message Bus source and register it with LiveLink system.

**Status:** 📋 **PLANNED**

**Reference:** Architecture.md - "Creates FLiveLinkMessageBusSource via ILiveLinkClient"

### Modify LiveLinkBridge.cpp

Add to `Initialize()`:
```cpp
bool FLiveLinkBridge::Initialize(const FString& ProviderName) {
    FScopeLock Lock(&CriticalSection);
    
    if (bInitialized) {
        return true;
    }
    
    // Get LiveLink client
    ILiveLinkClient* LiveLinkClient = &IModularFeatures::Get().GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    if (!LiveLinkClient) {
        UE_LOG(LogTemp, Error, TEXT("Failed to get LiveLink client"));
        return false;
    }
    
    // Create source
    FLiveLinkSourceHandle SourceHandle = LiveLinkClient->AddSource(MakeShared<FLiveLinkMessageBusSource>(FText::FromString(ProviderName)));
    if (!SourceHandle.IsValid()) {
        UE_LOG(LogTemp, Error, TEXT("Failed to create LiveLink source"));
        return false;
    }
    
    LiveLinkSource = SourceHandle;
    this->ProviderName = ProviderName;
    bInitialized = true;
    
    UE_LOG(LogTemp, Log, TEXT("LiveLink initialized: %s"), *ProviderName);
    return true;
}
```

### Add Member Variables

```cpp
// In LiveLinkBridge.h
private:
    FLiveLinkSourceHandle LiveLinkSource;
```

### Update Build.cs Dependencies

Add LiveLink modules:
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

**Note:** UE 5.3+ includes LiveLink modules by default. The exact module names and availability should be verified against UE 5.6 documentation.

### Success Criteria
- ✅ Compiles with LiveLink headers
- ✅ Initialize creates source handle successfully
- ✅ **CRITICAL TEST:** Launch Unreal Editor, open LiveLink window, run `ULL_Initialize()` from C#
- ✅ Source appears in LiveLink window with configured name
- ✅ Source shows "Connected" status (green indicator)
- ✅ No subjects yet (we register those in 6.6)

**Deliverable:** Working LiveLink source creation, visible in Unreal Editor

---

## Sub-Phase 6.6: Transform Subject Registration & Updates 📋 PLANNED

**Objective:** Implement transform subject registration and frame updates.

**Status:** 📋 **PLANNED**

**Reference:** Architecture.md data flow - "LiveLink provider submits FLiveLinkFrameDataStruct"

### Implement in LiveLinkBridge.cpp

**RegisterTransformSubject:**
```cpp
void FLiveLinkBridge::RegisterTransformSubject(const FName& SubjectName) {
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkSource.IsValid()) {
        return;
    }
    
    // Create static data for transform subject
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    
    // Push subject to LiveLink
    ILiveLinkClient* Client = &IModularFeatures::Get().GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    Client->PushSubjectStaticData_AnyThread(LiveLinkSource, FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName), ULiveLinkTransformRole::StaticClass(), MoveTemp(StaticData));
    
    TransformSubjects.Add(SubjectName, TArray<FName>());
    
    UE_LOG(LogTemp, Log, TEXT("Registered transform subject: %s"), *SubjectName.ToString());
}
```

**UpdateTransformSubject:**
```cpp
void FLiveLinkBridge::UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform) {
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkSource.IsValid()) {
        return;
    }
    
    // Auto-register if not registered (matches mock behavior)
    if (!TransformSubjects.Contains(SubjectName)) {
        RegisterTransformSubject(SubjectName);
    }
    
    // Create frame data
    FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
    FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
    TransformFrameData->Transform = Transform;
    TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    // Push frame
    ILiveLinkClient* Client = &IModularFeatures::Get().GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    Client->PushSubjectFrameData_AnyThread(LiveLinkSource, FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName), MoveTemp(FrameData));
}
```

### Update C API Functions

In `UnrealLiveLinkAPI.cpp`, convert `ULL_Transform` to `FTransform`:
```cpp
void ULL_UpdateObject(const char* subjectName, const ULL_Transform* transform) {
    if (!subjectName || !transform) {
        return;
    }
    
    FName SubjectFName(subjectName);
    
    // Convert ULL_Transform to FTransform
    FVector Position(transform->position[0], transform->position[1], transform->position[2]);
    FQuat Rotation(transform->rotation[0], transform->rotation[1], transform->rotation[2], transform->rotation[3]);
    FVector Scale(transform->scale[0], transform->scale[1], transform->scale[2]);
    FTransform UnrealTransform(Rotation, Position, Scale);
    
    FLiveLinkBridge::Get().UpdateTransformSubject(SubjectFName, UnrealTransform);
}
```

### Success Criteria
- ✅ Call `ULL_RegisterObject("TestCube")` from C#
- ✅ Subject "TestCube" appears in Unreal LiveLink window
- ✅ Call `ULL_UpdateObject("TestCube", transform)` repeatedly
- ✅ Subject shows green status (receiving updates)
- ✅ Create Unreal actor, add LiveLink component, bind to "TestCube"
- ✅ Actor transforms in viewport (position updates visible)

**Deliverable:** Working transform streaming to Unreal Editor

---

## Sub-Phase 6.7: Coordinate Conversion Validation 📋 PLANNED

**Objective:** Verify coordinate system handling between managed layer and Unreal.

**Status:** 📋 **PLANNED**

**Reference:** Architecture.md - "CoordinateConverter.cs in managed layer (before P/Invoke) - Native layer receives already-converted Unreal coordinates"

### Analysis Required

The managed layer already converts Simio → Unreal coordinates before calling P/Invoke. The native layer receives values in **Unreal coordinate space**.

**Expected behavior:**
- `ULL_Transform` arrives with position in centimeters (Unreal units)
- Position already converted from Simio's coordinate system (Z-up) to Unreal's (Z-up, axis-swapped)
- Rotation already converted from Euler angles to normalized quaternion
- Native layer should perform **direct pass-through**: `ULL_Transform` → `FTransform`

**Test plan:**
- Place object at Simio origin (0,0,0) → Should appear at Unreal origin (0,0,0)
- Place object at Simio (5m, 0, 0) → Should appear at Unreal (500cm, 0, 0)
- Verify rotations match expected orientation
- Confirm no mirroring or axis flips

### If Unexpected Conversion Needed

If testing reveals coordinate issues, create `CoordinateHelpers.h` with:
```cpp
FTransform ConvertULLTransformToUnreal(const ULL_Transform* transform);
```

**However:** Architecture.md explicitly states native layer should NOT do coordinate conversion. Any issues found should be addressed in managed layer's `CoordinateConverter.cs`.

### Success Criteria
- ✅ Object at Simio (0,0,0) appears at Unreal origin
- ✅ Object at Simio (5,0,0) appears at Unreal (500cm, 0, 0)
- ✅ Rotations match expected orientation
- ✅ No mirroring or axis flips

**Deliverable:** Confirmed coordinate system correctness, helpers if needed

---

## Sub-Phase 6.8: String/FName Caching 📋 PLANNED

**Objective:** Cache FName conversions for performance optimization.

**Status:** 📋 **PLANNED**

**Reference:** Architecture.md - "String/FName conversion with caching for performance"

### Add to LiveLinkBridge.h

```cpp
private:
    TMap<FString, FName> NameCache;
    
    FName GetCachedName(const char* cString);
```

### Implement in LiveLinkBridge.cpp

```cpp
FName FLiveLinkBridge::GetCachedName(const char* cString) {
    if (!cString) {
        return NAME_None;
    }
    
    FString StringKey(cString);
    
    if (FName* CachedName = NameCache.Find(StringKey)) {
        return *CachedName;
    }
    
    FName NewName(cString);
    NameCache.Add(StringKey, NewName);
    return NewName;
}
```

### Update All API Functions

Replace `FName(subjectName)` with `FLiveLinkBridge::Get().GetCachedName(subjectName)`.

### Success Criteria
- ✅ First call creates FName and caches it
- ✅ Subsequent calls use cached FName
- ✅ Cache cleared on Shutdown
- ✅ No performance regression in profiler

**Deliverable:** Optimized string conversion with caching

---

## Sub-Phase 6.9: Properties & Data Subjects 📋 PLANNED

**Objective:** Implement property streaming for transform+properties and data-only subjects.

**Status:** 📋 **PLANNED**

**Reference:** Architecture.md - Transform+properties with TransformRole and Data subjects with BasicRole

### Implement Property Support

**RegisterObjectWithProperties:**
```cpp
void FLiveLinkBridge::RegisterTransformSubjectWithProperties(const FName& SubjectName, const TArray<FName>& PropertyNames) {
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkSource.IsValid()) {
        return;
    }
    
    // Create static data with properties
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    TransformStaticData->PropertyNames = PropertyNames;
    
    ILiveLinkClient* Client = &IModularFeatures::Get().GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    Client->PushSubjectStaticData_AnyThread(LiveLinkSource, FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName), ULiveLinkTransformRole::StaticClass(), MoveTemp(StaticData));
    
    TransformSubjects.Add(SubjectName, PropertyNames);
}
```

**UpdateObjectWithProperties:**
```cpp
void FLiveLinkBridge::UpdateTransformSubjectWithProperties(const FName& SubjectName, const FTransform& Transform, const TArray<float>& PropertyValues) {
    FScopeLock Lock(&CriticalSection);
    
    // Validate property count matches registration
    if (TransformSubjects.Contains(SubjectName)) {
        const TArray<FName>& RegisteredProps = TransformSubjects[SubjectName];
        if (RegisteredProps.Num() != PropertyValues.Num()) {
            UE_LOG(LogTemp, Error, TEXT("Property count mismatch for %s: expected %d, got %d"), 
                *SubjectName.ToString(), RegisteredProps.Num(), PropertyValues.Num());
            return;
        }
    }
    
    // Create frame with properties
    FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
    FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
    TransformFrameData->Transform = Transform;
    TransformFrameData->PropertyValues = PropertyValues;
    TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    ILiveLinkClient* Client = &IModularFeatures::Get().GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    Client->PushSubjectFrameData_AnyThread(LiveLinkSource, FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName), MoveTemp(FrameData));
}
```

### Data Subjects (BasicRole)

Similar implementation but use:
- `FLiveLinkBaseStaticData` instead of `FLiveLinkTransformStaticData`
- `FLiveLinkBaseFrameData` instead of `FLiveLinkTransformFrameData`
- `ULiveLinkBasicRole::StaticClass()` instead of Transform role
- No Transform, only PropertyNames and PropertyValues

### Success Criteria
- ✅ Register transform subject with properties from Simio TransmitValuesStep
- ✅ Properties visible in LiveLink window
- ✅ Create Blueprint, use "Get LiveLink Property Value" node
- ✅ Property values update in real-time in Blueprint
- ✅ Data-only subjects work without transforms
- ✅ Property count validation prevents mismatches

**Deliverable:** Full property streaming support

---

## Sub-Phase 6.10: Error Handling & Validation 📋 PLANNED

**Objective:** Add comprehensive error checking and validation for production readiness.

**Status:** 📋 **PLANNED**

**Reference:** Architecture.md - "Parameter validation with error messages"

### Add to All API Functions

```cpp
// Example pattern
void ULL_UpdateObject(const char* subjectName, const ULL_Transform* transform) {
    // Null checks
    if (!subjectName) {
        UE_LOG(LogTemp, Error, TEXT("ULL_UpdateObject: subjectName is NULL"));
        return;
    }
    
    if (!transform) {
        UE_LOG(LogTemp, Error, TEXT("ULL_UpdateObject: transform is NULL"));
        return;
    }
    
    // Initialization check
    if (!FLiveLinkBridge::Get().IsInitialized()) {
        UE_LOG(LogTemp, Error, TEXT("ULL_UpdateObject: Bridge not initialized"));
        return;
    }
    
    // Validate transform values
    if (!IsFinite(transform->position[0]) || !IsFinite(transform->position[1]) || !IsFinite(transform->position[2])) {
        UE_LOG(LogTemp, Error, TEXT("ULL_UpdateObject: Invalid position values"));
        return;
    }
    
    // ... proceed with operation
}
```

### Validation Functions

Add helper:
```cpp
bool IsFinite(double value) {
    return !FMath::IsNaN(value) && FMath::IsFinite(value);
}
```

### Success Criteria
- ✅ Null parameters logged and handled gracefully
- ✅ Invalid values (NaN, Inf) rejected with log message
- ✅ Operations before Initialize fail gracefully
- ✅ Property count mismatches logged with helpful context
- ✅ No crashes under error conditions

**Deliverable:** Production-ready error handling

---

## Sub-Phase 6.11: Dependency Analysis & Deployment Package 📋 PLANNED

**Objective:** Identify all DLL dependencies and create complete deployment package.

**Status:** 📋 **PLANNED**

**Reference:** UnrealLiveLinkCInterface warns: "copying just the dll may not be enough. There may be other dependencies that need to be copied such as the tbbmalloc.dll"

### Tools Required
- Dependencies.exe (https://github.com/lucasg/Dependencies)

### Process

**1. Analyze DLL dependencies:**
```powershell
# Use Dependencies.exe GUI or command line
Dependencies.exe C:\UE\UE_5.6\Engine\Binaries\Win64\UnrealLiveLinkNative.dll

# Common Unreal dependencies expected:
# - tbbmalloc.dll (Intel Threading Building Blocks)
# - UnrealEditor-Core.dll  
# - UnrealEditor-CoreUObject.dll
# - LiveLink module DLLs
# - Various Windows runtime DLLs
```

**2. Create deployment folder:**
```
lib\native\win-x64\
├── UnrealLiveLinkNative.dll        # Main DLL
├── UnrealLiveLinkNative.pdb        # Debug symbols  
├── tbbmalloc.dll                   # (if required)
├── [other identified dependencies]
└── VERSION.txt                     # Dependency info
```

**3. Copy dependencies from UE installation:**
```powershell
$UEBinaries = "C:\UE\UE_5.6\Engine\Binaries\Win64"
$DeployDir = "lib\native\win-x64"

# Copy main DLL
Copy-Item "$UEBinaries\UnrealLiveLinkNative.dll" $DeployDir
Copy-Item "$UEBinaries\UnrealLiveLinkNative.pdb" $DeployDir

# Copy identified dependencies (example)
Copy-Item "$UEBinaries\tbbmalloc.dll" $DeployDir -ErrorAction SilentlyContinue
# Add others as identified by Dependencies.exe
```

**4. Test on clean machine:**
- Copy deployment folder to test PC without UE installed
- Run Simio with deployed DLLs  
- Verify no missing DLL errors on startup

### Success Criteria
- ✅ All dependencies identified via Dependencies.exe
- ✅ DLLs copied to deployment folder
- ✅ Works on machine without Unreal Engine installed
- ✅ Simio can load and call all 12 functions
- ✅ No DllNotFoundException or missing dependency errors

**Note:** This may require 50-200MB of Unreal DLLs for redistribution, but enables deployment without full UE installation.

**Deliverable:** Complete deployment package with all dependencies

---

## Testing & Validation

### Build Verification
```powershell
# Build native DLL
.\build\BuildNative.ps1

# Verify exports
dumpbin /EXPORTS lib\native\win-x64\UnrealLiveLink.Native.dll

# Should show all 12 functions
```

### Integration Testing
```powershell
# Replace mock DLL with real native DLL
Copy-Item lib\native\win-x64\UnrealLiveLink.Native.dll `
          "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\"

# Run Simio test model
# Launch Unreal Editor with LiveLink window open
```

### Manual Verification Checklist
- [ ] LiveLink source appears with correct name
- [ ] Subjects register dynamically as Simio creates objects
- [ ] Transforms update in real-time (30-60 Hz visible)
- [ ] Multiple objects work simultaneously
- [ ] Properties visible in LiveLink window
- [ ] Blueprint can read property values
- [ ] Objects removed when Simio destroys them
- [ ] Clean shutdown (no leaks)

### Performance Testing
- [ ] 100 objects @ 30 Hz sustained
- [ ] Frame update time < 5ms average
- [ ] Memory stable over 10-minute run
- [ ] No frame drops or stutter

---

## Known Issues & Questions

### Issues Resolved in Sub-Phase 6.1

**✅ RESOLVED: Build System Clarity**
- ~~Documents mention both "Program target" and "Plugin"~~
- **RESOLUTION:** Confirmed as UBT Program target (`TargetType.Program`)
- Successfully built UnrealLiveLinkNative.exe (25MB) using Program target configuration
- **Note:** Need to investigate how to output DLL instead of EXE for Sub-Phase 6.3

**✅ RESOLVED: Module Dependencies**
- ~~Need complete list of required Unreal modules~~
- **RESOLUTION:** Current Build.cs successfully compiles with Core and CoreUObject
- LiveLink modules will be added incrementally in Sub-Phases 6.5-6.9
- Current minimal dependencies validated in Sub-Phase 6.1

### Active Questions for Sub-Phase 6.2+

**QUESTION 1: DLL vs EXE Output**
- Current configuration produces UnrealLiveLinkNative.exe (25MB)
- Need to change configuration to produce UnrealLiveLinkNative.dll
- **Action:** Research UBT documentation for DLL output from Program target
- **Priority:** HIGH - Required for Sub-Phase 6.3

**QUESTION 2: LiveLink API Usage Patterns**
- Architecture.md references "FLiveLinkMessageBusSource"
- NativeLayerDevelopment.md shows code examples
- **Question:** Verify exact API patterns work in UE 5.6
- **Priority:** MEDIUM - Needed for Sub-Phase 6.5

**QUESTION 3: Coordinate System Verification**
- Managed layer converts Simio → Unreal coordinates
- Native layer should do direct pass-through
- **Question:** Does FTransform constructor need specific axis ordering?
- **Priority:** LOW - Will be validated in Sub-Phase 6.7 testing

**QUESTION 4: Thread Safety Implementation**
- LiveLink APIs have `_AnyThread` suffix variants
- FCriticalSection locks planned for bridge class
- **Question:** Are there additional thread safety concerns in UE 5.6?
- **Priority:** LOW - Will be validated during implementation

### Suggestions for Plan Improvements

**✅ IMPLEMENTED: Build Script Automation**
- ~~Create build script skeleton first~~
- **DONE:** BuildNative.ps1 created with intelligent UE detection
- Automated project copying, UBT compilation, and output management
- Successfully validated in Sub-Phase 6.1

**SUGGESTION 1: Research DLL Output Configuration**
- Current output is EXE, need DLL for P/Invoke
- Research UBT Target.cs configuration for DLL output
- May need `LinkType = TargetLinkType.Modular` or similar
- **Action:** Document findings before Sub-Phase 6.3

**SUGGESTION 2: Incremental LiveLink Integration**
- Sub-Phase 6.5 adds LiveLink modules to Build.cs
- Verify modules compile before implementing actual code
- Test with minimal "hello world" LiveLink source creation
- Reduces risk of discovering compilation issues late

**SUGGESTION 3: Testing Checkpoints**
- Add git tag after each completed sub-phase
- Example: `phase6.1-complete`, `phase6.2-complete`
- Enables easy rollback to known-good state
- Documents progression for future reference

---

## Phase 6 Status Update - October 17, 2025

### Current Progress

**✅ COMPLETED:**
- **Sub-Phase 6.1**: UBT Project Setup (October 17, 2025)
  - UE 5.6 source installation (20.7GB at C:\UE\UE_5.6_Source)
  - Complete UBT project structure created
  - Working BuildNative.ps1 automation with UE auto-detection
  - Successful native executable compilation (25MB UnrealLiveLinkNative.exe)
  - Full build environment established and validated
  - Build time: 3-5 seconds (incremental)

**🔄 IN PROGRESS:**
- **Sub-Phase 6.2**: Type Definitions (C API Structs)
  - Started: October 17, 2025
  - Next: Define ULL_Transform struct (80 bytes) in UnrealLiveLinkTypes.h
  - Next: Implement return codes (ULL_OK=-0, ULL_ERROR=-1, etc.)
  - Next: Add compile-time size validation with static_assert

**� PLANNED:**
- Sub-Phases 6.3 through 6.11 (detailed above)

**📊 Progress:** 1/11 sub-phases complete = ~9% of Phase 6 complete

### Key Technical Achievements

1. **✅ UE Source Build Environment**: Successfully set up and validated UE 5.6 source installation
2. **✅ UBT Integration**: Established working UnrealBuildTool compilation pipeline  
3. **✅ Build Automation**: Created intelligent PowerShell script with automatic UE detection
4. **✅ Native Foundation**: Generated 25MB executable with proper UE Core integration
5. **✅ Architecture Validation**: Confirmed approach viable using reference project as guide
6. **✅ Windows Integration**: Proper WinMain entry point and required UE globals defined

### Build System Status

```
BuildNative.ps1:      ✅ Working
UE Installation:      ✅ C:\UE\UE_5.6_Source (source build, 20.7GB)
UE Version:           ✅ 5.6  
Compilation:          ✅ 3-5 second incremental builds
Output:               ✅ UnrealLiveLinkNative.exe (25MB)
Output Location:      ✅ lib\native\win-x64\UnrealLiveLinkNative.exe
Testing:              ✅ Executable runs successfully
```

### Next Immediate Steps

1. **Sub-Phase 6.2 Implementation:**
   - Create `UnrealLiveLinkTypes.h` with ULL_Transform struct
   - Define return codes matching managed layer expectations
   - Add static_assert for 80-byte struct size validation
   - Verify header compiles standalone (no UE dependencies)

2. **DLL Output Research:**
   - Investigate UBT configuration for DLL output vs current EXE
   - Update Target.cs or Build.cs as needed
   - Required before Sub-Phase 6.3 can begin

3. **Validation:**
   - Ensure types header matches C# marshaling exactly
   - Test struct size with sizeof() in test compilation

**Ready for Phase 6.2 implementation** - Type definitions and C API structure contracts

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
- ✅ 100 objects @ 30 Hz sustained (per Architecture.md)
- ✅ < 5ms update latency
- ✅ Memory stable (< 100MB for 50 objects)
- ✅ No frame drops over 10-minute test

**Quality Requirements:**
- ✅ No crashes or exceptions
- ✅ Comprehensive error logging
- ✅ Parameter validation on all functions
- ✅ Thread-safe for concurrent calls

**Integration Requirements:**
- ✅ Existing Simio test models pass
- ✅ All managed layer tests still pass
- ✅ No changes required to C# code
- ✅ Compatible with UE 5.3+

---

## Next Steps

### Immediate Actions (Sub-Phase 6.2)

1. **Create UnrealLiveLinkTypes.h**
   - Location: `src/Native/UnrealLiveLink.Native/Source/UnrealLiveLinkNative/Public/UnrealLiveLinkTypes.h`
   - Define ULL_Transform struct with exact 80-byte layout
   - Define return code constants (negative for errors)
   - Add API version constant
   - Include static_assert for compile-time validation
   - Ensure pure C compatibility (extern "C" guards)

2. **Verify Type Definitions**
   - Test compilation with minimal program
   - Confirm sizeof(ULL_Transform) == 80
   - Verify no UE dependencies in types header
   - Validate against C# marshaling expectations

3. **Research DLL Output Configuration**
   - Investigate UBT Target.cs settings for DLL vs EXE output
   - Document required changes to configuration files
   - Prepare for Sub-Phase 6.3 implementation

### Short-term Roadmap (Next 1-2 weeks)

- Complete Sub-Phases 6.2 and 6.3 (Types + Stub API)
  - Get DLL exporting 12 stub functions
  - Verify P/Invoke compatibility from C# managed layer
  - Validate function signatures match mock DLL exactly

- Complete Sub-Phases 6.4 and 6.5 (Bridge + LiveLink Integration)
  - Implement LiveLinkBridge singleton with state tracking
  - Add LiveLink modules to build dependencies
  - Create first real LiveLink Message Bus source
  - Verify source appears in Unreal Editor LiveLink window

### Medium-term Roadmap

- Sub-Phases 6.6, 6.7, 6.8 (Transform Streaming + Optimization)
  - Implement transform subject registration and updates
  - Validate coordinate system handling
  - Add FName caching for performance

- Sub-Phases 6.9, 6.10, 6.11 (Properties + Polish + Deployment)
  - Implement property streaming for all subject types
  - Add comprehensive error handling and validation
  - Perform dependency analysis and create deployment package

### Completion Criteria

**Phase 6 will be complete when:**
- ✅ All 11 sub-phases completed and validated
- ✅ All 12 C API functions working with real LiveLink integration
- ✅ Drop-in replacement for mock DLL (no C# changes required)
- ✅ All managed layer tests still pass (47/47)
- ✅ Performance targets met (100 objects @ 30 Hz)
- ✅ Complete deployment package with dependencies identified
- ✅ Tested on clean machine without UE installation

