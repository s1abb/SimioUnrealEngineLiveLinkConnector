# Architecture

**Purpose:** System design reference - the "what" and "why" of the connector.  
**Audience:** Developers, architects, technical reviewers  
**Scope:** Timeless design decisions, component relationships, technical constraints  
**Not Included:** Implementation details (see layer dev docs), current status (see DevelopmentPlan.md)

---

## System Overview
- High-level purpose and capabilities
- Key design principles (mock-first, graceful degradation)
- Major use cases (transform streaming, data streaming, object management)

## Architecture Layers

### Managed Layer (C#)
- Purpose: Simio extension providing Element and Steps
- Key components: Element, Steps, UnrealIntegration, Utils
- Simio API integration points

### Native Layer (C++)
- Purpose: P/Invoke bridge to Unreal LiveLink
- P/Invoke API surface (11 exported functions)
- Unreal LiveLink integration approach

### Mock Native Layer
- Role in development workflow
- Relationship to real implementation
- Testing and CI strategy

## Component Diagram
```
[Simio Process] → [Simio Steps] → [LiveLinkManager] → [P/Invoke] → [Native Bridge] → [Unreal LiveLink]
                                         ↓
                                   [CoordinateConverter]
```

## Data Flow

### Transform Streaming Pipeline
- Simio position/rotation → CoordinateConverter → ULL_Transform → Native → LiveLink subject

### Data Streaming Pipeline  
- Simio expressions → Property arrays → Native → LiveLink data subject

### Object Lifecycle
- Register → Update (many times) → Remove → Cleanup

## Key Technical Decisions

### Coordinate System Conversion
- **Why needed:** Simio (Z-up, meters) vs Unreal (Z-up, centimeters, left-handed)
- **Where conversion happens:** CoordinateConverter.cs in managed layer
- **Rotation handling:** Euler angles → Quaternions
- [Link to CoordinateSystems.md for mathematical details]

### Configuration Management
- **LiveLinkConfiguration design:** Centralized validation and defaults
- **Property validation strategy:** Utils layer with context-aware error reporting
- **Path normalization:** Relative paths resolved to Simio project folder

### Error Handling Philosophy
- **Graceful degradation:** Simulation continues even if visualization fails
- **When to block vs warn:** Network issues = warn; invalid config = block
- **User feedback:** TraceInformation for success, ReportError for failures

### Threading Model
- **Simio threading:** Generally single-threaded per model, but Steps may execute in parallel
- **LiveLinkManager thread-safety:** Singleton with thread-safe initialization
- **Native layer synchronization:** FCriticalSection for provider access

## Interface Contracts

### P/Invoke API Surface
**Lifecycle:**
- `ULL_Initialize(providerName)` → int
- `ULL_IsConnected()` → int
- `ULL_Shutdown()` → void

**Transform Subjects:**
- `ULL_RegisterObject(name)`
- `ULL_RegisterObjectWithProperties(name, propertyNames[], count)`
- `ULL_UpdateObject(name, transform)`
- `ULL_UpdateObjectWithProperties(name, transform, values[], count)`
- `ULL_RemoveObject(name)`

**Data Subjects:**
- `ULL_RegisterDataSubject(name, propertyNames[], count)`
- `ULL_UpdateDataSubject(name, values[], count)`

**Utility:**
- `ULL_GetVersion()` → const char*

### Simio Extension Points
- **IElementDefinition / IElement:** Connection lifecycle management
- **IStepDefinition / IStep:** Process integration (Create, Update, Transmit, Destroy)
- **Key patterns:** Property readers, ExecutionContext, TraceInformation/ReportError

## Non-Functional Requirements

### Performance Constraints
- **Update frequency:** Support 30-60 Hz for moving objects
- **Object count:** Handle 100+ concurrent objects
- **Memory footprint:** <100MB for typical simulation

### Deployment Model
- **Location:** `%PROGRAMFILES%\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\`
- **DLL loading:** Simio loads managed DLL → P/Invoke loads native DLL
- **Dependencies:** System.Drawing.Common (6.0.0), native DLL must be in same folder

### Version Compatibility
- **Simio:** 15.x, 16.x (API stable across versions)
- **Unreal Engine:** 5.1+, tested with 5.3
- **.NET Framework:** 4.8 (Simio requirement)

### Security Considerations
- **P/Invoke boundary:** Validate all pointer parameters for null
- **Path traversal:** PropertyValidation.NormalizeFilePath prevents directory traversal
- **Network endpoint:** Validate host/port format before connection

## Technology Stack
- **Languages:** C# (.NET Framework 4.8), C++ (Unreal Engine UBT)
- **Key APIs:** Simio Extensions API, Unreal LiveLink API
- **Build tools:** MSBuild, dotnet CLI, PowerShell, Unreal Build Tool
- **Testing:** MSTest, Mock DLL

## Testing Strategy (High-Level)
- **Test pyramid:** Unit (coordinate conversion, utils) → Integration (mock DLL) → E2E (real Simio + Unreal)
- **Mock vs real:** Mock for CI/development, real for validation
- [Link to TestAndBuildInstructions.md for execution details]

## Related Documentation
- **Implementation guides:** [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md), [NativeLayerDevelopment.md](NativeLayerDevelopment.md)
- **Build/test workflows:** [TestAndBuildInstructions.md](TestAndBuildInstructions.md)
- **Current status:** [DevelopmentPlan.md](DevelopmentPlan.md)
- **Mathematical details:** [CoordinateSystems.md](CoordinateSystems.md)