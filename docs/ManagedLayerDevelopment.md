# Managed Layer Development Guide

**Purpose:** Complete implementation reference for the C# managed layer.  
**Audience:** C# developers implementing/extending the managed layer  
**Scope:** Implementation patterns, code organization, testing approach  
**Not Included:** Current status/progress (see DevelopmentPlan.md), architecture rationale (see Architecture.md)

---

## Overview
- Managed layer role: Simio extension providing Element and 4 Steps
- Key capabilities: transform streaming, data streaming, object lifecycle management
- Integration point: Simio's IElementDefinition and IStepDefinition interfaces
- [Link to Architecture.md for design context]

## Implementation Patterns

### Property Reading (Simio API)

**Pattern 1: String Properties**
```csharp
private string ReadStringProperty(string propertyName, IElementData elementData, string defaultValue = "")
{
    // Implementation showing IStringProperty usage
}
```

**Pattern 2: Expression Properties (Real/Integer)**
```csharp
private double ReadRealProperty(string propertyName, IElementData elementData, double defaultValue = 0.0)
{
    // Implementation showing IExpressionProperty evaluation
}
```

**Pattern 3: Element References**
```csharp
private TElement GetElementReference<TElement>(string propertyName, IStepExecutionContext context)
    where TElement : class
{
    // Implementation showing IElementProperty.GetElement()
}
```

**Pattern 4: Repeating Groups**
```csharp
private void ProcessRepeatGroup(string groupName, IStepExecutionContext context, 
    Action<string, double> processRow)
{
    // Implementation showing IRepeatingPropertyReader usage
}
```

### Error Reporting

**Pattern: Context-aware error messages**
```csharp
// BAD: Generic error
context.ExecutionInformation.ReportError($"Update failed: {ex.Message}");

// GOOD: Specific context
context.ExecutionInformation.ReportError($"Failed to update object '{objectName}': {ex.Message}");
```

**Pattern: TraceInformation for success feedback**
```csharp
// Low-frequency steps: Always trace
context.ExecutionInformation.TraceInformation($"LiveLink object '{objectName}' created successfully");

// High-frequency steps: Loop protection (once per second)
if (!_lastTraceTime.HasValue || (DateTime.Now - _lastTraceTime.Value).TotalSeconds >= 1.0)
{
    context.ExecutionInformation.TraceInformation($"Position updated for '{objectName}'");
    _lastTraceTime = DateTime.Now;
}
```

### Coordinate Conversion

**Pattern: Always use CoordinateConverter**
```csharp
// NEVER do inline conversion
// BAD: double unrealX = simioX * 100;

// ALWAYS use converter
var unrealPos = CoordinateConverter.SimioPositionToUnreal(simioX, simioY, simioZ);
var quaternion = CoordinateConverter.EulerToQuaternion(heading, pitch, roll);
```

## Component Development Sequence

### Phase 1: UnrealIntegration Layer
**Goal:** P/Invoke abstraction and coordinate utilities

1. **Types.cs** - P/Invoke marshaling structs
   - Define ULL_Transform with StructLayout(LayoutKind.Sequential)
   - Validate struct size matches native expectations
   - Add XML comments documenting field order importance

2. **UnrealLiveLinkNative.cs** - P/Invoke declarations
   - Declare all 11 exported functions with correct signatures
   - Use CallingConvention.Cdecl
   - DllImport("UnrealLiveLink.Native.dll")

3. **CoordinateConverter.cs** - Coordinate utilities
   - SimioPositionToUnreal(x, y, z) → (X, -Z, Y) * 100
   - EulerToQuaternion(heading, pitch, roll) → [X, Y, Z, W]
   - Unit tests with known transformations

4. **LiveLinkObjectUpdater.cs** - Per-object wrapper
   - Lazy registration on first update
   - Track property schema to prevent mismatches
   - Implement IDisposable for cleanup

5. **LiveLinkManager.cs** - Singleton coordinator
   - Thread-safe initialization (lock + flag)
   - Dictionary<string, LiveLinkObjectUpdater> for registry
   - Connection health tracking

### Phase 2: Element Implementation
**Goal:** Connection lifecycle management

1. **SimioUnrealEngineLiveLinkElementDefinition.cs**
   - Generate unique GUID for MY_ID
   - Define schema with 7 properties in 3 categories
   - Implement IElementDefinition interface

2. **SimioUnrealEngineLiveLinkElement.cs**
   - Constructor: Read all 7 properties with validation
   - Initialize(): Create LiveLinkConfiguration, call LiveLinkManager
   - Shutdown(): Cleanup via LiveLinkManager
   - Add TraceInformation for user visibility

### Phase 3: Step Implementations
**Goal:** Simio process integration

**Steps to Implement:**
1. **CreateObjectStep** - Initial registration with transform + optional properties
2. **SetObjectPositionOrientationStep** - High-frequency transform updates
3. **TransmitValuesStep** - Data subjects with repeating group
4. **DestroyObjectStep** - Cleanup and unregister

