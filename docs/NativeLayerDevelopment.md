# Native Layer Development Guide

**Purpose:** Technical reference for C++ native bridge to Unreal LiveLink.  
**Audience:** C++ developers implementing/extending the native layer  
**Scope:** API contract, build procedures, implementation patterns, technical design  
**Not Included:** Project status/phase tracking (see DevelopmentPlan.md)

**Last Updated:** October 21, 2025

**Implementation Status:**
- ✅ Sub-Phase 6.1-6.6: UBT setup, type definitions, C API layer, LiveLinkBridge singleton, **GEngineLoop initialization**, LiveLink framework integration, and transform subject registration
- 📋 Sub-Phase 6.7+: Additional properties, data subjects, optimization (planned)

**Critical Context:** DLL hosted in Simio.exe, supports multiple initialization cycles with restart optimization

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

### Critical Adaptations for DLL Context

**Reference Implementation:** Standalone executable (single-use)  
**Our Implementation:** DLL hosted in Simio.exe (multi-use with restart)

**Key Differences:**

| Aspect | Reference | Ours |
|--------|-----------|------|
| **Context** | Standalone .exe | DLL in Simio.exe |
| **Lifecycle** | Single init/shutdown | Multiple init/shutdown cycles |
| **GEngineLoop** | PreInit once | PreInit once, cached via static flag |
| **Shutdown** | AppExit() terminates process | NO AppExit - would kill Simio |
| **Module unloading** | Unload at shutdown | Keep loaded for restart |
| **Init timing** | ~20ms (irrelevant) | First: 21ms, Subsequent: 1ms |
| **Restart support** | N/A | Critical - users run simulations repeatedly |

**Architectural Enabler:** Static initialization flag `bGEngineLoopInitialized`
- Prevents fatal "Delayed Startup phase already run" crash
- Enables 21x faster subsequent initialization
- Makes DLL practical for Simio multi-run scenarios

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

**UE Installation Auto-Detection:**
1. Priority 1: Source build (`C:\UE\UE_5.6_Source`)
2. Priority 2: Binary build (`C:\UE\UE_5.6`)
3. Fallback: Manual `-UEPath` parameter

**What BuildNative.ps1 Does:**
1. Auto-detects UE installation (using priorities above)
2. Copies source to `[UE_ROOT]\Engine\Source\Programs\UnrealLiveLinkNative\`
3. Runs `GenerateProjectFiles.bat` to update UE5.sln (source builds only)
4. Builds via UBT command line:
   ```
   .\Engine\Build\BatchFiles\Build.bat UnrealLiveLinkNative Win64 Development
   ```
5. Copies output to `lib\native\win-x64\`

**Build Output Naming:**
- UBT builds: `UnrealLiveLinkNative.dll` (UE naming convention)
- BuildNative.ps1 copies to: `UnrealLiveLink.Native.dll` (repo convention with dot separator)
- Both refer to the same 28.5 MB DLL
- Also generates: `UnrealLiveLink.Native.pdb` (debug symbols)

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

**WORKING Configuration:**
```csharp
using UnrealBuildTool;
using System.Collections.Generic;

[SupportedPlatforms(UnrealPlatformClass.Desktop)]
public class UnrealLiveLinkNativeTarget : TargetRules
{
    public UnrealLiveLinkNativeTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Program;
        bShouldCompileAsDLL = true;  // DLL output for P/Invoke
        LinkType = TargetLinkType.Monolithic;
        LaunchModuleName = "UnrealLiveLinkNative";
        
        // Minimal program - key to build success
        bBuildDeveloperTools = false;
        bBuildWithEditorOnlyData = true;      // CRITICAL for ApplicationCore
        bCompileAgainstEngine = false;        // CRITICAL - keeps build minimal (71 modules)
        bCompileAgainstCoreUObject = true;
        bCompileWithPluginSupport = false;
        bIncludePluginsForTargetPlatforms = false;
        bCompileICU = false;
        
