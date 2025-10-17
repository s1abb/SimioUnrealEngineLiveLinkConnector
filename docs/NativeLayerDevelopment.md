# Native Layer Development Guide

**Purpose:** Complete implementation reference for the C++ native layer.  
**Audience:** C++ developers implementing the Unreal plugin  
**Scope:** Native implementation patterns, LiveLink integration, P/Invoke contract  
**Not Included:** Current status/progress (see DevelopmentPlan.md), architecture rationale (see Architecture.md)

---

## Overview
- Native layer role: Bridge managed C# to Unreal LiveLink Message Bus
- Deployment options: Unreal plugin (preferred) or standalone DLL
- P/Invoke contract: 11 exported C functions
- [Link to Architecture.md for design context]

## P/Invoke API Contract

### Lifecycle Functions
```cpp
// Initialize LiveLink provider with given name
extern "C" __declspec(dllexport) int ULL_Initialize(const char* providerName);

// Check if provider is connected
extern "C" __declspec(dllexport) int ULL_IsConnected();

// Shutdown provider and cleanup resources
extern "C" __declspec(dllexport) void ULL_Shutdown();
```

### Transform Subject Functions
```cpp
// Register transform subject (no properties)
extern "C" __declspec(dllexport) void ULL_RegisterObject(const char* name);

// Register transform subject with custom properties
extern "C" __declspec(dllexport) void ULL_RegisterObjectWithProperties(
    const char* name, 
    const char** propertyNames, 
    int propertyCount);

// Update transform only
extern "C" __declspec(dllexport) void ULL_UpdateObject(
    const char* name, 
    const ULL_Transform* transform);

// Update transform and properties
extern "C" __declspec(dllexport) void ULL_UpdateObjectWithProperties(
    const char* name,
    const ULL_Transform* transform,
    const float* propertyValues,
    int propertyCount);

// Remove subject
extern "C" __declspec(dllexport) void ULL_RemoveObject(const char* name);
```

### Data Subject Functions
```cpp
// Register data-only subject (no transform)
extern "C" __declspec(dllexport) void ULL_RegisterDataSubject(
    const char* name,
    const char** propertyNames,
    int propertyCount);

// Update data subject properties
extern "C" __declspec(dllexport) void ULL_UpdateDataSubject(
    const char* name,
    const float* propertyValues,
    int propertyCount);
```

### Utility Functions
```cpp
// Get version string
extern "C" __declspec(dllexport) const char* ULL_GetVersion();
```

### Data Types (Marshaling Contract)

**ULL_Transform Structure:**
```cpp
struct ULL_Transform
{
    double Position[3];   // [X, Y, Z] in Unreal coordinates (centimeters)
    double Rotation[4];   // Quaternion [X, Y, Z, W]
    double Scale[3];      // [X, Y, Z] scale factors
};
// Total size: 80 bytes (10 doubles)
```

### Memory Management Rules
- **String ownership:** Managed layer owns all input strings (const char*), native must NOT free
- **String returns:** Native owns returned string buffers, must persist until next call
- **Array parameters:** Passed as pointer + count, no dynamic allocation by native
- **Transform struct:** Passed by pointer, managed layer allocates, native reads only

## Implementation Patterns

### LiveLinkBridge Singleton
**Purpose:** Central coordinator for LiveLink provider and subject registry

```cpp
class LiveLinkBridge
{
public:
    static LiveLinkBridge& Get();
    
    // Provider management
    bool Initialize(const FString& ProviderName);
    void Shutdown();
    bool IsConnected() const;
    
    // Transform subjects
    void RegisterTransformSubject(const FName& SubjectName);
    void RegisterTransformSubjectWithProperties(
        const FName& SubjectName,
        const TArray<FName>& PropertyNames);
    void UpdateTransformSubject(
        const FName& SubjectName,
        const FTransform& Transform,
        const TArray<float>* PropertyValues = nullptr);
    
    // Data subjects
    void RegisterDataSubject(
        const FName& SubjectName,
        const TArray<FName>& PropertyNames);
    void UpdateDataSubject(
        const FName& SubjectName,
        const TArray<float>& PropertyValues);
    
    // Cleanup
    void RemoveSubject(const FName& SubjectName);
    
private:
    TSharedPtr<ILiveLinkProvider> Provider;
    FGuid SourceGuid;
    TMap<FName, FLiveLinkSubjectKey> SubjectRegistry;
    TMap<FName, TArray<FName>> PropertySchemas; // Track registered properties
    FCriticalSection ProviderLock;
};
```

### Coordinate Conversion Helpers
```cpp
// Convert ULL_Transform to Unreal FTransform
// Note: Coordinate conversion happens in managed layer, native just constructs FTransform
FTransform ConvertToUnrealTransform(const ULL_Transform* transform);

// Construct quaternion from components
FQuat MakeQuaternion(double x, double y, double z, double w);

// Construct vector from array
FVector MakeVector(const double* components);
```

