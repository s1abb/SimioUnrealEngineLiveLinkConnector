# SimioUnrealEngineLiveLinkConnector

**Purpose:** Project landing page, quick orientation, and navigation hub.

## Overview
A C# Simio extension and C++ native bridge that enables real-time streaming of simulation data from Simio to Unreal Engine via LiveLink protocol. Allows Simio simulation entities to control virtual objects in Unreal Engine for visualization, digital twin applications, and mixed reality experiences.

## Quick Start
```powershell
# Clone and navigate to repository
git clone https://github.com/s1abb/SimioUnrealEngineLiveLinkConnector.git
cd SimioUnrealEngineLiveLinkConnector

See [Test & Build Instructions](docs/TestAndBuildInstructions.md) for detailed setup and troubleshooting.

## Documentation Map

### Core Development Docs
- **[Architecture](docs/Architecture.md)** - System design and technical architecture
- **[Development Plan](docs/DevelopmentPlan.md)** - Current status and phased task checklists
- **[Managed Layer Development](docs/ManagedLayerDevelopment.md)** - C# implementation guide
- **[Native Layer Development](docs/NativeLayerDevelopment.md)** - C++/Unreal implementation guide
- **[Test & Build Instructions](docs/TestAndBuildInstructions.md)** - Build, test, and deploy workflows

### Reference Documentation
- [Coordinate Systems](docs/CoordinateSystems.md) - Mathematical transformations
- [Simio Instructions](docs/SimioInstructions.md) - End-user setup guide
- [Unreal Setup](docs/UnrealSetup.md) - Unreal Engine configuration

## Repository Structure
```
SimioUnrealEngineLiveLinkConnector/
├── src/
│   ├── Managed/          # C# Simio extension (✅ Complete)
│   └── Native/           # C++ UE Program & mock (🔄 Phase 6.1+ in progress)
├── tests/
│   ├── Unit.Tests/       # Unit tests (✅ 33+ passing)
│   ├── Integration.Tests/# Integration tests (✅ Working)
│   └── E2E.Tests/        # End-to-end tests (📋 Planned)
├── build/                # Build automation scripts (✅ Complete)
├── lib/native/win-x64/   # Build outputs (DLL + EXE)
├── docs/                 # Comprehensive documentation (✅ Current)
└── examples/             # Reference implementations & patterns
```

## Prerequisites
### Required
- Windows 10/11
- Visual Studio 2022 Build Tools (with C++ workload)
- .NET Framework 4.8 Developer Pack
- PowerShell 5.1+

### Optional
- **Simio** (for local testing and deployment)
- **Unreal Engine 5.6 Source** (for native development) 
  - Binary installation insufficient - source build required at `C:\UE\UE_5.6_Source\`
  - See [Native Layer Development](docs/NativeLayerDevelopment.md) for setup details

## Key Features

### Managed Layer (C# Simio Extension)
- ✅ **4 Custom Steps**: CreateObject, SetPosition, TransmitValues, DestroyObject
- ✅ **1 Custom Element**: UnrealEngineConnector with comprehensive properties
- ✅ **Coordinate System Integration**: Simio ↔ Unreal transformations
- ✅ **P/Invoke Bridge**: Seamless native DLL integration
- ✅ **Production Ready**: All tests passing, deployable to Simio

### Native Layer (C++ UE Integration)
- ✅ **UBT Build System**: Complete UnrealBuildTool integration
- ✅ **UE 5.6 Compatibility**: Source build environment operational  
- 🔄 **LiveLink Protocol**: Implementation in progress (Phase 6.2+)
- 🔄 **Message Bus Integration**: Planned real-time data streaming
- 📋 **C API**: 12-function interface for Simio P/Invoke

### Development & Testing
- ✅ **Mock DLL**: Complete API simulation for development
- ✅ **Automated Builds**: PowerShell scripts for all components
- ✅ **Comprehensive Testing**: Unit, integration, and validation tests
- ✅ **Documentation**: Complete technical and user documentation

