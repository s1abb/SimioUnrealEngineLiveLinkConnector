# Native Layer Development Guide

**Purpose:** Technical reference for C++ native bridge to Unreal LiveLink.  
**Audience:** C++ developers implementing/extending the native layer  
**Scope:** API contract, build procedures, implementation patterns, technical design  
**Not Included:** Project status/phase tracking (see DevelopmentPlan.md)

**Last Updated:** October 20, 2025

**Implementation Status:**
- ✅ Sub-Phase 6.1-6.6: UBT setup, type definitions, C API layer, LiveLinkBridge singleton, LiveLink framework integration, and transform subject registration
- 📋 Sub-Phase 6.7+: Additional properties, data subjects, optimization (planned)

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
// All functions in UnrealLiveLink.API.cpp
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
├── Public/
│   ├── UnrealLiveLink.Native.h
│   ├── UnrealLiveLink.Types.h
│   └── UnrealLiveLink.API.h
├── Private/
│   ├── UnrealLiveLink.Native.cpp (WinMain entry point)
│   ├── UnrealLiveLink.API.cpp (C API implementation)
│   ├── LiveLinkBridge.h
│   ├── LiveLinkBridge.cpp
│   ├── CoordinateHelpers.h
│   └── TypesValidation.cpp
├── UnrealLiveLinkNative.Build.cs
└── UnrealLiveLinkNative.Target.cs
```

**Note:** When BuildNative.ps1 runs, it copies this entire structure to `[UE_ROOT]\Engine\Source\Programs\UnrealLiveLinkNative\` where UBT builds it. No `Source/` subfolder is used - this matches standard UE Program structure (see BlankProgram, etc.).

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
- `UnrealLiveLink.Native.dll` (29.7 MB)
- `UnrealLiveLink.Native.pdb` (debug symbols)

**Build Times:**
- First build: ~120 seconds (2 minutes)
- Incremental: ~10-15 seconds
- Modules compiled: 71 (minimal dependency configuration)

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

**WORKING Configuration (Sub-Phase 6.6):**
```csharp
using UnrealBuildTool;
using System.Collections.Generic;

public class UnrealLiveLinkNativeTarget : TargetRules
{
    public UnrealLiveLinkNativeTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Program;
        LinkType = TargetLinkType.Monolithic;
        LaunchModuleName = "UnrealLiveLinkNative";
        
        // CRITICAL: These settings are required for Program targets with LiveLink
        bCompileAgainstEngine = false;          // Keep minimal - don't pull in 551 modules!
        bCompileAgainstCoreUObject = true;      // Need Core + CoreUObject
        bBuildWithEditorOnlyData = true;        // Required for LiveLink (counter-intuitive but necessary)
        
        bCompileWithPluginSupport = false;      // Not needed for Program targets
        bCompileICU = false;                    // Disable ICU localization
        bUseLoggingInShipping = true;           // Enable UE_LOG in release builds
        
        IncludeOrderVersion = EngineIncludeOrderVersion.Latest;  // Use latest include order
        
        // Output as DLL
        bShouldCompileAsDLL = true;
    }
}
```

**Critical Notes:**
- `bCompileAgainstEngine = false` - Keeps build minimal (71 modules instead of 551)
- `bBuildWithEditorOnlyData = true` - Counter-intuitive but required for Program targets to access LiveLink features
- This configuration came from analyzing the UnrealLiveLinkCInterface reference project

---

#### UnrealLiveLinkNative.Build.cs

**Location:** `src/Native/UnrealLiveLink.Native/UnrealLiveLinkNative.Build.cs`

**WORKING Configuration (Sub-Phase 6.6):**
```csharp
using UnrealBuildTool;

