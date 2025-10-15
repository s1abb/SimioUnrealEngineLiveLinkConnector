# End-to-End Tests

## Purpose
Tests that validate complete workflows from Simio simulation through the connector to Unreal Engine LiveLink.

## Scope
- **Full workflow validation** - Simio model → Connector → Native layer → Unreal Engine
- **Performance testing** - Validate streaming performance under load
- **Real-world scenarios** - Test with actual Simio models and Unreal projects
- **Error recovery** - Test system behavior when components fail or disconnect

## Requirements
- Compiled and deployed SimioUnrealEngineLiveLinkConnector in Simio
- Running Unreal Engine with LiveLink subsystem active
- Native library compiled and available
- Test Simio models with known entity patterns

## Test Framework
- MSTest with .NET Framework 4.8 (for Simio compatibility)
- May include Unreal Engine test automation (C++ with Automation Framework)
- Performance monitoring tools integration

## Test Categories
- **Smoke Tests** - Basic connectivity and data flow
- **Load Tests** - High entity count scenarios
- **Stress Tests** - Long-running simulations
- **Compatibility Tests** - Different Unreal Engine and Simio versions

## Future Implementation
This folder will contain end-to-end tests once the full integration is complete in Phase 4-5 of the development plan.