        // Diagnostics
        bUseLoggingInShipping = true;
        GlobalDefinitions.Add("UE_TRACE_ENABLED=1");
        
        // Latest include order
        IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
    }
}
```

**Critical Notes:**
- `bCompileAgainstEngine = false` - Keeps build minimal (71 modules instead of 551)
- `bBuildWithEditorOnlyData = true` - Counter-intuitive but required for Program targets to access LiveLink features
- `bCompileWithPluginSupport = false` - Not needed for Program targets
- `bIncludePluginsForTargetPlatforms = false` - Reduce build scope
- `bCompileICU = false` - Disable ICU localization
- `bUseLoggingInShipping = true` - Enable UE_LOG in release builds
- `GlobalDefinitions.Add("UE_TRACE_ENABLED=1")` - Enable Unreal Insights tracing
- This configuration came from analyzing the UnrealLiveLinkCInterface reference project

---

#### UnrealLiveLinkNative.Build.cs

**Location:** `src/Native/UnrealLiveLink.Native/UnrealLiveLinkNative.Build.cs`

**WORKING Configuration:**
```csharp
using UnrealBuildTool;

public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        
        // Disable unity builds for cleaner compilation
        bUseUnity = false;
        
        // CRITICAL: Use PrivateDependencyModuleNames, not Public
        // This keeps dependencies internal and avoids symbol export issues
        PrivateDependencyModuleNames.AddRange(new string[] 
        {
            "Core",                         // Unreal fundamentals
            "CoreUObject",                  // UObject system
            "ApplicationCore",              // CRITICAL: Provides minimal runtime without full engine
                                           // Contains FMemory_* symbols needed for Program targets
            "Projects",                     // CRITICAL: Plugin loading system (for IPluginManager)
            "LiveLinkInterface",            // LiveLink API types
            "LiveLinkMessageBusFramework",  // Message Bus framework
            "UdpMessaging",                 // Network transport for LiveLink
        });
        
        // Required for GEngineLoop and module system access
        PrivateIncludePaths.Add("Runtime/Launch/Public");
        
        // Export symbols for DLL
        PublicDefinitions.Add("ULL_API=__declspec(dllexport)");
        
        // Optimize for shipping
        OptimizeCode = CodeOptimization.InShippingBuildsOnly;
        
        bEnableExceptions = false;     // Unreal doesn't use exceptions
        bUseRTTI = false;              // No RTTI needed
    }
}
```

**Critical Notes:**
- `ApplicationCore` module is ESSENTIAL - provides FMemory symbols and minimal runtime
- `Projects` module is ESSENTIAL - provides IPluginManager for plugin loading
- `PrivateIncludePaths` for Launch module - needed for GEngineLoop access
- `PrivateDependencyModuleNames` instead of `PublicDependencyModuleNames` - cleaner separation
- This configuration resolves the FMemory linker errors that occur without ApplicationCore

**Why This Works:**
1. **ApplicationCore** - Bridges the gap between Program targets and Engine features without requiring full engine compilation
2. **Projects** - Enables plugin system for LiveLink plugins
3. **Private Dependencies** - Keeps LiveLink dependencies internal, not exposed in public API
4. **Minimal Set** - Only includes what's absolutely necessary (71 modules compiled)

---

## GEngineLoop Initialization (Critical)

### Overview

**What is GEngineLoop?**
- Unreal Engine's core initialization system
- Manages engine subsystems, module loading, plugin system
- Required before using ANY Unreal APIs (including LiveLink)

**Why We Need It:**
- LiveLink uses UObject system, module system, plugin system
- Cannot call ILiveLinkProvider::CreateLiveLinkProvider() without initialized engine
- Reference implementation uses GEngineLoop - proven pattern

**DLL Context Challenge:**
- Reference: Single initialization per process lifetime (standalone .exe)
- Ours: Multiple initializations per process lifetime (DLL in Simio.exe)
- Problem: Calling GEngineLoop.PreInit() twice causes fatal crash
- Solution: Static flag tracks initialization state

---

### Static Initialization Flag Pattern

**The Critical Discovery:**
```cpp
// Global static flag - persists across DLL function calls
static bool bGEngineLoopInitialized = false;

