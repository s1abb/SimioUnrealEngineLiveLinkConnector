#include "LiveLinkBridge.h"
#include "UnrealLiveLink.Native.h"
#include "Math/Transform.h"
#include "Misc/ScopeLock.h"
#include "Misc/CString.h"

// LiveLink includes (Sub-Phase 6.6)
#include "ILiveLinkClient.h"
#include "ILiveLinkSource.h"
#include "Features/IModularFeatures.h"
#include "LiveLinkTypes.h"
#include "Roles/LiveLinkTransformRole.h"
#include "Roles/LiveLinkTransformTypes.h"

//=============================================================================
// Minimal LiveLink Source Implementation (Sub-Phase 6.6)
//=============================================================================
// Temporary minimal source until we can access LiveLinkMessageBusSource
// This allows us to test the LiveLink integration without plugin dependencies
//=============================================================================
class FSimioLiveLinkSource : public ILiveLinkSource
{
public:
	FSimioLiveLinkSource(const FText& InSourceType, const FText& InSourceMachineName)
		: SourceType(InSourceType), SourceMachineName(InSourceMachineName)
	{
	}

	virtual void ReceiveClient(ILiveLinkClient* InClient, FGuid InSourceGuid) override
	{
		Client = InClient;
		SourceGuid = InSourceGuid;
	}

	virtual bool IsSourceStillValid() const override { return true; }
	virtual bool RequestSourceShutdown() override { return true; }
	virtual FText GetSourceType() const override { return SourceType; }
	virtual FText GetSourceMachineName() const override { return SourceMachineName; }
	virtual FText GetSourceStatus() const override { return FText::FromString(TEXT("Active")); }

private:
	FText SourceType;
	FText SourceMachineName;
	ILiveLinkClient* Client = nullptr;
	FGuid SourceGuid;
};

//=============================================================================
// Sub-Phase 6.6: LiveLinkBridge Implementation with Transform Subject Registration
//=============================================================================
// This implementation creates actual LiveLink Message Bus source and streams
// transform data to Unreal Engine's LiveLink subsystem.
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
	
	// Remove LiveLink source if created (Sub-Phase 6.6)
	if (bLiveLinkSourceCreated && LiveLinkSourceGuid.IsValid())
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("Shutdown: Removing LiveLink source (GUID: %s)"), 
		       *LiveLinkSourceGuid.ToString());
		
		ILiveLinkClient* Client = &IModularFeatures::Get()
			.GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
		
		if (Client)
		{
			Client->RemoveSource(LiveLinkSourceGuid);
			UE_LOG(LogUnrealLiveLinkNative, Log, 
			       TEXT("Shutdown: ✅ LiveLink source removed successfully"));
		}
		else
		{
			UE_LOG(LogUnrealLiveLinkNative, Warning, 
			       TEXT("Shutdown: ILiveLinkClient not available, cannot remove source"));
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
	
	// Sub-Phase 6.6: Check if LiveLink source is created
	if (bLiveLinkSourceCreated && LiveLinkSourceGuid.IsValid())
	{
		return ULL_OK;
	}
	
	// Sub-Phase 6.5: Check if LiveLink framework is ready
	if (bLiveLinkReady)
	{
		return ULL_OK;
	}
	
	return ULL_NOT_CONNECTED;
}

//=============================================================================
// Helper Methods (Sub-Phase 6.6)
//=============================================================================

