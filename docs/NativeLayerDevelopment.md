# Native Layer Development Guide

**Purpose:** Technical reference for C++ native bridge to Unreal LiveLink.  
**Audience:** C++ developers implementing/extending the native layer  
**Scope:** API contract, build procedures, implementation patterns, technical design  
**Not Included:** Project status/phase tracking (see DevelopmentPlan.md)

**Last Updated:** October 17, 2025

---

## Architecture Foundation

### Validated Reference: UnrealLiveLinkCInterface

Our architecture is based on a **proven production system**:

> "Provides a C Interface to the Unreal Live Link Message Bus API. This allows for third party packages to stream to Unreal Live Link without requiring to compile with the Unreal Build Tool (UBT). This is done by exposing the Live Link API via a C interface in a shared object compiled using UBT."

**Key Validation Points:**
- Build approach works: UBT-compiled standalone program
- Third-party integration works: P/Invoke → DLL → LiveLink Message Bus
- Performance proven: "thousands of float values @ 60Hz"
- Deployment complexity known: Additional DLLs required (tbbmalloc.dll, etc.)

**Our Architecture:**
```
Simio (C# P/Invoke) → Native DLL (UBT-compiled) → LiveLink Message Bus → Unreal Editor
```

**Performance Validation:**
- Reference handles: ~3,000+ floats @ 60Hz = ~180,000 values/sec
- Our target: 100 objects @ 30Hz × 10 doubles = 30,000 values/sec
- **Safety margin: 6x lighter than proven reference**

---

## API Contract

### Complete Function List (12 Functions)

Reference implementation: `src/Native/Mock/MockLiveLink.h` (verified working)

#### Lifecycle (4 functions)
```cpp
int ULL_Initialize(const char* providerName);     // Returns 0 on success
void ULL_Shutdown();                               // Clean shutdown
int ULL_GetVersion();                              // Returns 1 (API version)
int ULL_IsConnected();                             // Returns 0 if connected
```

#### Transform Subjects (5 functions)
```cpp
void ULL_RegisterObject(const char* subjectName);
void ULL_RegisterObjectWithProperties(const char* subjectName, const char** propertyNames, int propertyCount);
void ULL_UpdateObject(const char* subjectName, const ULL_Transform* transform);
void ULL_UpdateObjectWithProperties(const char* subjectName, const ULL_Transform* transform, const float* propertyValues, int propertyCount);
void ULL_RemoveObject(const char* subjectName);
```

#### Data Subjects (3 functions)
```cpp
void ULL_RegisterDataSubject(const char* subjectName, const char** propertyNames, int propertyCount);
void ULL_UpdateDataSubject(const char* subjectName, const char** propertyNames, const float* propertyValues, int propertyCount);
void ULL_RemoveDataSubject(const char* subjectName);
```

---

### ULL_Transform Structure

**Definition:**
```cpp
typedef struct {
    double position[3];    // X, Y, Z in centimeters (Unreal coordinates)
    double rotation[4];    // Quaternion X, Y, Z, W (normalized)
    double scale[3];       // X, Y, Z scale factors (typically 1.0)
} ULL_Transform;
```

**Memory Layout:**
- Position: 3 × 8 bytes = 24 bytes
- Rotation: 4 × 8 bytes = 32 bytes
- Scale: 3 × 8 bytes = 24 bytes
- **Total: 80 bytes**

**Validation:**
```cpp
static_assert(sizeof(ULL_Transform) == 80, "Size must match C# marshaling");
```

**C# Verification:**
```csharp
// Verified in Types.cs
Marshal.SizeOf<ULL_Transform>() == 80  // Must match
```

---

### Return Codes

**CRITICAL:** Must match managed layer expectations exactly.

**Standard Constants:**
```cpp
#define ULL_OK                 0    // Success
#define ULL_ERROR             -1    // General error
#define ULL_NOT_CONNECTED     -2    // Not connected to Unreal
#define ULL_NOT_INITIALIZED   -3    // Not initialized

#define ULL_API_VERSION        1    // API version for compatibility
```

**From UnrealLiveLinkNative.cs:**
```csharp
public const int ULL_OK = 0;
public const int ULL_ERROR = -1;
public const int ULL_NOT_CONNECTED = -2;
public const int ULL_NOT_INITIALIZED = -3;
```

**Note:** Mock DLL uses positive error codes (1, 2) for simplicity. Real implementation MUST use negative values as shown above.

