#include "UnrealLiveLink.API.h"
#include "UnrealLiveLink.Native.h"
#include "LiveLinkBridge.h"
#include "Math/Transform.h"
#include "Misc/CString.h"

//=============================================================================
// Sub-Phase 6.4: C API Implementation with LiveLinkBridge
//=============================================================================
// This file implements all 12 C API functions using the LiveLinkBridge singleton.
// Functions perform parameter validation and delegate to FLiveLinkBridge.
//
// Implementation Status by Sub-Phase:
// - 6.3 (Complete): Stub functions with logging and validation
// - 6.4 (Current): LiveLinkBridge state tracking with thread safety
// - 6.5: Add actual FLiveLinkMessageBusSource
// - 6.6: Implement transform subject registration and updates
// - 6.9: Implement property and data subject support
//=============================================================================

//=============================================================================
// Helper Functions
//=============================================================================

/// <summary>
/// Convert ULL_Transform to Unreal's FTransform
/// </summary>
static FTransform ConvertToFTransform(const ULL_Transform* transform)
{
    if (!transform)
    {
        return FTransform::Identity;
    }
    
    // Position: centimeters (ULL_Transform uses cm, FVector uses cm)
    FVector Location(
        transform->position[0],
        transform->position[1],
        transform->position[2]
    );
    
    // Rotation: quaternion (X, Y, Z, W)
    FQuat Rotation(
        transform->rotation[0],
        transform->rotation[1],
        transform->rotation[2],
        transform->rotation[3]
    );
    
    // Scale: uniform or non-uniform
    FVector Scale(
        transform->scale[0],
        transform->scale[1],
        transform->scale[2]
    );
    
    return FTransform(Rotation, Location, Scale);
}

/// <summary>
/// Convert array of C strings to TArray of FNames using cached names
/// </summary>
static TArray<FName> ConvertPropertyNames(const char** propertyNames, int propertyCount)
{
    TArray<FName> Result;
    Result.Reserve(propertyCount);
    
    for (int i = 0; i < propertyCount; i++)
    {
        if (propertyNames[i])
        {
            Result.Add(FLiveLinkBridge::Get().GetCachedName(propertyNames[i]));
        }
        else
        {
            Result.Add(NAME_None);
        }
    }
    
    return Result;
}

/// <summary>
/// Convert array of floats to TArray
/// </summary>
static TArray<float> ConvertPropertyValues(const float* propertyValues, int propertyCount)
{
    TArray<float> Result;
    Result.Reserve(propertyCount);
    
    for (int i = 0; i < propertyCount; i++)
    {
        Result.Add(propertyValues[i]);
    }
    
    return Result;
}

//=============================================================================
// Lifecycle Management Implementation
//=============================================================================

extern "C"
{
    __declspec(dllexport) int ULL_Initialize(const char* providerName)
    {
        // Parameter validation
        if (!providerName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_Initialize: providerName is NULL"));
            return ULL_ERROR;
        }

        if (providerName[0] == '\0')
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_Initialize: providerName is empty"));
            return ULL_ERROR;
        }

        // Convert to FString and delegate to LiveLinkBridge
        FString ProviderNameStr = UTF8_TO_TCHAR(providerName);

        // LiveLinkBridge::Initialize is idempotent - always returns true if valid params
        bool bSuccess = FLiveLinkBridge::Get().Initialize(ProviderNameStr);
        
        UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_Initialize: %s"), 
               bSuccess ? TEXT("Success") : TEXT("Failed"));
        
        return bSuccess ? ULL_OK : ULL_ERROR;
    }

    __declspec(dllexport) void ULL_Shutdown()
    {
        UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_Shutdown called"));
        
        FLiveLinkBridge::Get().Shutdown();
        
        UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_Shutdown: Complete"));
    }

    __declspec(dllexport) int ULL_GetVersion()
    {
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("ULL_GetVersion called, returning %d"),
               ULL_API_VERSION);

        return ULL_API_VERSION;
    }

    __declspec(dllexport) int ULL_IsConnected()
    {
        int status = FLiveLinkBridge::Get().GetConnectionStatus();
        
        UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_IsConnected: Status = %d"), status);
        
        return status;
    }