### String Conversion Utilities
```cpp
// Convert C string to FName (cached for performance)
FName ConvertToFName(const char* str);

// Convert string array to TArray<FName>
TArray<FName> ConvertPropertyNames(const char** names, int count);

// Static buffer for ULL_GetVersion return
const char* GetVersionString();
```

### Error Handling Pattern
```cpp
// Validate inputs at C API boundary
extern "C" __declspec(dllexport) void ULL_UpdateObject(const char* name, const ULL_Transform* transform)
{
    if (!name || !transform)
    {
        UE_LOG(LogLiveLink, Error, TEXT("ULL_UpdateObject: Null parameter"));
        return;
    }
    
    try
    {
        LiveLinkBridge::Get().UpdateTransformSubject(
            ConvertToFName(name),
            ConvertToUnrealTransform(transform));
    }
    catch (const std::exception& ex)
    {
        UE_LOG(LogLiveLink, Error, TEXT("ULL_UpdateObject failed: %s"), *FString(ex.what()));
    }
}
```

## Development Sequence

### Phase 1: Unreal Plugin Structure
**Goal:** Establish build system and module

1. Create plugin descriptor (.uplugin)
   - Plugin name: UnrealLiveLinkNative
   - Modules: [{ Name: "UnrealLiveLinkNative", Type: "Runtime" }]
   - Dependencies: LiveLink, LiveLinkInterface

2. Configure Build.cs
   - Add public/private dependencies
   - Define exported module macro

3. Set up Target.cs for standalone builds
   - Program target for testing outside Unreal Editor

4. Verify plugin loads in Unreal Editor
   - Check Plugins window
   - Verify module appears in log

### Phase 2: LiveLink Integration
**Goal:** Implement LiveLink provider and subject management

1. Implement LiveLinkBridge singleton
   - Construct ILiveLinkProvider via ILiveLinkClient
   - Generate unique source GUID
   - Thread-safe initialization

2. Register transform subjects
   - Create FLiveLinkSubjectKey(SourceGuid, SubjectName)
   - Call Provider->RegisterSubject with ULiveLinkTransformRole

3. Send frame data
   - Construct FLiveLinkFrameDataStruct
   - Populate FLiveLinkTransformFrameData
   - Call Provider->UpdateSubjectFrameData

4. Handle subject removal
   - Provider->RemoveSubject(SubjectKey)
   - Clean up registry entries

5. Implement data subjects (similar to transform, but different role)

### Phase 3: C API Export Layer
**Goal:** Expose P/Invoke-compatible surface

1. Implement C function wrappers
   - Each wrapper validates inputs, calls LiveLinkBridge, handles exceptions

2. Add coordinate conversion helpers
   - ULL_Transform → FTransform construction

3. Implement string conversion with FName caching
   - TMap<FString, FName> cache for repeated lookups

4. Add logging infrastructure
   - DECLARE_LOG_CATEGORY_EXTERN(LogLiveLinkBridge, Log, All)
   - Use UE_LOG for all diagnostic output

5. Export functions with __declspec(dllexport)
   - Ensure C linkage (extern "C")
   - Verify calling convention (__cdecl)

### Phase 4: Testing & Validation
**Goal:** Verify P/Invoke compatibility and LiveLink behavior

1. Build native test harness (C++ console app)
   - LoadLibrary the DLL
   - GetProcAddress for each function
   - Call functions and verify behavior

2. Validate struct marshaling
   - Pass known ULL_Transform from test harness
   - Verify no corruption or misalignment

3. Test in Unreal Editor
   - Open LiveLink window
   - Verify subjects appear
   - Check frame rate and data accuracy

4. Stress test (100+ objects at 30 Hz)
   - Measure CPU/memory usage
   - Check for leaks

5. Integration test with managed layer
   - Deploy both DLLs to Simio
   - Run full Create → Update → Destroy cycle

## Build Configuration

### Unreal Build Tool Settings
```csharp
// UnrealLiveLink.Native.Build.cs
public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",
            "CoreUObject",
            "Engine",
            "LiveLink",
            "LiveLinkInterface"
        });

        // Export symbols for P/Invoke
        PublicDefinitions.Add("UNREALLIVELINKAPI_EXPORTS=1");
    }
}
```

### Calling Convention
- Use `__cdecl` (default for extern "C")
- Matches managed declaration: `CallingConvention.Cdecl`
- Consistent across all exported functions

### Build Variants
- **Development:** Full logging, assertions enabled, debug symbols
- **Shipping:** Minimal logging, optimizations enabled, smaller binary

## Testing Approach