public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        
        // CRITICAL: Use PrivateDependencyModuleNames, not Public
        // This keeps dependencies internal and avoids symbol export issues
        PrivateDependencyModuleNames.AddRange(new string[] 
        {
            "Core",                         // Unreal fundamentals
            "CoreUObject",                  // UObject system
            "ApplicationCore",              // CRITICAL: Provides minimal runtime without full engine
                                           // Contains FMemory_* symbols needed for Program targets
            "LiveLinkInterface",            // LiveLink API types
            "LiveLinkMessageBusFramework",  // Message Bus framework
            "UdpMessaging",                 // Network transport for LiveLink
        });
        
        bEnableExceptions = false;     // Unreal doesn't use exceptions
        bUseRTTI = false;              // No RTTI needed
    }
}
```

**Critical Notes:**
- `ApplicationCore` module is ESSENTIAL - provides FMemory symbols and minimal runtime
- `PrivateDependencyModuleNames` instead of `PublicDependencyModuleNames` - cleaner separation
- This configuration resolves the FMemory linker errors that occur without ApplicationCore

**Why This Works:**
1. **ApplicationCore** - Bridges the gap between Program targets and Engine features without requiring full engine compilation
2. **Private Dependencies** - Keeps LiveLink dependencies internal, not exposed in public API
3. **Minimal Set** - Only includes what's absolutely necessary (71 modules compiled)

---

## Mock Implementation (Reference Baseline)

### Purpose & Status

**Location:** `src/Native/Mock/MockLiveLink.cpp` (437 lines)

**Purpose:**
- Validates P/Invoke marshaling without Unreal dependency
- Provides behavioral reference for real implementation
- Enables managed layer development and testing
- Fast build/test cycles (2 seconds vs 2 minutes)

**Build:**
```powershell
.\build\BuildMockDLL.ps1
# Output: lib\native\win-x64\UnrealLiveLink.Native.dll (mock version, 75 KB)
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

**Real Implementation:**
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

**Pattern 2: Property Count Validation (Enhanced in Sub-Phase 6.4)**
```cpp
// MockLiveLink.cpp lines 207-215
if (it != g_transformObjectProperties.end()) {
    if (propertyCount != (int)it->second.size()) {
        LogError("Property count mismatch");
        return;
    }
}
```

**Real Implementation:**
```cpp
// Using FSubjectInfo struct (Sub-Phase 6.4)
void FLiveLinkBridge::UpdateTransformSubjectWithProperties(
    const FName& SubjectName, 
    const FTransform& Transform,
    const TArray<float>& PropertyValues) {
    
    FScopeLock Lock(&CriticalSection);
    
    if (const FSubjectInfo* SubjectInfo = TransformSubjects.Find(SubjectName)) {
        if (SubjectInfo->ExpectedPropertyCount != PropertyValues.Num()) {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("Property count mismatch for '%s': expected %d, got %d"),
                   *SubjectName.ToString(),
                   SubjectInfo->ExpectedPropertyCount,
                   PropertyValues.Num());
            return;
        }
        
        // Property count matches - proceed with update
    }
}
```

---

**Pattern 3: State Tracking (Sub-Phase 6.4 Enhancement)**
```cpp
// Mock uses global state (simple, but not thread-safe)
static bool g_isInitialized = false;
static std::unordered_set<std::string> g_transformObjects;
static std::unordered_map<std::string, std::vector<std::string>> g_transformObjectProperties;
```

**Real Implementation Uses FSubjectInfo:**
```cpp
// Enhanced structure stores property metadata (Sub-Phase 6.4)
struct FSubjectInfo {
    TArray<FName> PropertyNames;
    int32 ExpectedPropertyCount;
    
    FSubjectInfo() : ExpectedPropertyCount(0) {}
    FSubjectInfo(const TArray<FName>& InPropertyNames) 
        : PropertyNames(InPropertyNames)
        , ExpectedPropertyCount(InPropertyNames.Num()) {}
};

// LiveLinkBridge uses member variables with thread safety
class FLiveLinkBridge {
private:
    bool bInitialized = false;
    TMap<FName, FSubjectInfo> TransformSubjects;  // Enhanced with property tracking
    TMap<FName, FSubjectInfo> DataSubjects;       // Enhanced with property tracking
    TMap<FString, FName> NameCache;                // Added in Sub-Phase 6.4
    FCriticalSection CriticalSection;              // Thread safety
};
```

