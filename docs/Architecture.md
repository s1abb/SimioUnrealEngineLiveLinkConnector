# Architecture

**Purpose:** System design reference - the "what" and "why" of the connector.  
**Audience:** Developers, architects, technical reviewers  
**Scope:** Timeless design decisions, component relationships, technical constraints  
**Not Included:** Implementation details (see layer dev docs), current status (see DevelopmentPlan.md)

**Last Updated:** October 17, 2025

---

## System Overview

SimioUnrealEngineLiveLinkConnector streams real-time simulation data from Simio to Unreal Engine via the LiveLink Message Bus protocol. It enables visualization of discrete-event simulation models with 3D graphics, providing both geometric transforms and custom data properties.

**Key Capabilities:**
- **Transform Streaming:** 3D object position, rotation, and scale at simulation runtime
- **Data Streaming:** Simulation metrics, KPIs, and system state without 3D representation
- **Object Management:** Dynamic registration, updates, and removal during simulation

**Design Principles:**
- **Mock-First Development:** Mock native DLL enables rapid managed layer development without Unreal Engine dependency
- **Graceful Degradation:** Simulation continues even if visualization connection fails
- **Lazy Registration:** Objects auto-register on first use, reducing boilerplate
- **Configuration-Driven:** Comprehensive Element properties for flexible deployment
- **Message Bus Architecture:** Uses Unreal's LiveLink Message Bus for loose coupling (third-party integration pattern)

---

## Architecture Validation

### Reference Implementation: UnrealLiveLinkCInterface

Our architecture is based on a **proven production system**:

> "provides a C Interface to the Unreal Live Link Message Bus API. This allows for third party packages to stream to Unreal Live Link without requiring to compile with the Unreal Build Tool (UBT). This is done by exposing the Live Link API via a C interface in a shared object compiled using UBT."

**Validated Design Decisions:**
- ✅ **Third-Party Integration:** External application → UBT-compiled DLL → LiveLink Message Bus → Unreal Editor
- ✅ **Standalone Program:** Built with UBT as a Program target (NOT a plugin)
- ✅ **C API:** `extern "C"` functions for language interoperability
- ✅ **Performance:** Proven to handle "thousands of float values @ 60Hz" in production
- ✅ **Deployment Reality:** Additional Unreal DLLs required (tbbmalloc.dll, etc.)
- ✅ **Message Bus Protocol:** Stable across Unreal versions (4.26 through 5.x)

**Performance Baseline:**
- Reference system: ~3,000+ floats @ 60Hz = ~180,000 values/sec
- Our target: 100 objects @ 30Hz × 10 doubles = 30,000 values/sec
- **Safety margin: 6x lighter than proven reference**

---

## Architecture Layers

### Managed Layer (C#)

**Purpose:** Simio extension providing Element and Steps for LiveLink integration

**Key Components:**
- **Element** - Connection lifecycle management, configuration validation
- **Steps** - Simio process integration (CreateObject, SetPositionOrientation, TransmitValues, DestroyObject)
- **UnrealIntegration** - P/Invoke abstraction, coordinate conversion, object registry
- **Utils** - Property validation, path handling, network validation, UE detection

**Simio API Integration:**
- Implements `IElementDefinition` and `IElement` for connection management
- Implements `IStepDefinition` and `IStep` for process actions
- Uses `IPropertyReader` for runtime property evaluation
- Reports via `IExecutionInformation.TraceInformation` and `ReportError`

---

### Native Layer (C++)

**Purpose:** P/Invoke bridge between managed C# and Unreal LiveLink Message Bus

**Architecture Type:** Standalone UBT Program (NOT a plugin)
- Built with Unreal Build Tool as a Program target
- Lives in `Engine/Source/Programs/UnrealLiveLinkNative/`
- Outputs standalone executable/DLL
- No Unreal Editor required for third-party integration

**Key Components:**
- **P/Invoke API Surface:** 12 exported C functions with `__cdecl` calling convention
- **LiveLinkBridge:** Singleton managing FLiveLinkMessageBusSource and subject registry
- **Coordinate Helpers:** ULL_Transform → FTransform conversion utilities (optional, managed layer handles primary conversion)
- **String Conversion:** C string → FName caching for performance

