# Architecture

## Overview
SimioUnrealEngineLiveLinkConnector is a Simio extension that streams real-time object transforms and simulation data from Simio simulations to Unreal Engine via LiveLink. It consists of two layers: a C# managed layer integrating with Simio's API, and a C++ native bridge communicating with Unreal's LiveLink Message Bus.

**Key Capabilities:**
1. **Transform Streaming:** Real-time 3D object position, rotation, and scale
2. **Data Streaming:** Simulation metrics, KPIs, and system state (NEW!)
3. **Property Streaming:** Custom float properties attached to objects or data subjects
4. **Dynamic Management:** Register, update, and remove objects during simulation

## Repo Structure
C:.
|   README.md
|   SimioUnrealEngineLiveLinkConnector.sln
|   
+---build
|       BuildAndDeploy.ps1
|       BuildManaged.ps1
|       BuildNative.ps1
|       DeployToSimio.ps1
|       
+---docs
|       Architecture.md
|       BuildInstructions.md
|       CoordinateSystems.md
|       ManagedLayerDevelopment.md
|       NativeLayerDevelopment.md
|       SimioInstructions.md
|       TestInstructions.md
|       UnrealSetup.md
|
+---examples
|   \---OmniverseConnector
|           CreatePrimStep.md
|           CreatePrimStepDefinition.md
|           DestroyPrimStep.md
|           DestroyPrimStepDefinition.md
|           OmniverseElement.md
|           OmniverseElementDefinition.md
|           SetPrimPositionAndOrientationStep.md
|           SetPrimPositionAndOrientationStepDefinition.md
|
+---lib
|   \---native
|       \---win-x64
|               UnrealLiveLink.Native.dll
|               UnrealLiveLink.Native.pdb
|               VERSION.txt
|
+---src
|   +---Managed
|   |   |   SimioUnrealEngineLiveLinkConnector.csproj
|   |   |
|   |   +---Element
|   |   |       SimioUnrealEngineLiveLinkElement.cs
|   |   |       SimioUnrealEngineLiveLinkElementDefinition.cs
|   |   |
|   |   +---Steps
|   |   |       CreateObjectStep.cs
|   |   |       CreateObjectStepDefinition.cs
|   |   |       DestroyObjectStep.cs
|   |   |       DestroyObjectStepDefinition.cs
|   |   |       SetObjectPositionOrientationStep.cs
|   |   |       SetObjectPositionOrientationStepDefinition.cs
|   |   |       TransmitValuesStep.cs
|   |   |       TransmitValuesStepDefinition.cs
|   |   |
|   |   \---UnrealIntegration
|   |           CoordinateConverter.cs
|   |           LiveLinkManager.cs
|   |           LiveLinkObjectUpdater.cs
|   |           Types.cs
|   |           UnrealLiveLinkNative.cs
|   |
|
\---Native
    \---UnrealLiveLink.Native
        |   UnrealLiveLink.Native.Build.cs
        |   UnrealLiveLink.Native.Target.cs
        |
        +---Private
        |       CoordinateHelpers.h
        |       LiveLinkBridge.cpp
        |       LiveLinkBridge.h
        |       UnrealLiveLink.Native.cpp
        |
        \---Public
                UnrealLiveLink.Native.h
                UnrealLiveLink.Types.h
|
\---tests
    \---Integration.Tests

## System Components

### Managed Layer (C#)
**Purpose:** Simio extension providing custom Elements and Steps for Unreal Engine integration.

- **Simio Integration** (`Element/`, `Steps/`)
  - **SimioUnrealEngineLiveLinkElement:** Manages LiveLink connection lifecycle and health
  - **CreateObjectStep:** Registers new 3D objects with LiveLink and sets initial transform
  - **SetObjectPositionOrientationStep:** Updates object transforms each simulation step
  - **TransmitValuesStep (NEW!):** Streams pure simulation data/metrics without 3D objects
  - **DestroyObjectStep:** Unregisters objects from LiveLink

- **UnrealIntegration** (`UnrealIntegration/`)
  - `Types.cs`: C# structs matching native layout (`ULL_Transform`) for P/Invoke marshaling
  - `UnrealLiveLinkNative.cs`: P/Invoke declarations for all native DLL functions
  - `CoordinateConverter.cs`: Simio ↔ Unreal coordinate system and rotation conversion
  - `LiveLinkObjectUpdater.cs`: Per-object wrapper with lazy registration and property management
  - `LiveLinkManager.cs`: Singleton managing connection lifecycle and object registry

### Native Layer (C++)
**Purpose:** Simplified, purpose-built LiveLink bridge designed specifically for Simio's requirements.