---

### Calling Convention & Export

**C API Pattern:**
```cpp
// All functions in UnrealLiveLinkAPI.cpp
extern "C" {
    __declspec(dllexport) int ULL_Initialize(const char* providerName) {
        // Implementation
    }
    
    __declspec(dllexport) void ULL_Shutdown() {
        // Implementation
    }
    
    // ... all 12 functions
}
```

**Key Points:**
- `extern "C"` prevents C++ name mangling
- `__cdecl` calling convention (implicit with extern "C")
- `__declspec(dllexport)` exports function from DLL
- ANSI string marshaling (`const char*`)

**C# P/Invoke Declaration:**
```csharp
[DllImport("UnrealLiveLink.Native.dll", CallingConvention = CallingConvention.Cdecl)]
public static extern int ULL_Initialize(string providerName);
```

---

### Memory Management Rules

**Established by Mock Implementation:**

1. **Input Strings:** Managed layer owns all `const char*` parameters
   - Native receives pointers
   - Native MUST NOT free
   - Lifetime guaranteed for function call duration

2. **Arrays:** Passed as pointer + count
   - No length prefix
   - Native validates count parameter
   - Native does not own or allocate arrays

3. **Structs:** Passed by pointer
   - Managed layer allocates
   - Native reads only
   - No ownership transfer

4. **Return Values:** By value only (integers)
   - No pointers returned
   - No dynamic allocation

---

## Build System

### Build Environment Requirements

**Unreal Engine:**
- Version: 5.6 source build required
- Recommended location: `C:\UE\UE_5.6_Source`
- Size: ~20GB installed
- Type: Source installation (required for UBT)

**Build Tools:**
- Visual Studio 2022 Build Tools (no full IDE required)
- Windows 10/11 SDK
- .NET SDK (for UBT itself)

**Project Structure:**
```
src/Native/UnrealLiveLink.Native/
├── Source/
│   └── UnrealLiveLinkNative/
│       ├── Public/
│       │   └── (headers)
│       ├── Private/
│       │   └── UnrealLiveLinkNative.cpp (WinMain entry point)
│       ├── UnrealLiveLinkNative.Build.cs
│       └── UnrealLiveLinkNative.h
└── UnrealLiveLinkNative.Target.cs
```

---

### Build Process

**Automated Build:**
```powershell
cd C:\repos\SimioUnrealEngineLiveLinkConnector
.\build\BuildNative.ps1
```

**What BuildNative.ps1 Does:**
1. Auto-detects UE installation (searches common paths)
2. Copies source to `[UE_ROOT]\Engine\Source\Programs\UnrealLiveLinkNative\`
3. Runs `GenerateProjectFiles.bat` to update UE5.sln
4. Builds via UBT command line:
   ```
   .\Engine\Build\BatchFiles\Build.bat UnrealLiveLinkNative Win64 Development
   ```
5. Copies output to `lib\native\win-x64\`

**Expected Output:**
- `UnrealLiveLinkNative.dll` (target for DLL configuration)
- `UnrealLiveLinkNative.pdb` (debug symbols)

**Build Times:**
- First build: ~30 seconds
- Incremental: 3-5 seconds

---

### Alternative Build Methods

**Option A: UBT Direct (fastest)**
```powershell
cd C:\UE\UE_5.6_Source
.\Engine\Build\BatchFiles\Build.bat UnrealLiveLinkNative Win64 Development
```

**Option B: MSBuild (VS Code friendly)**
```powershell
.\build\SetupVSEnvironment.ps1
cd C:\UE\UE_5.6_Source
msbuild UE5.sln /t:Programs\UnrealLiveLinkNative /p:Configuration=Development
```

**Option C: Visual Studio IDE (for debugging)**
```powershell
# Generate solution files
cd C:\UE\UE_5.6_Source
.\GenerateProjectFiles.bat

# Open UE5.sln in Visual Studio
# Navigate to: Programs > UnrealLiveLinkNative
# Build project
```

---

### Build Configuration Files

#### UnrealLiveLinkNative.Target.cs

**Location:** `src/Native/UnrealLiveLink.Native/UnrealLiveLinkNative.Target.cs`

**Key Configuration:**
```csharp
using UnrealBuildTool;
using System.Collections.Generic;

