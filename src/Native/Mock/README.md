# Mock UnrealLiveLink.Native DLL

## Overview
Simple C++ mock implementation of UnrealLiveLink.Native.dll for P/Invoke validation and managed layer testing.

## Purpose
- **Validate P/Invoke marshaling** without Unreal Engine dependencies
- **Test complete managed layer** with realistic API responses
- **Enable fast development cycles** with immediate feedback
- **Verify error handling paths** through controlled error simulation

## Features
- ✅ **Same API contract** as real Unreal Engine implementation
- ✅ **Comprehensive logging** of all function calls and parameters
- ✅ **State tracking** for registered objects and properties
- ✅ **Error simulation** for testing failure scenarios
- ✅ **Fast build times** using Visual Studio C++ compiler

## Build
```powershell
# From repository root
.\build\BuildMockDLL.ps1

# Output: lib\native\win-x64\UnrealLiveLink.Native.dll (mock version)
```

## Usage
The mock DLL automatically logs all API calls to console output:
```
[MOCK] ULL_Initialize(sourceName='SimioSimulation')
[MOCK] ULL_RegisterObject(objectName='Entity1')
[MOCK] ULL_UpdateObject(objectName='Entity1', pos=[100.0,200.0,300.0])
```

## Transition to Real Implementation
Replace mock DLL with real Unreal Engine implementation - same API, no code changes required.

## State Tracking
- Tracks initialized state
- Tracks registered objects
- Tracks object properties
- Validates parameter consistency