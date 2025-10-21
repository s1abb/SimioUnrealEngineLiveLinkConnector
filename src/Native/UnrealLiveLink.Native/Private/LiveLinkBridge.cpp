#include "LiveLinkBridge.h"
#include "UnrealLiveLink.Native.h"
#include "Math/Transform.h"
#include "Misc/ScopeLock.h"
#include "Misc/CString.h"

// UE Program initialization (GEngineLoop)
// Note: Don't include RequiredProgramMainCPPInclude.h here - it's in UnrealLiveLinkNativeMain.cpp only!
#include "LaunchEngineLoop.h"              // For GEngineLoop access
#include "Modules/ModuleManager.h"
#include "Interfaces/IPluginManager.h"
#include "Misc/CommandLine.h"

// LiveLink includes Message Bus Provider API
// Disable C4099 warning: UE has inconsistent class/struct forward declarations
#pragma warning(push)
#pragma warning(disable: 4099)
#include "LiveLinkProvider.h"              // 🆕 Message Bus Provider API (no ILiveLinkClient needed!)
#include "LiveLinkTypes.h"
#include "Roles/LiveLinkTransformRole.h"
#include "Roles/LiveLinkTransformTypes.h"
#pragma warning(pop)

//=============================================================================
// LiveLink Message Bus Integration
//=============================================================================
// Using ILiveLinkProvider API which handles Message Bus communication automatically.
// This enables cross-process streaming: Simio → Message Bus → Unreal Engine
// Protocol: UDP multicast (230.0.0.1:6666 default)
//=============================================================================

//=============================================================================
// LiveLinkBridge Implementation with Transform Subject Registration
//=============================================================================
// This implementation creates actual LiveLink Message Bus source and streams
// transform data to Unreal Engine's LiveLink subsystem.
//=============================================================================

// Static member initialization
// CRITICAL: GEngineLoop.PreInit() can only be called ONCE per process
bool FLiveLinkBridge::bGEngineLoopInitialized = false;

FLiveLinkBridge& FLiveLinkBridge::Get()
{
	static FLiveLinkBridge Instance;
	return Instance;
}

//=============================================================================
// Lifecycle Management
//=============================================================================

bool FLiveLinkBridge::Initialize(const FString& InProviderName)
{
	FScopeLock Lock(&CriticalSection);
	
	if (bInitialized)
	{
		// Idempotent behavior: if already initialized, return true
		// This matches expected behavior from integration tests
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("Initialize: Already initialized with provider '%s', returning true (idempotent)"), 
		       *ProviderName);
		return true;
	}
	
	// Initialize Unreal Engine runtime environment
	// Based on reference: UnrealLiveLinkCInterface (github.com/jakedowns/UnrealLiveLinkCInterface)
	// CRITICAL: GEngineLoop.PreInit() can only be called ONCE per process!
	// Check static flag to prevent crash on simulation restart
	
	if (!bGEngineLoopInitialized)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("Initialize: Initializing Unreal Engine runtime for Message Bus support"));
		
		// Initialize GEngineLoop with Messaging support
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: Calling GEngineLoop.PreInit..."));
		int32 PreInitResult = GEngineLoop.PreInit(TEXT("UnrealLiveLinkNative -Messaging"));
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: GEngineLoop.PreInit returned %d"), PreInitResult);
		
		if (PreInitResult != 0)
		{
			UE_LOG(LogUnrealLiveLinkNative, Error, 
			       TEXT("Initialize: ❌ GEngineLoop.PreInit FAILED with code %d"), PreInitResult);
			return false;
		}
		
		// Ensure target platform manager is referenced early (must be on main thread)
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: Getting target platform manager..."));
		GetTargetPlatformManager();
		
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: Processing newly loaded UObjects..."));
		ProcessNewlyLoadedUObjects();
		
		// Tell module manager it may now process newly-loaded UObjects
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: Starting module manager..."));
		FModuleManager::Get().StartProcessingNewlyLoadedObjects();
		
		// Load UdpMessaging module (required for Message Bus communication)
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: Loading UdpMessaging module..."));
		FModuleManager::Get().LoadModule(TEXT("UdpMessaging"));
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: ✅ UdpMessaging module loaded"));
		
		// Load plugins if available
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: Loading plugins..."));
		IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::PreDefault);
		IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::Default);
		IPluginManager::Get().LoadModulesForEnabledPlugins(ELoadingPhase::PostDefault);
		UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: ✅ Plugins loaded"));
		
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("Initialize: ✅ Unreal Engine runtime initialized successfully"));
		
		// Mark as initialized (process-wide, never reset)
		bGEngineLoopInitialized = true;
	}
	else
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("Initialize: ✅ GEngineLoop already initialized (reusing existing runtime)"));
	}
	
	// Store provider name and mark as initialized
	ProviderName = InProviderName;
	bInitialized = true;
	bLiveLinkReady = true;
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("Initialize: Ready for LiveLink integration with provider '%s'"), 
	       *ProviderName);
	
	return true;
}