**Design Principles:**
- Minimal API surface (only what Simio needs)
- Two subject types: Transform objects (3D) and Data subjects (metrics)
- Modern C++ with clean internal implementation
- Simple P/Invoke interface for C# interop

- **Public API** (`Public/`)
  - `UnrealLiveLink.Native.h`: Clean C API with lifecycle, transform, and data functions
  - `UnrealLiveLink.Types.h`: C-compatible structs (`ULL_Transform`, return codes)

- **Implementation** (`Private/`)
  - `UnrealLiveLink.Native.cpp`: C API exports with parameter validation
  - `LiveLinkBridge.h/.cpp`: Modern C++ wrapper with subject registry and name caching
  - `CoordinateHelpers.h`: Transform utilities (optional)

**Build:** Compiled using Unreal Build Tool (UBT) as a Program target linking against LiveLink, CoreUObject, and messaging modules.

## Data Flow

### Transform Objects (3D Visualization)
```
Simio Model Entity
    ↓ (Execute Step)
SetObjectPositionOrientationStep
    ↓ (Get or create updater)
LiveLinkManager.GetOrCreateObject(name)
    ↓ (Coordinate conversion)
CoordinateConverter.SimioToUnreal(position, euler) → (position, quaternion)
    ↓ (P/Invoke)
UnrealLiveLinkNative.ULL_UpdateObject(name, transform)
    ↓ (DLL boundary)
UnrealLiveLink.Native.dll → LiveLinkBridge.UpdateTransformSubject()
    ↓ (LiveLink API)
FLiveLinkProvider.UpdateSubjectFrameData(FLiveLinkTransformFrameData)
    ↓ (UDP Message Bus)
Unreal Engine LiveLink Plugin
    ↓ (Apply to scene)
Actor with LiveLinkComponent
```

### Data Subjects (Metrics/KPIs) - NEW!
```
Simio Model (System State)
    ↓ (Execute Step)
TransmitValuesStep
    ↓ (Collect metrics)
Repeat Group: PropertyName + PropertyValue expressions
    ↓ (P/Invoke)
UnrealLiveLinkNative.ULL_UpdateDataSubject(subjectName, propNames[], values[])
    ↓ (DLL boundary)
UnrealLiveLink.Native.dll → LiveLinkBridge.UpdateDataSubject()
    ↓ (LiveLink API, identity transform)
FLiveLinkProvider.UpdateSubjectFrameData(FLiveLinkBaseFrameData)
    ↓ (UDP Message Bus)
Unreal Engine LiveLink Plugin
    ↓ (Read in Blueprints)
Get LiveLink Property Value("PropertyName") → float
```

## Coordinate Systems

**Simio:** Right-handed, Y-up, meters, Euler angles (degrees)  
**Unreal:** Left-handed, Z-up, centimeters, Quaternions  

**Conversion:** Performed in `CoordinateConverter.cs`
- **Position:** (X, Y, Z) → (X×100, -Z×100, Y×100) - axis remapping with unit conversion
- **Rotation:** Euler degrees → Quaternion [X,Y,Z,W] with axis remapping
- **Scale:** (X, Y, Z) → (X, Z, Y) - axis remapping only

## Communication Protocol

**LiveLink Message Bus (UDP)**
- **Auto-discovery:** Native bridge broadcasts provider presence, Unreal discovers automatically
- **Stateless:** Each update is independent (no session state)
- **Subject-based:** Objects/data streams identified by string names
- **Frame timing:** Includes world time for synchronization
- **Two subject types:**
  - **Transform Role:** 3D objects with position/rotation/scale + optional properties
  - **Basic Role:** Data-only subjects with named float properties (identity transform)

**Connection Flow:**
1. `ULL_Initialize("ProviderName")` - Create provider, start broadcasting
2. Unreal Editor LiveLink window shows provider in Message Bus Sources
3. Add source - establishes connection
4. `ULL_IsConnected()` returns true when handshake complete
5. Register subjects, stream updates at desired frequency

## Build Dependencies

**Managed:**
- .NET Framework 4.8
- SimioAPI.dll (Simio SDK)
- UnrealLiveLink.Native.dll (pre-built or built via BuildNative.ps1)

**Native:**
- Unreal Engine 5.6+ source code
- Unreal Build Tool (UBT)
- Required UE modules: Core, CoreUObject, LiveLink, LiveLinkInterface, Messaging, UdpMessaging
- MSVC 2022 (required by UBT)
- Windows 10/11 x64

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
- **Factory Pattern:** StepDefinitions create Step instances via Simio's plugin architecture
- **Singleton Pattern:** LiveLinkManager manages single connection per process
- **Wrapper Pattern:** LiveLinkObjectUpdater encapsulates per-object state and lazy registration
- **Strategy Pattern:** CoordinateConverter handles different coordinate system transformations

