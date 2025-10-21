# Managed Layer Development Guide

**Purpose:** Complete implementation reference for the C# managed layer.  
**Audience:** C# developers implementing/extending the managed layer  
**Scope:** Implementation patterns, code organization, testing approach  
**Not Included:** Current status/progress (see DevelopmentPlan.md), architecture rationale (see Architecture.md)

**Last Updated:** October 21, 2025  
**Implementation Context:** DLL hosted in Simio.exe, supports multiple simulation runs per session

---

## Overview

- **Managed layer role:** Simio extension providing Element and 4 Steps
- **Key capabilities:** Transform streaming, data streaming, object lifecycle management
- **Integration point:** Simio's IElementDefinition and IStepDefinition interfaces
- **Total API surface:** 12 P/Invoke functions, 8 Element properties, 4 Steps
- **Lifecycle model:** Supports multiple Initialize/Shutdown cycles (simulation restart scenarios)
- **Performance:** First initialization ~21ms, subsequent ~1ms (native layer optimization)
- [Link to Architecture.md for design context]

**Critical Context: DLL Hosted in Simio Process**
- Managed layer is loaded as extension DLL in Simio.exe
- Element.Initialize() may be called multiple times (user runs simulation repeatedly)
- Native layer handles restart optimization (static initialization flag)
- Managed layer must be stateless or properly reset in Shutdown()

---

## Implementation Patterns

### Step Constructor Pattern

**Standard constructor for all Steps:**
```csharp
internal class CreateObjectStep : IStep
{
    private readonly IPropertyReaders _readers;

    public CreateObjectStep(IPropertyReaders readers)
    {
        _readers = readers ?? throw new ArgumentNullException(nameof(readers));
    }

    public ExitType Execute(IStepExecutionContext context)
    {
        // Step logic...
    }
}
```

**Key Points:**
- Store `IPropertyReaders` as private readonly field
- Validate not null in constructor
- Access properties via `_readers.GetProperty(propertyName)`

---

### Property Reading Patterns (Simio API)

**Pattern 1: String Properties**
```csharp
private string GetStringProperty(string propertyName, IStepExecutionContext context)
{
    try
    {
        var reader = (IExpressionPropertyReader)_readers.GetProperty(propertyName);
        var value = reader.GetExpressionValue(context);
        return value?.ToString() ?? string.Empty;
    }
    catch
    {
        return string.Empty;
    }
}
```

**Pattern 2: Double Properties with Fallback**
```csharp
private bool TryGetDoubleProperty(string propertyName, IStepExecutionContext context, out double value)
{
    try
    {
        var reader = (IExpressionPropertyReader)_readers.GetProperty(propertyName);
        var expressionValue = reader.GetExpressionValue(context);
        
        // Try direct cast first
        if (expressionValue is double doubleValue)
        {
            value = doubleValue;
            return true;
        }
        
        // Fallback to string parsing
        if (double.TryParse(expressionValue?.ToString(), out double parsedValue))
        {
            value = parsedValue;
            return true;
        }
        
        value = 0.0;
        return false;
    }
    catch
    {
        value = 0.0;
        return false;
    }
}
```

**Pattern 3: Integer Properties (Cast from Double)**
```csharp
private int ReadIntegerProperty(string propertyName, IElementData elementData, int defaultValue)
{
    try
    {
        IPropertyReader reader = elementData.Properties.GetProperty(propertyName);
        // Simio stores integers as doubles internally
        return (int)reader.GetDoubleValue(elementData.ExecutionContext);
    }
    catch
    {
        return defaultValue;
    }
}
```

**Pattern 4: Boolean Properties (Double to Boolean)**
```csharp
private bool ReadBooleanProperty(string propertyName, IElementData elementData, bool defaultValue)
{
    try
    {
        IPropertyReader reader = elementData.Properties.GetProperty(propertyName);
        // Convert double to boolean (0 = false, non-zero = true)
        double value = reader.GetDoubleValue(elementData.ExecutionContext);
        return Math.Abs(value) > 1e-10;
    }
    catch
    {
        return defaultValue;
    }
}
```

**Pattern 5: Element References**
```csharp
// Get element reference with specific type
var connectorElement = ((IElementProperty)_readers.GetProperty("UnrealEngineConnector"))
    .GetElement(context) as SimioUnrealEngineLiveLinkElement;

if (connectorElement == null)
{
    context.ExecutionInformation.ReportError("Failed to resolve 'Unreal Engine Connector' element reference");
    return ExitType.FirstExit;
}
```