void FLiveLinkBridge::Shutdown()
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("Shutdown: Not initialized, nothing to do"));
		return;
	}
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("Shutdown: Clearing %d transform subjects, %d data subjects, %d cached names"), 
	       TransformSubjects.Num(), 
	       DataSubjects.Num(), 
	       NameCache.Num());
	
	// Remove LiveLink provider if created
	if (bLiveLinkSourceCreated && LiveLinkProvider.IsValid())
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("Shutdown: Removing LiveLink Message Bus Provider '%s'"), 
		       *ProviderName);
		
		// Reset shared pointer (will trigger cleanup)
		LiveLinkProvider.Reset();
		bLiveLinkSourceCreated = false;
		
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("Shutdown: ✅ LiveLink provider removed successfully"));
	}
	
	// Clear all state
	TransformSubjects.Empty();
	DataSubjects.Empty();
	NameCache.Empty();
	ProviderName.Empty();
	bInitialized = false;
	bLiveLinkReady = false;
	
	// DO NOT shutdown GEngineLoop in DLL!
	// WARNING: RequestEngineExit() and AppExit() terminate the HOST PROCESS (Simio.exe)!
	// This is only safe in standalone programs, not DLLs loaded by other applications.
	UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Shutdown: Skipping GEngineLoop shutdown (DLL loaded in host process)"));
	
	// DON'T CALL THESE IN A DLL:
	// RequestEngineExit(TEXT("UnrealLiveLinkNative shutting down"));  // ← Terminates Simio!
	// FEngineLoop::AppPreExit();  // ← Unsafe in DLL
	// FModuleManager::Get().UnloadModulesAtShutdown();  // ← May crash host
	// FEngineLoop::AppExit();  // ← Terminates host process!
	
	UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Shutdown: Complete (resources released, modules remain loaded)"));
}

int FLiveLinkBridge::GetConnectionStatus() const
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		return ULL_NOT_INITIALIZED;
	}
	
	// Check if LiveLink provider is created
	if (bLiveLinkSourceCreated && LiveLinkProvider.IsValid())
	{
		return ULL_OK;
	}
	
	// Check if LiveLink framework is ready
	if (bLiveLinkReady)
	{
		return ULL_OK;
	}
	
	return ULL_NOT_CONNECTED;
}

//=============================================================================
// Helper Methods
//=============================================================================

void FLiveLinkBridge::EnsureLiveLinkSource()
{
	// Note: Caller must hold CriticalSection lock
	
	if (bLiveLinkSourceCreated)
	{
		ULL_VERBOSE_LOG(TEXT("EnsureLiveLinkSource: Provider already exists, skipping"));
		return;
	}
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Creating LiveLink Message Bus Provider '%s'"), 
	       *ProviderName);
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Using UDP Message Bus for cross-process communication"));
	
	// Create LiveLink Message Bus Provider
	// This enables communication between Simio process → UE process via UDP multicast
	// Default protocol: UDP 230.0.0.1:6666
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Calling ILiveLinkProvider::CreateLiveLinkProvider..."));
	
	LiveLinkProvider = ILiveLinkProvider::CreateLiveLinkProvider(ProviderName);
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: CreateLiveLinkProvider returned, checking validity..."));
	
	if (!LiveLinkProvider.IsValid())
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("EnsureLiveLinkSource: ❌ Failed to create ILiveLinkProvider"));
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("EnsureLiveLinkSource: Check that UdpMessaging module is available"));
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("EnsureLiveLinkSource: Check that GEngineLoop.PreInit() succeeded"));
		return;
	}
	
	bLiveLinkSourceCreated = true;
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: ✅ SUCCESS! LiveLink Message Bus Provider created"));
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Broadcasting to UDP Message Bus (230.0.0.1:6666)"));
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Check Unreal Editor → Window → LiveLink for source '%s'"), 
	       *ProviderName);
}