**Unreal LiveLink Integration via Message Bus:**
- Creates `FLiveLinkMessageBusSource` via `ILiveLinkClient`
- Registers subjects with `ULiveLinkTransformRole` or `ULiveLinkBasicRole`
- Submits frame data via `PushSubjectFrameData_AnyThread`
- Communicates over UDP (port 6666 default) to Unreal Editor
- No direct coupling - Editor and DLL run as separate processes

**Deployment Dependencies (Critical):**
> "copying just the dll may not be enough. There may be other dependencies that need to be copied such as the tbbmalloc.dll" - Reference Project Warning

Expected additional DLLs (50-200MB total):
- tbbmalloc.dll (Intel Threading Building Blocks)
- UnrealEditor-Core.dll
- UnrealEditor-CoreUObject.dll
- UnrealEditor-LiveLink.dll
- UnrealEditor-LiveLinkInterface.dll
- UnrealEditor-Messaging.dll
- UnrealEditor-UdpMessaging.dll

---

### Mock Native Layer

**Purpose:** Development and testing without Unreal Engine dependency

**Role in Workflow:**
- Implements complete 12-function API surface
- Provides file logging for debugging P/Invoke calls
- Enables CI/CD testing without Unreal Engine installation
- Validates managed layer independently of native implementation

**Relationship to Real Implementation:**
- Binary-compatible API (same function signatures, struct layouts)
- Mock simulates success paths with state tracking
- Real implementation provides actual LiveLink Message Bus integration
- Mock useful for: Development, unit testing, CI/CD, rapid iteration

---

## Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         Simio Process                                │
│  (Model entities moving, producing, triggering steps)                │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Simio Steps (Managed C#)                          │
│  CreateObject | SetPositionOrientation | TransmitValues | Destroy   │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     LiveLinkManager (Singleton)                      │
│  - Object registry (ConcurrentDictionary)                            │
│  - Connection health (1s cache)                                      │
│  - Configuration management                                          │
└────────────┬───────────────────────────────────┬────────────────────┘
             │                                   │
             ▼                                   ▼
┌─────────────────────────┐      ┌──────────────────────────────────┐
│ LiveLinkObjectUpdater   │      │   CoordinateConverter            │
│ - Lazy registration     │      │   - Simio → Unreal coords        │
│ - Property tracking     │      │   - Euler → Quaternion           │
│ - Update buffering      │      │   - Meters → Centimeters         │
└────────────┬────────────┘      └──────────────┬───────────────────┘
             │                                   │
             └───────────────┬───────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  P/Invoke Boundary (12 Functions)                    │
│  ULL_Initialize | ULL_RegisterObject | ULL_UpdateObject | etc.      │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│           Native Bridge (UBT-Compiled Standalone Program)            │
│  - LiveLinkBridge singleton                                          │
│  - FLiveLinkMessageBusSource management                              │
│  - Subject registration/updates                                      │
│  - Thread-safe operations (FCriticalSection)                         │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼ (LiveLink Message Bus - UDP)
┌─────────────────────────────────────────────────────────────────────┐
│                  Unreal Engine LiveLink System                       │
│  - LiveLink window (source visibility)                               │
│  - Actor binding (transform subjects)                                │
│  - Blueprint access (data subjects via Get LiveLink Property Value)  │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Data Flow

### Transform Streaming Pipeline

**Simio → Unreal Transform:**
1. Simio Step reads entity position (X, Y, Z meters) and rotation (Heading, Pitch, Roll degrees)
2. `CoordinateConverter.SimioToUnrealTransform()` converts:
   - Position: `(X, Y, Z)` → `(X*100, -Z*100, Y*100)` cm (axis swap + unit conversion)
   - Rotation: Euler angles → Quaternion `[X, Y, Z, W]`
   - Scale: Pass-through (typically `[1, 1, 1]`)
3. `LiveLinkObjectUpdater.UpdateTransform()` marshals to `ULL_Transform` struct
4. P/Invoke call: `ULL_UpdateObject(name, transform)`
5. Native bridge constructs Unreal `FTransform` (direct pass-through expected)
6. LiveLink Message Bus source submits `FLiveLinkFrameDataStruct`
7. Frame transmitted over UDP to Unreal Editor (separate process)
8. Unreal Editor displays object in LiveLink window, bound actors update

### Data Streaming Pipeline

**Simio → Unreal Data Subject:**
1. Simio Step evaluates expressions (e.g., `Model.TotalThroughput`, `Queue.NumberInQueue`)
2. Step builds property name array: `["Throughput", "WIP", "Utilization"]`
3. Step builds property value array: `[247.5f, 42.0f, 0.87f]`
4. P/Invoke call: `ULL_UpdateDataSubject(subjectName, propertyNames, values, count)`
5. Native bridge creates/updates data subject with `ULiveLinkBasicRole` (no transform, only properties)
6. Frame transmitted via Message Bus to Unreal Editor
7. Unreal Blueprint reads via `Get LiveLink Property Value` node

### Object Lifecycle

**Registration → Update → Removal:**
1. **First Update:** `LiveLinkManager.GetOrCreateObject(name)` creates `LiveLinkObjectUpdater`
2. **Lazy Registration:** First `UpdateTransform()` call auto-registers via `ULL_RegisterObject(name)`
3. **Repeated Updates:** High-frequency calls (30-60 Hz) update transform only
4. **Property Registration:** If properties needed, auto-registers with `ULL_RegisterObjectWithProperties()`
5. **Removal:** `ULL_RemoveObject(name)` marks subject as stale (~5s timeout in Unreal)
6. **Cleanup:** `LiveLinkManager.Shutdown()` disposes all updaters, calls `ULL_Shutdown()`

---

## Key Technical Decisions

### Message Bus Architecture (Validated)

**Why Message Bus vs Direct Plugin:**
- **Loose Coupling:** Simio and Unreal run as separate processes
- **No UBT for Consumer:** Third-party apps use standard P/Invoke (no Unreal dependency)
- **Production Proven:** Reference project validates this approach
- **Protocol Stability:** Message Bus protocol stable across UE versions
- **Network Capable:** Can stream to remote Unreal instances (UDP)

**Trade-offs:**
- **Extra Memory Copy:** One additional copy per frame (proven acceptable in reference)
- **Deployment Size:** Requires Unreal DLLs (50-200MB vs ~1MB for pure native)
- **Connection Dependency:** Requires Unreal Editor running with LiveLink Message Bus Source active

### Coordinate System Conversion

**Why Needed:**
- Simio: Z-up, right-handed, meters
- Unreal: Z-up, left-handed, centimeters
- Axis mapping differs: Simio Y → Unreal Z, Simio Z → Unreal -Y

**Where Conversion Happens:**
- `CoordinateConverter.cs` in managed layer (before P/Invoke)
- Native layer receives already-converted Unreal coordinates
- No coordinate math expected in native bridge (single responsibility)
- Native performs direct pass-through: `ULL_Transform` → `FTransform`

**Rotation Handling:**
- Simio uses Euler angles (Heading, Pitch, Roll in degrees)
- Unreal uses Quaternions `[X, Y, Z, W]` for interpolation and stability
- Conversion: `EulerToQuaternion()` with axis remapping and degree→radian conversion

[See CoordinateSystems.md for mathematical derivations]

### Configuration Management

**LiveLinkConfiguration Design:**
- Centralized validation via `Validate()` method
- Default values for all properties (zero-config quick start)
- `CreateValidated()` factory method sanitizes edge cases
- Stored in LiveLinkManager for runtime access by Steps

**Property Validation Strategy:**
- Utils layer provides context-aware validation (`PropertyValidation.cs`)
- Validation runs at Element construction time (fail-fast)
- Clear error messages via `ExecutionContext.ExecutionInformation.ReportError()`
- Network endpoints, paths, timeouts all validated before use

**Path Normalization:**
- Relative paths resolved to Simio project folder
- `PathUtils.SafePathCombine()` prevents directory traversal
- Log file paths created if parent directories missing

### Error Handling Philosophy

**Graceful Degradation:**
- Simulation continues even if LiveLink fails (visualization is optional)
- Connection failures log warnings, don't block simulation execution
- Missing native DLL detected early with helpful error message

**When to Block vs Warn:**
- **Block:** Invalid configuration (empty source name, port out of range)
- **Block:** API version mismatch (native DLL incompatible)
- **Warn:** Connection timeout (Unreal not running)
- **Warn:** Network issues (firewall, wrong host)

**User Feedback:**
- `TraceInformation` for successful operations (visible in Simio Trace window)
- `ReportError` for failures with actionable context (object name, specific issue)
- High-frequency steps throttle traces (1-second loop protection)

### Threading Model

**Simio Threading:**
- Generally single-threaded per model instance
- Steps may execute in parallel for different entities (rare)
- Element Initialize/Shutdown called on main thread

**LiveLinkManager Thread-Safety:**
- Singleton with thread-safe initialization (`Lazy<T>` with `isThreadSafe: true`)
- Object registry uses `ConcurrentDictionary<string, LiveLinkObjectUpdater>`
- Initialization lock prevents race conditions during setup

**Native Layer Synchronization:**
- `FCriticalSection` protects LiveLink API access
- Subject registry access serialized (Unreal APIs not thread-safe)
- C API functions may be called from multiple threads (protect shared state)
- LiveLink provides `_AnyThread` function variants for thread-safe operations

---

## Interface Contracts

### P/Invoke API Surface

**Total Functions:** 12 (4 lifecycle + 5 transform subjects + 3 data subjects)

#### Lifecycle Functions (4)

```cpp
int ULL_Initialize(const char* providerName);
int ULL_GetVersion();
int ULL_IsConnected();
void ULL_Shutdown();
```

**Return Codes:**
- `ULL_OK (0)` - Operation successful
- `ULL_ERROR (-1)` - General error
- `ULL_NOT_CONNECTED (-2)` - Not connected to Unreal Engine
- `ULL_NOT_INITIALIZED (-3)` - LiveLink not initialized

**API Version:**
- Current version: `1`
- Checked at initialization: `LiveLinkManager.Initialize()` validates compatibility
- Mismatch throws `LiveLinkInitializationException` with version details

#### Transform Subject Functions (5)

```cpp
void ULL_RegisterObject(const char* subjectName);

void ULL_RegisterObjectWithProperties(
    const char* subjectName, 
    const char** propertyNames, 
    int propertyCount);

void ULL_UpdateObject(
    const char* subjectName, 
    const ULL_Transform* transform);

void ULL_UpdateObjectWithProperties(
    const char* subjectName,
    const ULL_Transform* transform,
    const float* propertyValues,
    int propertyCount);

void ULL_RemoveObject(const char* subjectName);
```

**ULL_Transform Structure:**
```cpp
struct ULL_Transform {
    double position[3];  // [X, Y, Z] in centimeters (Unreal coordinates)
    double rotation[4];  // [X, Y, Z, W] quaternion (normalized)
    double scale[3];     // [X, Y, Z] scale factors
};
// Total size: 80 bytes (10 doubles)
```

#### Data Subject Functions (3)

```cpp
void ULL_RegisterDataSubject(
    const char* subjectName, 
    const char** propertyNames, 
    int propertyCount);

void ULL_UpdateDataSubject(
    const char* subjectName, 
    const char** propertyNames,     // NULL if already registered
    const float* propertyValues,
    int propertyCount);

void ULL_RemoveDataSubject(const char* subjectName);
```

**Calling Convention:** `__cdecl` (CallingConvention.Cdecl in C#)  
**String Marshaling:** ANSI strings (`CharSet.Ansi`, `UnmanagedType.LPStr`)  
**Array Marshaling:** Pointer + count (no length prefix)

---

### Simio Extension Points

**IElementDefinition / IElement:**
- **Connection lifecycle management:** Initialize on simulation start, Shutdown on end
- **8 properties total:**
  - **LiveLink Connection (5):** SourceName, LiveLinkHost, LiveLinkPort, ConnectionTimeout, RetryAttempts
  - **Logging (2):** EnableLogging (ExpressionProperty - evaluates at runtime), LogFilePath
  - **Unreal Engine (1):** UnrealEnginePath (user-specified installation path)
- **Configuration object:** `LiveLinkConfiguration` with validation
- **Health status:** `IsConnectionHealthy` property for Steps to check

**IStepDefinition / IStep:**
- **Process integration:** 4 step types (Create, SetPositionOrientation, TransmitValues, Destroy)
- **Property readers:** String, Real, Integer, Expression, Repeating groups, Element references
- **Execution context:** Access to ExecutionInformation for errors/traces

**Key Patterns:**
- Property reading via `IPropertyReader.GetStringValue()`, `GetDoubleValue()`
- Repeating groups via `IRepeatingPropertyReader` with row iteration
- Error reporting: `context.ExecutionInformation.ReportError("message")`
- Success traces: `context.ExecutionInformation.TraceInformation("message")`

---

## Non-Functional Requirements

### Performance Constraints

**Update Frequency:**
- Target: 30-60 Hz for moving objects (transform updates)
- Achievable: 100+ Hz for data subjects (lighter payload)
- Bottleneck: P/Invoke overhead (~0.1ms per call) + Message Bus transmission

**Object Count:**
- Target: 100+ concurrent objects
- Tested: Mock DLL handles 500+ objects in unit tests
- Reference: Production system handles "thousands of floats @ 60Hz"
- Limitation: Unreal LiveLink UI performance degrades beyond ~200 subjects

**Memory Footprint:**
- Target: <100MB for typical simulation (50 objects, 10 data subjects)
- Managed layer: ~10MB base + ~1KB per object (including updater, buffers)
- Native layer: ~20MB base (Unreal Engine LiveLink modules overhead)
- Deployment: 50-200MB total (including Unreal DLL dependencies)

**Connection Health Check Caching:**
- Cache duration: 1 second
- Rationale: `ULL_IsConnected()` is expensive P/Invoke + Message Bus check
- Implementation: `LiveLinkManager._lastConnectionCheckTime` timestamp
- Impact: Reduces P/Invoke overhead by ~99% for frequent health checks

---

### Deployment Model

**Installation Location:**
```
%PROGRAMFILES%\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\
├── SimioUnrealEngineLiveLinkConnector.dll  (managed layer)
├── UnrealLiveLink.Native.dll               (native layer)
├── System.Drawing.Common.dll               (managed dependency)
└── [Unreal Engine Dependencies - 50-200MB total:]
    ├── tbbmalloc.dll
    ├── UnrealEditor-Core.dll
    ├── UnrealEditor-CoreUObject.dll
    ├── UnrealEditor-LiveLink.dll
    ├── UnrealEditor-LiveLinkInterface.dll
    ├── UnrealEditor-Messaging.dll
    ├── UnrealEditor-UdpMessaging.dll
    └── [additional Unreal runtime DLLs]
```

**DLL Loading Sequence:**
1. Simio scans UserExtensions folder at startup
2. Simio loads managed DLL via reflection
3. Simio discovers `IElementDefinition` and `IStepDefinition` implementations
4. Element.Initialize() triggers first P/Invoke call
5. .NET Framework loads `UnrealLiveLink.Native.dll` from same directory
6. Native DLL dynamically loads Unreal dependencies (must be in same folder)
7. Native DLL initializes LiveLink Message Bus source

**Dependency Management (Critical):**
> "copying just the dll may not be enough. There may be other dependencies that need to be copied such as the tbbmalloc.dll" - Reference Project Warning

- Use Dependencies.exe tool to identify required DLLs
- Test deployment on machine without Unreal Engine installed
- All dependencies must be in same folder as native DLL
- No PATH search - .NET loads from managed DLL location

---

### Version Compatibility

**Simio:**
- Supported: 15.x, 16.x
- API Stability: Simio Extensions API stable across versions
- Breaking changes rare: Forward compatibility maintained

**Unreal Engine:**
- Supported: 5.1+
- Tested: 5.3, 5.4, 5.6
- LiveLink Message Bus Protocol: Stable since UE 4.26
- Note: "The Unreal Live Link Message Bus line protocol doesn't change that often. As such, you will find that building the Unreal DLL will work for a number of Unreal versions." - Reference Project

**.NET Framework:**
- Required: 4.8
- Simio constraint: Ships with .NET Framework 4.8 runtime
- Cannot use .NET Core/.NET 6+ (Simio limitation)

**API Version Checking:**
- Managed layer expects version 1
- Native layer reports version via `ULL_GetVersion()`
- Initialization fails if mismatch detected
- Future versions: Increment version number for breaking changes

---

### Security Considerations

**P/Invoke Boundary Validation:**
- All pointer parameters checked for null before dereference
- Array count parameters validated (non-negative, reasonable bounds)
- String lengths implicitly validated by null-terminator scanning

**Path Traversal Protection:**
- `PropertyValidation.NormalizeFilePath()` resolves paths via `Path.GetFullPath()`
- Rejects paths with ".." sequences outside project folder
- Log file creation uses sanitized paths only

**Network Endpoint Validation:**
- Host format validated (DNS name or IP address)
- Port range checked (1-65535)
- No raw socket access (LiveLink Message Bus handles network layer)
- Connection timeout enforced (prevents infinite hangs)

**Privilege Requirements:**
- Deployment requires Administrator (writes to Program Files)
- Runtime requires no special privileges (user-level Simio process)

---

## Technology Stack

**Languages:**
- C# 10.0 (.NET Framework 4.8) - Managed layer
- C++ (Unreal Engine UBT) - Native layer
- C (P/Invoke surface) - Exported API

**Key APIs:**
- Simio Extensions API - IElementDefinition, IStepDefinition, IPropertyReader
- Unreal LiveLink API - FLiveLinkMessageBusSource, ILiveLinkClient, FLiveLinkFrameDataStruct
- .NET P/Invoke - DllImport, marshaling, struct layout

**Build Tools:**
- MSBuild 17.0+ (Visual Studio 2022)
- dotnet CLI 6.0+ (build/test automation)
- PowerShell 5.1+ (build scripts, deployment)
- Unreal Build Tool (UBT) - Native layer compilation

**Testing:**
- MSTest (unit test framework)
- Mock native DLL (development/CI testing)
- Integration tests (managed + mock native)
- Manual E2E tests (Simio + Unreal Editor)

**Dependencies:**
- System.Drawing.Common 6.0.0 (NuGet)
- Simio API assemblies (from installation)
- Unreal Engine LiveLink modules (for native implementation)

---

## Testing Strategy (High-Level)

**Test Pyramid:**
```
        E2E Tests (Manual)
       /                  \
   Integration Tests (Mock)
  /                          \
Unit Tests (Coordinate, Utils)
```

**Unit Tests:**
- **Coordinate Conversion:** Known transformations, edge cases, inverse operations
- **Types:** Struct size validation (80 bytes), marshaling correctness
- **Utils:** Path normalization, network validation, UE detection
- **Configuration:** Validation rules, default values, error cases

**Integration Tests (Mock DLL):**
- **Lifecycle:** Initialize → IsConnected → Shutdown
- **Object Workflow:** RegisterObject → UpdateObject → RemoveObject
- **Property Workflow:** RegisterWithProperties → UpdateWithProperties
- **Data Subjects:** RegisterDataSubject → UpdateDataSubject → RemoveDataSubject
- **Error Handling:** Null pointers, invalid counts, disposed updaters

**E2E Tests (Real Simio + Unreal):**
- **Transform Streaming:** Objects appear and move in Unreal viewport
- **Data Subject Streaming:** Blueprint reads property values correctly via Get LiveLink Property Value
- **Performance:** 100 objects at 30 Hz sustained
- **Connection Recovery:** Unreal restart, network interruption
- **Message Bus:** Verify UDP communication (port 6666 default)

**Mock vs Real:**
- **Development:** Mock for rapid iteration (no Unreal Engine needed)
- **CI/CD:** Mock for automated testing (no GUI dependencies)
- **Validation:** Real for final integration testing with Message Bus
- **Production:** Real DLL with dependencies deployed to end users

[See TestAndBuildInstructions.md for test execution commands]

---

## Related Documentation

**Implementation Guides:** 
- [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md) - C# implementation patterns and development sequence
- [NativeLayerDevelopment.md](NativeLayerDevelopment.md) - C++ implementation, LiveLink Message Bus integration, build procedures

**Build/Test Workflows:** 
- [TestAndBuildInstructions.md](TestAndBuildInstructions.md) - Build commands and troubleshooting

**Project Status:** 
- [DevelopmentPlan.md](DevelopmentPlan.md) - Current phase, milestones, completion tracking

**Technical Details:** 
- [CoordinateSystems.md](CoordinateSystems.md) - Coordinate transformation derivations

**User Guides:** 
- [SimioInstructions.md](SimioInstructions.md) - Simio deployment and usage
- [UnrealSetup.md](UnrealSetup.md) - Unreal Editor LiveLink configuration