---

## Implementation Patterns

### Singleton Pattern (LiveLinkBridge)

**Purpose:** Single instance managing all LiveLink connections

**Status:** ✅ Implemented in Sub-Phase 6.4

**Subject Info Structure:**
```cpp
// Track property information with subjects
struct FSubjectInfo {
    TArray<FName> PropertyNames;
    int32 ExpectedPropertyCount;
    
    FSubjectInfo() : ExpectedPropertyCount(0) {}
    FSubjectInfo(const TArray<FName>& InPropertyNames) 
        : PropertyNames(InPropertyNames)
        , ExpectedPropertyCount(InPropertyNames.Num()) {}
};
```

**Header (LiveLinkBridge.h):**
```cpp
class FLiveLinkBridge {
public:
    static FLiveLinkBridge& Get();  // Meyer's singleton
    
    // Lifecycle (idempotent Initialize)
    bool Initialize(const FString& ProviderName);
    void Shutdown();
    bool IsInitialized() const { return bInitialized; }
    int GetConnectionStatus() const;
    
    // Transform subjects (auto-registration on update)
    void RegisterTransformSubject(const FName& SubjectName);
    void RegisterTransformSubjectWithProperties(const FName& SubjectName, const TArray<FName>& PropertyNames);
    void UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform);
    void UpdateTransformSubjectWithProperties(const FName& SubjectName, const FTransform& Transform, const TArray<float>& PropertyValues);
    void RemoveTransformSubject(const FName& SubjectName);
    
    // Data subjects
    void RegisterDataSubject(const FName& SubjectName, const TArray<FName>& PropertyNames);
    void UpdateDataSubject(const FName& SubjectName, const TArray<float>& PropertyValues);
    void RemoveDataSubject(const FName& SubjectName);
    
    // FName caching (performance optimization)
    FName GetCachedName(const char* cString);
    
    // No copy/move
    FLiveLinkBridge(const FLiveLinkBridge&) = delete;
    FLiveLinkBridge& operator=(const FLiveLinkBridge&) = delete;
    
private:
    FLiveLinkBridge() = default;
    
    bool bInitialized = false;
    FString ProviderName;
    FGuid LiveLinkSourceGuid;           // Source identifier (Sub-Phase 6.6)
    bool bLiveLinkSourceCreated = false; // Track if source created (Sub-Phase 6.6)
    
    TMap<FName, FSubjectInfo> TransformSubjects;  // Property tracking
    TMap<FName, FSubjectInfo> DataSubjects;       // Property tracking
    TMap<FString, FName> NameCache;               // UTF8 string → FName cache
    
    mutable FCriticalSection CriticalSection;     // Thread safety
    
    // Helper methods (Sub-Phase 6.6)
    void EnsureLiveLinkSource();  // On-demand source creation
};
```