public class UnrealLiveLinkNativeTarget : TargetRules
{
    public UnrealLiveLinkNativeTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Program;              // Standalone program
        LinkType = TargetLinkType.Monolithic;   // Single executable/DLL
        LaunchModuleName = "UnrealLiveLinkNative";
        
        bCompileAgainstEngine = false;          // Minimal dependencies
        bCompileAgainstCoreUObject = true;      // Need Core + CoreUObject
        bUseLoggingInShipping = true;           // Enable UE_LOG in release
        
        // For DLL output (configure as needed)
        bShouldCompileAsDLL = true;
    }
}
```

---

#### UnrealLiveLinkNative.Build.cs

**Location:** `src/Native/UnrealLiveLink.Native/Source/UnrealLiveLinkNative/UnrealLiveLinkNative.Build.cs`

**Key Dependencies:**
```csharp
using UnrealBuildTool;

public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        
        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",                    // Unreal fundamentals
            "CoreUObject",             // UObject system
            "LiveLink",                // LiveLink core
            "LiveLinkInterface",       // LiveLink API
            "Messaging",               // Message Bus
            "UdpMessaging"             // Network transport
        });
        
        bEnableExceptions = false;     // Unreal doesn't use exceptions
        bUseRTTI = false;              // No RTTI needed
    }
}
```

**Note:** LiveLink modules should be added incrementally as implementation progresses.

---

## Mock Implementation (Reference Baseline)

### Purpose & Status

**Location:** `src/Native/Mock/MockLiveLink.cpp` (437 lines)

**Purpose:**
- Validates P/Invoke marshaling without Unreal dependency
- Provides behavioral reference for real implementation
- Enables managed layer development and testing
- Fast build/test cycles (5 seconds vs minutes)

**Build:**
```powershell
.\build\BuildMockDLL.ps1
# Output: lib\native\win-x64\UnrealLiveLink.Native.dll (mock version)
```

---

### Key Mock Patterns to Replicate

**Pattern 1: Auto-Registration**
```cpp
// MockLiveLink.cpp lines 186-191
if (g_transformObjects.find(subjectName) == g_transformObjects.end()) {
    g_transformObjects.insert(subjectName);
}
```

**Real Implementation Should:**
```cpp
void FLiveLinkBridge::UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform) {
    FScopeLock Lock(&CriticalSection);
    
    // Auto-register if not registered
    if (!TransformSubjects.Contains(SubjectName)) {
        RegisterTransformSubject(SubjectName);
    }
    
    // Update frame...
}
```

---

**Pattern 2: Property Count Validation**
```cpp
// MockLiveLink.cpp lines 207-215
if (it != g_transformObjectProperties.end()) {
    if (propertyCount != (int)it->second.size()) {
        LogError("Property count mismatch");
        return;
    }
}
```

**Real Implementation Should:**
```cpp
if (const TArray<FName>* RegisteredProps = TransformSubjects.Find(SubjectName)) {
    if (RegisteredProps->Num() != PropertyValues.Num()) {
        UE_LOG(LogTemp, Error, TEXT("Property count mismatch for %s"), *SubjectName.ToString());
        return;
    }
}
```

---

**Pattern 3: State Tracking**
```cpp
// Mock uses global state
static bool g_isInitialized = false;
static std::unordered_set<std::string> g_transformObjects;
static std::unordered_map<std::string, std::vector<std::string>> g_transformObjectProperties;
```

**Real Implementation Should:**
```cpp
// LiveLinkBridge uses member variables with thread safety
class FLiveLinkBridge {
private:
    bool bInitialized = false;
    TMap<FName, TArray<FName>> TransformSubjects;
    TMap<FName, TArray<FName>> DataSubjects;
    FCriticalSection CriticalSection;  // Thread safety
};
```

---

## Implementation Patterns

### Singleton Pattern (LiveLinkBridge)

**Purpose:** Single instance managing all LiveLink connections

**Header (LiveLinkBridge.h):**
```cpp
class FLiveLinkBridge {
public:
    static FLiveLinkBridge& Get();
    
    // Lifecycle
    bool Initialize(const FString& ProviderName);
    void Shutdown();
    bool IsInitialized() const { return bInitialized; }
    
    // Transform subjects
    void RegisterTransformSubject(const FName& SubjectName);
    void UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform);
    // ... etc
    
    // No copy/move
    FLiveLinkBridge(const FLiveLinkBridge&) = delete;
    FLiveLinkBridge& operator=(const FLiveLinkBridge&) = delete;
    
