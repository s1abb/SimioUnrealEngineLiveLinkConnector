#pragma once

#ifdef __cplusplus
extern "C" {
#endif

// Transform structure matching C# ULL_Transform exactly
// Must maintain binary compatibility for P/Invoke marshaling
typedef struct {
    double position[3];    // X, Y, Z position in centimeters (Unreal units)
    double rotation[4];    // Quaternion X, Y, Z, W (normalized)
    double scale[3];       // X, Y, Z scale factors (typically 1.0)
} ULL_Transform;

//
// Core Lifecycle API - MUST MATCH UnrealLiveLinkNative.cs EXACTLY
//

/// <summary>
/// Initialize LiveLink with the specified provider name
/// </summary>
/// <param name="providerName">Name for this LiveLink provider (e.g., "SimioSimulation")</param>
/// <returns>0 on success, error code on failure</returns>
__declspec(dllexport) int ULL_Initialize(const char* providerName);

/// <summary>
/// Shutdown LiveLink and clean up all resources
/// </summary>
__declspec(dllexport) void ULL_Shutdown();

/// <summary>
/// Get API version number
/// </summary>
/// <returns>Version number for compatibility checking</returns>
__declspec(dllexport) int ULL_GetVersion();

/// <summary>
/// Check if connected to Unreal Engine
/// </summary>
/// <returns>0 if connected, error code otherwise</returns>
__declspec(dllexport) int ULL_IsConnected();

//
// Transform Subjects (3D Objects) - MUST MATCH UnrealLiveLinkNative.cs EXACTLY
//

/// <summary>
/// Register a transform subject (3D object)
/// </summary>
/// <param name="subjectName">Unique identifier for this object</param>
__declspec(dllexport) void ULL_RegisterObject(const char* subjectName);

/// <summary>
/// Register a transform subject with custom properties
/// </summary>
/// <param name="subjectName">Unique identifier for this object</param>
/// <param name="propertyNames">Array of property name strings</param>
/// <param name="propertyCount">Number of properties in the array</param>
__declspec(dllexport) void ULL_RegisterObjectWithProperties(
    const char* subjectName, 
    const char** propertyNames, 
    int propertyCount
);

/// <summary>
/// Update transform for an object
/// </summary>
/// <param name="subjectName">Object identifier</param>
/// <param name="transform">Transform data</param>
__declspec(dllexport) void ULL_UpdateObject(
    const char* subjectName, 
    const ULL_Transform* transform
);

/// <summary>
/// Update transform and property values for an object
/// </summary>
/// <param name="subjectName">Object identifier</param>
/// <param name="transform">Transform data</param>
/// <param name="propertyValues">Array of property values (float)</param>
/// <param name="propertyCount">Number of values in the array</param>
__declspec(dllexport) void ULL_UpdateObjectWithProperties(
    const char* subjectName, 
    const ULL_Transform* transform,
    const float* propertyValues,
    int propertyCount
);

/// <summary>
/// Remove an object from LiveLink
/// </summary>
/// <param name="subjectName">Object identifier to remove</param>
__declspec(dllexport) void ULL_RemoveObject(const char* subjectName);

//
// Data Subjects (Metrics/KPIs) - MUST MATCH UnrealLiveLinkNative.cs EXACTLY
//

/// <summary>
/// Register a data-only subject (no 3D representation)
/// </summary>
/// <param name="subjectName">Unique identifier</param>
/// <param name="propertyNames">Array of property name strings</param>
/// <param name="propertyCount">Number of properties in the array</param>
__declspec(dllexport) void ULL_RegisterDataSubject(
    const char* subjectName, 
    const char** propertyNames, 
    int propertyCount
);

/// <summary>
/// Update property values for a data subject
/// </summary>
/// <param name="subjectName">Subject identifier</param>
/// <param name="propertyNames">Array of property names (can be NULL if already registered)</param>
/// <param name="propertyValues">Array of property values (float)</param>
/// <param name="propertyCount">Number of values in the array</param>
__declspec(dllexport) void ULL_UpdateDataSubject(
    const char* subjectName, 
    const char** propertyNames,
    const float* propertyValues,
    int propertyCount
);

/// <summary>
/// Remove a data subject from LiveLink
/// </summary>
/// <param name="subjectName">Subject identifier to remove</param>
__declspec(dllexport) void ULL_RemoveDataSubject(const char* subjectName);

#ifdef __cplusplus
}
#endif