//=============================================================================
// Transform Subjects
//=============================================================================

void FLiveLinkBridge::RegisterTransformSubject(const FName& SubjectName)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("RegisterTransformSubject: Not initialized, ignoring '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	if (TransformSubjects.Contains(SubjectName))
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("RegisterTransformSubject: '%s' already registered"), 
		       *SubjectName.ToString());
		return;
	}
	
	// Ensure LiveLink source exists (create on first registration)
	EnsureLiveLinkSource();
	
	if (!bLiveLinkSourceCreated)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("RegisterTransformSubject: LiveLink source not available, cannot register '%s'"), 
		       *SubjectName.ToString());
		// Still track locally for later retry
		TransformSubjects.Add(SubjectName, FSubjectInfo());
		return;
	}
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubject: Registering '%s' (no properties)"), 
	       *SubjectName.ToString());
	
	// Create static data (structure definition - sent once per subject)
	FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
	FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
	
	if (!TransformStaticData)
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("RegisterTransformSubject: Failed to cast static data for '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	// No properties for basic transform subject
	TransformStaticData->PropertyNames.Empty();
	
	// Push static data to LiveLink via Message Bus Provider
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubject: Broadcasting static data via Message Bus..."));
	
	LiveLinkProvider->UpdateSubjectStaticData(
		SubjectName,
		ULiveLinkTransformRole::StaticClass(),
		MoveTemp(StaticData));
	
	// Track locally
	TransformSubjects.Add(SubjectName, FSubjectInfo());
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubject: ✅ Successfully registered '%s' via Message Bus"), 
	       *SubjectName.ToString());
}

void FLiveLinkBridge::RegisterTransformSubjectWithProperties(
	const FName& SubjectName, 
	const TArray<FName>& PropertyNames)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("RegisterTransformSubjectWithProperties: Not initialized, ignoring '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	if (TransformSubjects.Contains(SubjectName))
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("RegisterTransformSubjectWithProperties: '%s' already registered"), 
		       *SubjectName.ToString());
		return;
	}
	
	// Ensure LiveLink source exists
	EnsureLiveLinkSource();
	
	if (!bLiveLinkSourceCreated)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("RegisterTransformSubjectWithProperties: LiveLink source not available, cannot register '%s'"), 
		       *SubjectName.ToString());
		// Still track locally
		TransformSubjects.Add(SubjectName, FSubjectInfo(PropertyNames));
		return;
	}
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubjectWithProperties: Registering '%s' with %d properties"), 
	       *SubjectName.ToString(), 
	       PropertyNames.Num());
	
	// Log property names
	for (int32 i = 0; i < PropertyNames.Num(); i++)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("  Property[%d]: '%s'"), 
		       i, 
		       *PropertyNames[i].ToString());
	}
	
	// Create static data with property names
	FLiveLinkStaticDataStruct StaticData(FLiveLinkTransformStaticData::StaticStruct());
	FLiveLinkTransformStaticData* TransformStaticData = StaticData.Cast<FLiveLinkTransformStaticData>();
	
	if (!TransformStaticData)
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("RegisterTransformSubjectWithProperties: Failed to cast static data for '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	// Set property names
	TransformStaticData->PropertyNames = PropertyNames;
	
	// Push static data to LiveLink via Message Bus Provider
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubjectWithProperties: Broadcasting static data with properties via Message Bus..."));
	
	LiveLinkProvider->UpdateSubjectStaticData(
		SubjectName,
		ULiveLinkTransformRole::StaticClass(),
		MoveTemp(StaticData));
	
	// Track locally with properties
	TransformSubjects.Add(SubjectName, FSubjectInfo(PropertyNames));
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubjectWithProperties: ✅ Successfully registered '%s' with %d properties via Message Bus"), 
	       *SubjectName.ToString(), 
	       PropertyNames.Num());
}