private:
    FLiveLinkBridge() = default;
    
    bool bInitialized = false;
    FString ProviderName;
    FLiveLinkSourceHandle LiveLinkSource;
    
    TMap<FName, TArray<FName>> TransformSubjects;
    TMap<FName, TArray<FName>> DataSubjects;
    TMap<FString, FName> NameCache;
    
    FCriticalSection CriticalSection;
};
```

**Implementation (LiveLinkBridge.cpp):**
```cpp
FLiveLinkBridge& FLiveLinkBridge::Get() {
    static FLiveLinkBridge Instance;
    return Instance;
}
```

**Usage in C API:**
```cpp
extern "C" {
    __declspec(dllexport) int ULL_Initialize(const char* providerName) {
        if (!providerName) return ULL_ERROR;
        
        FString ProviderFString(providerName);
        bool success = FLiveLinkBridge::Get().Initialize(ProviderFString);
        return success ? ULL_OK : ULL_ERROR;
    }
}
```

---

### Thread Safety Pattern

**Why Needed:** LiveLink APIs may be called from multiple threads

**Pattern:**
```cpp
void FLiveLinkBridge::SomeMethod() {
    FScopeLock Lock(&CriticalSection);  // RAII lock - auto-unlocks on scope exit
    
    // All operations here are thread-safe
    // ...
}
```

**Critical Sections:**
- All public methods in FLiveLinkBridge
- Access to TransformSubjects and DataSubjects maps
- LiveLink API calls (use `_AnyThread` variants when available)

---

### FName Caching Pattern

**Purpose:** Optimize repeated string→FName conversions (performance critical for 30-60Hz updates)

**Implementation:**
```cpp
class FLiveLinkBridge {
private:
    TMap<FString, FName> NameCache;
    
public:
    FName GetCachedName(const char* cString) {
        if (!cString) return NAME_None;
        
        FString StringKey(cString);
        if (FName* CachedName = NameCache.Find(StringKey)) {
            return *CachedName;  // Cache hit - fast
        }
        
        FName NewName(cString);
        NameCache.Add(StringKey, NewName);
        return NewName;  // Cache miss - store for next time
    }
};
```

**Usage:**
```cpp
extern "C" {
    void ULL_UpdateObject(const char* subjectName, const ULL_Transform* transform) {
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        // Use SubjectFName...
    }
}
```

---

## LiveLink Integration

### ILiveLinkProvider Creation

**Purpose:** Register as LiveLink source with Message Bus

**Implementation Pattern:**
```cpp
bool FLiveLinkBridge::Initialize(const FString& InProviderName) {
    FScopeLock Lock(&CriticalSection);
    
    if (bInitialized) return true;
    
    // Get LiveLink client
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    if (!Client) {
        UE_LOG(LogTemp, Error, TEXT("Failed to get LiveLink client"));
        return false;
    }
    
    // Create Message Bus source
    TSharedPtr<ILiveLinkSource> Source = MakeShared<FLiveLinkMessageBusSource>(
        FText::FromString(InProviderName));
    
    LiveLinkSource = Client->AddSource(Source);
    
    if (!LiveLinkSource.IsValid()) {
        UE_LOG(LogTemp, Error, TEXT("Failed to create LiveLink source"));
        return false;
    }
    
    ProviderName = InProviderName;
    bInitialized = true;
    
    UE_LOG(LogTemp, Log, TEXT("LiveLink initialized: %s"), *InProviderName);
    return true;
}
```

**Verification:**
- LiveLink source appears in Unreal Editor LiveLink window
- Source name matches Initialize parameter
- Status shows "Connected" (green indicator)

---

### Transform Subject Registration

**Purpose:** Create subject with ULiveLinkTransformRole

**Implementation Pattern:**
```cpp
void FLiveLinkBridge::RegisterTransformSubject(const FName& SubjectName) {
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkSource.IsValid()) return;
    
    // Create static data (structure definition - sent once)
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    // TransformStaticData->PropertyNames can be set here if needed
    
    // Push to LiveLink
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    Client->PushSubjectStaticData_AnyThread(
        LiveLinkSource,
        FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName),
        ULiveLinkTransformRole::StaticClass(),
        MoveTemp(StaticData));
    
    TransformSubjects.Add(SubjectName, TArray<FName>());
    
    UE_LOG(LogTemp, Log, TEXT("Registered transform subject: %s"), *SubjectName.ToString());
}
```

---

### Frame Data Submission

**Purpose:** Update subject with current transform (called 30-60 times per second)

**Implementation Pattern:**
```cpp
void FLiveLinkBridge::UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform) {
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkSource.IsValid()) return;
    
    // Auto-register if needed
    if (!TransformSubjects.Contains(SubjectName)) {
        RegisterTransformSubject(SubjectName);
    }
    
    // Create frame data (per-frame update)
    FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
    FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
    
    TransformFrameData->Transform = Transform;
    TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    // Push frame
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    Client->PushSubjectFrameData_AnyThread(
        LiveLinkSource,
        FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName),
        MoveTemp(FrameData));
}
```

**Performance Note:** `_AnyThread` functions are thread-safe and optimized for high-frequency calls

---

### Transform Subjects with Properties

**Purpose:** Stream both transforms and custom properties (e.g., velocity, status)

**Registration with Properties:**
```cpp
void FLiveLinkBridge::RegisterTransformSubjectWithProperties(
    const FName& SubjectName, 
    const TArray<FName>& PropertyNames) 
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkSource.IsValid()) return;
    
    // Create static data with properties
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    TransformStaticData->PropertyNames = PropertyNames;
    
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    Client->PushSubjectStaticData_AnyThread(
        LiveLinkSource,
        FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName),
        ULiveLinkTransformRole::StaticClass(),
        MoveTemp(StaticData));
    
    TransformSubjects.Add(SubjectName, PropertyNames);
}
```

**Update with Properties:**
```cpp
void FLiveLinkBridge::UpdateTransformSubjectWithProperties(
    const FName& SubjectName, 
    const FTransform& Transform, 
    const TArray<float>& PropertyValues)
{
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
    
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    Client->PushSubjectFrameData_AnyThread(
        LiveLinkSource,
        FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName),
        MoveTemp(FrameData));
}
```

---

### Data Subjects

**Purpose:** Stream metrics/KPIs without 3D transforms

**Key Differences:**
- Use `ULiveLinkBasicRole` instead of `ULiveLinkTransformRole`
- Use `FLiveLinkBaseStaticData` and `FLiveLinkBaseFrameData`
- No Transform - only PropertyNames and PropertyValues arrays

**Implementation Pattern:**
```cpp
void FLiveLinkBridge::UpdateDataSubject(
    const FName& SubjectName,
    const TArray<FName>& PropertyNames,
    const TArray<float>& PropertyValues)
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkSource.IsValid()) return;
    
    // Create frame with properties only
    FLiveLinkFrameDataStruct FrameData(FLiveLinkBaseFrameData::StaticStruct());
    FLiveLinkBaseFrameData* BaseFrameData = FrameData.Cast<FLiveLinkBaseFrameData>();
    
    BaseFrameData->PropertyValues = PropertyValues;
    BaseFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    // Push frame
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    Client->PushSubjectFrameData_AnyThread(
        LiveLinkSource,
        FLiveLinkSubjectKey(LiveLinkSource.GetSourceGuid(), SubjectName),
        MoveTemp(FrameData));
}
```

---

## Coordinate Systems

### Managed Layer Handles Conversion

**From CoordinateConverter.cs (already implemented):**
```csharp
public static double[] SimioPositionToUnreal(double x, double y, double z) {
    return new double[] {
        x * 100.0,      // Simio meters → Unreal centimeters
        -z * 100.0,     // Simio Z → Unreal -Y (axis swap)
        y * 100.0       // Simio Y → Unreal Z (axis swap)
    };
}
```

**Native Layer Receives:**
- Position already in centimeters
- Position already in Unreal coordinate system (X-forward, Y-right, Z-up)
- Rotation already as normalized quaternion

---

### Native Layer Conversion (Direct Pass-Through Expected)

**Implementation (validation needed):**
```cpp
FTransform ConvertULLTransformToUnreal(const ULL_Transform* transform) {
    if (!transform) return FTransform::Identity;
    
    // Direct conversion - no coordinate adjustment needed
    FVector Position(
        transform->position[0],  // X (already centimeters)
        transform->position[1],  // Y (already Unreal Y)
        transform->position[2]); // Z (already Unreal Z)
    
    FQuat Rotation(
        transform->rotation[0],  // X
        transform->rotation[1],  // Y
        transform->rotation[2],  // Z
        transform->rotation[3]); // W (already normalized)
    
    FVector Scale(
        transform->scale[0],
        transform->scale[1],
        transform->scale[2]);
    
    return FTransform(Rotation, Position, Scale);
}
```

**Verification Test:**
- Place object at Simio origin (0, 0, 0)
- Should appear at Unreal origin (0, 0, 0)
- No mirroring or axis flips

---

## Dependency Management (Critical for Deployment)

### Reference Project Warning

> "copying just the dll may not be enough. There may be other dependencies that need to be copied such as the tbbmalloc.dll. Use the Dependencies application to determine what additional dlls are needed."

**Reality:** Native DLL will require additional Unreal Engine DLLs for redistribution.

---

### Dependencies.exe Tool

**Required Tool:** https://github.com/lucasg/Dependencies

**Purpose:** Analyze DLL to identify runtime dependencies

**Usage:**
```powershell
# After building UnrealLiveLinkNative.dll
Dependencies.exe C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll
```

**Expected Dependencies:**
- tbbmalloc.dll (Intel Threading Building Blocks)
- UnrealEditor-Core.dll
- UnrealEditor-CoreUObject.dll
- UnrealEditor-LiveLink.dll
- UnrealEditor-LiveLinkInterface.dll
- UnrealEditor-Messaging.dll
- UnrealEditor-UdpMessaging.dll
- Additional Unreal runtime DLLs

**Package Size:** Expect 50-200MB total (including all dependencies)

---

### Deployment Package Structure

**Target Structure:**
```
lib/native/win-x64/
├── UnrealLiveLinkNative.dll
├── UnrealLiveLinkNative.pdb
├── tbbmalloc.dll
├── UnrealEditor-Core.dll
├── UnrealEditor-CoreUObject.dll
└── [other required DLLs]
```

**Deployment to Simio:**
```
%PROGRAMFILES%\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\
├── SimioUnrealEngineLiveLinkConnector.dll (managed)
├── UnrealLiveLink.Native.dll (native)
├── System.Drawing.Common.dll
└── [Unreal dependency DLLs]
```

**Testing:** Must test on clean machine WITHOUT Unreal Engine installed

---

## Testing Strategy

### Unit Tests (C++)

**Struct Size Validation:**
```cpp
// Compile-time check
static_assert(sizeof(ULL_Transform) == 80, "Size must match C# marshaling");