**Usage in C API:**
```cpp
extern "C" {
    __declspec(dllexport) int ULL_Initialize(const char* providerName) {
        if (!providerName || providerName[0] == '\0') {
            UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_Initialize: Invalid providerName"));
            return ULL_ERROR;
        }
        
        FString ProviderFString = UTF8_TO_TCHAR(providerName);
        bool success = FLiveLinkBridge::Get().Initialize(ProviderFString);
        return success ? ULL_OK : ULL_ERROR;
    }
    
    __declspec(dllexport) int ULL_IsConnected() {
        return FLiveLinkBridge::Get().GetConnectionStatus();
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

**Status:** ✅ Implemented in Sub-Phase 6.4

**Implementation (LiveLinkBridge.cpp):**
```cpp
// Thread-safe FName caching with TMap<FString, FName>
FName FLiveLinkBridge::GetCachedName(const char* cString) {
    FScopeLock Lock(&CriticalSection);  // Thread-safe access to cache
    
    if (!cString || cString[0] == '\0') {
        return NAME_None;
    }
    
    FString StringKey = UTF8_TO_TCHAR(cString);
    
    if (FName* CachedName = NameCache.Find(StringKey)) {
        return *CachedName;  // Cache hit - fast path
    }
    
    // Cache miss - create and store
    FName NewName(*StringKey);
    NameCache.Add(StringKey, NewName);
    
    return NewName;
}
```

**Usage in C API:**
```cpp
extern "C" {
    __declspec(dllexport) void ULL_UpdateObject(
        const char* subjectName,
        const ULL_Transform* transform) {
        
        if (!subjectName || !transform) return;
        
        // Use cached FName instead of creating new one each call
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        
        // Convert ULL_Transform to FTransform
        FTransform UnrealTransform = ConvertToFTransform(transform);
        
        // Update subject
        FLiveLinkBridge::Get().UpdateTransformSubject(SubjectFName, UnrealTransform);
    }
}
```

**Performance Impact:**
- First call: Full string→FName conversion + hash insert
- Subsequent calls: Hash lookup only (much faster)
- Typical simulation: 10-100 subject names, called 30-60 times/second
- Cache hits after first frame reduce CPU overhead significantly

---

## LiveLink Integration

### Custom LiveLink Source (Sub-Phase 6.6)

**Status:** ✅ IMPLEMENTED

**Purpose:** Minimal ILiveLinkSource implementation that avoids plugin header dependencies

**Implementation (LiveLinkBridge.cpp):**
```cpp
// Custom LiveLink source - minimal implementation
// Avoids dependency on FLiveLinkMessageBusSource (plugin-specific header)
class FSimioLiveLinkSource : public ILiveLinkSource
{
public:
    FSimioLiveLinkSource(const FText& InSourceType, const FText& InSourceMachineName)
        : SourceType(InSourceType)
        , SourceMachineName(InSourceMachineName)
    {
    }

    // ILiveLinkSource interface
    virtual void ReceiveClient(ILiveLinkClient* InClient, FGuid InSourceGuid) override
    {
        Client = InClient;
        SourceGuid = InSourceGuid;
    }

    virtual bool IsSourceStillValid() const override { return true; }
    
    virtual bool RequestSourceShutdown() override { return true; }
    
    virtual FText GetSourceType() const override { return SourceType; }
    
    virtual FText GetSourceMachineName() const override { return SourceMachineName; }
    
