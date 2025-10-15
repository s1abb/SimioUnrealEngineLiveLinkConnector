# Native Layer Development Plan - Simio-Specific

## Overview
This document outlines the implementation of a **simplified, purpose-built** LiveLink native bridge for the SimioUnrealEngineLiveLinkConnector. This is a clean-slate design focused exclusively on Simio's requirements.

**Key Design Principles:**
- **Minimal API surface** - only what Simio needs
- **Two subject types** - Transform objects and Data-only subjects
- **Modern C++** - clean internal implementation
- **Simple P/Invoke** - straightforward C# interop
- **No legacy baggage** - no unused features

---

## Capabilities

### Transform Subjects
Stream 3D objects with position, rotation, and scale:
- Position (X, Y, Z) in centimeters
- Rotation as quaternion (X, Y, Z, W)
- Scale (X, Y, Z)
- Optional: Custom float properties (e.g., speed, load, battery level)

**Use Case:** Simio entities moving in 3D space (vehicles, robots, pallets)

### Data Subjects (NEW!)
Stream pure simulation metrics without 3D representation:
- Subject name is user-defined (e.g., "SystemMetrics", "ProductionKPIs")
- Array of named float properties
- No transform data (uses identity transform internally)

**Use Case:** Dashboard metrics, KPIs, system state (throughput, utilization, queue depths)

---

## File Structure

```
src/Native/UnrealLiveLink.Native/
├── Public/
│   ├── UnrealLiveLink.Native.h          # C API declarations
│   └── UnrealLiveLink.Types.h           # C-compatible structs
│
├── Private/
│   ├── UnrealLiveLink.Native.cpp        # C API implementations
│   ├── LiveLinkBridge.h                 # C++ wrapper class
│   ├── LiveLinkBridge.cpp               # Core LiveLink logic
│   └── CoordinateHelpers.h              # Utility functions (optional)
│
├── UnrealLiveLink.Native.Build.cs       # UBT build config
└── UnrealLiveLink.Native.Target.cs      # UBT target config
```

---

## Phase 1: Project Configuration

### 1.1 UnrealLiveLink.Native.Target.cs

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
        
        // Minimal configuration
        bBuildDeveloperTools = false;
        bUseMallocProfiler = false;
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

---

### 1.2 UnrealLiveLink.Native.Build.cs

```csharp
using UnrealBuildTool;

public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        
        // Required Unreal modules for LiveLink
        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "LiveLink",
            "LiveLinkInterface",
            "Messaging",
            "UdpMessaging"
        });
        
        PrivateDependencyModuleNames.AddRange(new string[]
        {
            "Sockets",
            "Networking"
        });
        
        // Standard settings
        bEnableExceptions = false;
        bUseRTTI = false;
        
        PublicDefinitions.Add("ULL_EXPORTS=1");
    }
}
```

---

## Phase 2: Public API (C Interface)

### 2.1 UnrealLiveLink.Types.h
**Purpose:** Minimal C-compatible data structures for P/Invoke

```cpp
#ifndef UNREAL_LIVELINK_TYPES_H
#define UNREAL_LIVELINK_TYPES_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// API version
#define ULL_API_VERSION 1

// Return codes
#define ULL_OK              0
#define ULL_ERROR          -1
#define ULL_NOT_CONNECTED  -2
#define ULL_NOT_INITIALIZED -3

/**
 * Transform structure for 3D objects
 * All values are doubles for C# interop simplicity
 */
struct ULL_Transform
{
    double position[3];     // X, Y, Z in centimeters
    double rotation[4];     // Quaternion: X, Y, Z, W
    double scale[3];        // Scale: X, Y, Z
};

#ifdef __cplusplus
}
#endif

#endif // UNREAL_LIVELINK_TYPES_H
```