void FLiveLinkBridge::UpdateTransformSubject(
	const FName& SubjectName, 
	const FTransform& Transform)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		// Throttle logging for high-frequency updates
		static int32 NotInitializedCount = 0;
		if (++NotInitializedCount % 60 == 1)
		{
			UE_LOG(LogUnrealLiveLinkNative, Warning, 
			       TEXT("UpdateTransformSubject: Not initialized (count: %d)"), 
			       NotInitializedCount);
		}
		return;
	}
	
	// Auto-register if not already registered (matches mock behavior)
	if (!TransformSubjects.Contains(SubjectName))
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("UpdateTransformSubject: Auto-registering '%s'"), 
		       *SubjectName.ToString());
		
		RegisterTransformSubject(SubjectName);
	}
	
	// Check if LiveLink source available
	if (!bLiveLinkSourceCreated)
	{
		// Throttle warning
		static int32 NoSourceCount = 0;
		if (++NoSourceCount % 60 == 1)
		{
			UE_LOG(LogUnrealLiveLinkNative, Warning, 
			       TEXT("UpdateTransformSubject: LiveLink source not available (count: %d)"), 
			       NoSourceCount);
		}
		return;
	}
	
	// Create frame data
	FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
	FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
	
	if (!TransformFrameData)
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("UpdateTransformSubject: Failed to cast frame data for '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	// Set transform and timestamp
	TransformFrameData->Transform = Transform;
	TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
	
	// Push frame data to LiveLink via Message Bus Provider
	LiveLinkProvider->UpdateSubjectFrameData(
		SubjectName,
		MoveTemp(FrameData));
	
	// Throttle success logging for high-frequency updates
	static int32 UpdateCount = 0;
	if (++UpdateCount % 60 == 1)
	{
		FVector Location = Transform.GetLocation();
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("UpdateTransformSubject: '%s' (count: %d) - Location: (%.2f, %.2f, %.2f) [Message Bus]"), 
		       *SubjectName.ToString(), 
		       UpdateCount, 
		       Location.X, Location.Y, Location.Z);
	}
}

void FLiveLinkBridge::UpdateTransformSubjectWithProperties(
	const FName& SubjectName, 
	const FTransform& Transform, 
	const TArray<float>& PropertyValues)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		static int32 NotInitializedCount = 0;
		if (++NotInitializedCount % 60 == 1)
		{
			UE_LOG(LogUnrealLiveLinkNative, Warning, 
			       TEXT("UpdateTransformSubjectWithProperties: Not initialized (count: %d)"), 
			       NotInitializedCount);
		}
		return;
	}
	
	// Validate property count
	if (const FSubjectInfo* SubjectInfo = TransformSubjects.Find(SubjectName))
	{
		if (SubjectInfo->ExpectedPropertyCount != PropertyValues.Num())
		{
			UE_LOG(LogUnrealLiveLinkNative, Error, 
			       TEXT("UpdateTransformSubjectWithProperties: Property count mismatch for '%s' - expected %d, got %d"), 
			       *SubjectName.ToString(), 
			       SubjectInfo->ExpectedPropertyCount, 
			       PropertyValues.Num());
			return;
		}
	}
	else
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("UpdateTransformSubjectWithProperties: '%s' not registered, cannot validate property count"), 
		       *SubjectName.ToString());
	}
	
	// Check if LiveLink source available
	if (!bLiveLinkSourceCreated)
	{
		static int32 NoSourceCount = 0;
		if (++NoSourceCount % 60 == 1)
		{
			UE_LOG(LogUnrealLiveLinkNative, Warning, 
			       TEXT("UpdateTransformSubjectWithProperties: LiveLink source not available (count: %d)"), 
			       NoSourceCount);
		}
		return;
	}
	
	// Create frame data
	FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
	FLiveLinkTransformFrameData* TransformFrameData = FrameData.Cast<FLiveLinkTransformFrameData>();
	
	if (!TransformFrameData)
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("UpdateTransformSubjectWithProperties: Failed to cast frame data for '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	// Set transform, timestamp, and properties
	TransformFrameData->Transform = Transform;
	TransformFrameData->WorldTime = FLiveLinkWorldTime(FPlatformTime::Seconds());
	TransformFrameData->PropertyValues = PropertyValues;
	
	// Push frame data to LiveLink via Message Bus Provider
	LiveLinkProvider->UpdateSubjectFrameData(
		SubjectName,
		MoveTemp(FrameData));
	
	// Throttle success logging
	static int32 UpdateCount = 0;
	if (++UpdateCount % 60 == 1)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("UpdateTransformSubjectWithProperties: '%s' (count: %d) with %d properties [Message Bus]"), 
		       *SubjectName.ToString(), 
		       UpdateCount, 
		       PropertyValues.Num());
	}
}