**Each step requires:**
- `*Step.cs` - IStep implementation with Execute()
- `*StepDefinition.cs` - Schema with DefineSchema() and factory method
- Property readers initialized in constructor
- Error handling with context
- TraceInformation with appropriate frequency

### Phase 4: Utils Infrastructure
**Goal:** Shared validation and path handling

1. **PropertyValidation.cs** - Element property validation
   - NormalizeFilePath(path, context) - Resolve relative paths
   - ValidateUnrealEnginePath(path, context) - Check UE installation
   - ValidateNetworkEndpoint(host, port, context) - Network validation

2. **PathUtils.cs** - Path utilities
   - SafePathCombine - Sanitize invalid chars
   - EnsureDirectoryExists - Create parent folders
   - GetSafeFileName - Replace invalid filename chars

3. **NetworkUtils.cs** - Network validation
   - IsValidHostname - DNS name validation
   - IsValidPort - Range check (1-65535)

4. **UnrealEngineDetection.cs** - UE installation detection
   - IsValidUnrealEngineInstallation(path) - Check for UE editor executable
   - GetEngineVersion(path) - Parse version from path

## Code Organization

### Namespace Structure
```
SimioUnrealEngineLiveLinkConnector
├── Element (Element + ElementDefinition)
├── Steps (4 Step + StepDefinition pairs)
├── UnrealIntegration (LiveLinkManager, Updater, Types, Native, Converter)
└── Utils (PropertyValidation, PathUtils, NetworkUtils, UnrealEngineDetection)
```

### Dependency Rules
- ✅ Steps → UnrealIntegration (allowed)
- ✅ Steps → Element (allowed for reference)
- ✅ Utils → Simio API (allowed for context-aware validation)
- ❌ UnrealIntegration → Simio API (NOT allowed - keep pure P/Invoke)

## Testing Approach

### Unit Test Strategy

**CoordinateConverter Tests:**
- Test known Simio positions convert to correct Unreal coordinates
- Test Euler angle edge cases (0°, 90°, 180°, 270°)
- Verify quaternion normalization

**Types Tests:**
- Validate Marshal.SizeOf(typeof(ULL_Transform))
- Verify struct field offsets

**LiveLinkManager Tests:**
- Singleton behavior (multiple Instance calls return same object)
- Initialize/shutdown lifecycle
- GetOrCreateObject creates on first call, returns existing thereafter

**Utils Tests:**
- PathUtils: Valid/invalid paths, relative resolution
- NetworkUtils: Valid hostnames/ports, edge cases
- PropertyValidation: Integration with mock ExecutionContext
- UnrealEngineDetection: Valid/invalid UE paths

### Integration Test Strategy

**Mock DLL Tests:**
- Full workflow: Initialize → CreateObject → UpdateObject → DestroyObject → Shutdown
- Property registration and updates
- Error handling (null names, invalid transforms)

**Test Execution:**
```powershell
# See TestAndBuildInstructions.md for full commands
dotnet test tests/Unit.Tests/Unit.Tests.csproj
```

## Simio API Patterns & Gotchas

### Interface Discovery
- Actual interface for step schema: `IPropertyDefinitions` (not IStepSchema)
- Element reference property: Requires element type GUID parameter
- Expression properties: Evaluate at runtime via context

### Dependency Management
- System.Drawing.Common version: Use 6.0.0 for .NET Framework 4.8
- App.config binding redirects: Required for version conflicts
- Simio DLLs: Copy from installation folder for test execution

### Test Environment Setup
- Tests copy Simio DLLs post-build if available
- Override Simio path: `-p:SimioInstallDir="D:\Path"`
- Mock ExecutionContext: Avoid custom implementations (complex interfaces)

## Code Review Checklist

Before marking a component complete:
- [ ] Compiles without warnings
- [ ] Unit tests written and passing
- [ ] Error messages include object/context information
- [ ] TraceInformation added for user visibility
- [ ] Uses CoordinateConverter (never inline conversion)
- [ ] Property readers follow established patterns
- [ ] No hardcoded paths or magic strings
- [ ] IDisposable implemented where resource cleanup needed
- [ ] Thread-safety considered (document assumptions)
- [ ] XML comments for public APIs

## Definition of Complete

Managed layer is complete when:
- [ ] All 4 step types implemented and tested
- [ ] Element handles all 7 properties correctly
- [ ] 50+ unit tests passing (coverage targets met)
- [ ] Integration tests pass with mock DLL
- [ ] Deploys to Simio without errors
- [ ] All properties visible/functional in Simio UI
- [ ] TraceInformation provides adequate user feedback
- [ ] Error handling gracefully handles all failure modes
- [ ] No warnings in build output
- [ ] Code reviewed and documented

## Related Documentation
- **System design:** [Architecture.md](Architecture.md)
- **Native layer:** [NativeLayerDevelopment.md](NativeLayerDevelopment.md)
- **Build/test:** [TestAndBuildInstructions.md](TestAndBuildInstructions.md)
- **Current status:** [DevelopmentPlan.md](DevelopmentPlan.md)