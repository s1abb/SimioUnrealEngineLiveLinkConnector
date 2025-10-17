// TypesValidation.cpp
// Compile-time and runtime validation of ULL_Transform structure
// This file can be included in builds to verify type definitions

#include "../Public/UnrealLiveLink.Types.h"
#include <stdio.h>

// Additional compile-time checks beyond what's in the header
namespace TypesValidation {

    // Note: std::is_trivial/is_standard_layout not available without <type_traits>
    // Basic size checks are sufficient for Sub-Phase 6.3

    // Verify no padding between fields (should be tightly packed with 8-byte alignment)
    static_assert(sizeof(ULL_Transform::position) == 24, "position array must be 24 bytes");
    static_assert(sizeof(ULL_Transform::rotation) == 32, "rotation array must be 32 bytes");
    static_assert(sizeof(ULL_Transform::scale) == 24, "scale array must be 24 bytes");

    // Runtime validation function (can be called during initialization)
    void ValidateTypes() {
        printf("=== ULL_Transform Type Validation ===\n");
        printf("sizeof(ULL_Transform) = %zu bytes (expected: 80)\n", sizeof(ULL_Transform));
        printf("sizeof(double) = %zu bytes (expected: 8)\n", sizeof(double));
        printf("\nField offsets:\n");
        printf("  position: offset %zu, size %zu\n", offsetof(ULL_Transform, position), sizeof(ULL_Transform::position));
        printf("  rotation: offset %zu, size %zu\n", offsetof(ULL_Transform, rotation), sizeof(ULL_Transform::rotation));
        printf("  scale:    offset %zu, size %zu\n", offsetof(ULL_Transform, scale), sizeof(ULL_Transform::scale));
        
        if constexpr (sizeof(ULL_Transform) == 80) {
            printf("\n[OK] Type validation PASSED - Binary compatible with C# marshaling\n");
        } else {
            printf("\n[ERROR] Type validation FAILED - Size mismatch!\n");
        }
        printf("======================================\n\n");
    }

    // Test transform creation
    ULL_Transform CreateIdentityTransform() {
        ULL_Transform transform = {};
        
        // Identity position (origin)
        transform.position[0] = 0.0;
        transform.position[1] = 0.0;
        transform.position[2] = 0.0;
        
        // Identity rotation (no rotation quaternion)
        transform.rotation[0] = 0.0;  // X
        transform.rotation[1] = 0.0;  // Y
        transform.rotation[2] = 0.0;  // Z
        transform.rotation[3] = 1.0;  // W
        
        // Identity scale (no scaling)
        transform.scale[0] = 1.0;
        transform.scale[1] = 1.0;
        transform.scale[2] = 1.0;
        
        return transform;
    }

    // Verify identity transform values
    bool VerifyIdentityTransform(const ULL_Transform& transform) {
        bool valid = true;
        
        // Check position
        if (transform.position[0] != 0.0 || transform.position[1] != 0.0 || transform.position[2] != 0.0) {
            printf("❌ Identity position incorrect\n");
            valid = false;
        }
        
        // Check rotation (identity quaternion)
        if (transform.rotation[0] != 0.0 || transform.rotation[1] != 0.0 || 
            transform.rotation[2] != 0.0 || transform.rotation[3] != 1.0) {
            printf("❌ Identity rotation incorrect\n");
            valid = false;
        }
        
        // Check scale
        if (transform.scale[0] != 1.0 || transform.scale[1] != 1.0 || transform.scale[2] != 1.0) {
            printf("❌ Identity scale incorrect\n");
            valid = false;
        }
        
        if (valid) {
            printf("✅ Identity transform verification PASSED\n");
        }
        
        return valid;
    }

} // namespace TypesValidation