void FLiveLinkBridge::RemoveTransformSubject(const FName& SubjectName)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("RemoveTransformSubject: Not initialized, ignoring '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	if (TransformSubjects.Remove(SubjectName) > 0)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("RemoveTransformSubject: Removed '%s' from local tracking"), 
		       *SubjectName.ToString());
		
		// Remove from LiveLink if provider exists
		if (bLiveLinkSourceCreated && LiveLinkProvider.IsValid())
		{
			LiveLinkProvider->RemoveSubject(SubjectName);
			UE_LOG(LogUnrealLiveLinkNative, Log, 
			       TEXT("RemoveTransformSubject: ✅ Removed '%s' from LiveLink via Message Bus"), 
			       *SubjectName.ToString());
		}
	}
	else
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("RemoveTransformSubject: '%s' not found (safe to call on non-existent subjects)"), 
		       *SubjectName.ToString());
	}
}

//=============================================================================
// Data Subjects
//=============================================================================

void FLiveLinkBridge::RegisterDataSubject(
	const FName& SubjectName, 
	const TArray<FName>& PropertyNames)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("RegisterDataSubject: Not initialized, ignoring '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	if (DataSubjects.Contains(SubjectName))
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("RegisterDataSubject: '%s' already registered"), 
		       *SubjectName.ToString());
		return;
	}
	
	// Register with properties
	DataSubjects.Add(SubjectName, FSubjectInfo(PropertyNames));
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterDataSubject: Registered '%s' with %d properties"), 
	       *SubjectName.ToString(), 
	       PropertyNames.Num());
	
	// Log property names
	for (int32 i = 0; i < PropertyNames.Num(); i++)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("  Property[%d]: '%s'"), 
		       i, 
		       *PropertyNames[i].ToString());
	}
	
	// TODO: Register with LiveLink as BasicRole
}

void FLiveLinkBridge::UpdateDataSubject(
	const FName& SubjectName, 
	const TArray<float>& PropertyValues)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		static int32 NotInitializedCount = 0;
		if (++NotInitializedCount % 60 == 1)
		{
			UE_LOG(LogUnrealLiveLinkNative, Warning, 
			       TEXT("UpdateDataSubject: Not initialized (count: %d)"), 
			       NotInitializedCount);
		}
		return;
	}
	
	// Validate property count
	if (const FSubjectInfo* SubjectInfo = DataSubjects.Find(SubjectName))
	{
		if (SubjectInfo->ExpectedPropertyCount != PropertyValues.Num())
		{
			UE_LOG(LogUnrealLiveLinkNative, Error, 
			       TEXT("UpdateDataSubject: Property count mismatch for '%s' - expected %d, got %d"), 
			       *SubjectName.ToString(), 
			       SubjectInfo->ExpectedPropertyCount, 
			       PropertyValues.Num());
			return;
		}
	}
	else
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("UpdateDataSubject: '%s' not registered, cannot validate property count"), 
		       *SubjectName.ToString());
	}
	
	// Throttle logging
	static int32 UpdateCount = 0;
	if (++UpdateCount % 60 == 1)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("UpdateDataSubject: '%s' (count: %d) with %d properties"), 
		       *SubjectName.ToString(), 
		       UpdateCount, 
		       PropertyValues.Num());
	}
	
	// TODO: Push data frame to LiveLink
}

void FLiveLinkBridge::RemoveDataSubject(const FName& SubjectName)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		UE_LOG(LogUnrealLiveLinkNative, Warning, 
		       TEXT("RemoveDataSubject: Not initialized, ignoring '%s'"), 
		       *SubjectName.ToString());
		return;
	}
	
	if (DataSubjects.Remove(SubjectName) > 0)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("RemoveDataSubject: Removed '%s'"), 
		       *SubjectName.ToString());
		
		// TODO: Mark subject as removed in LiveLink
	}
	else
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("RemoveDataSubject: '%s' not found (safe to call on non-existent subjects)"), 
		       *SubjectName.ToString());
	}
}

//=============================================================================
// FName Caching (Performance Optimization)
//=============================================================================

FName FLiveLinkBridge::GetCachedName(const char* cString)
{
	FScopeLock Lock(&CriticalSection);
	
	if (!cString || cString[0] == '\0')
	{
		return NAME_None;
	}
	
	FString StringKey = UTF8_TO_TCHAR(cString);
	
	// Check cache first
	if (FName* CachedName = NameCache.Find(StringKey))
	{
		// Cache hit - fast path
		return *CachedName;
	}
	
	// Cache miss - create and store
	FName NewName(*StringKey);
	NameCache.Add(StringKey, NewName);
	
	return NewName;
}