    virtual FText GetSourceStatus() const override 
    { 
        return FText::FromString(TEXT("Active")); 
    }

private:
    FText SourceType;
    FText SourceMachineName;
    ILiveLinkClient* Client = nullptr;
    FGuid SourceGuid;
};
```

**Why Custom Source:**
- Plugin headers (like `FLiveLinkMessageBusSource`) not accessible to Program targets
- Minimal implementation provides all required functionality
- Validated by 25/25 integration tests passing

---

### LiveLink Source Creation (Sub-Phase 6.6)

**Status:** ✅ IMPLEMENTED

**On-Demand Source Creation:**
```cpp
void FLiveLinkBridge::EnsureLiveLinkSource()
{
    FScopeLock Lock(&CriticalSection);
    
    if (bLiveLinkSourceCreated)
    {
        return;  // Already created
    }
    
    UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("EnsureLiveLinkSource: Creating LiveLink source..."));
    
    // Get LiveLink client via modular features
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    if (!Client)
    {
        UE_LOG(LogUnrealLiveLinkNative, Error, 
               TEXT("❌ EnsureLiveLinkSource: Failed to get ILiveLinkClient"));
        return;
    }
    
    // Create custom LiveLink source
    TSharedPtr<ILiveLinkSource> Source = MakeShared<FSimioLiveLinkSource>(
        FText::FromString(TEXT("Simio")),
        FText::FromString(ProviderName));
    
    // Add source to LiveLink
    LiveLinkSourceGuid = Client->AddSource(Source);
    bLiveLinkSourceCreated = true;
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("✅ EnsureLiveLinkSource: LiveLink source created successfully (GUID: %s)"),
           *LiveLinkSourceGuid.ToString());
}
```

**Required Includes:**
```cpp
#include "ILiveLinkClient.h"
#include "Features/IModularFeatures.h"
#include "LiveLinkTypes.h"
```

**Shutdown Update:**
```cpp
void FLiveLinkBridge::Shutdown()
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized)
    {
        UE_LOG(LogUnrealLiveLinkNative, Warning, TEXT("Shutdown: Not initialized"));
        return;
    }
    
    // Remove LiveLink source if created
    if (bLiveLinkSourceCreated && LiveLinkSourceGuid.IsValid())
    {
        ILiveLinkClient* Client = &IModularFeatures::Get()
            .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
        
        if (Client)
        {
            Client->RemoveSource(LiveLinkSourceGuid);
            UE_LOG(LogUnrealLiveLinkNative, Log, 
                   TEXT("✅ Shutdown: Removed LiveLink source (GUID: %s)"),
                   *LiveLinkSourceGuid.ToString());
        }
        
        LiveLinkSourceGuid.Invalidate();
        bLiveLinkSourceCreated = false;
    }
    
    // Clear all state
    TransformSubjects.Empty();
    DataSubjects.Empty();
    NameCache.Empty();
    ProviderName.Empty();
    bInitialized = false;
    
    UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Shutdown: Complete"));
}
```

---

### Transform Subject Registration (Sub-Phase 6.6)

**Status:** ✅ IMPLEMENTED

**Purpose:** Create subject with ULiveLinkTransformRole

**Implementation:**
```cpp
void FLiveLinkBridge::RegisterTransformSubject(const FName& SubjectName)
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized)
    {
        UE_LOG(LogUnrealLiveLinkNative, Warning, 
               TEXT("RegisterTransformSubject: Not initialized"));
        return;
    }
    
    // Ensure LiveLink source exists
    EnsureLiveLinkSource();
    
    if (!bLiveLinkSourceCreated || !LiveLinkSourceGuid.IsValid())
    {
        UE_LOG(LogUnrealLiveLinkNative, Error, 
               TEXT("RegisterTransformSubject: LiveLink source not created"));
        return;
    }
    
    // Get LiveLink client
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    if (!Client)
    {
        UE_LOG(LogUnrealLiveLinkNative, Error, 
               TEXT("RegisterTransformSubject: Failed to get ILiveLinkClient"));
        return;
    }
    
    // Create static data (structure definition - sent once)
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    
    // Push to LiveLink
    FLiveLinkSubjectKey SubjectKey(LiveLinkSourceGuid, SubjectName);
    Client->PushSubjectStaticData_AnyThread(
        SubjectKey,
        ULiveLinkTransformRole::StaticClass(),
        MoveTemp(StaticData));
    
    // Track in local map (no properties)
    TransformSubjects.Add(SubjectName, FSubjectInfo());
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("✅ RegisterTransformSubject: Registered '%s'"), 
           *SubjectName.ToString());
}
```

**Important Notes:**
- `FLiveLinkStaticDataStruct` - Container for static data (sent once per subject)
- `FLiveLinkTransformStaticData` - Static data for transform subjects
- `ULiveLinkTransformRole::StaticClass()` - Identifies this as a transform subject
- Use `_AnyThread` variants for thread safety

---

### Frame Data Submission (Sub-Phase 6.6)

**Status:** ✅ IMPLEMENTED

**Purpose:** Update subject with current transform (called 30-60 times per second)

**Implementation:**
```cpp
void FLiveLinkBridge::UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform)
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !bLiveLinkSourceCreated)
    {
        return;
    }
    
    // Auto-register if needed
    if (!TransformSubjects.Contains(SubjectName))
    {
        ULL_VERBOSE_LOG(TEXT("UpdateTransformSubject: Auto-registering '%s'"), 
                       *SubjectName.ToString());
        RegisterTransformSubject(SubjectName);
    }
    
    // Get LiveLink client
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    if (!Client)
    {
        return;
    }
    
    // Create frame data (per-frame update)
    FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
    FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
    
    TransformFrameData->Transform = Transform;
    TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    // Push frame
    FLiveLinkSubjectKey SubjectKey(LiveLinkSourceGuid, SubjectName);
    Client->PushSubjectFrameData_AnyThread(SubjectKey, MoveTemp(FrameData));
    
    // Throttled logging (every 60th call)
    static int32 UpdateCount = 0;
    if (++UpdateCount % 60 == 0)
    {
        ULL_VERBOSE_LOG(TEXT("UpdateTransformSubject: Updated '%s' (%d updates)"), 
                       *SubjectName.ToString(), UpdateCount);
    }
}
```

**Performance Note:** `_AnyThread` functions are thread-safe and optimized for high-frequency calls

---

### Transform Subjects with Properties

**Status:** 📋 Planned for Sub-Phase 6.7

**Purpose:** Stream both transforms and custom properties (e.g., velocity, status)

**Registration with Properties:**
```cpp
void FLiveLinkBridge::RegisterTransformSubjectWithProperties(
    const FName& SubjectName, 
    const TArray<FName>& PropertyNames) 
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized) return;
    
    EnsureLiveLinkSource();
    
    if (!bLiveLinkSourceCreated) return;
    
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    if (!Client) return;
    
    // Create static data with properties
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    TransformStaticData->PropertyNames = PropertyNames;
    
    FLiveLinkSubjectKey SubjectKey(LiveLinkSourceGuid, SubjectName);
    Client->PushSubjectStaticData_AnyThread(
        SubjectKey,
        ULiveLinkTransformRole::StaticClass(),
        MoveTemp(StaticData));
    
    // Store property info
    TransformSubjects.Add(SubjectName, FSubjectInfo(PropertyNames));
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
    if (const FSubjectInfo* SubjectInfo = TransformSubjects.Find(SubjectName))
    {
        if (SubjectInfo->ExpectedPropertyCount != PropertyValues.Num())
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("Property count mismatch for %s: expected %d, got %d"), 
                   *SubjectName.ToString(), 
                   SubjectInfo->ExpectedPropertyCount, 
                   PropertyValues.Num());
            return;
        }
    }
    
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    if (!Client) return;
    
    // Create frame with properties
    FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
    FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
    TransformFrameData->Transform = Transform;
    TransformFrameData->PropertyValues = PropertyValues;
    TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    FLiveLinkSubjectKey SubjectKey(LiveLinkSourceGuid, SubjectName);
    Client->PushSubjectFrameData_AnyThread(SubjectKey, MoveTemp(FrameData));
}
```

---

### Data Subjects

**Status:** 📋 Planned for Sub-Phase 6.8

**Purpose:** Stream metrics/KPIs without 3D transforms

**Key Differences:**
- Use `ULiveLinkBasicRole` instead of `ULiveLinkTransformRole`
- Use `FLiveLinkBaseStaticData` and `FLiveLinkBaseFrameData`
- No Transform - only PropertyNames and PropertyValues arrays

**Implementation Pattern:**
```cpp
void FLiveLinkBridge::UpdateDataSubject(
    const FName& SubjectName,
    const TArray<float>& PropertyValues)
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !bLiveLinkSourceCreated) return;
    
    ILiveLinkClient* Client = &IModularFeatures::Get()
        .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
    
    if (!Client) return;
    
    // Create frame with properties only
    FLiveLinkFrameDataStruct FrameData(FLiveLinkBaseFrameData::StaticStruct());
    FLiveLinkBaseFrameData* BaseFrameData = FrameData.Cast<FLiveLinkBaseFrameData>();
    
    BaseFrameData->PropertyValues = PropertyValues;
    BaseFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    // Push frame
    FLiveLinkSubjectKey SubjectKey(LiveLinkSourceGuid, SubjectName);
    Client->PushSubjectFrameData_AnyThread(SubjectKey, MoveTemp(FrameData));
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

