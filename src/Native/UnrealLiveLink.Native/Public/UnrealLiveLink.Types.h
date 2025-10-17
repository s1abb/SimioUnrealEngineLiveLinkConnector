#pragma once

#include <stddef.h>  // For offsetof macro

// Pure C API - no C++ types in this header
// This header must be standalone (no Unreal Engine dependencies)
// and compatible with C# P/Invoke marshaling

#ifdef __cplusplus
extern "C" {
#endif

// =============================================================================
// Return Codes
// =============================================================================
// CRITICAL: Must match managed layer expectations exactly (negative for errors)
// See: src/Managed/.../UnrealLiveLinkNative.cs

#define ULL_OK                  0    // Operation successful
#define ULL_ERROR              -1    // General error
#define ULL_NOT_CONNECTED      -2    // Not connected to Unreal Engine
#define ULL_NOT_INITIALIZED    -3    // LiveLink not initialized

// =============================================================================
// API Version
// =============================================================================
// Used for compatibility checking between managed and native layers

#define ULL_API_VERSION         1    // Current API version

// =============================================================================
// Transform Structure
// =============================================================================
// CRITICAL: Must be exactly 80 bytes to match C# marshaling
// C# verification: Marshal.SizeOf<ULL_Transform>() == 80
//
// Memory Layout:
//   - Position: 3 doubles × 8 bytes = 24 bytes
//   - Rotation: 4 doubles × 8 bytes = 32 bytes
//   - Scale:    3 doubles × 8 bytes = 24 bytes
//   - Total:                          80 bytes

#pragma pack(push, 8)  // Ensure 8-byte alignment for doubles

typedef struct ULL_Transform {
    double position[3];  // X, Y, Z in centimeters (Unreal coordinate system)
    double rotation[4];  // Quaternion [X, Y, Z, W] (normalized)
    double scale[3];     // X, Y, Z scale factors (typically 1.0)
} ULL_Transform;

#pragma pack(pop)

// =============================================================================
// Compile-Time Validation
// =============================================================================
// These static assertions ensure binary compatibility with C# marshaling

static_assert(sizeof(double) == 8, "double must be 8 bytes");
static_assert(sizeof(ULL_Transform) == 80, "ULL_Transform size must be 80 bytes to match C# marshaling");

// Verify individual field offsets (optional but helpful for debugging)
static_assert(offsetof(ULL_Transform, position) == 0, "position offset must be 0");
static_assert(offsetof(ULL_Transform, rotation) == 24, "rotation offset must be 24");
static_assert(offsetof(ULL_Transform, scale) == 56, "scale offset must be 56");

#ifdef __cplusplus
}
#endif

// =============================================================================
// Implementation Notes
// =============================================================================
//
// Coordinate System:
//   - Managed layer (CoordinateConverter.cs) converts Simio → Unreal coordinates
//   - Native layer receives already-converted values
//   - Position: Centimeters (Unreal units)
//   - Rotation: Normalized quaternion (Unreal convention)
//   - Scale: Direct pass-through
//
// Mock vs Real Implementation:
//   - Mock DLL uses positive error codes (1, 2) for simplicity
//   - Real implementation MUST use negative codes as defined above
//   - API version checking happens in managed layer Initialize()
//
// Thread Safety:
//   - This header defines data structures only
//   - Thread safety handled in LiveLinkBridge implementation
//
// =============================================================================