### Native Test Harness
**Standalone C++ console app:**
```cpp
int main()
{
    HMODULE dll = LoadLibraryA("UnrealLiveLink.Native.dll");
    auto Initialize = (int(*)(const char*))GetProcAddress(dll, "ULL_Initialize");
    
    int result = Initialize("NativeTest");
    printf("Initialize returned: %d\n", result);
    
    // Test all functions...
    
    FreeLibrary(dll);
    return 0;
}
```

### Integration with Mock DLL
- Mock serves as reference implementation
- Native must match mock API exactly
- Use mock for comparison during development

### Validation Checklist
- [ ] All exports visible: `dumpbin /EXPORTS UnrealLiveLink.Native.dll`
- [ ] Struct size matches managed: sizeof(ULL_Transform) == 80 bytes
- [ ] LiveLink subjects appear in Unreal Editor window
- [ ] Transform updates reflect in actors bound to subjects
- [ ] Data subject properties readable in Blueprints (Get LiveLink Property Value)
- [ ] No crashes under rapid updates (stress test)
- [ ] Clean shutdown with no resource leaks (verify with Task Manager)

## LiveLink Provider Implementation

### ILiveLinkProvider Interface
**Key responsibilities:**
```cpp
// Obtained from ILiveLinkClient
ILiveLinkClient* LiveLinkClient = &IModularFeatures::Get()
    .GetModularFeature<ILiveLinkClient>(ILiveLinkClient::ModularFeatureName);

TSharedPtr<ILiveLinkProvider> Provider = LiveLinkClient->CreateProvider(
    TEXT("SimioConnector"),
    MakeShared<FLiveLinkProvider_SimioConnector>());
```

### Subject Registration
**Transform subjects:**
```cpp
FLiveLinkSubjectKey SubjectKey(SourceGuid, SubjectName);
Provider->RegisterSubject(SubjectKey, ULiveLinkTransformRole::StaticClass());
```

**Data subjects:**
```cpp
// Use base role for pure data
Provider->RegisterSubject(SubjectKey, ULiveLinkBasicRole::StaticClass());
```

### Frame Data Submission
**Transform with properties:**
```cpp
FLiveLinkFrameDataStruct FrameData(FLiveLinkTransformFrameData::StaticStruct());
FLiveLinkTransformFrameData* TransformData = FrameData.Cast<FLiveLinkTransformFrameData>();

TransformData->Transform = UnrealTransform;
TransformData->PropertyValues.Append(PropertyValues);

Provider->UpdateSubjectFrameData(SubjectKey, MoveTemp(FrameData));
```

## Error Handling Strategy

### Validation Layers
1. **C API layer:** Null checks, validate array counts match expectations
2. **Bridge layer:** Verify provider initialized, subject exists in registry
3. **LiveLink layer:** Handle Unreal API errors (invalid role, subject not found)

### Logging Strategy
- **Development:** Verbose logging of all operations
- **Shipping:** Errors only, minimal performance impact
- **Hot paths:** Avoid logging in UpdateObject (called frequently)

## Memory Management & Threading

### Threading Model
- **LiveLink thread-safety:** Provider methods are thread-safe
- **Bridge synchronization:** Use FCriticalSection for registry access
- **C API calls:** May come from multiple threads (protect shared state)

### Memory Ownership
- **Managed → Native:** Const pointers, native reads only
- **Native internal:** TSharedPtr for provider, TMap for registry
- **No shared ownership:** Clear boundary at C API surface

## Performance Considerations

### Optimization Targets
- Support 100+ objects at 30 Hz
- <5ms per UpdateObject call (including LiveLink submission)
- Minimal heap allocations in hot paths

### Profiling Points
- FName conversion overhead (string → FName lookup)
- Frame data struct construction/submission
- Registry lookups (TMap performance)

### Optimization Techniques
- Cache FName conversions in TMap
- Reuse FLiveLinkFrameDataStruct where possible
- Avoid unnecessary copies of TArray<float>

## Definition of Complete

Native layer is complete when:
- [ ] All 11 C API functions implemented and exported
- [ ] LiveLink provider successfully creates and manages subjects
- [ ] Subjects visible in Unreal LiveLink UI window
- [ ] Transform updates visible in Unreal actors bound to subjects
- [ ] Data subjects readable in Blueprints via Get LiveLink Property Value
- [ ] Native test harness validates all functions work correctly
- [ ] Integration tests pass with managed layer
- [ ] Performance targets met (100 objects @ 30Hz, <5ms per update)
- [ ] No memory leaks under stress testing (verified with profiling tools)
- [ ] Clean shutdown verified (all resources released)
- [ ] Builds successfully with Unreal Build Tool
- [ ] Code reviewed and documented

## Related Documentation
- **System design:** [Architecture.md](Architecture.md)
- **Managed layer:** [ManagedLayerDevelopment.md](ManagedLayerDevelopment.md)
- **Build/test:** [TestAndBuildInstructions.md](TestAndBuildInstructions.md)
- **Current status:** [DevelopmentPlan.md](DevelopmentPlan.md)