### Native Layer Conversion (Direct Pass-Through)

**Implementation:**
```cpp
FTransform ConvertToFTransform(const ULL_Transform* transform) {
    if (!transform) return FTransform::Identity;
    
    // Direct conversion - no coordinate adjustment needed
    // Managed layer already converted to Unreal coordinate system
    FVector Position(
        transform->position[0],  // X (already centimeters, Unreal X)
        transform->position[1],  // Y (already centimeters, Unreal Y)
        transform->position[2]); // Z (already centimeters, Unreal Z)
    
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

**Verification:**
- Place object at Simio origin (0, 0, 0) → appears at Unreal origin (0, 0, 0)
- No additional coordinate conversion needed in native layer

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
Dependencies.exe C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLink.Native.dll
```

**Expected Dependencies:**
- tbbmalloc.dll (Intel Threading Building Blocks)
- UnrealEditor-Core.dll
- UnrealEditor-CoreUObject.dll
- UnrealEditor-ApplicationCore.dll
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
├── UnrealLiveLink.Native.dll
├── UnrealLiveLink.Native.pdb
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

**Current Status:** 25/25 tests passing (100%) with real UE DLL

**Test Categories:**
- Lifecycle operations (Initialize, Shutdown, GetVersion, IsConnected)
- Transform subject registration and updates
- Data subject operations
- Parameter validation
- Thread safety
- Performance (FName caching)

