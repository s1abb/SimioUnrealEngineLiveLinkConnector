# Coordinate Systems Documentation

## Overview

This document defines the coordinate systems used by Simio and Unreal Engine, and how the SimioUnrealEngineLiveLinkConnector handles transformations between them. Understanding these differences is critical for accurate object positioning and orientation in the LiveLink data stream.

---

## Simio Coordinate System

### Spatial Axes
Simio uses a **standard 3D Cartesian coordinate system**:

- **X-axis**: Runs **left to right**
- **Y-axis**: Runs **bottom to top** (ground level at Y=0)  
- **Z-axis**: Runs **from user's viewpoint into the scene**

### Rotation System (Entity Movement)
Simio defines rotations using aviation/navigation terminology:

#### **Heading**
- **Definition**: Rotation around the **Z-axis** (vertical axis)
- **Measurement**: Degrees from "north" (up direction in 2D top-down view)
- **Convention**: Compass headings with north pointing up
- **Values**:
  - `0°` = North (up in 2D view)
  - `90°` = East (right)
  - `180°` = South (down)
  - `270°` = West (left)

#### **Pitch**  
- **Definition**: Rotation around the **Y-axis** (lateral axis)
- **Measurement**: Angle relative to the floor in degrees
- **Convention**: Positive pitch = climb, negative pitch = descend
- **Examples**:
  - `45°` = 45-degree climb angle
  - `-30°` = 30-degree descent angle

#### **Roll**
- **Definition**: Rotation around the **X-axis** (longitudinal axis) 
- **Measurement**: Banking/tilting motion in degrees
- **Convention**: Standard aviation roll (left wing down = negative)

### Simio Properties
```
Entity.Location.X, Entity.Location.Y, Entity.Location.Z  // Position
Entity.Movement.Heading, Entity.Movement.Pitch, Entity.Movement.Roll  // Rotation
```

---

## Unreal Engine Coordinate System

### Spatial Axes
Unreal Engine uses a **Z-up, left-handed coordinate system**:

- **X-axis**: **Forward/Backward** (Red) - Positive values are forward
- **Y-axis**: **Left/Right** (Green) - Positive values are to the right  
- **Z-axis**: **Up/Down** (Blue) - Positive values are upward

### Handedness Verification
UE follows the **left-hand rule**:
1. Point left index finger along positive X-axis (forward)
2. Point left middle finger along positive Y-axis (right)  
3. Thumb points along positive Z-axis (up)

### Coordinate Spaces
- **World Space**: Fixed coordinate system for the entire level
- **Origin**: Center of the scene (0,0,0) at world grid intersection
- **Scale**: Unreal units (typically centimeters)

---

## Critical Coordinate System Differences

| Aspect | Simio | Unreal Engine |
|--------|-------|---------------|
| **Handedness** | Right-handed (standard) | Left-handed |
| **Up Axis** | Y-up | Z-up |
| **Forward Axis** | Z-axis (into scene) | X-axis |
| **Right Axis** | X-axis | Y-axis |
| **Units** | Meters | Centimeters |
| **Rotation Convention** | Heading/Pitch/Roll | Euler XYZ |

---

## Transformation Requirements

### Position Conversion
```csharp
// Simio (X,Y,Z) meters → Unreal (X,Y,Z) centimeters
Simio: (X_s, Y_s, Z_s) in meters
Unreal: (X_u, Y_u, Z_u) in centimeters

Conversion:
X_u = X_s * 100        // Forward: Simio X (left-right) → Unreal X (forward-back)  
Y_u = -Z_s * 100       // Right: Simio -Z (out of scene) → Unreal Y (left-right)
Z_u = Y_s * 100        // Up: Simio Y (bottom-top) → Unreal Z (down-up)
```

### Rotation Conversion  
```csharp
// Simio Heading/Pitch/Roll → Unreal Euler XYZ
Simio_Heading → ??? // Z-axis rotation in Simio coordinate frame
Simio_Pitch   → ??? // Y-axis rotation in Simio coordinate frame  
Simio_Roll    → ??? // X-axis rotation in Simio coordinate frame

// Requires coordinate frame transformation + Euler angle mapping
// CRITICAL: Must account for axis remapping AND handedness change
```

### Scale Conversion
```csharp  
// Simio (X,Y,Z) → Unreal (X,Z,Y) with axis remapping
Simio: (Scale_X, Scale_Y, Scale_Z)
Unreal: (Scale_X, Scale_Z, Scale_Y)  // Y and Z swapped for axis mapping
```

---

## Implementation Impact

### CoordinateConverter.cs Requirements

#### **Position Transformation** ✅ **Currently Implemented**
```csharp
public static void SimioPositionToUnreal(double simioX, double simioY, double simioZ,
    out double unrealX, out double unrealY, out double unrealZ)
{
    unrealX = simioX * 100.0;      // Forward
    unrealY = -simioZ * 100.0;     // Right (negated for coordinate handedness)
    unrealZ = simioY * 100.0;      // Up
}
```

#### **Rotation Transformation** 🚧 **Needs Verification**
Current implementation converts Euler angles to quaternions, but **coordinate frame mapping is complex**:

1. **Axis Remapping**: Simio's Heading (Z-rotation) may not directly map to Unreal's Z-rotation due to coordinate differences
2. **Handedness Impact**: Left-handed vs right-handed affects rotation direction  
3. **Euler Order**: Rotation application order matters (XYZ vs ZYX vs others)

#### **Critical Questions to Resolve**:
1. Does Simio Heading map to Unreal Z-rotation after coordinate transformation?
2. How does the handedness change affect rotation directions?
3. What Euler angle order does Unreal LiveLink expect?

### Step Property Updates Required

#### **Current Schema** (Generic):
```csharp
OrientationX, OrientationY, OrientationZ  // Generic Euler angles
```

#### **Proposed Schema** (Simio-Specific):
```csharp
Heading, Pitch, Roll  // Matches Simio terminology
// Default values: Entity.Movement.Heading, Entity.Movement.Pitch, Entity.Movement.Roll
```

---

## Validation Strategy

### Phase 1: Mock DLL Testing
1. **Create mock native DLL** with logging
2. **Test known transformation values**:
   - Simio (0,0,0) → Unreal (0,0,0) 
   - Simio (1,1,1) → Unreal (100,-100,100)
   - Test rotation: Heading=90° should produce specific Unreal result

### Phase 2: Unreal Engine Integration  
1. **Visual validation** in Unreal Editor LiveLink panel
2. **Test object placement** at known coordinates
3. **Test object rotation** with known Heading/Pitch/Roll values
4. **Verify handedness** with asymmetric test objects

### Phase 3: End-to-End Validation
1. **Simio entity movement** → **Unreal object movement**
2. **Animation paths** should match expected trajectories
3. **Performance validation** with multiple moving objects

---

## Documentation Consistency Requirements

This coordinate system analysis impacts:

1. **ManagedLayerDevelopment.md**: Update rotation property specifications
2. **NativeLayerDevelopment.md**: Ensure C++ implementation matches conversion logic
3. **DevelopmentPlan.md**: Update testing phases to include coordinate validation
4. **Step Definitions**: Change property names from OrientationX/Y/Z to Heading/Pitch/Roll

---

## References

- **Unreal Engine Documentation**: Coordinate Systems (Left-handed, Z-up)
- **Simio Documentation**: Entity Movement properties (Heading/Pitch/Roll)
- **Aviation Standards**: Standard definitions for Heading/Pitch/Roll conventions