// Runtime verification (in module startup)
check(sizeof(ULL_Transform) == 80);
UE_LOG(LogTemp, Log, TEXT("ULL_Transform size: %d bytes"), sizeof(ULL_Transform));
```

---

### Integration Tests (C# with Real DLL)

**Prerequisites:**
- Real native DLL built and deployed with dependencies
- Unreal Editor 5.6+ running
- LiveLink window open
- Message Bus Source added to LiveLink

**Test Pattern:**
```csharp
[TestClass]
[TestCategory("RequiresUnrealEditor")]
public class RealNativeIntegrationTests {
    [TestMethod]
    public void TestRealLiveLinkConnection() {
        // Initialize
        int result = UnrealLiveLinkNative.ULL_Initialize("IntegrationTest");
        Assert.AreEqual(0, result, "Initialize should succeed");
        
        // Wait for Message Bus connection
        Thread.Sleep(2000);
        
        // Check connection
        int status = UnrealLiveLinkNative.ULL_IsConnected();
        Assert.AreEqual(0, status, "Should be connected to Unreal Editor");
        
        // Register subject
        UnrealLiveLinkNative.ULL_RegisterObject("TestCube");
        
        // Update transform
        var transform = ULL_Transform.Identity();
        UnrealLiveLinkNative.ULL_UpdateObject("TestCube", ref transform);
        
        // Manual verification: Check LiveLink window for "TestCube" subject
        // Manual verification: TestCube should appear with green status indicator
        
        // Cleanup
        UnrealLiveLinkNative.ULL_Shutdown();
    }
    