**Native Layer:**
- **Singleton Pattern:** FLiveLinkBridge manages single FLiveLinkProvider instance
- **Bridge Pattern:** C++ classes bridge between C exports and Unreal's C++ API
- **Registry Pattern:** Subject registry tracks registration state and property schemas
- **RAII Pattern:** Automatic resource cleanup in destructors
- **Export Pattern:** `extern "C"` functions with `CallingConvention.Cdecl` for P/Invoke

## Error Handling

**Connection Failures:** 
- Steps check `Element.IsConnectionHealthy` before operations
- Graceful degradation: simulation continues, warnings logged
- Clear error messages via `context.ExecutionInformation.ReportError()`

**Missing Objects:** 
- Lazy registration: objects/subjects auto-register on first update
- `LiveLinkObjectUpdater` tracks registration state per object

**Invalid Data:**
- Parameter validation in native layer (null checks, array bounds)
- Property count validation (must match registration)
- Coordinate conversion handles NaN/infinity gracefully

**DLL Load Failures:** 
- `DllNotFoundException` with troubleshooting guidance
- Fallback mode consideration (simulation runs without visualization)

**Native Errors:**
- Return codes: `ULL_OK`, `ULL_ERROR`, `ULL_NOT_CONNECTED`, `ULL_NOT_INITIALIZED`
- Comprehensive logging via Unreal's logging system

## Performance Characteristics

**Targets:**
- **Latency:** < 5ms (C# call to Unreal receipt)
- **Transform Throughput:** 1000+ objects @ 30 Hz sustained
- **Data Subject Throughput:** 100+ subjects @ 10 Hz (typical dashboard rate)
- **Memory per Object:** < 1 KB (static + per-frame overhead)
- **Initialization Time:** < 2 seconds (from Initialize() to first connection)

**Optimizations:**
- **Name Caching:** FString→FName conversions cached in native layer
- **Subject Registry:** Avoid repeated registration calls
- **Lazy Registration:** Auto-register on first update
- **Minimal API:** Only functions Simio actually needs
- **Buffer Reuse:** Property arrays reused in managed layer

## Key Architectural Decisions

### LiveLink vs File-Based Approaches
- **Memory-based streaming** (not file I/O like USD/Omniverse)
- **UDP Message Bus** protocol for real-time performance
- **Subject-based** identification (not file paths)
- **Stateless updates** (no load/modify/save cycle)

### Two Subject Types (Innovation)
- **Transform Subjects:** 3D objects with position/rotation/scale
- **Data Subjects:** Pure metrics/KPIs without 3D representation
- **Unified API:** Both support custom float properties
- **Blueprint Integration:** All data accessible via "Get LiveLink Property Value"

### Simplified Design Principles
- **Minimal API surface:** Only what Simio needs (not general-purpose)
- **Single provider per process:** Matches Simio's single-simulation model
- **Auto-registration:** Reduces setup complexity for modelers
- **Clean separation:** C# handles Simio integration, C++ handles Unreal integration

## Usage Patterns

### Transform Objects
```csharp
// Simio modeler workflow:
1. Add UnrealEngineLiveLinkElement to model (set SourceName)
2. CreateObjectStep: Register "Forklift_01" with initial position
3. SetObjectPositionOrientationStep: Update position each simulation step
4. (Optional) Include custom properties: Speed, Load, BatteryLevel
5. DestroyObjectStep: Cleanup when entity destroyed

// Unreal side:
1. Enable LiveLink plugin
2. Add Message Bus Source → "SimioSimulation" appears
3. Add LiveLinkComponent to Actor, set Subject Name = "Forklift_01"
4. Read properties in Blueprint: Get LiveLink Property Value("Speed")
```

### Data Subjects (NEW!)
```csharp
// Simio modeler workflow:
1. TransmitValuesStep with SubjectName = "ProductionMetrics"
2. Repeat Group: Add rows for "Throughput", "Utilization", "QueueDepth"
3. Each row evaluates Simio expressions for current values
4. Called periodically (e.g., every minute) to update dashboard

// Unreal side:
1. No 3D actor needed
2. Blueprint: Get LiveLink Subject Data("ProductionMetrics")
3. Extract properties: Get Property Value("Throughput") → display on UI
4. Build real-time dashboards, charts, HUDs
```

## Future Considerations

- **Cross-Platform:** Linux support (libUnrealLiveLink.Native.so)
- **Network Distribution:** Multiple Unreal instances via unicast endpoints  
- **Performance:** Batch updates for high object counts
- **Advanced Data:** Animation curves, skeletal data beyond transforms
- **Integration:** Web dashboards consuming same LiveLink data
- **Reliability:** Connection recovery, automatic reconnection