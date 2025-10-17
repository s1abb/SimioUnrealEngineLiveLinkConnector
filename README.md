# SimioUnrealEngineLiveLinkConnector

**Purpose:** Project landing page, quick orientation, and navigation hub.

## Overview
[2-3 sentence description of what this project does]

## Quick Start
[Absolute minimal steps to build and run]
```powershell
# Link to TestAndBuildInstructions.md for full details
```

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
├── src/Managed/          # C# Simio extension
├── src/Native/           # C++ Unreal plugin & mock
├── tests/                # Unit, integration, E2E tests
├── build/                # Build automation scripts
├── docs/                 # Documentation
└── examples/             # Reference implementations
```

## Prerequisites
- Windows with Visual Studio Build Tools or full VS
- .NET Framework 4.8 Developer Pack
- PowerShell
- Simio (optional for local testing)
- Unreal Engine (optional for native development)

## Contributing
[Brief guidelines or link to contributing doc]

## License
[License information]