**Design Notes:**
- Using `double` for all values (easier C# marshaling than mixed float/double)
- No complex metadata, timecode, or role-specific structs
- Properties passed as separate arrays (not embedded in struct)

---

### 2.2 UnrealLiveLink.Native.h
**Purpose:** Clean C API for P/Invoke

```cpp
#ifndef UNREAL_LIVELINK_NATIVE_H
#define UNREAL_LIVELINK_NATIVE_H

#include "UnrealLiveLink.Types.h"

// DLL export macro
#ifdef _WIN32
    #define ULL_API __declspec(dllexport)
#else
    #define ULL_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

//=============================================================================
// Lifecycle Management
//=============================================================================

/**
 * Initialize LiveLink system with provider name
 * Must be called before any other functions
 * 
 * @param providerName Name displayed in Unreal's LiveLink window (e.g., "SimioSimulation")
 * @return ULL_OK on success, ULL_ERROR on failure
 * 
 * @note Call only once per process. Multiple calls return ULL_OK if already initialized.
 * @note NOT thread-safe - call from main thread only
 */
ULL_API int ULL_Initialize(const char* providerName);

/**
 * Shutdown LiveLink system
 * Flushes all messages and releases resources
 * 
 * @note Safe to call multiple times
 * @note Blocks briefly to ensure clean shutdown
 */
ULL_API void ULL_Shutdown();

/**
 * Get API version number
 * @return Version number (ULL_API_VERSION)
 */
ULL_API int ULL_GetVersion();

//=============================================================================
// Connection Status
//=============================================================================

/**
 * Check if connected to Unreal Engine
 * @return ULL_OK if connected, ULL_NOT_CONNECTED if no connection, ULL_NOT_INITIALIZED if not initialized
 * 
 * @note Connection may take 1-2 seconds after initialization
 * @note Unreal must be running with LiveLink window open
 */
ULL_API int ULL_IsConnected();

//=============================================================================
// Transform Subjects (3D Objects)
//=============================================================================

/**
 * Register a transform subject (3D object)
 * Call once per object before sending updates
 * 
 * @param subjectName Unique identifier for this object (e.g., "Forklift_01")
 * 
 * @note Subsequent calls with same name are ignored (no error)
 * @note Name is case-sensitive
 */
ULL_API void ULL_RegisterObject(const char* subjectName);

/**
 * Register a transform subject with custom properties
 * Properties will be available in Unreal Blueprints
 * 
 * @param subjectName Unique identifier for this object
 * @param propertyNames Array of property names (e.g., ["Speed", "Load", "Battery"])
 * @param propertyCount Number of properties
 * 
 * @note Property names are case-sensitive
 * @note All subsequent updates must include same number of property values
 */
ULL_API void ULL_RegisterObjectWithProperties(
    const char* subjectName,
    const char** propertyNames,
    int32_t propertyCount);

/**
 * Update transform for an object
 * Registers object automatically if not already registered
 * 
 * @param subjectName Object identifier
 * @param transform Transform data (position, rotation, scale)
 * 
 * @note Call at your desired update rate (e.g., 30 Hz)
 * @note If object not registered, auto-registers without properties
 */
ULL_API void ULL_UpdateObject(
    const char* subjectName,
    const struct ULL_Transform* transform);

/**
 * Update transform and property values for an object
 * 
 * @param subjectName Object identifier
 * @param transform Transform data
 * @param propertyValues Array of property values (must match registration order)
 * @param propertyCount Number of property values (must match registration count)
 * 
 * @note Property count must match registration count, or update is ignored
 */
ULL_API void ULL_UpdateObjectWithProperties(
    const char* subjectName,
    const struct ULL_Transform* transform,
    const float* propertyValues,
    int32_t propertyCount);

/**
 * Remove an object from LiveLink
 * Object becomes "stale" in Unreal after ~5 seconds
 * 
 * @param subjectName Object identifier to remove
 * 
 * @note Safe to call on non-existent objects (no error)
 */
ULL_API void ULL_RemoveObject(const char* subjectName);

//=============================================================================
// Data Subjects (Metrics/KPIs) - NEW!
//=============================================================================

/**
 * Register a data-only subject (no 3D representation)
 * Used for streaming simulation metrics, KPIs, system state
 * 
 * @param subjectName Unique identifier (e.g., "SystemMetrics", "ProductionKPIs")
 * @param propertyNames Array of property names (e.g., ["Throughput", "Utilization", "WIP"])
 * @param propertyCount Number of properties
 * 
 * @note Subject name is arbitrary - use descriptive names for clarity
 * @note Properties are read in Unreal via "Get LiveLink Property Value" Blueprint node
 */
ULL_API void ULL_RegisterDataSubject(
    const char* subjectName,
    const char** propertyNames,
    int32_t propertyCount);

/**
 * Update property values for a data subject
 * Registers automatically if not already registered
 * 
 * @param subjectName Subject identifier
 * @param propertyNames Array of property names (if auto-registering)
 * @param propertyValues Array of property values
 * @param propertyCount Number of properties
 * 
 * @note If subject not registered, auto-registers with provided property names
 * @note If already registered, propertyNames can be NULL (ignored)
 * @note Property count must match registration count if already registered
 */
ULL_API void ULL_UpdateDataSubject(
    const char* subjectName,
    const char** propertyNames,
    const float* propertyValues,
    int32_t propertyCount);

/**
 * Remove a data subject from LiveLink
 * 
 * @param subjectName Subject identifier to remove
 */
ULL_API void ULL_RemoveDataSubject(const char* subjectName);

#ifdef __cplusplus
}
#endif

#endif // UNREAL_LIVELINK_NATIVE_H
```

**API Design Decisions:**

1. **Combined Initialize**: Provider name passed to `Initialize()` (no separate `SetProviderName()`)
2. **Auto-registration**: `UpdateObject()` auto-registers if needed (convenience)
3. **Separate Data API**: Clear distinction between transform objects and data subjects
4. **Property flexibility**: Support both with and without properties
5. **Simple types**: All arrays are C arrays, no complex structs

---

## Phase 3: C++ Implementation

### 3.1 LiveLinkBridge.h
**Purpose:** Modern C++ wrapper around Unreal's LiveLink API

```cpp
#pragma once

#include "CoreMinimal.h"
#include "ILiveLinkProvider.h"
#include "UnrealLiveLink.Types.h"

namespace UnrealLiveLink
{
    /**
     * Subject type classification
     */
    enum class ESubjectType
    {
        Transform,  // 3D object with position/rotation/scale
        Data        // Pure data/metrics (no 3D representation)
    };
    
    /**
     * Subject registration information
     */
    struct FSubjectInfo
    {
        ESubjectType Type;
        TArray<FName> PropertyNames;
        bool bIsRegistered;
        
        FSubjectInfo()
            : Type(ESubjectType::Transform)
            , bIsRegistered(false)
        {}
    };
    
    /**
     * RAII bridge between C API and Unreal's LiveLink C++ API
     * Singleton pattern - one instance per process
     */
    class FLiveLinkBridge
    {
    public:
        // Get singleton instance
        static FLiveLinkBridge& Get();
        
        // No copy/move
        FLiveLinkBridge(const FLiveLinkBridge&) = delete;
        FLiveLinkBridge& operator=(const FLiveLinkBridge&) = delete;
        
        //=====================================================================
        // Lifecycle
        //=====================================================================
        
        bool Initialize(const FString& InProviderName);
        void Shutdown();
        bool IsInitialized() const { return bIsInitialized; }
        bool HasConnection() const;
        
        //=====================================================================
        // Transform Subjects (3D Objects)
        //=====================================================================
        
        void RegisterTransformSubject(const FString& SubjectName);
        
        void RegisterTransformSubjectWithProperties(
            const FString& SubjectName,
            const TArray<FString>& PropertyNames);
        
        void UpdateTransformSubject(
            const FString& SubjectName,
            const ULL_Transform& Transform);
        
        void UpdateTransformSubjectWithProperties(
            const FString& SubjectName,
            const ULL_Transform& Transform,
            const TArray<float>& PropertyValues);
        
        //=====================================================================
        // Data Subjects (Metrics/KPIs)
        //=====================================================================
        
        void RegisterDataSubject(
            const FString& SubjectName,
            const TArray<FString>& PropertyNames);
        
        void UpdateDataSubject(
            const FString& SubjectName,
            const TArray<FString>& PropertyNames,
            const TArray<float>& PropertyValues);
        
        //=====================================================================
        // Subject Management
        //=====================================================================
        
        void RemoveSubject(const FString& SubjectName);
        bool IsSubjectRegistered(const FString& SubjectName) const;
        
    private:
        FLiveLinkBridge();
        ~FLiveLinkBridge();
        
        // Unreal's LiveLink provider
        TSharedPtr<ILiveLinkProvider> Provider;
        
        // Provider name
        FString ProviderName;
        
        // State tracking
        bool bIsInitialized;
        
        // Subject registry (track what's registered and their properties)
        TMap<FString, FSubjectInfo> RegisteredSubjects;
        
        // Name cache for performance (avoid repeated FString->FName conversions)
        TMap<FString, FName> NameCache;
        
        //=====================================================================
        // Internal Helpers
        //=====================================================================
        
        // Get or create cached FName
        FName GetCachedName(const FString& StringName);
        
        // Convert C struct to Unreal transform
        FTransform ConvertTransform(const ULL_Transform& InTransform) const;
        
        // Create identity transform (for data subjects)
        FTransform GetIdentityTransform() const;
        
        // Register subject with LiveLink (internal)
        void RegisterSubjectInternal(
            const FString& SubjectName,
            ESubjectType Type,
            const TArray<FString>& PropertyNames);
        
        // Update subject frame data (internal)
        void UpdateSubjectInternal(
            const FString& SubjectName,
            const FTransform& Transform,
            const TArray<float>& PropertyValues);
    };
    
} // namespace UnrealLiveLink
```

**Key Design Features:**
- **Subject Registry**: Track what's registered and property counts
- **Name Caching**: Performance optimization for repeated FString→FName conversions
- **Type Tracking**: Know if subject is Transform or Data type
- **Auto-registration**: Internal logic to register on first update if needed

---

### 3.2 LiveLinkBridge.cpp (Part 1: Lifecycle)

```cpp
#include "LiveLinkBridge.h"
#include "LiveLinkProvider.h"
#include "LiveLinkTypes.h"
#include "Roles/LiveLinkTransformRole.h"
#include "Roles/LiveLinkTransformTypes.h"
#include "Roles/LiveLinkBasicRole.h"
#include "Roles/LiveLinkBasicTypes.h"
#include "Features/IModularFeatures.h"
#include "INetworkMessagingExtension.h"
#include "Shared/UdpMessagingSettings.h"
#include "Misc/CommandLine.h"
#include "Misc/ConfigCacheIni.h"
#include "Modules/ModuleManager.h"
#include "RequiredProgramMainCPPInclude.h"

DEFINE_LOG_CATEGORY_STATIC(LogLiveLinkBridge, Log, All);

namespace UnrealLiveLink
{
    //=========================================================================
    // Singleton and Lifecycle
    //=========================================================================
    
    FLiveLinkBridge::FLiveLinkBridge()
        : ProviderName(TEXT("SimioSimulation"))
        , bIsInitialized(false)
    {
    }
    
    FLiveLinkBridge::~FLiveLinkBridge()
    {
        Shutdown();
    }
    
    FLiveLinkBridge& FLiveLinkBridge::Get()
    {
        static FLiveLinkBridge Instance;
        return Instance;
    }
    
    bool FLiveLinkBridge::Initialize(const FString& InProviderName)
    {
        if (bIsInitialized)
        {
            UE_LOG(LogLiveLinkBridge, Warning, 
                TEXT("Already initialized, ignoring"));
            return true;
        }
        
        ProviderName = InProviderName;
        
        UE_LOG(LogLiveLinkBridge, Display, 
            TEXT("Initializing LiveLink Bridge with provider name: %s"), 
            *ProviderName);
        
        // Initialize Unreal Engine subsystems
        GEngineLoop.PreInit(TEXT("UnrealLiveLinkNative -Messaging"));
        
        // Ensure target platform manager exists
        GetTargetPlatformManager();
        
        // Initialize UObject system
        ProcessNewlyLoadedUObjects();
        
        // Load required modules
        FModuleManager::Get().StartProcessingNewlyLoadedObjects();
        FModuleManager::Get().LoadModule(TEXT("UdpMessaging"));
        
        // Create LiveLink provider
        Provider = ILiveLinkProvider::CreateLiveLinkProvider(ProviderName);
        if (!Provider.IsValid())
        {
            UE_LOG(LogLiveLinkBridge, Error, 
                TEXT("Failed to create LiveLink provider"));
            return false;
        }
        
        // Tick to process initial messages
        FTSTicker::GetCoreTicker().Tick(1.0f);
        
        bIsInitialized = true;
        
        UE_LOG(LogLiveLinkBridge, Display, 
            TEXT("LiveLink Bridge initialized successfully"));
        
        return true;
    }
    
    void FLiveLinkBridge::Shutdown()
    {
        if (!bIsInitialized)
        {
            return;
        }
        
        UE_LOG(LogLiveLinkBridge, Display, TEXT("Shutting down LiveLink Bridge"));
        
        // Clear all subjects
        RegisteredSubjects.Empty();
        NameCache.Empty();
        
        // Tick to flush remaining messages
        if (Provider.IsValid())
        {
            FTSTicker::GetCoreTicker().Tick(1.0f);
        }
        
        // Release provider
        Provider.Reset();
        
        // Shutdown Unreal Engine subsystems
        RequestEngineExit(TEXT("LiveLink Bridge shutting down"));
        FEngineLoop::AppPreExit();
        FModuleManager::Get().UnloadModulesAtShutdown();
        FEngineLoop::AppExit();
        
        bIsInitialized = false;
        
        UE_LOG(LogLiveLinkBridge, Display, TEXT("LiveLink Bridge shut down"));
    }
    
    bool FLiveLinkBridge::HasConnection() const
    {
        return bIsInitialized && Provider.IsValid() && Provider->HasConnection();
    }
    
    //=========================================================================
    // Internal Helpers
    //=========================================================================
    
    FName FLiveLinkBridge::GetCachedName(const FString& StringName)
    {
        if (FName* Cached = NameCache.Find(StringName))
        {
            return *Cached;
        }
        
        FName NewName(*StringName);
        NameCache.Add(StringName, NewName);
        return NewName;
    }
    
    FTransform FLiveLinkBridge::ConvertTransform(const ULL_Transform& InTransform) const
    {
        FTransform OutTransform;
        
        // Quaternion (x, y, z, w)
        OutTransform.SetRotation(FQuat(
            InTransform.rotation[0],
            InTransform.rotation[1],
            InTransform.rotation[2],
            InTransform.rotation[3]));
        
        // Translation (x, y, z) - already in centimeters
        OutTransform.SetTranslation(FVector(
            InTransform.position[0],
            InTransform.position[1],
            InTransform.position[2]));
        
        // Scale (x, y, z)
        OutTransform.SetScale3D(FVector(
            InTransform.scale[0],
            InTransform.scale[1],
            InTransform.scale[2]));
        
        return OutTransform;
    }
    
    FTransform FLiveLinkBridge::GetIdentityTransform() const
    {
        return FTransform::Identity;
    }
    
} // namespace UnrealLiveLink
```

---

### 3.3 LiveLinkBridge.cpp (Part 2: Transform Subjects)

```cpp
namespace UnrealLiveLink
{
    //=========================================================================
    // Transform Subjects (3D Objects)
    //=========================================================================
    
    void FLiveLinkBridge::RegisterTransformSubject(const FString& SubjectName)
    {
        RegisterTransformSubjectWithProperties(SubjectName, TArray<FString>());
    }
    
    void FLiveLinkBridge::RegisterTransformSubjectWithProperties(
        const FString& SubjectName,
        const TArray<FString>& PropertyNames)
    {
        RegisterSubjectInternal(SubjectName, ESubjectType::Transform, PropertyNames);
    }
    
    void FLiveLinkBridge::UpdateTransformSubject(
        const FString& SubjectName,
        const ULL_Transform& Transform)
    {
        UpdateTransformSubjectWithProperties(SubjectName, Transform, TArray<float>());
    }
    
    void FLiveLinkBridge::UpdateTransformSubjectWithProperties(
        const FString& SubjectName,
        const ULL_Transform& Transform,
        const TArray<float>& PropertyValues)
    {
        if (!Provider.IsValid())
        {
            UE_LOG(LogLiveLinkBridge, Error, 
                TEXT("Cannot update subject: Not initialized"));
            return;
        }
        
        // Auto-register if not registered
        if (!IsSubjectRegistered(SubjectName))
        {
            UE_LOG(LogLiveLinkBridge, Verbose, 
                TEXT("Auto-registering transform subject: %s"), *SubjectName);
            
            // Extract property names if we have property values
            TArray<FString> PropNames;
            // Note: Can't infer property names, so register without them
            RegisterTransformSubject(SubjectName);
        }
        
        // Verify property count matches if properties were registered
        FSubjectInfo* Info = RegisteredSubjects.Find(SubjectName);
        if (Info && Info->PropertyNames.Num() > 0)
        {
            if (PropertyValues.Num() != Info->PropertyNames.Num())
            {
                UE_LOG(LogLiveLinkBridge, Warning,
                    TEXT("Property count mismatch for %s: expected %d, got %d"),
                    *SubjectName, Info->PropertyNames.Num(), PropertyValues.Num());
                return;
            }
        }
        
        // Convert and update
        FTransform UnrealTransform = ConvertTransform(Transform);
        UpdateSubjectInternal(SubjectName, UnrealTransform, PropertyValues);
    }
    
} // namespace UnrealLiveLink
```

---

### 3.4 LiveLinkBridge.cpp (Part 3: Data Subjects - NEW!)

```cpp
namespace UnrealLiveLink
{
    //=========================================================================
    // Data Subjects (Metrics/KPIs)
    //=========================================================================
    
    void FLiveLinkBridge::RegisterDataSubject(
        const FString& SubjectName,
        const TArray<FString>& PropertyNames)
    {
        RegisterSubjectInternal(SubjectName, ESubjectType::Data, PropertyNames);
    }
    
    void FLiveLinkBridge::UpdateDataSubject(
        const FString& SubjectName,
        const TArray<FString>& PropertyNames,
        const TArray<float>& PropertyValues)
    {
        if (!Provider.IsValid())
        {
            UE_LOG(LogLiveLinkBridge, Error, 
                TEXT("Cannot update data subject: Not initialized"));
            return;
        }
        
        // Auto-register if not registered
        if (!IsSubjectRegistered(SubjectName))
        {
            if (PropertyNames.Num() > 0)
            {
                UE_LOG(LogLiveLinkBridge, Verbose, 
                    TEXT("Auto-registering data subject: %s with %d properties"),
                    *SubjectName, PropertyNames.Num());
                RegisterDataSubject(SubjectName, PropertyNames);
            }
            else
            {
                UE_LOG(LogLiveLinkBridge, Error,
                    TEXT("Cannot auto-register data subject %s: no property names provided"),
                    *SubjectName);
                return;
            }
        }
        
        // Verify property count matches
        FSubjectInfo* Info = RegisteredSubjects.Find(SubjectName);
        if (Info && Info->PropertyNames.Num() != PropertyValues.Num())
        {
            UE_LOG(LogLiveLinkBridge, Warning,
                TEXT("Property count mismatch for %s: expected %d, got %d"),
                *SubjectName, Info->PropertyNames.Num(), PropertyValues.Num());
            return;
        }
        
        // Data subjects use identity transform
        FTransform IdentityTransform = GetIdentityTransform();
        UpdateSubjectInternal(SubjectName, IdentityTransform, PropertyValues);
    }
    
} // namespace UnrealLiveLink
```

---

### 3.5 LiveLinkBridge.cpp (Part 4: Internal Registration & Update)

```cpp
namespace UnrealLiveLink
{
    //=========================================================================
    // Internal Implementation
    //=========================================================================
    
    void FLiveLinkBridge::RegisterSubjectInternal(
        const FString& SubjectName,
        ESubjectType Type,
        const TArray<FString>& PropertyNames)
    {
        if (!Provider.IsValid())
        {
            UE_LOG(LogLiveLinkBridge, Error, 
                TEXT("Cannot register subject: Not initialized"));
            return;
        }
        
        // Check if already registered
        if (IsSubjectRegistered(SubjectName))
        {
            UE_LOG(LogLiveLinkBridge, Verbose, 
                TEXT("Subject %s already registered, ignoring"), *SubjectName);
            return;
        }
        
        FName SubjectFName = GetCachedName(SubjectName);
        
        // Determine role based on type
        UClass* RoleClass = (Type == ESubjectType::Transform) 
            ? ULiveLinkTransformRole::StaticClass()
            : ULiveLinkBasicRole::StaticClass();
        
        // Create static data based on type
        if (Type == ESubjectType::Transform)
        {
            // Transform role static data
            FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
            FLiveLinkTransformStaticData& TransformData = 
                *StaticData.Cast<FLiveLinkTransformStaticData>();
            
            // Add custom properties
            for (const FString& PropName : PropertyNames)
            {
                TransformData.PropertyNames.Add(FName(*PropName));
            }
            
            Provider->UpdateSubjectStaticData(SubjectFName, RoleClass, MoveTemp(StaticData));
        }
        else // ESubjectType::Data
        {
            // Basic role static data
            FLiveLinkStaticDataStruct StaticData(FLiveLinkBasicStaticData::StaticStruct());
            FLiveLinkBasicStaticData& BasicData = 
                *StaticData.Cast<FLiveLinkBasicStaticData>();
            
            // Add properties
            for (const FString& PropName : PropertyNames)
            {
                BasicData.PropertyNames.Add(FName(*PropName));
            }
            
            Provider->UpdateSubjectStaticData(SubjectFName, RoleClass, MoveTemp(StaticData));
        }
        
        // Track registration
        FSubjectInfo& Info = RegisteredSubjects.Add(SubjectName);
        Info.Type = Type;
        Info.bIsRegistered = true;
        for (const FString& PropName : PropertyNames)
        {
            Info.PropertyNames.Add(FName(*PropName));
        }
        
        UE_LOG(LogLiveLinkBridge, Display,
            TEXT("Registered %s subject: %s with %d properties"),
            (Type == ESubjectType::Transform) ? TEXT("Transform") : TEXT("Data"),
            *SubjectName, PropertyNames.Num());
    }
    
    void FLiveLinkBridge::UpdateSubjectInternal(
        const FString& SubjectName,
        const FTransform& Transform,
        const TArray<float>& PropertyValues)
    {
        if (!Provider.IsValid())
        {
            return;
        }
        
        FSubjectInfo* Info = RegisteredSubjects.Find(SubjectName);
        if (!Info || !Info->bIsRegistered)
        {
            UE_LOG(LogLiveLinkBridge, Warning,
                TEXT("Attempting to update unregistered subject: %s"), *SubjectName);
            return;
        }
        
        FName SubjectFName = GetCachedName(SubjectName);
        
        // Create frame data based on type
        if (Info->Type == ESubjectType::Transform)
        {
            FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
            FLiveLinkTransformFrameData& TransformFrame = 
                *FrameData.Cast<FLiveLinkTransformFrameData>();
            
            // Set transform
            TransformFrame.Transform = Transform;
            
            // Set timestamp
            TransformFrame.WorldTime = FPlatformTime::Seconds();
            
            // Set property values
            for (float Value : PropertyValues)
            {
                TransformFrame.PropertyValues.Add(Value);
            }
            
            Provider->UpdateSubjectFrameData(SubjectFName, MoveTemp(FrameData));
        }
        else // ESubjectType::Data
        {
            FLiveLinkFrameDataStruct FrameData(FLiveLinkBaseFrameData::StaticStruct());
            FLiveLinkBaseFrameData& BaseFrame = 
                *FrameData.Cast<FLiveLinkBaseFrameData>();
            
            // Set timestamp
            BaseFrame.WorldTime = FPlatformTime::Seconds();
            
            // Set property values
            for (float Value : PropertyValues)
            {
                BaseFrame.PropertyValues.Add(Value);
            }
            
            Provider->UpdateSubjectFrameData(SubjectFName, MoveTemp(FrameData));
        }
    }
    
    //=========================================================================
    // Subject Management
    //=========================================================================
    
    void FLiveLinkBridge::RemoveSubject(const FString& SubjectName)
    {
        if (!Provider.IsValid())
        {
            return;
        }
        
        if (!IsSubjectRegistered(SubjectName))
        {
            return;
        }
        
        FName SubjectFName = GetCachedName(SubjectName);
        Provider->RemoveSubject(SubjectFName);
        
        RegisteredSubjects.Remove(SubjectName);
        
        UE_LOG(LogLiveLinkBridge, Display, 
            TEXT("Removed subject: %s"), *SubjectName);
    }
    
    bool FLiveLinkBridge::IsSubjectRegistered(const FString& SubjectName) const
    {
        const FSubjectInfo* Info = RegisteredSubjects.Find(SubjectName);
        return Info && Info->bIsRegistered;
    }
    
} // namespace UnrealLiveLink
```

---

### 3.6 UnrealLiveLink.Native.cpp (C Exports)

```cpp
#include "UnrealLiveLink.Native.h"
#include "LiveLinkBridge.h"

using namespace UnrealLiveLink;

// Helper: Convert C string array to TArray<FString>
static TArray<FString> ConvertStringArray(const char** strings, int32_t count)
{
    TArray<FString> Result;
    if (strings)
    {
        for (int32_t i = 0; i < count; ++i)
        {
            if (strings[i])
            {
                Result.Add(FString(strings[i]));
            }
        }
    }
    return Result;
}

// Helper: Convert C float array to TArray<float>
static TArray<float> ConvertFloatArray(const float* values, int32_t count)
{
    TArray<float> Result;
    if (values)
    {
        for (int32_t i = 0; i < count; ++i)
        {
            Result.Add(values[i]);
        }
    }
    return Result;
}

//=============================================================================
// C Exports
//=============================================================================

extern "C"
{
    //=========================================================================
    // Lifecycle
    //=========================================================================
    
    ULL_API int ULL_Initialize(const char* providerName)
    {
        if (!providerName || strlen(providerName) == 0)
        {
            return ULL_ERROR;
        }
        
        return FLiveLinkBridge::Get().Initialize(FString(providerName)) 
            ? ULL_OK 
            : ULL_ERROR;
    }
    
    ULL_API void ULL_Shutdown()
    {
        FLiveLinkBridge::Get().Shutdown();
    }
    
    ULL_API int ULL_GetVersion()
    {
        return ULL_API_VERSION;
    }
    
    ULL_API int ULL_IsConnected()
    {
        if (!FLiveLinkBridge::Get().IsInitialized())
        {
            return ULL_NOT_INITIALIZED;
        }
        
        return FLiveLinkBridge::Get().HasConnection() 
            ? ULL_OK 
            : ULL_NOT_CONNECTED;
    }
    
    //=========================================================================
    // Transform Subjects
    //=========================================================================
    
    ULL_API void ULL_RegisterObject(const char* subjectName)
    {
        if (subjectName)
        {
            FLiveLinkBridge::Get().RegisterTransformSubject(FString(subjectName));
        }
    }
    
    ULL_API void ULL_RegisterObjectWithProperties(
        const char* subjectName,
        const char** propertyNames,
        int32_t propertyCount)
    {
        if (subjectName)
        {
            TArray<FString> PropNames = ConvertStringArray(propertyNames, propertyCount);
            FLiveLinkBridge::Get().RegisterTransformSubjectWithProperties(
                FString(subjectName), 
                PropNames);
        }
    }
    
    ULL_API void ULL_UpdateObject(
        const char* subjectName,
        const struct ULL_Transform* transform)
    {
        if (subjectName && transform)
        {
            FLiveLinkBridge::Get().UpdateTransformSubject(
                FString(subjectName),
                *transform);
        }
    }
    
    ULL_API void ULL_UpdateObjectWithProperties(
        const char* subjectName,
        const struct ULL_Transform* transform,
        const float* propertyValues,
        int32_t propertyCount)
    {
        if (subjectName && transform)
        {
            TArray<float> PropValues = ConvertFloatArray(propertyValues, propertyCount);
            FLiveLinkBridge::Get().UpdateTransformSubjectWithProperties(
                FString(subjectName),
                *transform,
                PropValues);
        }
    }
    
    ULL_API void ULL_RemoveObject(const char* subjectName)
    {
        if (subjectName)
        {
            FLiveLinkBridge::Get().RemoveSubject(FString(subjectName));
        }
    }
    
    //=========================================================================
    // Data Subjects (NEW!)
    //=========================================================================
    
    ULL_API void ULL_RegisterDataSubject(
        const char* subjectName,
        const char** propertyNames,
        int32_t propertyCount)
    {
        if (subjectName)
        {
            TArray<FString> PropNames = ConvertStringArray(propertyNames, propertyCount);
            FLiveLinkBridge::Get().RegisterDataSubject(
                FString(subjectName),
                PropNames);
        }
    }
    
    ULL_API void ULL_UpdateDataSubject(
        const char* subjectName,
        const char** propertyNames,
        const float* propertyValues,
        int32_t propertyCount)
    {
        if (subjectName && propertyValues)
        {
            TArray<FString> PropNames = ConvertStringArray(propertyNames, propertyCount);
            TArray<float> PropValues = ConvertFloatArray(propertyValues, propertyCount);
            
            FLiveLinkBridge::Get().UpdateDataSubject(
                FString(subjectName),
                PropNames,
                PropValues);
        }
    }
    
    ULL_API void ULL_RemoveDataSubject(const char* subjectName)
    {
        if (subjectName)
        {
            FLiveLinkBridge::Get().RemoveSubject(FString(subjectName));
        }
    }
    
} // extern "C"
```

---

## Phase 4: Build Process

### 4.1 Build Steps (Windows)

```powershell
# BuildNative.ps1

# Configuration
$UE_ROOT = "C:\UE\UE_5.6"
$PROJECT_ROOT = "C:\repos\SimioUnrealEngineLiveLinkConnector"
$UE_PROGRAMS = "$UE_ROOT\Engine\Source\Programs"
$OUTPUT_DIR = "$PROJECT_ROOT\lib\native\win-x64"

# 1. Copy source to Unreal Programs directory
Write-Host "Copying source to Unreal Engine..." -ForegroundColor Cyan
$SOURCE = "$PROJECT_ROOT\src\Native\UnrealLiveLink.Native"
$DEST = "$UE_PROGRAMS\UnrealLiveLinkNative"

if (Test-Path $DEST) {
    Remove-Item -Recurse -Force $DEST
}
Copy-Item -Recurse $SOURCE $DEST

# 2. Regenerate project files
Write-Host "Regenerating Unreal Engine project files..." -ForegroundColor Cyan
Push-Location $UE_ROOT
& .\GenerateProjectFiles.bat
Pop-Location

# 3. Build with UBT
Write-Host "Building UnrealLiveLinkNative..." -ForegroundColor Cyan
$UBT = "$UE_ROOT\Engine\Build\BatchFiles\Build.bat"
& $UBT UnrealLiveLinkNative Win64 Development

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# 4. Copy output DLL and PDB
Write-Host "Copying output files..." -ForegroundColor Cyan
$BIN_DIR = "$UE_ROOT\Engine\Binaries\Win64"

New-Item -ItemType Directory -Force -Path $OUTPUT_DIR | Out-Null

Copy-Item "$BIN_DIR\UnrealLiveLink.Native.dll" $OUTPUT_DIR
Copy-Item "$BIN_DIR\UnrealLiveLink.Native.pdb" $OUTPUT_DIR

# 5. Create version info
$VERSION = "1.0.0"
$VERSION_INFO = @"
Version: $VERSION
Built: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Configuration: Development
Platform: Win64
"@
$VERSION_INFO | Out-File "$OUTPUT_DIR\VERSION.txt"

Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $OUTPUT_DIR" -ForegroundColor Green
```

### 4.2 Verify Build

```powershell
# Check exports
dumpbin /exports "lib\native\win-x64\UnrealLiveLink.Native.dll"

# Expected exports:
# ULL_Initialize
# ULL_Shutdown
# ULL_GetVersion
# ULL_IsConnected
# ULL_RegisterObject
# ULL_RegisterObjectWithProperties
# ULL_UpdateObject
# ULL_UpdateObjectWithProperties
# ULL_RemoveObject
# ULL_RegisterDataSubject
# ULL_UpdateDataSubject
# ULL_RemoveDataSubject
```

---

## Phase 5: Testing Strategy

### 5.1 C# Integration Test

```csharp
using System;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;

namespace SimioUnrealEngineLiveLinkConnector.Tests
{
    [TestFixture]
    public class NativeIntegrationTests
    {
        [Test]
        public void TestLifecycle()
        {
            // Initialize
            int result = UnrealLiveLinkNative.ULL_Initialize("TestProvider");
            Assert.AreEqual(0, result, "Initialize should succeed");
            
            // Check version
            int version = UnrealLiveLinkNative.ULL_GetVersion();
            Assert.AreEqual(1, version);
            
            // Shutdown
            UnrealLiveLinkNative.ULL_Shutdown();
        }
        
        [Test]
        public void TestTransformObject()
        {
            UnrealLiveLinkNative.ULL_Initialize("TestProvider");
            
            // Register object
            UnrealLiveLinkNative.ULL_RegisterObject("TestCube");
            
            // Create transform
            var transform = new ULL_Transform
            {
                position = new double[] { 100, 200, 0 },
                rotation = new double[] { 0, 0, 0, 1 },  // Identity quat
                scale = new double[] { 1, 1, 1 }
            };
            
            // Update multiple times
            for (int i = 0; i < 10; i++)
            {
                transform.position[0] = 100 + i * 10;
                UnrealLiveLinkNative.ULL_UpdateObject("TestCube", ref transform);
                Thread.Sleep(33); // ~30 FPS
            }
            
            UnrealLiveLinkNative.ULL_Shutdown();
        }
        
        [Test]
        public void TestTransformWithProperties()
        {
            UnrealLiveLinkNative.ULL_Initialize("TestProvider");
            
            // Register with properties
            string[] propNames = { "Speed", "Load", "Battery" };
            UnrealLiveLinkNative.ULL_RegisterObjectWithProperties(
                "Forklift_01", 
                propNames, 
                propNames.Length);
            
            // Update with properties
            var transform = new ULL_Transform
            {
                position = new double[] { 0, 0, 0 },
                rotation = new double[] { 0, 0, 0, 1 },
                scale = new double[] { 1, 1, 1 }
            };
            
            float[] propValues = { 5.5f, 80.0f, 0.75f };
            
            UnrealLiveLinkNative.ULL_UpdateObjectWithProperties(
                "Forklift_01",
                ref transform,
                propValues,
                propValues.Length);
            
            UnrealLiveLinkNative.ULL_Shutdown();
        }
        
        [Test]
        public void TestDataSubject()
        {
            UnrealLiveLinkNative.ULL_Initialize("TestProvider");
            
            // Register data subject
            string[] propNames = { "Throughput", "Utilization", "WIP", "QueueDepth" };
            UnrealLiveLinkNative.ULL_RegisterDataSubject(
                "ProductionMetrics",
                propNames,
                propNames.Length);
            
            // Update metrics
            float[] values = { 125.5f, 0.85f, 42.0f, 8.0f };
            
            for (int i = 0; i < 10; i++)
            {
                values[0] += 1.0f; // Increment throughput
                values[3] = (float)(8 + Math.Sin(i) * 3); // Vary queue depth
                
                UnrealLiveLinkNative.ULL_UpdateDataSubject(
                    "ProductionMetrics",
                    null, // Already registered
                    values,
                    values.Length);
                
                Thread.Sleep(100);
            }
            
            UnrealLiveLinkNative.ULL_Shutdown();
        }
        
        [Test]
        public void TestMixedSubjects()
        {
            UnrealLiveLinkNative.ULL_Initialize("TestProvider");
            
            // Register both types
            UnrealLiveLinkNative.ULL_RegisterObject("Vehicle_01");
            
            string[] metricNames = { "SystemTime", "TotalEntities" };
            UnrealLiveLinkNative.ULL_RegisterDataSubject("SystemMetrics", metricNames, 2);
            
            // Update both
            var transform = new ULL_Transform
            {
                position = new double[] { 0, 0, 0 },
                rotation = new double[] { 0, 0, 0, 1 },
                scale = new double[] { 1, 1, 1 }
            };
            
            for (int i = 0; i < 10; i++)
            {
                // Update transform
                transform.position[0] = i * 50;
                UnrealLiveLinkNative.ULL_UpdateObject("Vehicle_01", ref transform);
                
                // Update metrics
                float[] metrics = { (float)i * 0.1f, (float)(i + 1) };
                UnrealLiveLinkNative.ULL_UpdateDataSubject("SystemMetrics", null, metrics, 2);
                
                Thread.Sleep(33);
            }
            
            UnrealLiveLinkNative.ULL_Shutdown();
        }
    }
}
```

### 5.2 Manual Test with Unreal Editor

**Test Procedure:**
1. Build native DLL
2. Run C# test program
3. Open Unreal Editor (5.6+)
4. **Window → LiveLink**
5. Click **+ Source** → **Message Bus Source**
6. **Verify:** "TestProvider" appears in sources list
7. **Verify:** Subjects appear (TestCube, Forklift_01, ProductionMetrics)
8. **Verify:** Transform updates in real-time
9. Create Blueprint:
   ```
   Get LiveLink Subject Data → Get Property Value("Throughput")
   ```
10. **Verify:** Property values update

---

## Phase 6: Usage Examples

### Example 1: Simple Moving Object

```csharp
// Initialize once
UnrealLiveLinkNative.ULL_Initialize("MySimulation");

// Register object
UnrealLiveLinkNative.ULL_RegisterObject("Robot_01");

// Simulation loop (30 Hz)
for (int frame = 0; frame < 1000; frame++)
{
    double time = frame / 30.0;
    
    var transform = new ULL_Transform
    {
        position = new double[] 
        { 
            Math.Cos(time) * 500,  // Circle motion
            Math.Sin(time) * 500,
            50
        },
        rotation = new double[] { 0, 0, 0, 1 },
        scale = new double[] { 1, 1, 1 }
    };
    
    UnrealLiveLinkNative.ULL_UpdateObject("Robot_01", ref transform);
    
    Thread.Sleep(33); // 30 FPS
}

// Cleanup
UnrealLiveLinkNative.ULL_Shutdown();
```

### Example 2: Object with Properties

```csharp
// Initialize
UnrealLiveLinkNative.ULL_Initialize("WarehouseSimulation");

// Register with properties
string[] properties = { "Speed", "Load", "BatteryLevel" };
UnrealLiveLinkNative.ULL_RegisterObjectWithProperties(
    "Forklift_05",
    properties,
    properties.Length);

// Update in simulation
var transform = new ULL_Transform();
float[] propValues = new float[3];

while (simulationRunning)
{
    // Get current state from Simio
    var entity = GetSimioEntity("Forklift_05");
    
    transform.position = ConvertPosition(entity.Location);
    transform.rotation = ConvertRotation(entity.Orientation);
    transform.scale = new double[] { 1, 1, 1 };
    
    propValues[0] = (float)entity.Speed;
    propValues[1] = (float)entity.Load;
    propValues[2] = (float)entity.Battery;
    
    UnrealLiveLinkNative.ULL_UpdateObjectWithProperties(
        "Forklift_05",
        ref transform,
        propValues,
        propValues.Length);
    
    Thread.Sleep(33);
}
```

### Example 3: Dashboard Metrics (Data Subject)

```csharp
// Initialize
UnrealLiveLinkNative.ULL_Initialize("FactorySimulation");

// Register metrics subject
string[] metrics = 
{ 
    "TotalThroughput", 
    "AverageUtilization", 
    "CurrentWIP", 
    "BottleneckQueue",
    "EnergyConsumption"
};

UnrealLiveLinkNative.ULL_RegisterDataSubject("FactoryKPIs", metrics, metrics.Length);

// Update every second
while (simulationRunning)
{
    float[] values = new float[]
    {
        (float)CalculateThroughput(),
        (float)CalculateUtilization(),
        (float)GetCurrentWIP(),
        (float)GetBottleneckQueueDepth(),
        (float)GetEnergyConsumption()
    };
    
    UnrealLiveLinkNative.ULL_UpdateDataSubject(
        "FactoryKPIs",
        null, // Already registered
        values,
        values.Length);
    
    Thread.Sleep(1000); // Update every second
}
```

### Example 4: Mixed Scenario

```csharp
// Manufacturing line with 5 robots and system metrics

UnrealLiveLinkNative.ULL_Initialize("ManufacturingLine");

// Register robots with properties
for (int i = 1; i <= 5; i++)
{
    string[] props = { "CycleTime", "PartsProcessed", "Status" };
    UnrealLiveLinkNative.ULL_RegisterObjectWithProperties(
        $"Robot_{i:D2}",
        props,
        props.Length);
}

// Register system metrics
string[] systemMetrics = 
{
    "LineSpeed",
    "Efficiency",
    "Defects",
    "TotalOutput"
};
UnrealLiveLinkNative.ULL_RegisterDataSubject("LineMetrics", systemMetrics, 4);

// Simulation loop
while (true)
{
    // Update each robot
    for (int i = 1; i <= 5; i++)
    {
        var robot = GetRobot(i);
        
        var transform = robot.GetTransform();
        float[] props = 
        {
            robot.CycleTime,
            robot.PartsProcessed,
            robot.Status
        };
        
        UnrealLiveLinkNative.ULL_UpdateObjectWithProperties(
            $"Robot_{i:D2}",
            ref transform,
            props,
            3);
    }
    
    // Update system metrics
    float[] metrics = 
    {
        GetLineSpeed(),
        CalculateEfficiency(),
        GetDefectCount(),
        GetTotalOutput()
    };
    UnrealLiveLinkNative.ULL_UpdateDataSubject("LineMetrics", null, metrics, 4);
    
    Thread.Sleep(33); // 30 Hz
}
```

---

## Phase 7: Performance Optimizations

### String Caching (Already Implemented)

The `FLiveLinkBridge` already caches `FName` conversions:
```cpp
FName FLiveLinkBridge::GetCachedName(const FString& StringName)
{
    if (FName* Cached = NameCache.Find(StringName))
    {
        return *Cached;
    }
    
    FName NewName(*StringName);
    NameCache.Add(StringName, NewName);
    return NewName;
}
```

### Batch Updates (Optional Enhancement)

```cpp
// Add to LiveLinkBridge.h
void UpdateMultipleTransformSubjects(
    const TArray<FString>& SubjectNames,
    const TArray<ULL_Transform>& Transforms);

// Add C export
ULL_API void ULL_UpdateObjectBatch(
    const char** subjectNames,
    const struct ULL_Transform* transforms,
    int32_t count);
```

### Property Array Reuse (For C# Callers)

```csharp
// In C# managed layer - reuse arrays
public class LiveLinkObjectUpdater
{
    private float[] _propertyBuffer; // Reused across updates
    
    public void UpdateWithProperties(...)
    {
        // Reuse buffer instead of allocating
        if (_propertyBuffer == null || _propertyBuffer.Length != count)
        {
            _propertyBuffer = new float[count];
        }
        
        // Fill buffer...
        
        UnrealLiveLinkNative.ULL_UpdateObjectWithProperties(..., _propertyBuffer, ...);
    }
}
```

---

## Phase 8: Error Handling & Diagnostics

### Enhanced Logging (Optional)

```cpp
// Add to C API
enum ULL_LogLevel
{
    ULL_LOG_VERBOSE = 0,
    ULL_LOG_DISPLAY = 1,
    ULL_LOG_WARNING = 2,
    ULL_LOG_ERROR = 3
};

ULL_API void ULL_SetLogLevel(enum ULL_LogLevel level);

ULL_API void ULL_SetLogCallback(void (*callback)(int level, const char* message));
```

### Struct Size Validation

```cpp
// Add to C API for debugging
ULL_API int ULL_GetTransformSize();

// In C#
[Test]
public void VerifyStructSize()
{
    int expectedSize = Marshal.SizeOf<ULL_Transform>();
    int actualSize = UnrealLiveLinkNative.ULL_GetTransformSize();
    Assert.AreEqual(expectedSize, actualSize);
}
```

---

## Development Checklist

### Phase 1: Project Setup
- [ ] Create `UnrealLiveLink.Native.Target.cs`
- [ ] Create `UnrealLiveLink.Native.Build.cs`
- [ ] Test UBT project generation
- [ ] Verify module dependencies load

### Phase 2: Public API
- [ ] Create `UnrealLiveLink.Types.h`
- [ ] Create `UnrealLiveLink.Native.h`
- [ ] Document all functions
- [ ] Verify struct sizes for C# marshaling

### Phase 3: C++ Implementation
- [ ] Implement `LiveLinkBridge.h` (interface)
- [ ] Implement lifecycle (Initialize, Shutdown)
- [ ] Implement transform subject functions
- [ ] Implement data subject functions (NEW!)
- [ ] Implement subject registry tracking
- [ ] Implement name caching
- [ ] Add comprehensive logging
- [ ] Add error handling

### Phase 4: C Exports
- [ ] Implement all C exports in `UnrealLiveLink.Native.cpp`
- [ ] Add parameter validation
- [ ] Add helper functions for array conversion
- [ ] Test each export individually

### Phase 5: Build
- [ ] Build with UBT on Windows
- [ ] Verify DLL exports (dumpbin)
- [ ] Check for missing dependencies
- [ ] Test from C# P/Invoke

### Phase 6: Testing
- [ ] Unit tests (struct marshaling)
- [ ] Integration tests (lifecycle)
- [ ] Integration tests (transform subjects)
- [ ] Integration tests (data subjects) - NEW!
- [ ] Manual test with Unreal Editor
- [ ] Performance profiling
- [ ] Memory leak testing

### Phase 7: Documentation
- [ ] Complete code comments
- [ ] Create usage examples
- [ ] Document common issues
- [ ] Create troubleshooting guide

---

## Common Issues & Solutions

### Issue: DLL Not Found
**Symptom:** `DllNotFoundException` from C#

**Solutions:**
- Ensure DLL in same directory as executable
- Check architecture (x64 vs x86)
- Verify all Unreal dependencies present

### Issue: Entry Point Not Found
**Symptom:** `EntryPointNotFoundException`

**Solutions:**
- Verify function name matches exactly
- Check `CallingConvention.Cdecl`
- Ensure `extern "C"` used
- Use `dumpbin /exports` to verify

### Issue: Struct Marshaling Error
**Symptom:** Corrupted data in Unreal

**Solutions:**
- Verify C# `[StructLayout(LayoutKind.Sequential)]`
- Check array sizes match
- Ensure double vs float consistency
- Test with `Marshal.SizeOf`

### Issue: No Connection to Unreal
**Symptom:** `ULL_IsConnected()` returns `ULL_NOT_CONNECTED`

**Solutions:**
- Ensure Unreal Editor running
- Open LiveLink window (Window → LiveLink)
- Add Message Bus Source
- Check firewall not blocking UDP
- Wait 1-2 seconds after initialize

### Issue: Properties Not Updating
**Symptom:** Property values don't change in Unreal

**Solutions:**
- Verify property count matches registration
- Check property names match exactly (case-sensitive)
- Ensure subject registered before update
- Use Verbose logging to debug

### Issue: Data Subject Not Visible
**Symptom:** Data subject doesn't appear in LiveLink

**Solutions:**
- Verify registered as Data subject (not Transform)
- Check subject name is unique
- Ensure at least one property registered
- Use `ULL_RegisterDataSubject()` not `ULL_RegisterObject()`

---

## Performance Targets

| Metric | Target | Notes |
|--------|--------|-------|
| Initialization Time | < 2 seconds | From `Initialize()` to first connection |
| Update Latency | < 5 ms | C# call to Unreal receipt |
| Transform Throughput | 1000+ objects @ 30 Hz | Sustained without frame drops |
| Data Subject Throughput | 100+ subjects @ 10 Hz | Typical dashboard update rate |
| Memory per Object | < 1 KB | Static + per-frame overhead |
| Property Overhead | < 100 bytes per property | Per subject |

---

## Summary of Key Differences from Original Plan

### Simplified API
- **Removed:** Camera, Light, Animation roles
- **Removed:** Complex metadata, timecode
- **Removed:** Multi-step initialization (single `Initialize()` call)
- **Added:** Dedicated data subject functions
- **Cleaner:** Consistent naming (ULL_ prefix)

### New Features
- **Data Subjects:** Separate API for metrics/KPIs
- **Auto-registration:** Subjects register on first update
- **Subject Registry:** Track registration state and property counts
- **Name Caching:** Performance optimization

### Modern C++
- **Namespace:** `UnrealLiveLink` for organization
- **Enum Class:** `ESubjectType` for type safety
- **RAII:** Proper resource management
- **TMap/TArray:** Unreal containers throughout

---

## Next Steps

1. **Week 1: Core Implementation**
   - Day 1-2: Project setup and build configuration
   - Day 3-4: LiveLinkBridge implementation
   - Day 5: C exports and initial testing

2. **Week 2: Testing and Polish**
   - Day 6-7: Integration testing
   - Day 8: Performance optimization
   - Day 9: Documentation
   - Day 10: Final testing with Unreal Editor

**Total: 2 weeks to complete native layer**