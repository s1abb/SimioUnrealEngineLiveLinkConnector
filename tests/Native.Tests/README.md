# Native C++ Tests

## Purpose
Unit tests for the native C++ layer (UnrealLiveLink.Native.dll) implemented with modern C++ testing frameworks.

## Scope
- **C++ class unit tests** - Test individual classes in isolation
- **LiveLink integration** - Validate Unreal Engine LiveLink API usage
- **Memory management** - Test for memory leaks and proper RAII
- **Performance** - Benchmark critical paths and data structures
- **Thread safety** - Validate concurrent access patterns

## Test Framework Options
- **Google Test (GTest)** - Industry standard C++ testing framework
- **Catch2** - Modern, header-only alternative
- **Unreal Engine Test Framework** - If deeper UE integration needed

## Build Integration
- Separate CMake or Unreal Build Tool configuration
- Runs independently of managed layer tests
- CI/CD integration with native compilation pipeline
- Code coverage reporting for C++ code

## Test Categories
- **Unit Tests** - Individual function and class testing
- **Component Tests** - LiveLink message formatting, UDP communication
- **Mock Tests** - Test C++ layer without requiring Unreal Engine
- **Benchmark Tests** - Performance validation for high-frequency operations

## Requirements
- C++17 or later compiler
- Unreal Engine development libraries
- Test framework dependencies (GTest/Catch2)
- Mock frameworks for Unreal Engine APIs

## Future Implementation
This folder will contain C++ tests once the native layer is implemented in Phase 3 of the development plan.