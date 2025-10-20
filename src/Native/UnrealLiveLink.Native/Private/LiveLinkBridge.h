#pragma once

#include "CoreMinimal.h"
#include "HAL/CriticalSection.h"
#include "Math/Transform.h"
#include "UnrealLiveLink.Types.h"

// LiveLink includes (Sub-Phase 6.5)
// Note: For DLL usage, we cannot use ILiveLinkProvider (requires full UE engine loop).
// Sub-Phase 6.5 focuses on architectural readiness for LiveLink integration.
// Actual source creation will happen when subjects are registered in Sub-Phase 6.6+

//=============================================================================
// Sub-Phase 6.5: LiveLinkBridge with Message Bus Source
//=============================================================================
// This class manages LiveLink state AND actual LiveLink Message Bus integration.
// 
// Implementation Status:
// - Sub-Phase 6.4 (Complete): State tracking, thread safety, FName caching
// - Sub-Phase 6.5 (Current): LiveLink Message Bus source creation
// - Sub-Phase 6.6: Transform subject registration/updates
// - Sub-Phase 6.9: Property streaming
//=============================================================================

/// <summary>
/// Subject information with property metadata
/// </summary>
struct FSubjectInfo
{
	TArray<FName> PropertyNames;
	int32 ExpectedPropertyCount;
	
	FSubjectInfo() 
		: ExpectedPropertyCount(0) 
	{}
	
	FSubjectInfo(const TArray<FName>& InPropertyNames) 
		: PropertyNames(InPropertyNames)
		, ExpectedPropertyCount(InPropertyNames.Num()) 
	{}
};

/// <summary>
/// Singleton managing LiveLink state and connections
/// Thread-safe implementation with FCriticalSection
/// </summary>
class FLiveLinkBridge
{
public:
	/// <summary>
	/// Get singleton instance
	/// </summary>
	static FLiveLinkBridge& Get();
	
	//=============================================================================
	// Lifecycle Management
	//=============================================================================
	
	/// <summary>
	/// Initialize the LiveLink bridge
	/// </summary>
	/// <param name="InProviderName">Provider name displayed in Unreal's LiveLink window</param>
	/// <returns>true if initialized successfully, false if already initialized</returns>
	bool Initialize(const FString& InProviderName);
	
	/// <summary>
	/// Shutdown the LiveLink bridge and clear all state
	/// Safe to call multiple times. Allows re-initialization.
	/// </summary>
	void Shutdown();
	
	/// <summary>
	/// Check if bridge is initialized
	/// </summary>
	bool IsInitialized() const { return bInitialized; }
	
	/// <summary>
	/// Get connection status for ULL_IsConnected API
	/// </summary>
	/// <returns>ULL_OK if connected, ULL_NOT_INITIALIZED or ULL_NOT_CONNECTED otherwise</returns>
	int GetConnectionStatus() const;
	
	//=============================================================================
	// Transform Subjects (3D Objects with position/rotation/scale)
	//=============================================================================
	
	/// <summary>
	/// Register a transform subject without properties
	/// </summary>
	void RegisterTransformSubject(const FName& SubjectName);
	
	/// <summary>
	/// Register a transform subject with custom properties
	/// </summary>
	void RegisterTransformSubjectWithProperties(const FName& SubjectName, const TArray<FName>& PropertyNames);
	
	/// <summary>
	/// Update transform for a subject (auto-registers if needed)
	/// </summary>
	void UpdateTransformSubject(const FName& SubjectName, const FTransform& Transform);
	
	/// <summary>
	/// Update transform and properties for a subject
	/// </summary>
	void UpdateTransformSubjectWithProperties(const FName& SubjectName, const FTransform& Transform, const TArray<float>& PropertyValues);
	
	/// <summary>
	/// Remove a transform subject
	/// </summary>
	void RemoveTransformSubject(const FName& SubjectName);
	
	//=============================================================================
	// Data Subjects (Properties only, no 3D representation)
	//=============================================================================
	
	/// <summary>
	/// Register a data-only subject
	/// </summary>
	void RegisterDataSubject(const FName& SubjectName, const TArray<FName>& PropertyNames);
	
	/// <summary>
	/// Update data subject property values
	/// </summary>
	void UpdateDataSubject(const FName& SubjectName, const TArray<float>& PropertyValues);
	
	/// <summary>
	/// Remove a data subject
	/// </summary>
	void RemoveDataSubject(const FName& SubjectName);
	
	//=============================================================================
	// FName Caching (Performance Optimization)
	//=============================================================================
	
	/// <summary>
	/// Get cached FName for C string (optimizes repeated conversions)
	/// </summary>
	/// <param name="cString">UTF-8 C string</param>
	/// <returns>Cached FName or newly created and cached FName</returns>
	FName GetCachedName(const char* cString);
	
	//=============================================================================
	// Singleton Pattern (No Copy/Move)
	//=============================================================================
	
	FLiveLinkBridge(const FLiveLinkBridge&) = delete;
	FLiveLinkBridge& operator=(const FLiveLinkBridge&) = delete;
	FLiveLinkBridge(FLiveLinkBridge&&) = delete;
	FLiveLinkBridge& operator=(FLiveLinkBridge&&) = delete;
	
private:
	/// <summary>
	/// Private constructor for singleton
	/// </summary>
	FLiveLinkBridge() = default;
	
	/// <summary>
	/// Private destructor
	/// </summary>
	~FLiveLinkBridge() = default;
	
	//=============================================================================
	// Member Variables
	//=============================================================================
	
	bool bInitialized = false;
	FString ProviderName;
	
	// LiveLink integration flags (Sub-Phase 6.5)
	// Note: Actual LiveLink source will be created on-demand in Sub-Phase 6.6
	//       when first subject is registered
	bool bLiveLinkReady = false;
	
	// Subject tracking with property metadata
	TMap<FName, FSubjectInfo> TransformSubjects;
	TMap<FName, FSubjectInfo> DataSubjects;
	
	// FName cache for performance
	TMap<FString, FName> NameCache;
	
	// Thread safety
	mutable FCriticalSection CriticalSection;
};