int ULL_Initialize(const char* providerName) {
    if (bGEngineLoopInitialized) {
        // Already initialized - skip GEngineLoop initialization
        // Native layer remains ready for LiveLink operations
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("ULL_Initialize: Already initialized, skipping GEngineLoop"));
        return ULL_OK;  // Fast path: ~1ms
    }
    
    // First initialization - full engine startup
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("ULL_Initialize: First initialization, starting GEngineLoop..."));
    
    // [GEngineLoop initialization code here]
    
    bGEngineLoopInitialized = true;  // Mark complete
    return ULL_OK;  // Slow path: ~21ms
}
```

**Why Static Flag Works:**
- `static` keyword: Variable persists in DLL's data segment
- Survives across ULL_Initialize() calls
- Survives ULL_Shutdown() (intentional - keep engine alive)
- Reset only when DLL unloads (Simio exits)

**Performance Impact:**
- First call: ~21ms (GEngineLoop.PreInit + module loading)
- Subsequent calls: ~1ms (flag check only)
- **21x speedup for simulation restart scenarios**

---

### Full Initialization Sequence

**Complete ULL_Initialize Implementation:**
```cpp
int ULL_Initialize(const char* providerName) {
    // Validate input
    if (!providerName || providerName[0] == '\0') {
        UE_LOG(LogUnrealLiveLinkNative, Error, 
               TEXT("ULL_Initialize: Invalid providerName"));
        return ULL_ERROR;
    }
    
    // Check if already initialized (restart scenario)
    if (bGEngineLoopInitialized) {
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("ULL_Initialize: Already initialized (restart detected)"));
        
        // LiveLinkBridge handles provider recreation if needed
        FString ProviderFString = UTF8_TO_TCHAR(providerName);
        bool success = FLiveLinkBridge::Get().Initialize(ProviderFString);
        return success ? ULL_OK : ULL_ERROR;
    }
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("========================================"));
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("ULL_Initialize: First initialization..."));
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("========================================"));
    
    // Step 1: Initialize GEngineLoop (core engine systems)
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Step 1: GEngineLoop.PreInit..."));
    int32 Result = GEngineLoop.PreInit(TEXT("UnrealLiveLinkNative -Messaging"));
    if (Result != 0) {
        UE_LOG(LogUnrealLiveLinkNative, Error, 
               TEXT("GEngineLoop.PreInit failed with code %d"), Result);
        return ULL_ERROR;
    }
    
    // Step 2: Initialize target platform manager (required for modules)
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Step 2: GetTargetPlatformManager..."));
    GetTargetPlatformManager();
    
    // Step 3: Process newly loaded UObjects
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Step 3: ProcessNewlyLoadedUObjects..."));
    ProcessNewlyLoadedUObjects();
    
    // Step 4: Start module processing
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Step 4: StartProcessingNewlyLoadedObjects..."));
    FModuleManager::Get().StartProcessingNewlyLoadedObjects();
    
    // Step 5: Load UdpMessaging module (required for LiveLink Message Bus)
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Step 5: Loading UdpMessaging module..."));
    FModuleManager::Get().LoadModule(TEXT("UdpMessaging"));
    
    // Step 6: Load enabled plugins (required for LiveLink plugins)
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Step 6: Loading enabled plugins..."));
    IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::PreDefault);
    
    // Mark initialization complete
    bGEngineLoopInitialized = true;
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("========================================"));
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("GEngineLoop initialization complete!"));
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("========================================"));
    
    // Step 7: Initialize LiveLink provider
    FString ProviderFString = UTF8_TO_TCHAR(providerName);
    bool success = FLiveLinkBridge::Get().Initialize(ProviderFString);
    
    return success ? ULL_OK : ULL_ERROR;
}
```

**Required Includes:**
```cpp
#include "RequiredProgramMainCPPInclude.h"  // For GEngineLoop
#include "Modules/ModuleManager.h"          // For FModuleManager
#include "Interfaces/IPluginManager.h"      // For IPluginManager
```

---

### Shutdown Implementation (DLL-Safe)

**Critical: Do NOT Call AppExit!**
```cpp
void ULL_Shutdown() {
    UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_Shutdown: Starting shutdown..."));
    
    // Shutdown LiveLink provider
    FLiveLinkBridge::Get().Shutdown();
    
    // CRITICAL: Do NOT call these functions in DLL context!
    // These terminate the host process (Simio would crash)
    //
    // DO NOT CALL:
    // - RequestEngineExit(TEXT("ULL_Shutdown"))
    // - FEngineLoop::AppPreExit()
    // - FModuleManager::Get().UnloadModulesAtShutdown()
    // - FEngineLoop::AppExit()
    
    // Keep GEngineLoop initialized (bGEngineLoopInitialized stays true)
    // Keep modules loaded (enables fast restart)
    // Native layer ready for next ULL_Initialize() call
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("ULL_Shutdown: Complete (modules kept loaded for restart)"));
}
```

**Why This Approach:**
1. **DLL Context:** We're loaded in Simio.exe, not standalone
2. **AppExit() terminates process:** Would crash Simio
3. **Module unloading unnecessary:** They'll be reused on restart
4. **Performance benefit:** Subsequent init 21x faster (1ms vs 21ms)

**Reference Implementation Difference:**
- Reference: Standalone .exe, AppExit() acceptable
- Ours: DLL in host process, AppExit() fatal

---

### Required Module Dependencies for GEngineLoop

**From Build.cs (Updated):**
```csharp
PrivateDependencyModuleNames.AddRange(new string[] 
{
    "Core",                         // GEngineLoop, basic types
    "CoreUObject",                  // UObject system
    "ApplicationCore",              // FMemory_* symbols
    "Projects",                     // IPluginManager (for Step 6)
    "LiveLinkInterface",            // LiveLink API
    "LiveLinkMessageBusFramework",  // Message Bus
    "UdpMessaging",                 // UDP transport (loaded at runtime in Step 5)
});