//=============================================================================
// Transform Subjects Implementation
//=============================================================================

    __declspec(dllexport) void ULL_RegisterObject(const char* subjectName)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_RegisterObject: subjectName is NULL"));
            return;
        }

        FString SubjectNameStr = UTF8_TO_TCHAR(subjectName);
        UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_RegisterObject: '%s'"), *SubjectNameStr);

        // Convert to FName and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        FLiveLinkBridge::Get().RegisterTransformSubject(SubjectFName);
    }

    __declspec(dllexport) void ULL_RegisterObjectWithProperties(
        const char* subjectName,
        const char** propertyNames,
        int propertyCount)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_RegisterObjectWithProperties: subjectName is NULL"));
            return;
        }

        if (propertyCount > 0 && !propertyNames)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_RegisterObjectWithProperties: propertyNames is NULL but propertyCount is %d"),
                   propertyCount);
            return;
        }

        if (propertyCount < 0)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_RegisterObjectWithProperties: propertyCount is negative (%d)"),
                   propertyCount);
            return;
        }

        FString SubjectNameStr = UTF8_TO_TCHAR(subjectName);
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("ULL_RegisterObjectWithProperties: '%s' with %d properties"),
               *SubjectNameStr, propertyCount);

        // Convert parameters and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        TArray<FName> PropNamesArray = ConvertPropertyNames(propertyNames, propertyCount);
        
        FLiveLinkBridge::Get().RegisterTransformSubjectWithProperties(SubjectFName, PropNamesArray);
    }

    __declspec(dllexport) void ULL_UpdateObject(
        const char* subjectName,
        const ULL_Transform* transform)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_UpdateObject: subjectName is NULL"));
            return;
        }

        if (!transform)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_UpdateObject: transform is NULL"));
            return;
        }

        // Convert parameters and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        FTransform UnrealTransform = ConvertToFTransform(transform);
        
        FLiveLinkBridge::Get().UpdateTransformSubject(SubjectFName, UnrealTransform);
    }

    __declspec(dllexport) void ULL_UpdateObjectWithProperties(
        const char* subjectName,
        const ULL_Transform* transform,
        const float* propertyValues,
        int propertyCount)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_UpdateObjectWithProperties: subjectName is NULL"));
            return;
        }

        if (!transform)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_UpdateObjectWithProperties: transform is NULL"));
            return;
        }

        if (propertyCount > 0 && !propertyValues)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_UpdateObjectWithProperties: propertyValues is NULL but propertyCount is %d"),
                   propertyCount);
            return;
        }

        // Convert parameters and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        FTransform UnrealTransform = ConvertToFTransform(transform);
        TArray<float> PropertiesArray = ConvertPropertyValues(propertyValues, propertyCount);
        
        FLiveLinkBridge::Get().UpdateTransformSubjectWithProperties(
            SubjectFName, UnrealTransform, PropertiesArray);
    }

    __declspec(dllexport) void ULL_RemoveObject(const char* subjectName)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_RemoveObject: subjectName is NULL"));
            return;
        }

        FString SubjectNameStr = UTF8_TO_TCHAR(subjectName);
        UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_RemoveObject: '%s'"), *SubjectNameStr);

        // Convert to FName and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        FLiveLinkBridge::Get().RemoveTransformSubject(SubjectFName);
    }

//=============================================================================
// Data Subjects Implementation
//=============================================================================

    __declspec(dllexport) void ULL_RegisterDataSubject(
        const char* subjectName,
        const char** propertyNames,
        int propertyCount)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_RegisterDataSubject: subjectName is NULL"));
            return;
        }

        if (propertyCount > 0 && !propertyNames)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_RegisterDataSubject: propertyNames is NULL but propertyCount is %d"),
                   propertyCount);
            return;
        }

        if (propertyCount < 0)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_RegisterDataSubject: propertyCount is negative (%d)"),
                   propertyCount);
            return;
        }

        FString SubjectNameStr = UTF8_TO_TCHAR(subjectName);
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("ULL_RegisterDataSubject: '%s' with %d properties"),
               *SubjectNameStr, propertyCount);

        // Convert parameters and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        TArray<FName> PropNamesArray = ConvertPropertyNames(propertyNames, propertyCount);
        
        FLiveLinkBridge::Get().RegisterDataSubject(SubjectFName, PropNamesArray);
    }

    __declspec(dllexport) void ULL_UpdateDataSubject(
        const char* subjectName,
        const char** propertyNames,
        const float* propertyValues,
        int propertyCount)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_UpdateDataSubject: subjectName is NULL"));
            return;
        }

        if (propertyCount > 0 && !propertyValues)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_UpdateDataSubject: propertyValues is NULL but propertyCount is %d"),
                   propertyCount);
            return;
        }

        if (propertyCount < 0)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_UpdateDataSubject: propertyCount is negative (%d)"),
                   propertyCount);
            return;
        }

        // Convert parameters and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        TArray<float> PropertiesArray = ConvertPropertyValues(propertyValues, propertyCount);
        
        FLiveLinkBridge::Get().UpdateDataSubject(SubjectFName, PropertiesArray);
    }

    __declspec(dllexport) void ULL_RemoveDataSubject(const char* subjectName)
    {
        // Parameter validation
        if (!subjectName)
        {
            UE_LOG(LogUnrealLiveLinkNative, Error, 
                   TEXT("ULL_RemoveDataSubject: subjectName is NULL"));
            return;
        }

        FString SubjectNameStr = UTF8_TO_TCHAR(subjectName);
        UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_RemoveDataSubject: '%s'"), *SubjectNameStr);

        // Convert to FName and delegate to LiveLinkBridge
        FName SubjectFName = FLiveLinkBridge::Get().GetCachedName(subjectName);
        FLiveLinkBridge::Get().RemoveDataSubject(SubjectFName);
    }

} // extern "C"
