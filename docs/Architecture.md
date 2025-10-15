# Architecture

## Overview
SimioUnrealEngineLiveLinkConnector is a Simio extension that streams real-time object transforms from Simio simulations to Unreal Engine via LiveLink. It consists of two layers: a C# managed layer integrating with Simio's API, and a C++ native bridge communicating with Unreal's LiveLink Message Bus.

## System Components

### Managed Layer (C#)
**Purpose:** Simio extension providing custom Elements and Steps for Unreal Engine integration.

- **Simio Integration** (`Element/`, `Steps/`)
  - Custom Element: Manages LiveLink connection lifecycle
  - CreateObjectStep: Registers new objects with LiveLink
  - SetObjectPositionOrientationStep: Updates object transforms each simulation step
  - DestroyObjectStep: Unregisters objects from LiveLink

- **UnrealIntegration** (`UnrealIntegration/`)
  - `UnrealLiveLinkNative.cs`: P/Invoke declarations for native DLL
  - `Types.cs`: C# structs matching native types (Transform, Quaternion)
  - `LiveLinkManager.cs`: High-level API managing connection and object registry
  - `LiveLinkObjectUpdater.cs`: Per-object wrapper handling coordinate conversion
  - `CoordinateConverter.cs`: Simio ↔ Unreal coordinate system translation

### Native Layer (C++)
**Purpose:** Bridge between C# P/Invoke and Unreal Engine's LiveLink C++ API.

**Based on:** UnrealLiveLinkCInterface (ported from C to C++)

- **Public API** (`Public/`)
  - `UnrealLiveLink.Native.h`: Exported C API functions
  - `UnrealLiveLink.Types.h`: Shared data structures

- **Implementation** (`Private/`)
  - `UnrealLiveLink.Native.cpp`: Main entry points, C exports
  - `LiveLinkBridge.cpp/.h`: C++ wrapper around Unreal's FLiveLinkProvider
  - `CoordinateHelpers.h`: Transform utilities

**Build:** Compiled using Unreal Build Tool (UBT) as a standalone program linking against Unreal Engine's LiveLink module.

## Data Flow
```
Simio Model
    ↓ (Execute Step)
SetObjectPositionOrientationStep
    ↓ (Call high-level API)
LiveLinkManager.UpdateObject(name, x, y, z, rotation)
    ↓ (Coordinate conversion)
CoordinateConverter.SimioToUnreal(transform)
    ↓ (P/Invoke)
UnrealLiveLinkNative.UpdateTransformFrame(...)
    ↓ (DLL boundary)
UnrealLiveLink.Native.dll
    ↓ (LiveLink API)
FLiveLinkProvider.UpdateSubjectFrameData(...)
    ↓ (UDP Message Bus)
Unreal Engine LiveLink Plugin
    ↓ (Apply transform)
Actor with LiveLinkComponent
```

## Coordinate Systems

**Simio:** Right-handed, Y-up, meters  
**Unreal:** Left-handed, Z-up, centimeters  

**Conversion:** Performed in `CoordinateConverter.cs`
- Position: (X, Y, Z) → (X×100, -Z×100, Y×100)
- Rotation: Euler degrees → Quaternion with axis remapping
- Scale: Passthrough (1:1 mapping)

## Communication Protocol

**LiveLink Message Bus (UDP)**
- Auto-discovery: Native bridge broadcasts presence, Unreal discovers automatically
- Stateless: Each transform update is independent
- Subject-based: Objects identified by string names
- Frame timing: Includes world time for synchronization

## Build Dependencies

**Managed:**
- .NET Framework 4.8
- SimioAPI.dll (Simio SDK)
- UnrealLiveLink.Native.dll (pre-built or built via BuildNative.ps1)

**Native:**
- Unreal Engine 5.6+ source code
- Unreal Build Tool (UBT)
- LiveLink plugin source (included with UE)
- MSVC 2022 (required by UBT)

## Deployment

**Simio Extension:**
1. Managed DLL → `%USERPROFILE%\Documents\Simio\UserExtensions\`
2. Native DLL → Same directory (automatically copied by .csproj)

**Unreal Engine:**
1. Enable LiveLink plugin
2. Add Message Bus Source in LiveLink window
3. Add LiveLinkComponent to actors, set Subject Name to match Simio object names

## Design Patterns

**Managed Layer:**
- Factory Pattern: StepDefinitions create Step instances
- Manager Pattern: LiveLinkManager as singleton managing connection
- Wrapper Pattern: LiveLinkObjectUpdater wraps per-object operations

**Native Layer:**
- Bridge Pattern: C++ classes bridge between C exports and Unreal API
- Singleton Pattern: Single FLiveLinkProvider instance
- Export Pattern: `extern "C"` functions prevent name mangling

## Error Handling

**Connection Failures:** LiveLinkManager checks HasConnection(), logs warnings, continues gracefully  
**Missing Objects:** Lazy registration - objects auto-register on first update  
**Invalid Transforms:** Validated in CoordinateConverter, clamped to safe ranges  
**DLL Load Failures:** P/Invoke throws DllNotFoundException with clear message

## Performance Characteristics

**Latency:** ~1-5ms per update (P/Invoke + UDP transmission)  
**Throughput:** 1000+ objects @ 30 Hz per CPU core  
**Scaling:** Linear with object count (no shared locks)  
**Memory:** ~100 bytes per registered object

## Differences from Omniverse Connector

**Removed:** USD file export/import functionality  
**Added:** LiveLink-specific initialization and connection management  
**Changed:** Transform representation (USD matrices → LiveLink quaternions)  
**Simplified:** No async file I/O, direct memory-based streaming

## Future Considerations

- **Linux Support:** Extend native build to libUnrealLiveLink.Native.so
- **Multiple Unreal Instances:** Unicast endpoints for network distribution
- **Animation Data:** Extend beyond transforms to skeletal animation
- **Batching:** Group multiple updates into single Message Bus packet