PrivateIncludePaths.Add("Runtime/Launch/Public");  // GEngineLoop access
```

**Why Each Module:**
- **Core**: GEngineLoop, FModuleManager, basic Unreal types
- **CoreUObject**: UObject system (required by LiveLink)
- **ApplicationCore**: FMemory_* functions (required by Program targets)
- **Projects**: IPluginManager (plugin loading in Step 6)
- **LiveLinkInterface**: ILiveLinkProvider and related APIs
- **LiveLinkMessageBusFramework**: Message Bus infrastructure
- **UdpMessaging**: Network transport (dynamically loaded in initialization)

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

**Header (LiveLinkBridge.h) - UPDATED:**
```cpp
class FLiveLinkBridge {
public:
    static FLiveLinkBridge& Get();  // Meyer's singleton
    
    // Lifecycle (idempotent Initialize, DLL-safe Shutdown)
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
    TSharedPtr<ILiveLinkProvider> LiveLinkProvider;  // Created via ILiveLinkProvider::CreateLiveLinkProvider
    
    TMap<FName, FSubjectInfo> TransformSubjects;  // Property tracking
    TMap<FName, FSubjectInfo> DataSubjects;       // Property tracking
    TMap<FString, FName> NameCache;               // UTF8 string → FName cache
    
    mutable FCriticalSection CriticalSection;     // Thread safety
};
```

**Initialization Implementation (LiveLinkBridge.cpp):**
```cpp
bool FLiveLinkBridge::Initialize(const FString& InProviderName) {
    FScopeLock Lock(&CriticalSection);
    
    if (bInitialized) {
        UE_LOG(LogUnrealLiveLinkNative, Warning, 
               TEXT("Initialize: Already initialized with provider '%s'"), 
               *ProviderName);
        
        // Check if provider name changed
        if (ProviderName != InProviderName) {
            UE_LOG(LogUnrealLiveLinkNative, Log, 
                   TEXT("Initialize: Provider name changed from '%s' to '%s', recreating..."),
                   *ProviderName, *InProviderName);
            
            // Reset and recreate with new name
            LiveLinkProvider.Reset();
            bInitialized = false;
            ProviderName = InProviderName;
        } else {
            // Same provider name - reuse existing provider
            return true;
        }
    } else {
        ProviderName = InProviderName;
    }
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Initialize: Creating LiveLink provider '%s'..."), 
           *ProviderName);
    
    // Create LiveLink provider (uses Message Bus internally)
    LiveLinkProvider = ILiveLinkProvider::CreateLiveLinkProvider(ProviderName);
    
    if (!LiveLinkProvider.IsValid()) {
        UE_LOG(LogUnrealLiveLinkNative, Error, 
               TEXT("Initialize: Failed to create LiveLink provider"));
        return false;
    }
    
    bInitialized = true;
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("✅ Initialize: LiveLink provider created successfully"));
    
    return true;
}
```

**Shutdown Implementation (DLL-Safe):**
```cpp
void FLiveLinkBridge::Shutdown() {
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized) {
        UE_LOG(LogUnrealLiveLinkNative, Warning, TEXT("Shutdown: Not initialized"));
        return;
    }
    
    UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Shutdown: Cleaning up..."));
    
    // Remove all subjects
    for (const auto& Pair : TransformSubjects) {
        LiveLinkProvider->RemoveSubject(Pair.Key);
    }
    TransformSubjects.Empty();
    
    for (const auto& Pair : DataSubjects) {
        LiveLinkProvider->RemoveSubject(Pair.Key);
    }
    DataSubjects.Empty();
    
    // Clear name cache
    NameCache.Empty();
    
    // Reset provider (releases Message Bus connection)
    LiveLinkProvider.Reset();
    
    ProviderName.Empty();
    bInitialized = false;
    
    // NOTE: GEngineLoop and modules stay initialized (bGEngineLoopInitialized stays true)
    // This enables fast restart (1ms vs 21ms)
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("✅ Shutdown: Complete (engine kept alive for restart)"));
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
- LiveLink API calls (ILiveLinkProvider methods are thread-safe)

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