**Pattern 6: Repeating Groups** *(For TransmitValuesStep)*
```csharp
var repeatGroup = (IRepeatingPropertyReader)_readers.GetProperty("Values");
int rowCount = repeatGroup.GetCount(context);

for (int i = 0; i < rowCount; i++)
{
    using (repeatGroup.EnterRowContext(context, i))
    {
        string name = GetStringPropertyFromGroup("Name", context, repeatGroup);
        double value = GetDoublePropertyFromGroup("Value", context, repeatGroup);
        
        // Process row...
    }
}
```

---

### Error Reporting Patterns

**Pattern: Context-Aware Error Messages**
```csharp
// ❌ BAD: Generic error
context.ExecutionInformation.ReportError($"Update failed: {ex.Message}");

// ✅ GOOD: Specific context with object name
context.ExecutionInformation.ReportError($"Failed to update object '{objectName}': {ex.Message}");

// ✅ EXCELLENT: Actionable guidance
context.ExecutionInformation.ReportError($"Failed to read position coordinates (X, Y, Z) for object '{objectName}'");
```

**Pattern: TraceInformation for Success Feedback**
```csharp
// Low-frequency steps (CreateObject, DestroyObject): Always trace
context.ExecutionInformation.TraceInformation($"LiveLink object '{objectName}' created at position ({x:F2}, {y:F2}, {z:F2})");

// High-frequency steps (SetPositionOrientation): Loop protection (once per second)
private DateTime? _lastTraceTime = null; // Instance field

// In Execute():
if (!_lastTraceTime.HasValue || (DateTime.Now - _lastTraceTime.Value).TotalSeconds >= 1.0)
{
    context.ExecutionInformation.TraceInformation($"LiveLink position updated for '{objectName}' ({x:F2}, {y:F2}, {z:F2})");
    _lastTraceTime = DateTime.Now;
}
```

**Why Loop Protection?**
- SetPositionOrientation called 30-60+ times per second per object
- Without throttling: Trace window floods, performance degrades
- 1-second throttle: User sees updates without overwhelming system

---

### Coordinate Conversion Pattern

**IMPORTANT:** Steps do NOT call CoordinateConverter directly!