void FLiveLinkBridge::EnsureLiveLinkSource()
{
	// Note: Caller must hold CriticalSection lock
	
	if (bLiveLinkSourceCreated)
	{
		ULL_VERBOSE_LOG(TEXT("EnsureLiveLinkSource: Source already exists, skipping"));
		return;
	}
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Attempting to create LiveLink source for provider '%s'"), 
	       *ProviderName);
	
	// Get LiveLink client from modular features
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Getting ILiveLinkClient from modular features..."));
	
	ILiveLinkClient* Client = &IModularFeatures::Get()
		.GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
	
	if (!Client)
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("EnsureLiveLinkSource: ❌ Failed to get ILiveLinkClient - LiveLink subsystem not available"));
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("EnsureLiveLinkSource: Is Unreal Engine running? Is LiveLink plugin enabled?"));
		return;
	}
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: ✅ ILiveLinkClient obtained successfully"));
	
	// Create custom LiveLink source (Sub-Phase 6.6)
	// Note: Using FSimioLiveLinkSource instead of FLiveLinkMessageBusSource
	// to avoid plugin dependency issues with Program targets
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: Creating FSimioLiveLinkSource..."));
	
	TSharedPtr<ILiveLinkSource> Source = MakeShared<FSimioLiveLinkSource>(
		FText::FromString(TEXT("Simio Connector")),
		FText::FromString(ProviderName));
	
	if (!Source.IsValid())
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("EnsureLiveLinkSource: ❌ Failed to create FSimioLiveLinkSource"));
		return;
	}
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: ✅ Source created, adding to LiveLink client..."));
	
	// Add source to LiveLink client
	LiveLinkSourceGuid = Client->AddSource(Source);
	
	if (!LiveLinkSourceGuid.IsValid())
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("EnsureLiveLinkSource: ❌ Failed to add source to LiveLink client (invalid GUID returned)"));
		return;
	}
	
	bLiveLinkSourceCreated = true;
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("EnsureLiveLinkSource: ✅ SUCCESS! Source added with GUID: %s"), 
	       *LiveLinkSourceGuid.ToString());
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
	
	// Get LiveLink client
	ILiveLinkClient* Client = &IModularFeatures::Get()
		.GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
	
	if (!Client)
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("RegisterTransformSubject: ILiveLinkClient not available"));
		return;
	}
	
	// Push static data to LiveLink
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubject: Pushing static data to LiveLink..."));
	
	Client->PushSubjectStaticData_AnyThread(
		FLiveLinkSubjectKey(LiveLinkSourceGuid, SubjectName),
		ULiveLinkTransformRole::StaticClass(),
		MoveTemp(StaticData));
	
	// Track locally
	TransformSubjects.Add(SubjectName, FSubjectInfo());
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubject: ✅ Successfully registered '%s'"), 
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
	
	// Get LiveLink client
	ILiveLinkClient* Client = &IModularFeatures::Get()
		.GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
	
	if (!Client)
	{
		UE_LOG(LogUnrealLiveLinkNative, Error, 
		       TEXT("RegisterTransformSubjectWithProperties: ILiveLinkClient not available"));
		return;
	}
	
	// Push static data to LiveLink
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubjectWithProperties: Pushing static data with properties to LiveLink..."));
	
	Client->PushSubjectStaticData_AnyThread(
		FLiveLinkSubjectKey(LiveLinkSourceGuid, SubjectName),
		ULiveLinkTransformRole::StaticClass(),
		MoveTemp(StaticData));
	
	// Track locally with properties
	TransformSubjects.Add(SubjectName, FSubjectInfo(PropertyNames));
	
	UE_LOG(LogUnrealLiveLinkNative, Log, 
	       TEXT("RegisterTransformSubjectWithProperties: ✅ Successfully registered '%s' with %d properties"), 
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
	
	// Get LiveLink client
	ILiveLinkClient* Client = &IModularFeatures::Get()
		.GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
	
	if (!Client)
	{
		static int32 NoClientCount = 0;
		if (++NoClientCount % 60 == 1)
		{
			UE_LOG(LogUnrealLiveLinkNative, Error, 
			       TEXT("UpdateTransformSubject: ILiveLinkClient not available (count: %d)"), 
			       NoClientCount);
		}
		return;
	}
	
	// Push frame data to LiveLink
	Client->PushSubjectFrameData_AnyThread(
		FLiveLinkSubjectKey(LiveLinkSourceGuid, SubjectName),
		MoveTemp(FrameData));
	
	// Throttle success logging for high-frequency updates
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
	
	// Get LiveLink client
	ILiveLinkClient* Client = &IModularFeatures::Get()
		.GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
	
	if (!Client)
	{
		static int32 NoClientCount = 0;
		if (++NoClientCount % 60 == 1)
		{
			UE_LOG(LogUnrealLiveLinkNative, Error, 
			       TEXT("UpdateTransformSubjectWithProperties: ILiveLinkClient not available (count: %d)"), 
			       NoClientCount);
		}
		return;
	}
	
	// Push frame data to LiveLink
	Client->PushSubjectFrameData_AnyThread(
		FLiveLinkSubjectKey(LiveLinkSourceGuid, SubjectName),
		MoveTemp(FrameData));
	
	// Throttle success logging
	static int32 UpdateCount = 0;
	if (++UpdateCount % 60 == 1)
	{
		UE_LOG(LogUnrealLiveLinkNative, Log, 
		       TEXT("UpdateTransformSubjectWithProperties: '%s' (count: %d) with %d properties"), 
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
		
		// Remove from LiveLink if source exists
		if (bLiveLinkSourceCreated && LiveLinkSourceGuid.IsValid())
		{
			ILiveLinkClient* Client = &IModularFeatures::Get()
				.GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);
			
			if (Client)
			{
				Client->RemoveSubject_AnyThread(FLiveLinkSubjectKey(LiveLinkSourceGuid, SubjectName));
				UE_LOG(LogUnrealLiveLinkNative, Log, 
				       TEXT("RemoveTransformSubject: ✅ Removed '%s' from LiveLink"), 
				       *SubjectName.ToString());
			}
			else
			{
				UE_LOG(LogUnrealLiveLinkNative, Warning, 
				       TEXT("RemoveTransformSubject: ILiveLinkClient not available, cannot remove from LiveLink"));
			}
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