### ILiveLinkProvider API (Working Implementation)

**Status:** ✅ IMPLEMENTED

**Purpose:** Create LiveLink provider that communicates via Message Bus

**API Usage:**
```cpp
// In FLiveLinkBridge::Initialize()
TSharedPtr<ILiveLinkProvider> LiveLinkProvider = 
    ILiveLinkProvider::CreateLiveLinkProvider(ProviderName);
```

**Why This API:**
- Validated by reference implementation
- Handles Message Bus creation internally (UDP multicast 230.0.0.1:6666)
- Thread-safe by design
- Cross-version compatible (stable since UE 4.26)
- No plugin-specific headers required (works in Program targets)

**Required Include:**
```cpp
#include "ILiveLinkProvider.h"
```

**Alternative (NOT Used):** `FLiveLinkMessageBusSource` - plugin-specific header, not accessible to Program targets

---

### Transform Subject Registration

**Status:** ✅ IMPLEMENTED

**Purpose:** Create subject with transform role

**Implementation:**
```cpp
void FLiveLinkBridge::RegisterTransformSubject(const FName& SubjectName)
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkProvider.IsValid())
    {
        UE_LOG(LogUnrealLiveLinkNative, Warning, 
               TEXT("RegisterTransformSubject: Not initialized"));
        return;
    }
    
    // Create static data (structure definition - sent once per subject)
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    
    // Push to LiveLink
    LiveLinkProvider->UpdateSubjectStaticData(
        SubjectName,
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
- `UpdateSubjectStaticData` is thread-safe

---

### Frame Data Submission

**Status:** ✅ IMPLEMENTED

**Purpose:** Update subject with current transform (called 30-60 times per second)

**Implementation:**
```cpp
void FLiveLinkBridge::UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform)
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized || !LiveLinkProvider.IsValid())
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
    
    // Create frame data (per-frame update)
    FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
    FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
    
    TransformFrameData->Transform = Transform;
    TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    // Push frame (thread-safe)
    LiveLinkProvider->UpdateSubjectFrameData(SubjectName, MoveTemp(FrameData));
    
    // Throttled logging (every 60th call)
    static int32 UpdateCount = 0;
    if (++UpdateCount % 60 == 0)
    {
        ULL_VERBOSE_LOG(TEXT("UpdateTransformSubject: Updated '%s' (%d updates)"), 
                       *SubjectName.ToString(), UpdateCount);
    }
}
```

**Performance Note:** `UpdateSubjectFrameData` is thread-safe and optimized for high-frequency calls

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
    
    if (!bInitialized || !LiveLinkProvider.IsValid()) return;
    
    // Create static data with properties
    FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
    FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
    TransformStaticData->PropertyNames = PropertyNames;
    
    LiveLinkProvider->UpdateSubjectStaticData(
        SubjectName,
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
    
    // Create frame with properties
    FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
    FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
    TransformFrameData->Transform = Transform;
    TransformFrameData->PropertyValues = PropertyValues;
    TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    LiveLinkProvider->UpdateSubjectFrameData(SubjectName, MoveTemp(FrameData));
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
    
    if (!bInitialized || !LiveLinkProvider.IsValid()) return;
    
    // Create frame with properties only
    FLiveLinkFrameDataStruct FrameData(FLiveLinkBaseFrameData::StaticStruct());
    FLiveLinkBaseFrameData* BaseFrameData = FrameData.Cast<FLiveLinkBaseFrameData>();
    
    BaseFrameData->PropertyValues = PropertyValues;
    BaseFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
    
    // Push frame
    LiveLinkProvider->UpdateSubjectFrameData(SubjectName, MoveTemp(FrameData));
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

### Our Approach: PATH-Based Dependency Resolution

Instead of packaging hundreds of megabytes of Unreal Engine DLLs with our connector, we solved the dependency problem by adding the UE binaries directory to PATH at runtime.

**What We Do:**
```ps1
# In BuildNative.ps1, RunIntegrationTests.ps1, and deployment scripts
$UEBinPath = "C:\UE\UE_5.6_Source\Engine\Binaries\Win64"
$env:PATH = "$UEBinPath;$env:PATH"
```

**Why This Works:**

Windows DLL Search Order: When loading a DLL, Windows searches:
1. Application directory
2. System directory
3. Directories in PATH ← **We leverage this**

Our 28.5 MB DLL depends on UE DLLs (Core, CoreUObject, LiveLink, etc.). At runtime, Windows finds those dependencies in the UE installation via PATH. No redistribution needed - we ship only our 28.5 MB DLL.

**Benefits:**
- ✅ Tiny package: 28.5 MB instead of 50-200 MB
- ✅ No version conflicts: Uses exact UE version from user's installation
- ✅ Simpler updates: Update UE separately from our connector
- ✅ Legal compliance: No redistribution of Epic's binaries

**Trade-off:**
- ❌ Requires UE 5.6 installation on target machine (but that's already a requirement for development workflow)

---

### Dependencies.exe Tool (Optional Analysis)

**Tool:** https://github.com/lucasg/Dependencies

**Purpose:** Analyze DLL to identify runtime dependencies (for reference/debugging)

**Usage:**
```powershell
# After building UnrealLiveLinkNative.dll
Dependencies.exe C:\UE\UE_5.6_Source\Engine\Binaries\Win64\UnrealLiveLinkNative.dll
```

**Expected Dependencies (resolved via PATH):**
- UnrealEditor-Core.dll
- UnrealEditor-CoreUObject.dll
- UnrealEditor-ApplicationCore.dll
- UnrealEditor-LiveLinkInterface.dll
- UnrealEditor-Messaging.dll
- UnrealEditor-UdpMessaging.dll
- tbbmalloc.dll (Intel Threading Building Blocks)
- Additional Unreal runtime DLLs

**Total Size if Packaged:** 50-200MB (which is why we use PATH instead)

---

### Deployment Package Structure

**Actual Structure (PATH-Based):**
```
lib/native/win-x64/
├── UnrealLiveLink.Native.dll (28.5 MB) ← Only this ships
└── UnrealLiveLink.Native.pdb (debug symbols)
```

**Deployment to Simio:**
```
%PROGRAMFILES%\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\
├── SimioUnrealEngineLiveLinkConnector.dll (managed)
├── UnrealLiveLink.Native.dll (native, 28.5 MB)
└── System.Drawing.Common.dll
```

**Deployment Requirements:**
1. User must have Unreal Engine 5.6 installed
2. Deployment script adds `C:\UE\UE_5.6_Source\Engine\Binaries\Win64` to system PATH
3. OR Simio launch script sets PATH before loading DLL

**Testing:** Test on machine WITH UE installed but WITHOUT bundled dependencies to verify PATH resolution works

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

**Current Status:** All tests passing (100%) with real UE DLL

**Test Categories:**
- Lifecycle operations (Initialize, Shutdown, GetVersion, IsConnected)
- Transform subject registration and updates
- Data subject operations
- Parameter validation
- Thread safety
- Performance (FName caching)
- **Restart scenarios (NEW - Critical)**

**Restart Testing (NEW):**
```csharp
[TestMethod]
public void NativeLayer_MultipleInitialize_ShouldNotCrash()
{
    // Simulate 10 simulation runs
    for (int i = 0; i < 10; i++)
    {
        int result = UnrealLiveLinkNative.ULL_Initialize("RestartTest");
        Assert.AreEqual(UnrealLiveLinkNative.ULL_OK, result);
        
        // Use LiveLink (create/update objects)
        // ...
        
        UnrealLiveLinkNative.ULL_Shutdown();
    }
    
    // Should complete without crashes, memory leaks, or performance degradation
}