**❌ INCORRECT (Don't do this):**
```csharp
// Direct conversion in Step
var position = CoordinateConverter.SimioPositionToUnreal(x, y, z);
var rotation = CoordinateConverter.EulerToQuaternion(heading, pitch, roll);
var transform = new ULL_Transform { position = position, rotation = rotation, ... };
UnrealLiveLinkNative.ULL_UpdateObject(objectName, ref transform);
```

**✅ CORRECT (Steps use LiveLinkObjectUpdater):**
```csharp
// Step delegates to LiveLinkObjectUpdater - conversion happens automatically
var objectUpdater = LiveLinkManager.Instance.GetOrCreateObject(objectName);
objectUpdater.UpdateTransform(x, y, z, heading, pitch, roll);
// CoordinateConverter called internally by LiveLinkObjectUpdater
```

**When to use CoordinateConverter directly:**
- Unit tests validating conversion logic
- Advanced scenarios extending LiveLinkObjectUpdater
- **Never in normal Step implementations** (encapsulation principle)

**Why this design?**
- **Separation of concerns:** Steps handle Simio logic, Updater handles Unreal details
- **Consistency:** All conversions happen in one place
- **Testability:** Conversion logic tested independently
- **Maintainability:** Coordinate system changes only affect one class

---

### Lifecycle Management Pattern (Multi-Run Support)

**Element Lifecycle:**
```csharp
public class SimioUnrealEngineLiveLinkElement : IElement
{
    private LiveLinkConfiguration? _configuration;
    
    public void Initialize(IElementData elementData)
    {
        // Read configuration properties
        _configuration = CreateConfiguration(elementData);
        
        // Initialize LiveLink (idempotent - safe to call multiple times)
        // First call: ~21ms (native GEngineLoop initialization)
        // Subsequent calls: ~1ms (native static flag optimization)
        LiveLinkManager.Instance.Initialize(_configuration);
        
        // Trace for user feedback
        elementData.ExecutionContext.ExecutionInformation.TraceInformation(
            $"LiveLink initialized: Source='{_configuration.SourceName}'");
    }
    
    public void Shutdown(IElementData elementData)
    {
        // Clean shutdown (native layer handles restart optimization)
        LiveLinkManager.Instance.Shutdown();
        
        // DO NOT add TraceInformation here - overwrites simulation traces
        // Native layer keeps modules loaded for fast restart
    }
}
```

**Key Points:**
- `Initialize()` may be called multiple times (simulation restart scenarios)
- First initialization: ~21ms (native layer startup)
- Subsequent: ~1ms (native static flag skips GEngineLoop.PreInit)
- `Shutdown()` cleans up LiveLink provider but keeps native modules loaded
- Performance optimization transparent to managed layer

**LiveLinkManager Restart Behavior:**
```csharp
public class LiveLinkManager
{
    private bool _isInitialized = false;
    
    public void Initialize(LiveLinkConfiguration config)
    {
        if (_isInitialized)
        {
            // Already initialized - native layer handles restart logic
            return;
        }
        
        // Validate API version compatibility
        int version = UnrealLiveLinkNative.ULL_GetVersion();
        if (version != 1)
        {
            throw new LiveLinkInitializationException(
                $"API version mismatch: expected 1, got {version}");
        }
        
        // Initialize native layer (idempotent - safe to call multiple times)
        int result = UnrealLiveLinkNative.ULL_Initialize(config.SourceName);
        if (result != UnrealLiveLinkNative.ULL_OK)
        {
            throw new LiveLinkInitializationException(
                $"Failed to initialize native layer: {result}");
        }
        
        _configuration = config;
        _isInitialized = true;
    }
    
    public void Shutdown()
    {
        if (!_isInitialized) return;
        
        // Clean up all objects
        foreach (var updater in _objectRegistry.Values)
        {
            updater.Dispose();
        }
        _objectRegistry.Clear();
        
        // Shutdown native layer (keeps modules loaded for restart)
        UnrealLiveLinkNative.ULL_Shutdown();
        
        _isInitialized = false;
    }
}
```

---

## Component Development Sequence

### Phase 1: UnrealIntegration Layer
**Goal:** P/Invoke abstraction and managed API surface

#### 1.1 Types.cs - Data Structures
- Define `ULL_Transform` with `[StructLayout(LayoutKind.Sequential)]`
- Validate struct size: 80 bytes (10 doubles)
- Define `LiveLinkConfiguration` class with validation methods
- Add helper methods: `Identity()`, `Create()`, `IsValid()`

#### 1.2 UnrealLiveLinkNative.cs - P/Invoke Declarations
- Declare all **12 exported functions** with correct signatures
- Use `CallingConvention.Cdecl` for all imports
- `DllImport("UnrealLiveLink.Native.dll")`
- Define return codes: `ULL_OK`, `ULL_ERROR`, `ULL_NOT_CONNECTED`, `ULL_NOT_INITIALIZED`
- Add helper methods: `IsSuccess()`, `GetReturnCodeDescription()`, `IsDllAvailable()`

#### 1.3 CoordinateConverter.cs - Conversion Utilities
- `SimioPositionToUnreal(x, y, z)` → `[X*100, -Z*100, Y*100]` cm
- `SimioScaleToUnreal(x, y, z)` → `[X, Z, Y]` (axis remap only)
- `EulerToQuaternion(rotX, rotY, rotZ)` → `[X, Y, Z, W]` normalized quaternion
- `SimioToUnrealTransform(...)` → complete `ULL_Transform` (convenience method)
- Add validation: `IsFinite()`, `IsQuaternionNormalized()`
- Unit tests with known transformations (origin, 90° rotations, edge cases)

#### 1.4 LiveLinkObjectUpdater.cs - Per-Object Wrapper
- Lazy registration on first `UpdateTransform()` call
- Track property schema to prevent mismatches
- Property buffer reuse (avoid allocations in hot path)
- Support two modes: transform-only vs transform+properties
- Implement `IDisposable` for cleanup (calls `ULL_RemoveObject`)
- Methods:
  - `UpdateTransform(x, y, z, rotX, rotY, rotZ, scaleX, scaleY, scaleZ)`
  - `UpdateWithProperties(x, y, z, rotX, rotY, rotZ, propertyNames, values, ...)`
  - `RegisterObject()`, `RegisterObjectWithProperties()`
  - `RemoveObject()`

#### 1.5 LiveLinkManager.cs - Singleton Coordinator
- Thread-safe initialization: `Lazy<LiveLinkManager>` with `isThreadSafe: true`
- Object registry: `ConcurrentDictionary<string, LiveLinkObjectUpdater>`
- Connection health tracking with 1-second cache (performance optimization)
- Configuration storage: `LiveLinkConfiguration` property
- API version checking at initialization
- **Restart support:** Idempotent `Initialize()` method (safe to call multiple times)
- Methods:
  - `Initialize(LiveLinkConfiguration)` or `Initialize(string sourceName)`
  - `Shutdown()`, `IsConnectionHealthy`, `GetOrCreateObject(name)`
  - `RemoveObject(name)`, `UpdateDataSubject(...)`, `RemoveDataSubject(...)`

---

### Phase 2: Element Implementation
**Goal:** Connection lifecycle management with 8 properties

#### 2.1 SimioUnrealEngineLiveLinkElementDefinition.cs
- Generate unique GUID: `[Guid]::NewGuid().ToString().ToUpper()` in PowerShell
- Define schema with **8 properties in 3 categories:**
  - **LiveLink Connection (5):** SourceName, LiveLinkHost, LiveLinkPort, ConnectionTimeout, RetryAttempts
  - **Logging (2):** EnableLogging (ExpressionProperty), LogFilePath
  - **Unreal Engine (1):** UnrealEnginePath
- Implement `IElementDefinition` interface
- `CreateElement()` factory method returns element instance

#### 2.2 SimioUnrealEngineLiveLinkElement.cs
- **Constructor:** Read all 8 properties using helper methods
- Create `LiveLinkConfiguration` object with validation
- Call `PropertyValidation` methods for path/network/UE validation
- **Initialize():** Call `LiveLinkManager.Instance.Initialize(config)`, add TraceInformation
- **Shutdown():** Call `LiveLinkManager.Instance.Shutdown()` (no TraceInformation to avoid overwriting traces)
- Expose `IsConnectionHealthy` property for Steps to check
- Property reader helpers: `ReadStringProperty()`, `ReadBooleanProperty()`, `ReadIntegerProperty()`, `ReadRealProperty()`
- **Restart awareness:** Initialize() may be called multiple times (native layer handles optimization)

---

### Phase 3: Step Implementations
**Goal:** Simio process integration (4 steps)

#### 3.1 CreateObjectStep
**Purpose:** Register object with initial transform

**Schema Properties:**
- UnrealEngineConnector (Element reference - requires GUID)
- ObjectName (Expression, default: "Entity.Name")
- X, Y, Z (Expression, default: "Entity.Location.X/Y/Z")
- Heading, Pitch, Roll (Expression, default: "Entity.Movement.Heading/Pitch/Roll")

**Execution Logic:**
1. Resolve element reference, validate connection healthy
2. Read object name, validate non-empty
3. Read position (X, Y, Z) and rotation (Heading, Pitch, Roll)
4. Get or create object updater: `LiveLinkManager.Instance.GetOrCreateObject(name)`
5. Update transform: `objectUpdater.UpdateTransform(x, y, z, heading, pitch, roll)`
6. Add TraceInformation: `"LiveLink object '{name}' created at position (x, y, z)"`

#### 3.2 SetObjectPositionOrientationStep
**Purpose:** High-frequency transform updates

**Schema Properties:** Same as CreateObjectStep

**Execution Logic:** Same as CreateObjectStep, but with loop-protected TraceInformation

**Key Difference:**
- Add instance field: `private DateTime? _lastTraceTime = null;`
- Throttle traces to once per second (see Error Reporting Patterns)

#### 3.3 TransmitValuesStep
**Purpose:** Stream data-only subjects (no 3D representation)

**Schema Properties:**
- UnrealEngineConnector (Element reference)
- SubjectName (Expression, default: "SimulationMetrics")
- **Values (Repeating Group):**
  - Name (String) - property name
  - Value (Expression) - property value

**Execution Logic:**
1. Resolve element reference, validate connection
2. Read subject name
3. Iterate repeating group, build arrays: `string[] names`, `float[] values`
4. Call `LiveLinkManager.Instance.UpdateDataSubject(subjectName, names, values)`
5. Add TraceInformation: `"LiveLink data subject '{name}' updated with {count} properties"`

#### 3.4 DestroyObjectStep
**Purpose:** Unregister object from LiveLink

**Schema Properties:**
- UnrealEngineConnector (Element reference)
- ObjectName (Expression)

**Execution Logic:**
1. Resolve element reference, validate connection
2. Read object name
3. Call `LiveLinkManager.Instance.RemoveObject(name)`
4. Add TraceInformation: `"LiveLink object '{name}' removed"`

**Each Step Requires:**
- `*Step.cs` - IStep implementation with `Execute(IStepExecutionContext)`
- `*StepDefinition.cs` - IStepDefinition with `DefineSchema(IPropertyDefinitions)` and `CreateStep()`
- Unique GUID constant
- Property readers initialized in constructor
- Error handling with context-aware messages
- TraceInformation (with loop protection for high-frequency steps)

---

### Phase 4: Utils Infrastructure
**Goal:** Shared validation, path handling, network utilities

#### 4.1 PropertyValidation.cs - Element Property Validation

**Methods:**
```csharp
// Path normalization and validation
public static string NormalizeFilePath(string rawPath, IExecutionContext context)
// Resolves relative paths, creates directories, reports errors

// Unreal Engine installation validation
public static string ValidateUnrealEnginePath(string path, IExecutionContext context)
// Uses UnrealEngineDetection to verify installation

// Network endpoint validation
public static void ValidateNetworkEndpoint(string host, int port, IExecutionContext context)
// Validates host format, port range, common config issues

// Range validation
public static double ValidateTimeout(double timeoutSeconds, IExecutionContext context)
public static int ValidateRetryAttempts(int retryAttempts, IExecutionContext context)
```

**Key Points:**
- All methods use `IExecutionContext` for error reporting
- Validation happens at Element construction time (fail-fast)
- Clear, actionable error messages

#### 4.2 PathUtils.cs - Path Handling Utilities

**Methods:**
```csharp
public static string SafeCombine(string path1, string path2)
// Combines paths safely, handles null/empty inputs

public static string SafeGetDirectoryName(string filePath)
// Gets directory name, handles edge cases

public static bool EnsureDirectoryExists(string directoryPath)
// Creates directory if needed, returns success

public static string MakeRelativePath(string fullPath, string basePath)
// Converts absolute to relative path

public static bool HasValidExtension(string filePath, params string[] validExtensions)
// Validates file extension

public static string SanitizeFilename(string filename, char replacement = '_')
// Removes invalid filename characters
```

#### 4.3 NetworkUtils.cs - Network Validation & Testing

**Methods:**
```csharp
// Async connectivity testing (optional - typically not called by Steps)
public static async Task<bool> IsHostReachableAsync(string host, int timeoutMs = 5000)
public static async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs = 5000)

// Synchronous validation (used by PropertyValidation)
public static bool IsLocalhostAddress(string host)
public static bool IsValidPort(int port)
public static int GetDefaultLiveLinkPort() // Returns 11111
public static string FormatEndpoint(string host, int port) // "host:port" or "[ipv6]:port"
public static int[] SuggestAlternatePorts(int basePort)
```

**Note:** Async methods exist for future extensibility but are not currently used by Steps. Focus on synchronous validation methods for Element configuration.

#### 4.4 UnrealEngineDetection.cs - UE Installation Detection

**Primary Method:**
```csharp
public static UnrealEngineValidationResult ValidateInstallation(string path)
// Returns detailed validation result with:
// - IsValid, ErrorMessage
// - HasUE4Editor, HasUE5Editor
// - Version, ExecutablePath
// - Checks for UE4Editor.exe or UnrealEditor.exe
// - Validates required DLLs (UnrealEditor-Core.dll, etc.)
```

**Helper Methods:**
```csharp
public static bool IsValidUnrealEngineInstallation(string path)
// Simple boolean check - used by PropertyValidation

public static string GetEngineVersion(string installPath)
// Extracts version from path (e.g., "UE_5.3") or detects "4.x" vs "5.x"
```

**UnrealEngineValidationResult Class:**
```csharp
public class UnrealEngineValidationResult
{
    public string Path { get; set; }
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; }
    public string Version { get; set; }
    public string ExecutablePath { get; set; }
    public bool HasUE4Editor { get; set; }
    public bool HasUE5Editor { get; set; }
}
```

---

## Code Organization

### Namespace Structure
```
SimioUnrealEngineLiveLinkConnector
├── Element
│   ├── SimioUnrealEngineLiveLinkElement.cs
│   └── SimioUnrealEngineLiveLinkElementDefinition.cs
├── Steps
│   ├── CreateObjectStep.cs + CreateObjectStepDefinition.cs
│   ├── SetObjectPositionOrientationStep.cs + SetObjectPositionOrientationStepDefinition.cs
│   ├── TransmitValuesStep.cs + TransmitValuesStepDefinition.cs
│   └── DestroyObjectStep.cs + DestroyObjectStepDefinition.cs
├── UnrealIntegration
│   ├── Types.cs (ULL_Transform, LiveLinkConfiguration)
│   ├── UnrealLiveLinkNative.cs (P/Invoke declarations)
│   ├── CoordinateConverter.cs (conversion utilities)
│   ├── LiveLinkObjectUpdater.cs (per-object wrapper)
│   └── LiveLinkManager.cs (singleton coordinator)
└── Utils
    ├── PropertyValidation.cs
    ├── PathUtils.cs
    ├── NetworkUtils.cs
    └── UnrealEngineDetection.cs
```

### Dependency Rules
- ✅ **Steps → UnrealIntegration** (allowed - Steps use LiveLinkManager/ObjectUpdater)
- ✅ **Steps → Element** (allowed - Steps reference Element for connection)
- ✅ **Utils → Simio API** (allowed - provides context-aware validation)
- ✅ **UnrealIntegration → Utils** (allowed - LiveLinkManager uses Utils indirectly via Configuration)
- ❌ **UnrealIntegration → Simio API** (NOT allowed - keep pure P/Invoke layer)
- ❌ **UnrealIntegration → Steps** (NOT allowed - inverted dependency)

**Rationale:** UnrealIntegration layer must remain Simio-agnostic for reusability and testability.

---

## Testing Approach

### Unit Test Strategy

#### CoordinateConverter Tests (18 tests)
```csharp
[TestClass]
public class CoordinateConverterTests
{
    [TestMethod]
    public void SimioPositionToUnreal_Origin_ShouldReturnOrigin()
    // Test zero position remains zero

    [TestMethod]
    public void SimioPositionToUnreal_KnownValues_ShouldConvertCorrectly()
    // Test: Simio(1m, 2m, 3m) → Unreal(100cm, -300cm, 200cm)

    [TestMethod]
    public void EulerToQuaternion_90DegreeRotations_ShouldProduceKnownQuaternions()
    // Test 90° X, Y, Z rotations produce normalized quaternions

    [TestMethod]
    public void IsQuaternionNormalized_ValidQuaternion_ShouldReturnTrue()
    // Test identity and 45° quaternions
}
```

#### Types Tests (5 tests)
```csharp
[TestClass]
public class TypesTests
{
    [TestMethod]
    public void ULL_Transform_StructSize_ShouldMatchExpected()
    // Verify 80 bytes via Marshal.SizeOf

    [TestMethod]
    public void ULL_Transform_Identity_ShouldCreateValidTransform()
    // Test Identity() factory method
}
```

#### LiveLinkManager Tests (12+ tests)
```csharp
[TestClass]
public class LiveLinkManagerTests
{
    [TestMethod]
    public void LiveLinkManager_Instance_ShouldReturnSameInstance()
    // Singleton behavior

    [TestMethod]
    public void LiveLinkManager_Initialize_ValidConfig_ShouldSucceed()
    // Test configuration-based initialization

    [TestMethod]
    public void LiveLinkManager_GetOrCreateObject_ShouldCreateOnFirstCall()
    // Test lazy object creation
    
    [TestMethod]
    public void LiveLinkManager_MultipleInitialize_ShouldNotThrow()
    // Test idempotent initialization (restart scenario)
    
    [TestMethod]
    public void LiveLinkManager_InitializeShutdownInitialize_ShouldWork()
    // Test full restart cycle (validates native layer restart logic)
}
```

#### Utils Tests
```csharp
[TestClass]
public class UtilsTests
{
    [TestMethod]
    public void PathUtils_SafeCombine_ShouldCombinePaths()
    // Test path joining

    [TestMethod]
    public void PathUtils_EnsureDirectoryExists_ShouldCreateDirectory()
    // Test directory creation

    [TestMethod]
    public void NetworkUtils_FormatEndpoint_ShouldFormatCorrectly()
    // Test "host:port" formatting

    // Note: PropertyValidation tests require IExecutionContext
    // Omit or use real Simio context (no custom mocks - too complex)
}
```

---

### Integration Test Strategy

**Mock DLL Tests:**
- Full workflow: `Initialize() → CreateObject() → UpdateObject() → DestroyObject() → Shutdown()`
- Property registration and updates
- Error handling: null names, invalid transforms, disposed objects
- **Restart scenarios:** Multiple `Initialize() → Shutdown()` cycles (critical for Simio)

**Restart Testing (Critical):**
```csharp
[TestClass]
public class RestartTests
{
    [TestMethod]
    public void MultipleRestarts_10Cycles_ShouldNotDegrade()
    {
        // Simulate 10 simulation runs in same session
        for (int i = 0; i < 10; i++)
        {
            // Initialize
            LiveLinkManager.Instance.Initialize("TestSource");
            Assert.IsTrue(LiveLinkManager.Instance.IsConnectionHealthy);
            
            // Use LiveLink (create/update/destroy objects)
            var updater = LiveLinkManager.Instance.GetOrCreateObject($"TestObj_{i}");
            updater.UpdateTransform(1.0, 2.0, 3.0, 0.0, 0.0, 0.0);
            
            // Shutdown
            LiveLinkManager.Instance.Shutdown();
        }
        
        // Verify no memory leaks, no performance degradation
    }
    
    [TestMethod]
    public void InitializeWithoutShutdown_ShouldNotLeak()
    {
        // Simulate user running simulation multiple times without proper cleanup
        for (int i = 0; i < 5; i++)
        {
            LiveLinkManager.Instance.Initialize("TestSource");
            // Note: No Shutdown() call
        }
        
        // Should not crash or leak resources
    }
}
```

**Performance Testing:**
```csharp
[TestClass]
public class PerformanceTests
{
    [TestMethod]
    public void FirstInitialization_ShouldCompleteWithin50ms()
    {
        var stopwatch = Stopwatch.StartNew();
        LiveLinkManager.Instance.Initialize("PerfTest");
        stopwatch.Stop();
        
        // First init: ~21ms native + ~5ms managed = ~26ms typical
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 50);
    }
    
    [TestMethod]
    public void SubsequentInitialization_ShouldCompleteWithin5ms()
    {
        // First init
        LiveLinkManager.Instance.Initialize("PerfTest");
        LiveLinkManager.Instance.Shutdown();
        
        // Second init (native static flag optimization)
        var stopwatch = Stopwatch.StartNew();
        LiveLinkManager.Instance.Initialize("PerfTest");
        stopwatch.Stop();
        
        // Subsequent: ~1ms native + ~2ms managed = ~3ms typical
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5);
    }
}
```

**Test Execution:**
```powershell
# Build mock DLL first
.\build\BuildMockDLL.ps1

# Run tests
dotnet test tests/Unit.Tests/Unit.Tests.csproj

# Override Simio installation path
dotnet test tests/Unit.Tests/Unit.Tests.csproj -p:SimioInstallDir="D:\Simio"
```

---

## Simio API Patterns & Gotchas

### Interface Discovery
**Issue:** Documentation often references `IStepSchema`, but actual interface is `IPropertyDefinitions`

```csharp
// ❌ INCORRECT (from outdated examples)
public void DefineSchema(IStepSchema schema)

// ✅ CORRECT (actual Simio API)
public void DefineSchema(IPropertyDefinitions schema)
```

**Element Reference Requirement:**
```csharp
// Element reference requires GUID parameter
var connectorProperty = schema.AddElementProperty("UnrealEngineConnector", 
    SimioUnrealEngineLiveLinkElementDefinition.MY_ID);
```

**Expression Property Runtime Evaluation:**
- Expression properties (e.g., "Entity.Location.X") evaluate at runtime
- Values can change each execution - don't cache results

### Dependency Management

**System.Drawing.Common Version Conflict:**
```xml
<!-- In .csproj -->
<PackageReference Include="System.Drawing.Common" Version="6.0.0" />
```

**Why 6.0.0?**
- Simio targets .NET Framework 4.8
- System.Drawing.Common 6.0.0 is compatible
- Later versions (7.0+) are .NET 6+ only

**App.config Binding Redirect** (for tests):
```xml
<dependentAssembly>
  <assemblyIdentity name="System.Drawing.Common" />
  <bindingRedirect oldVersion="0.0.0.0-6.0.0.0" newVersion="6.0.0.0" />
</dependentAssembly>
```

**Simio DLLs in Tests:**
- Post-build target copies `SimioAPI.dll` and `SimioAPI.Extensions.dll` from installation
- Override path: `-p:SimioInstallDir="D:\CustomPath"`
- Tests run without Simio installed if DLLs are in output directory

### Project Configuration

**Nullable Reference Types:**
```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <!-- Exclude nullable warnings for Simio API (not nullable-annotated) -->
  <WarningsNotAsErrors>CS8618;CS8625</WarningsNotAsErrors>
</PropertyGroup>
```

**Why exclude CS8618/CS8625?**
- Simio API predates nullable reference types
- Interfaces like `IElementData` don't have nullable annotations
- Excluding these warnings prevents false positives

### Test Environment Setup

**Best Practices:**
- Tests copy Simio DLLs post-build if present
- No custom Simio interface mocks (too complex - 50+ members)
- Use real Simio DLLs or skip Simio-dependent tests
- Focus unit tests on non-Simio-dependent code (CoordinateConverter, Utils)

**Simio-Dependent vs Independent Tests:**
```csharp
// ✅ GOOD: No Simio dependency
[TestMethod]
public void CoordinateConverter_Origin_ShouldReturnOrigin()

// ⚠️ REQUIRES SIMIO: Uses IExecutionContext
[TestMethod]
public void PropertyValidation_NormalizeFilePath_ValidPath()
// Either skip or use real Simio DLLs
```

---

## Code Review Checklist

Before marking a component complete:

**Compilation & Warnings:**
- [ ] Compiles without warnings
- [ ] No nullable reference type errors (CS8618/CS8625 expected)
- [ ] All `using` statements necessary

**Testing:**
- [ ] Unit tests written and passing
- [ ] Edge cases covered (null inputs, invalid values, empty strings)
- [ ] Integration tests pass with mock DLL
- [ ] **Restart scenarios tested** (multiple Initialize/Shutdown cycles)

**Code Quality:**
- [ ] Error messages include object/context information
- [ ] TraceInformation added for user visibility
- [ ] High-frequency steps have loop protection (1-second throttle)
- [ ] Uses `LiveLinkObjectUpdater` for transforms (never direct P/Invoke in Steps)
- [ ] Property readers follow established patterns
- [ ] No hardcoded paths or magic strings (use constants)

**Resource Management:**
- [ ] IDisposable implemented where resource cleanup needed
- [ ] No memory leaks (dispose object updaters, clear buffers)
- [ ] Singleton properly handles restart scenarios (reset state in Shutdown)

**Thread Safety:**
- [ ] Thread-safety considered (document assumptions)
- [ ] LiveLinkManager singleton used correctly
- [ ] No static mutable state in Steps

**Restart Stability:**
- [ ] Element.Initialize() can be called multiple times safely
- [ ] LiveLinkManager.Initialize() is idempotent
- [ ] Shutdown() properly resets all state for next run
- [ ] No static state persists between simulation runs

**Documentation:**
- [ ] XML comments for public APIs
- [ ] Inline comments for non-obvious logic
- [ ] README updated if new features added

---

## Performance Expectations

**Initialization Performance:**
- **First initialization:** ~26ms total (21ms native + 5ms managed)
  - Native: GEngineLoop.PreInit, module loading (one-time per Simio session)
  - Managed: Configuration validation, singleton setup
- **Subsequent initialization:** ~3ms total (1ms native + 2ms managed)
  - Native: Static flag check only (21x faster)
  - Managed: Configuration validation, connection check

**Runtime Performance:**
- Transform update: <1ms per call (including coordinate conversion)
- Data subject update: <0.5ms per call (lighter payload)
- Connection health check: <0.1ms (1-second cache reduces P/Invoke overhead)

**Why This Matters:**
- User runs simulation multiple times in same Simio session
- First run: ~26ms acceptable startup overhead
- Subsequent runs: ~3ms nearly imperceptible
- Native layer optimization (static flag) provides 21x speedup

---

## Definition of Complete

Managed layer is complete when:

**Functionality:**
- [ ] All 4 step types implemented and tested (Create, SetPosition, TransmitValues, Destroy)
- [ ] Element handles all 8 properties correctly
- [ ] All 12 P/Invoke functions wrapped and tested

**Testing:**
- [ ] 50+ unit tests passing (current: 37, target: 80+)
- [ ] Integration tests pass with mock DLL
- [ ] **Restart stability tests pass** (10+ Initialize/Shutdown cycles)
- [ ] Performance tests validate initialization timing (26ms / 3ms)
- [ ] No failing tests in CI

**Deployment:**
- [ ] Deploys to Simio without errors
- [ ] All properties visible and functional in Simio UI
- [ ] Steps appear in correct toolbox categories

**User Experience:**
- [ ] TraceInformation provides adequate feedback
- [ ] Error messages are clear and actionable
- [ ] No silent failures (all errors reported)
- [ ] Restart scenarios work seamlessly (user runs simulation 10+ times)

**Code Quality:**
- [ ] No warnings in build output
- [ ] Code reviewed and documented
- [ ] Follows established patterns consistently

**Restart Stability (Critical):**
- [ ] Element can be initialized multiple times in same session
- [ ] No performance degradation after 10+ simulation runs
- [ ] No memory leaks across multiple runs
- [ ] Native layer restart optimization confirmed (1ms subsequent init)

---

## Related Documentation

- **System design:** [Architecture.md](Architecture.md) - Technical architecture and design decisions
- **Native layer:** [NativeLayerDevelopment.md](NativeLayerDevelopment.md) - C++ implementation and LiveLink integration
- **Build/test:** [TestAndBuildInstructions.md](TestAndBuildInstructions.md) - Build commands and troubleshooting
- **Current status:** [DevelopmentPlan.md](DevelopmentPlan.md) - Phase tracking and completion checklists
- **Coordinate math:** [CoordinateSystems.md](CoordinateSystems.md) - Mathematical derivations for coordinate transformations
- **Lessons learned:** [ArchitecturalLessonsLearned.md](ArchitecturalLessonsLearned.md) - Comparison with reference implementation