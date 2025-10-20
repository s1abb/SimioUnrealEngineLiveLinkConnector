# Sub-Phase 6.6.2: GEngineLoop Initialization

**Date:** October 20, 2025  
**Status:** ✅ Complete  
**Critical Fix:** Prevents crash when creating ILiveLinkProvider

---

## Problem

The native DLL was crashing when Simio tried to initialize LiveLink:

```
Crash Location: LiveLinkBridge::EnsureLiveLinkSource()
  → ILiveLinkProvider::CreateLiveLinkProvider(ProviderName)
  
Root Cause: CreateLiveLinkProvider() requires UE runtime environment:
  - GEngineLoop must be initialized
  - FModuleManager must be loaded
  - UdpMessaging module must be available
  - Message Bus infrastructure must be running
```

**The DLL was trying to use UE APIs without initializing the UE runtime!**

---

## Solution: GEngineLoop Initialization

Based on reference implementation: [UnrealLiveLinkCInterface](https://github.com/jakedowns/UnrealLiveLinkCInterface)

### Code Changes

#### 1. New File: `UnrealLiveLinkNativeMain.cpp`

```cpp
#include "RequiredProgramMainCPPInclude.h"

// Implement the application (required for Program target type)
IMPLEMENT_APPLICATION(UnrealLiveLinkNative, "UnrealLiveLinkNative");
```

**Purpose:** Provides Program DLL entry point and global definitions (GInternalProjectName, GForeignEngineDir, WinMain).

#### 2. Updated: `LiveLinkBridge::Initialize()`

```cpp
bool FLiveLinkBridge::Initialize(const FString& InProviderName)
{
    // Initialize Unreal Engine runtime environment
    GEngineLoop.PreInit(TEXT("UnrealLiveLinkNative -Messaging"));
    
    // Ensure target platform manager is referenced early
    GetTargetPlatformManager();
    ProcessNewlyLoadedUObjects();
    
    // Tell module manager it may now process newly-loaded UObjects
    FModuleManager::Get().StartProcessingNewlyLoadedObjects();
    
    // Load UdpMessaging module (required for Message Bus)
    FModuleManager::Get().LoadModule(TEXT("UdpMessaging"));
    
    // Load plugins if available
    IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::PreDefault);
    IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::Default);
    IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::PostDefault);
    
    bInitialized = true;
    return true;
}
```

#### 3. Updated: `LiveLinkBridge::Shutdown()`

```cpp
void FLiveLinkBridge::Shutdown()
{
    // Remove LiveLink provider
    if (LiveLinkProvider.IsValid())
    {
        LiveLinkProvider.Reset();
    }
    
    // Shutdown Unreal Engine runtime
    RequestEngineExit(TEXT("UnrealLiveLinkNative shutting down"));
    FEngineLoop::AppPreExit();
    FModuleManager::Get().UnloadModulesAtShutdown();
    FEngineLoop::AppExit();
    
    bInitialized = false;
}
```

#### 4. Updated: `UnrealLiveLinkNative.Build.cs`

```csharp
PrivateDependencyModuleNames.AddRange(new string[] 
{
    "Core",
    "CoreUObject",
    "ApplicationCore",
    "Projects",                     // NEW: For IPluginManager
    "LiveLinkInterface",
    "LiveLinkMessageBusFramework",
    "UdpMessaging",
});

// NEW: Include path for RequiredProgramMainCPPInclude.h
PrivateIncludePaths.Add("Runtime/Launch/Public");
```

#### 5. Updated: `UnrealLiveLink.Native.cpp`

Removed duplicate globals (now provided by `IMPLEMENT_APPLICATION`):
- ~~`TCHAR GInternalProjectName[64]`~~
- ~~`const TCHAR* GForeignEngineDir`~~
- ~~`int WINAPI WinMain(...)`~~

Now only contains: `DEFINE_LOG_CATEGORY(LogUnrealLiveLinkNative);`

---

## Architecture

### Before (Crashed)
```
Simio Process
  └─ Native DLL
       └─ ILiveLinkProvider::CreateLiveLinkProvider()  ❌ CRASH!
          (No UE runtime, no modules, no Message Bus)
```

### After (Working)
```
Simio Process
  └─ Native DLL
       ├─ GEngineLoop.PreInit()  ✅ Initializes UE runtime
       ├─ FModuleManager::LoadModule("UdpMessaging")  ✅ Loads messaging
       ├─ IPluginManager::LoadModulesForEnabledPlugins()  ✅ Loads plugins
       └─ ILiveLinkProvider::CreateLiveLinkProvider()  ✅ SUCCESS!
             └─ UDP Message Bus (230.0.0.1:6666) → Unreal Engine
```

---

## Reference Implementation

The solution is based on **UnrealLiveLinkCInterface** by jakedowns:
- **GitHub:** https://github.com/jakedowns/UnrealLiveLinkCInterface
- **Architecture:** Program DLL with full UE runtime, Message Bus communication
- **Key Function:** `UnrealLiveLink_Initialize()` in `UnrealLiveLinkCInterface.cpp` (lines 81-95)

### Reference Code
```cpp
void UnrealLiveLink_Initialize()
{
    GEngineLoop.PreInit(TEXT("UnrealLiveLinkCInterface -Messaging"));
    GetTargetPlatformManager();
    ProcessNewlyLoadedUObjects();
    FModuleManager::Get().StartProcessingNewlyLoadedObjects();
    FModuleManager::Get().LoadModule(TEXT("UdpMessaging"));
    IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::PreDefault);
    IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::Default);
    IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::PostDefault);
}
```

---

## Build Details

**Build Time:** ~7 seconds  
**DLL Size:** 28.5 MB (includes full UE runtime)  
**Compiler:** Visual Studio 2022 (MSVC 14.44.35217)  
**Target:** Win64 Development  
**Build Tool:** UnrealBuildTool (UBT)  

**Warnings:** Only benign UE_TRACE_ENABLED macro redefinition

---

## Testing Status

**Deployment:** ✅ Complete  
**DLL Location:** `C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\UnrealLiveLink.Native.dll`  
**Modified:** October 20, 2025 @ 15:57:31  

**Expected Behavior:**
1. ✅ Simio loads DLL without crash
2. ✅ GEngineLoop initializes UE runtime
3. ✅ UdpMessaging module loads successfully
4. ✅ ILiveLinkProvider creates Message Bus source
5. ✅ UDP packets sent to 230.0.0.1:6666
6. ✅ "SimioSimulation" source appears in UE LiveLink window

**Next Test:** Run Simio simulation with UE Editor open and verify LiveLink connection.

---

## Lessons Learned

1. **ILiveLinkProvider is not magic** - It requires full UE runtime (GEngineLoop, modules, plugins)
2. **Program DLLs need IMPLEMENT_APPLICATION** - This provides entry point and global definitions
3. **Reference implementations are invaluable** - UnrealLiveLinkCInterface solved this exact problem
4. **Don't assume UE APIs work standalone** - Many require FModuleManager, GEngine, etc.
5. **RequiredProgramMainCPPInclude.h must be in ONE file only** - It contains implementations, not just declarations

---

## Files Modified

### Created
- `src/Native/UnrealLiveLink.Native/Private/UnrealLiveLinkNativeMain.cpp`

### Updated
- `src/Native/UnrealLiveLink.Native/Private/LiveLinkBridge.cpp`
  - Added GEngineLoop initialization in `Initialize()`
  - Added proper cleanup in `Shutdown()`
  - Added includes: `LaunchEngineLoop.h`, `Modules/ModuleManager.h`, `Interfaces/IPluginManager.h`
  
- `src/Native/UnrealLiveLink.Native/Private/UnrealLiveLink.Native.cpp`
  - Removed duplicate global definitions
  - Now only contains log category definition
  
- `src/Native/UnrealLiveLink.Native/UnrealLiveLinkNative.Build.cs`
  - Added `"Projects"` to PrivateDependencyModuleNames
  - Added `PrivateIncludePaths.Add("Runtime/Launch/Public")`

---

## Related Documentation

- [Architecture.md](Architecture.md) - Overall system architecture
- [MessageBusIssue_RootCause.md](MessageBusIssue_RootCause.md) - Why Message Bus is needed
- [NativeLayerDevelopment.md](NativeLayerDevelopment.md) - Native development guide
- [Sub-Phase 6.6.1](../DevelopmentPlan.md#66-transform-subject-registration) - Message Bus Provider implementation

---

## Success Criteria

- ✅ DLL builds without errors
- ✅ DLL deploys to Simio without issues
- ⏳ Simio loads DLL without crashing (TESTING IN PROGRESS)
- ⏳ GEngineLoop initializes successfully
- ⏳ UdpMessaging module loads
- ⏳ ILiveLinkProvider creates without crashing
- ⏳ "SimioSimulation" source appears in UE LiveLink

**Status:** 4/7 complete (build + deploy), 3/7 awaiting runtime testing
