#pragma once

#include "UnrealLiveLink.Types.h"

//=============================================================================
// Unreal LiveLink C API
//=============================================================================
// This header defines the 12-function C API for LiveLink integration.
// All functions use extern "C" linkage and __declspec(dllexport) for P/Invoke.
//
// IMPORTANT: Function signatures must match UnrealLiveLinkNative.cs exactly.
//
// Calling Convention: __cdecl (implicit with extern "C")
// String Marshaling: ANSI (const char*)
// Array Marshaling: Pointer + count (no length prefix)
//
// Actual LiveLink integration via FLiveLinkMessageBusSource
//=============================================================================

#ifdef __cplusplus
extern "C" {
#endif

//=============================================================================
// Lifecycle Management (4 functions)
//=============================================================================

/// <summary>
/// Initialize LiveLink system with provider name.
/// Must be called before any other functions.
/// </summary>
/// <param name="providerName">Name displayed in Unreal's LiveLink window (e.g., "SimioSimulation")</param>
/// <returns>ULL_OK on success, ULL_ERROR on failure</returns>
/// <remarks>
/// Create FLiveLinkMessageBusSource
/// </remarks>
__declspec(dllexport) int ULL_Initialize(const char* providerName);

/// <summary>
/// Shutdown LiveLink system.
/// Flushes all messages and releases resources.
/// </summary>
/// <remarks>
/// Cleanup LiveLink resources
/// Safe to call multiple times.
/// </remarks>
__declspec(dllexport) void ULL_Shutdown();

/// <summary>
/// Get API version number for compatibility checking.
/// </summary>
/// <returns>API version (currently 1)</returns>
/// <remarks>
/// Always returns ULL_API_VERSION (1).
/// Used by managed layer to verify compatibility.
/// </remarks>
__declspec(dllexport) int ULL_GetVersion();

/// <summary>
/// Check if connected to Unreal Engine.
/// </summary>
/// <returns>ULL_OK if connected, ULL_NOT_CONNECTED if no connection, ULL_NOT_INITIALIZED if not initialized</returns>
/// <remarks>
/// Check actual LiveLink connection status
/// </remarks>
__declspec(dllexport) int ULL_IsConnected();

//=============================================================================
// Transform Subjects (5 functions) - 3D Objects
//=============================================================================

/// <summary>
/// Register a transform subject (3D object).
/// Call once per object before sending updates.
/// </summary>
/// <param name="subjectName">Unique identifier for this object (e.g., "Forklift_01")</param>
/// <remarks>
/// Register with LiveLink as ULiveLinkTransformRole
/// Subsequent calls with same name are ignored.
/// Name is case-sensitive.
/// </remarks>
__declspec(dllexport) void ULL_RegisterObject(const char* subjectName);

/// <summary>
/// Register a transform subject with custom properties.
/// Properties will be available in Unreal Blueprints.
/// </summary>
/// <param name="subjectName">Unique identifier for this object</param>
/// <param name="propertyNames">Array of property names (e.g., ["Speed", "Load", "Battery"])</param>
/// <param name="propertyCount">Number of properties</param>
/// <remarks>
/// Register properties with LiveLink frame data
/// Property names are case-sensitive.
/// All subsequent updates must include same number of property values.
/// </remarks>
__declspec(dllexport) void ULL_RegisterObjectWithProperties(
    const char* subjectName,
    const char** propertyNames,
    int propertyCount);

/// <summary>
/// Update transform for an object.
/// Registers object automatically if not already registered.
/// </summary>
/// <param name="subjectName">Object identifier</param>
/// <param name="transform">Transform data (position, rotation, scale)</param>
/// <remarks>
/// Submit frame data to LiveLink
/// Call at your desired update rate (e.g., 30-60 Hz).
/// </remarks>
__declspec(dllexport) void ULL_UpdateObject(
    const char* subjectName,
    const ULL_Transform* transform);

/// <summary>
/// Update transform and property values for an object.
/// </summary>
/// <param name="subjectName">Object identifier</param>
/// <param name="transform">Transform data</param>
/// <param name="propertyValues">Array of property values (must match registration order)</param>
/// <param name="propertyCount">Number of property values (must match registration count)</param>
/// <remarks>
/// Submit frame data with properties to LiveLink
/// Property count must match registration count, or update is ignored.
/// </remarks>
__declspec(dllexport) void ULL_UpdateObjectWithProperties(
    const char* subjectName,
    const ULL_Transform* transform,
    const float* propertyValues,
    int propertyCount);

/// <summary>
/// Remove an object from LiveLink.
/// Object becomes "stale" in Unreal after ~5 seconds.
/// </summary>
/// <param name="subjectName">Object identifier to remove</param>
/// <remarks>
/// Mark subject as removed in LiveLink
/// Safe to call on non-existent objects (no error).
/// </remarks>
__declspec(dllexport) void ULL_RemoveObject(const char* subjectName);

//=============================================================================
// Data Subjects (3 functions) - Metrics/KPIs without 3D representation
//=============================================================================

/// <summary>
/// Register a data-only subject (no 3D representation).
/// </summary>
/// <param name="subjectName">Unique identifier</param>
/// <param name="propertyNames">Array of property name strings</param>
/// <param name="propertyCount">Number of properties in the array</param>
/// <remarks>
/// Register with LiveLink as ULiveLinkBasicRole
/// </remarks>
__declspec(dllexport) void ULL_RegisterDataSubject(
    const char* subjectName,
    const char** propertyNames,
    int propertyCount);

/// <summary>
/// Update property values for a data subject.
/// </summary>
/// <param name="subjectName">Subject identifier</param>
/// <param name="propertyNames">Array of property names (can be NULL if already registered)</param>
/// <param name="propertyValues">Array of property values (float)</param>
/// <param name="propertyCount">Number of values in the array</param>
/// <remarks>
/// Submit data frame to LiveLink
/// </remarks>
__declspec(dllexport) void ULL_UpdateDataSubject(
    const char* subjectName,
    const char** propertyNames,
    const float* propertyValues,
    int propertyCount);

/// <summary>
/// Remove a data subject from LiveLink.
/// </summary>
/// <param name="subjectName">Subject identifier to remove</param>
/// <remarks>
/// Mark data subject as removed in LiveLink
/// </remarks>
__declspec(dllexport) void ULL_RemoveDataSubject(const char* subjectName);

#ifdef __cplusplus
}
#endif

//=============================================================================
// Implementation Notes
//=============================================================================
//
// Export Decoration:
//   - __declspec(dllexport) marks functions for export from DLL
//   - extern "C" prevents C++ name mangling
//   - __cdecl calling convention (implicit)
//
// Memory Management:
//   - Input strings: Caller owns, lifetime guaranteed during call
//   - Input arrays: Caller owns, validated with count parameter
//   - Input structs: Caller owns, passed by pointer
//   - No dynamic allocation or ownership transfer
//
// Thread Safety:
//   - FCriticalSection in LiveLinkBridge
//
// Error Handling:
//   - Parameter validation (null checks, bounds)
//   - Logging via UE_LOG
//   - Return codes: ULL_OK (0), ULL_ERROR (-1), etc.
//
//=============================================================================
