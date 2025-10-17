# Phase 6: Native Layer Implementation Plan

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

## Sub-Phase 6.1: UBT Project Setup

**Objective:** Create UBT Program project structure and verify compilation.

**Reference:** Based on proven UnrealLiveLinkCInterface architecture pattern.

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

### Success Criteria
- ✅ Project appears in UE5.sln under "Programs" folder
- ✅ Compiles without errors (even as empty stub)  
- ✅ DLL generated in `Engine\Binaries\Win64`
- ✅ No Unreal Editor launch required
- ✅ Can use VS Code + Build Tools (not full Visual Studio required)

**Deliverable:** Buildable UBT project outputting empty DLL

---

## Sub-Phase 6.2: Type Definitions (C API Structs)

**Objective:** Define `ULL_Transform` and return codes matching the managed layer exactly.

### Return Code Constants

**CRITICAL:** Match managed layer expectations exactly.

From `UnrealLiveLinkNative.cs`:
```csharp
public const int ULL_OK = 0;
public const int ULL_ERROR = -1;
public const int ULL_NOT_CONNECTED = -2;
public const int ULL_NOT_INITIALIZED = -3;
```

**Important:** Mock DLL uses positive error codes (1, 2), but managed layer expects NEGATIVE error codes. 
Real implementation MUST use negative values for errors.

**Reference Project Note:** UnrealLiveLinkCInterface avoids `bool` types entirely, using `int` with values 0 and 1.
Our approach: Use `int` in C API, `bool` acceptable internally in C++.

**Must match C# exactly:**
```cpp
struct ULL_Transform {
    double position[3];  // X, Y, Z in centimeters
    double rotation[4];  // Quaternion X, Y, Z, W
    double scale[3];     // X, Y, Z scale
};
// Size MUST be 80 bytes
static_assert(sizeof(ULL_Transform) == 80, "Size mismatch with C#");
```

### Success Criteria
- ✅ `sizeof(ULL_Transform) == 80` (verified at compile time)
- ✅ Header compiles standalone (no Unreal dependencies in header)
- ✅ Pure C types (no C++ classes in struct)

**Deliverable:** Type header that matches managed layer exactly

---

## Sub-Phase 6.3: C API Export Layer (Stub Functions)

**Objective:** Export all 12 functions with stub implementations (logging only, no LiveLink yet).

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

### Build Configuration

Update `.Build.cs` to export symbols:
```csharp
PublicDefinitions.Add("ULL_API=__declspec(dllexport)");
```

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

## Sub-Phase 6.4: LiveLinkBridge Class (No LiveLink Integration)

**Objective:** Create C++ bridge class that tracks state but doesn't use LiveLink APIs yet.

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

## Sub-Phase 6.5: ILiveLinkProvider Creation

**Objective:** Create real `ILiveLinkProvider` and register it with LiveLink system.

**Reference:** Architecture.md - "Creates ILiveLinkProvider via ILiveLinkClient"

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

Add modules:
```csharp
PublicDependencyModuleNames.AddRange(new string[] {
    "Core",
    "CoreUObject", 
    "LiveLink",
    "LiveLinkInterface",
    "LiveLinkMessageBusFramework"
});
```

### Success Criteria
- ✅ Compiles with LiveLink headers
- ✅ Initialize creates source handle successfully
- ✅ **CRITICAL TEST:** Launch Unreal Editor, open LiveLink window, run `ULL_Initialize()` from C#
- ✅ Source appears in LiveLink window with configured name
- ✅ Source shows "Connected" status (green indicator)
- ✅ No subjects yet (we register those in 6.6)

**Deliverable:** Working LiveLink source creation, visible in Unreal Editor

---

## Sub-Phase 6.6: Transform Subject Registration & Updates

**Objective:** Implement transform subject registration and frame updates.

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

## Sub-Phase 6.7: Coordinate Conversion (If Needed)

**Objective:** Verify coordinate system handling between managed layer and Unreal.

**Reference:** Architecture.md - "CoordinateConverter.cs in managed layer (before P/Invoke)"

### Analysis Required

The managed layer already converts Simio → Unreal coordinates before calling P/Invoke. The native layer receives values in **Unreal coordinate space**.

**Question for verification:**
- Does `FTransform` expect the same coordinate system as our `ULL_Transform`?
- Test: Place object at Simio origin (0,0,0) → Should appear at Unreal origin

### If Conversion Needed

Create `CoordinateHelpers.h` with:
```cpp
FTransform ConvertULLTransformToUnreal(const ULL_Transform* transform);
```

### Success Criteria
- ✅ Object at Simio (0,0,0) appears at Unreal origin
- ✅ Object at Simio (5,0,0) appears at Unreal (500cm, 0, 0)
- ✅ Rotations match expected orientation
- ✅ No mirroring or axis flips

**Deliverable:** Confirmed coordinate system correctness, helpers if needed

---

## Sub-Phase 6.8: String/FName Caching

**Objective:** Cache FName conversions for performance.

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

## Sub-Phase 6.9: Properties & Data Subjects

**Objective:** Implement property streaming for transform+properties and data-only subjects.

**Reference:** Architecture.md - Transform+properties and Data subjects with BasicRole

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

## Sub-Phase 6.10: Error Handling & Validation

**Objective:** Add comprehensive error checking and validation.

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

## Sub-Phase 6.11: Dependency Analysis & Deployment Package

**Objective:** Identify all DLL dependencies and create deployment package.

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

### Issues Found During Planning

**ISSUE 1: Build System Clarity**
- Documents mention both "Program target" and "Plugin"
- Need to clarify: Is this a UBT Program or different structure?
- **Question:** What's the correct UBT target type for a standalone DLL?

**ISSUE 2: ILiveLinkProvider vs FLiveLinkMessageBusSource**
- Architecture.md says "Creates ILiveLinkProvider via ILiveLinkClient"
- Code examples use FLiveLinkMessageBusSource
- **Question:** Which approach is correct for external DLL?

**ISSUE 3: Module Dependencies**
- Need complete list of required Unreal modules
- LiveLinkMessageBusFramework vs LiveLinkInterface unclear
- **Question:** Minimum required modules for basic transform streaming?

**ISSUE 4: Coordinate System**
- Managed layer converts Simio → Unreal
- Native layer receives Unreal coords
- **Question:** Does FTransform need any further conversion?

**ISSUE 5: Thread Safety**
- LiveLink APIs have `_AnyThread` suffix
- **Question:** Are our FCriticalSection locks sufficient? Any other concerns?

### Suggestions for Plan Updates

**SUGGESTION 1: Add Sub-Phase 0**
- Create build script skeleton first
- Verify UBT setup before writing code
- Test compilation of empty project

**SUGGESTION 2: Split Sub-Phase 6.6**
- 6.6a: Registration only (subjects appear)
- 6.6b: Updates (transforms move)
- Smaller validation steps

**SUGGESTION 3: Add Rollback Strategy**
- Each sub-phase should be git-committable
- Document how to revert to previous working state

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

1. **Review this plan** - Confirm approach and address questions above
2. **Resolve issues** - Get clarity on build system, APIs, and architecture questions
3. **Update Architecture.md** - Document any corrections to planned design
4. **Begin Sub-Phase 6.1** - Start with project setup when ready

**Ready to address issues and start implementation when you give the go-ahead.**