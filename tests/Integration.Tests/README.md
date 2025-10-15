# Integration Tests

## Purpose
Tests that validate the interaction between the managed C# layer and the native C++ layer through P/Invoke.

## Scope
- **P/Invoke marshaling** - Verify data structures are correctly marshaled between managed and native code
- **Native library integration** - Test that UnrealLiveLink.Native.dll functions work correctly
- **Error handling** - Validate native error codes are properly handled by managed wrappers
- **Memory management** - Ensure no memory leaks in P/Invoke boundaries

## Requirements
- Requires compiled UnrealLiveLink.Native.dll in lib/native/win-x64/
- Uses actual native library (not mocked)
- May require Unreal Engine LiveLink running for full validation

## Test Framework
- MSTest with .NET Framework 4.8
- Project reference to main SimioUnrealEngineLiveLinkConnector project
- Native library dependency via DLL import

## Future Implementation
This folder will contain integration tests once the native layer is implemented in Phase 3 of the development plan.