    [TestMethod]
    public void TestPerformanceTarget() {
        UnrealLiveLinkNative.ULL_Initialize("PerformanceTest");
        Thread.Sleep(2000);
        
        // Register 100 objects
        for (int i = 0; i < 100; i++) {
            UnrealLiveLinkNative.ULL_RegisterObject($"Obj{i:D3}");
        }
        
        var transform = ULL_Transform.Identity();
        var sw = Stopwatch.StartNew();
        
        // Update all at 30Hz for 10 seconds
        for (int frame = 0; frame < 300; frame++) {
            for (int i = 0; i < 100; i++) {
                transform.position[0] = i * 10 + frame;
                UnrealLiveLinkNative.ULL_UpdateObject($"Obj{i:D3}", ref transform);
            }
            Thread.Sleep(33); // ~30Hz
        }
        
        sw.Stop();
        double avgMs = sw.ElapsedMilliseconds / 300.0;
        Assert.IsTrue(avgMs < 40, $"Average frame time {avgMs}ms exceeds 40ms target");
        
        UnrealLiveLinkNative.ULL_Shutdown();
    }
}
```

---

### Manual Testing Checklist

**Setup:**
1. [ ] Build real native DLL with UBT
2. [ ] Run Dependencies.exe to identify required DLLs
3. [ ] Copy DLL + all dependencies to deployment folder
4. [ ] Launch Unreal Editor
5. [ ] Open Window → LiveLink
6. [ ] Add Message Bus Source

**Basic Connectivity:**
1. [ ] Run Simio model
2. [ ] Verify source appears in LiveLink window
3. [ ] Verify source shows green (connected) status
4. [ ] Verify subjects appear dynamically

**Transform Streaming:**
1. [ ] Create empty actor in Unreal
2. [ ] Add LiveLink Component to actor
3. [ ] Set Subject Name to match Simio object
4. [ ] Play Simio simulation
5. [ ] Verify actor moves in Unreal viewport
6. [ ] Verify position/rotation matches Simio

**Property Streaming:**
1. [ ] Create Blueprint in Unreal
2. [ ] Add "Get LiveLink Property Value" node
3. [ ] Set Subject and Property name
4. [ ] Print value to screen
5. [ ] Play Simio simulation
6. [ ] Verify values update in real-time

**Performance:**
1. [ ] Run 100 objects @ 30Hz for 10 minutes
2. [ ] Monitor frame times (should stay <5ms)
3. [ ] Check memory usage (should be stable)
4. [ ] Verify no frame drops or stuttering

---

## Performance Targets

| Metric | Target | How to Measure |
|--------|--------|----------------|
| Initialization Time | < 2 seconds | From ULL_Initialize to first IsConnected success |
| Update Latency | < 5 ms | Timestamp in Simio vs receipt in Unreal |
| Throughput | 100 objects @ 30Hz | Sustained without frame drops |
| Memory per Object | < 1 KB | Profile with Unreal Insights |
| Build Time (Incremental) | < 10 seconds | Time BuildNative.ps1 after code change |

**Baseline:** Reference project handles "thousands of floats @ 60Hz" successfully  
**Our Target:** 30,000 values/sec (6x lighter than reference)

---

## Common Issues & Solutions

### Build Issues

**Issue: Module not found**
```
Error: Unable to find module 'LiveLinkInterface'
```
**Solution:** Verify module in Build.cs PublicDependencyModuleNames (UE 5.3+ has LiveLink by default)

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
```
IsConnected() returns -2 even with Unreal running
```
**Checklist:**
- [ ] Unreal Editor is actually running
- [ ] LiveLink window is open (Window → LiveLink)
- [ ] Message Bus Source added to LiveLink
- [ ] Waited 1-2 seconds after Initialize
- [ ] Firewall allows UDP on port 6666
- [ ] No other LiveLink source using same name

---

**Issue: Subjects not visible**
```
RegisterObject succeeds but subject doesn't appear in LiveLink window
```
**Checklist:**
- [ ] Subject registered before first update
- [ ] Subject name is unique and valid
- [ ] Provider name matches Initialize parameter
- [ ] Try manual refresh in LiveLink window (circular arrow button)
- [ ] Check UE_LOG output for errors
- [ ] Verify Message Bus Source is green (connected)

---

## Related Documentation

**Project Management:**
- [DevelopmentPlan.md](DevelopmentPlan.md) - Current phase status, milestones, completion tracking
- [Phase6DevelopmentPlan.md](Phase6DevelopmentPlan.md) - Detailed Phase 6 sub-phase breakdown

**System Design:**
- [Architecture.md](Architecture.md) - Overall system architecture and design rationale

**Implementation Guides:**
- [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md) - C# layer patterns and development sequence

**Build & Test:**
- [TestAndBuildInstructions.md](TestAndBuildInstructions.md) - Build commands and testing procedures

**Reference Implementation:**
- `src/Native/Mock/MockLiveLink.cpp` - Working implementation showing expected API behavior