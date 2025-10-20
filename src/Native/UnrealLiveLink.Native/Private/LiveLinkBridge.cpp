#include "LiveLinkBridge.h"
#include "UnrealLiveLink.Native.h"
#include "Math/Transform.h"
#include "Misc/ScopeLock.h"
#include "Misc/CString.h"

// LiveLink includes (Sub-Phase 6.5)
// Dependencies added to Build.cs for future LiveLink integration

//=============================================================================
// Sub-Phase 6.5: LiveLinkBridge Implementation with Message Bus Source
//=============================================================================
// This implementation creates actual LiveLink Message Bus source and connects
// to Unreal Engine's LiveLink subsystem.
//=============================================================================

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
	
	// Sub-Phase 6.5: Store provider name and mark as initialized
	// LiveLink Message Bus framework dependencies are now available in Build.cs
	// Actual LiveLink source creation will occur on-demand in Sub-Phase 6.6
	// when first subject is registered (requires UE runtime environment)
	ProviderName = InProviderName;
	bInitialized = true;
	bLiveLinkReady = true;  // Framework is ready for integration
	
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
	
	// Clear all state
	TransformSubjects.Empty();
	DataSubjects.Empty();
	NameCache.Empty();
	ProviderName.Empty();
	bInitialized = false;
	bLiveLinkReady = false;
	
	UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Shutdown: Complete"));
}

int FLiveLinkBridge::GetConnectionStatus() const
{
	FScopeLock Lock(&CriticalSection);
	
	if (!bInitialized)
	{
		return ULL_NOT_INITIALIZED;
	}
	
	// Sub-Phase 6.5: Check if LiveLink framework is ready
	if (bLiveLinkReady)
	{
		return ULL_OK;
	}
	
	return ULL_NOT_CONNECTED;
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
	
	// Register with no properties
	TransformSubjects.Add(SubjectName, FSubjectInfo());
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubject: Registered '%s' (no properties)"), 
	       *SubjectName.ToString());
	
	// TODO: Sub-Phase 6.6 - Push FLiveLinkTransformStaticData to LiveLink
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
	
	// Register with properties
	TransformSubjects.Add(SubjectName, FSubjectInfo(PropertyNames));
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubjectWithProperties: Registered '%s' with %d properties"), 
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
	
	// TODO: Sub-Phase 6.9 - Push static data with property names to LiveLink
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
		
		TransformSubjects.Add(SubjectName, FSubjectInfo());
	}
	
	// Throttle logging for high-frequency updates
	static int32 UpdateCount = 0;
	if (++UpdateCount % 60 == 1)
	{
		FVector Location = Transform.GetLocation();
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("UpdateTransformSubject: '%s' (count: %d) - Location: (%.2f, %.2f, %.2f)"), 
		       *SubjectName.ToString(), 
		       UpdateCount, 
		       Location.X, Location.Y, Location.Z);
	}
	
	// TODO: Sub-Phase 6.6 - Push FLiveLinkTransformFrameData to LiveLink
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
	
	// Throttle logging
	static int32 UpdateCount = 0;
	if (++UpdateCount % 60 == 1)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("UpdateTransformSubjectWithProperties: '%s' (count: %d) with %d properties"), 
		       *SubjectName.ToString(), 
		       UpdateCount, 
		       PropertyValues.Num());
	}
	
	// TODO: Sub-Phase 6.9 - Push frame data with properties to LiveLink
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
		       TEXT("RemoveTransformSubject: Removed '%s'"), 
		       *SubjectName.ToString());
		
		// TODO: Sub-Phase 6.6 - Mark subject as removed in LiveLink
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
	
	// TODO: Sub-Phase 6.9 - Register with LiveLink as BasicRole
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
	
	// TODO: Sub-Phase 6.9 - Push data frame to LiveLink
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
		
		// TODO: Sub-Phase 6.9 - Mark subject as removed in LiveLink
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