[TestMethod]
public void NativeLayer_FirstInitialization_ShouldBeSlow()
{
    var stopwatch = Stopwatch.StartNew();
    UnrealLiveLinkNative.ULL_Initialize("PerfTest");
    stopwatch.Stop();
    
    // First init: ~21ms (GEngineLoop initialization)
    Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 15 && 
                  stopwatch.ElapsedMilliseconds <= 50);
    
    UnrealLiveLinkNative.ULL_Shutdown();
}

[TestMethod]
public void NativeLayer_SubsequentInitialization_ShouldBeFast()
{
    // First init
    UnrealLiveLinkNative.ULL_Initialize("PerfTest");
    UnrealLiveLinkNative.ULL_Shutdown();
    
    // Second init (static flag bypass)
    var stopwatch = Stopwatch.StartNew();
    UnrealLiveLinkNative.ULL_Initialize("PerfTest");
    stopwatch.Stop();
    
    // Subsequent: ~1ms (21x faster)
    Assert.IsTrue(stopwatch.ElapsedMilliseconds <= 5);
    
    UnrealLiveLinkNative.ULL_Shutdown();
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

**Restart Stability (NEW - Critical):**
1. [ ] Run simulation in Simio
2. [ ] Stop simulation
3. [ ] Run simulation again (10+ times)
4. [ ] Verify first run: ~26ms startup (21ms native + 5ms managed)
5. [ ] Verify subsequent runs: ~3ms startup (1ms native + 2ms managed)
6. [ ] Verify no memory leaks (check Task Manager)
7. [ ] Verify consistent performance (no degradation after 10+ runs)

---

## Performance Targets

| Metric | Target | How to Measure | Notes |
|--------|--------|----------------|-------|
| **First Initialization** | < 50ms | Timestamp ULL_Initialize entry/exit | ~21ms native + ~5ms managed = ~26ms typical |
| **Subsequent Initialization** | < 5ms | Timestamp after first Shutdown | ~1ms native (static flag) + ~2ms managed = ~3ms typical |
| **Update Latency** | < 5 ms | Timestamp in Simio vs receipt in Unreal | Per-frame overhead |
| **Throughput** | 100 objects @ 30Hz | Sustained without frame drops | 30,000 values/sec target |
| **Memory per Object** | < 1 KB | Profile with Unreal Insights | Subject + frame data |
| **Build Time (Incremental)** | < 15 seconds | Time BuildNative.ps1 after code change | Hot compilation |

**Baseline:** Reference project handles "thousands of floats @ 60Hz" successfully  
**Our Target:** 30,000 values/sec (6x lighter than reference)

**Restart Performance (NEW):**
- **First init:** ~21ms (GEngineLoop.PreInit + module loading)
- **Subsequent init:** ~1ms (static flag bypass)
- **Speedup:** 21x faster restart
- **User Impact:** Subsequent simulation runs nearly instant

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

**Issue: GEngineLoop.PreInit not accessible**
```
Error: 'GEngineLoop' undeclared identifier
```
**Solution:** Add `PrivateIncludePaths.Add("Runtime/Launch/Public");` to Build.cs and include `RequiredProgramMainCPPInclude.h`

---

**Issue: IPluginManager not found**
```
Error: Cannot find IPluginManager
```
**Solution:** Add `Projects` module to PrivateDependencyModuleNames in Build.cs

---

### Runtime Issues

**Issue: DLL not found from Simio**
```
DllNotFoundException: UnrealLiveLink.Native.dll
```
**Solutions:**
1. Copy DLL to same folder as managed DLL
2. Verify 64-bit DLL for 64-bit process
3. Ensure UE binaries directory is in PATH

---

**Issue: DLL loads but fails at runtime with missing UE dependencies**
```
System.Exception: Unable to load DLL 'UnrealLiveLink.Native.dll': 
The specified module could not be found (or its dependencies)
```
**Solution:** Add UE binaries directory to PATH before loading DLL:
```ps1
$env:PATH = "C:\UE\UE_5.6_Source\Engine\Binaries\Win64;$env:PATH"
```
Or ensure deployment script sets PATH automatically. Use Dependencies.exe to identify missing DLLs.

---

**Issue: Crash on second ULL_Initialize call**
```
Fatal error: Delayed Startup phase already run
```
**Solution:** Implement static initialization flag `bGEngineLoopInitialized` to skip GEngineLoop.PreInit on subsequent calls

---

**Issue: Simio crashes on simulation restart**
```
Access violation or process termination
```
**Solution:** Remove `AppExit()`, `RequestEngineExit()`, and module unloading from ULL_Shutdown(). These terminate the host process.

---

**Issue: Subjects not visible**
```
RegisterObject succeeds but subject doesn't appear in LiveLink window
```
**Checklist:**
- [ ] Unreal Editor is running
- [ ] LiveLink window is open (Window → Virtual Production → Live Link)
- [ ] GEngineLoop properly initialized (check UE_LOG output)
- [ ] UdpMessaging module loaded (Step 5 of initialization)
- [ ] Plugins loaded (Step 6 of initialization)
- [ ] Subject registered AND updated at least once
- [ ] Check UE_LOG output for errors
- [ ] Try manual refresh in LiveLink window

---

**Issue: Slow performance after first initialization**
```
Second simulation run still takes 20ms to start
```
**Solution:** Verify static flag `bGEngineLoopInitialized` is NOT being reset in Shutdown(). Should stay true across restarts.

**Issue: Dependencies.exe shows many missing DLLs**
```
tbbmalloc.dll, UnrealEditor-Core.dll, UnrealEditor-CoreUObject.dll, etc.
```
**Solution:** This is expected with PATH-based approach. Don't package these DLLs - they're resolved via PATH at runtime. Only ship UnrealLiveLink.Native.dll (28.5 MB).

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