---

### Manual Testing Checklist

**Setup:**
1. [ ] Build real native DLL with UBT
2. [ ] Run Dependencies.exe to identify required DLLs
3. [ ] Copy DLL + all dependencies to deployment folder
4. [ ] Launch Unreal Editor
5. [ ] Open Window → LiveLink
6. [ ] Verify LiveLink source appears

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
| Build Time (Incremental) | < 15 seconds | Time BuildNative.ps1 after code change |

**Baseline:** Reference project handles "thousands of floats @ 60Hz" successfully  
**Our Target:** 30,000 values/sec (6x lighter than reference)

---

## Common Issues & Solutions

### Build Issues

**Issue: FMemory linker errors (RESOLVED)**
```
Error LNK2019: unresolved external symbol "FMemory_Malloc"
Error LNK2019: unresolved external symbol "FMemory_Realloc"
Error LNK2019: unresolved external symbol "FMemory_Free"
```
**Solution:** Add `ApplicationCore` to PrivateDependencyModuleNames and set `bBuildWithEditorOnlyData = true` in Target.cs

---

**Issue: Too many modules compiled (551 modules)**
```
Extremely slow build times (15+ minutes)
```
**Solution:** Set `bCompileAgainstEngine = false` in Target.cs to keep build minimal (71 modules)

---

**Issue: Module not found**
```
Error: Unable to find module 'LiveLinkInterface'
```
**Solution:** Verify module in Build.cs PrivateDependencyModuleNames

---

**Issue: Plugin headers not accessible**
```
Error: Cannot find FLiveLinkMessageBusSource.h
```
**Solution:** Use custom FSimioLiveLinkSource instead (see LiveLink Integration section)

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

**Issue: Subjects not visible**
```
RegisterObject succeeds but subject doesn't appear in LiveLink window
```
**Checklist:**
- [ ] Unreal Editor is running
- [ ] LiveLink window is open (Window → Virtual Production → Live Link)
- [ ] Subject registered and updated at least once
- [ ] Check UE_LOG output for errors
- [ ] Try manual refresh in LiveLink window

---

## Related Documentation

**Project Management:**
- [DevelopmentPlan.md](DevelopmentPlan.md) - Current phase status, milestones, completion tracking

**System Design:**
- [Architecture.md](Architecture.md) - Overall system architecture and design rationale

**Implementation Guides:**
- [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md) - C# layer patterns and development sequence

**Build & Test:**
- [TestAndBuildInstructions.md](TestAndBuildInstructions.md) - Build commands and testing procedures

**Reference Implementation:**
- `src/Native/Mock/MockLiveLink.cpp` - Working mock implementation showing expected API behavior

**Completion Reports:**
- [Sub-Phase6.6-Breakthrough.md](Sub-Phase6.6-Breakthrough.md) - Details on resolving build configuration issues