# Test and Build Instructions

## Overview
This document provides concise instructions for building and testing the SimioUnrealEngineLiveLinkConnector project.

## Prerequisites

### Development Environment
- **Visual Studio 2019/2022** or **VS Code** with C# extension
- **.NET Framework 4.8 SDK** (required for Simio compatibility)
- **PowerShell** (for build scripts)

### Future Requirements (Phase 3+)
- **Unreal Engine 5.0+** (for native layer development)
- **Visual Studio C++ workload** (for native compilation)

---

## Build Instructions

### Quick Build
```powershell
# Navigate to project root
cd C:\repos\SimioUnrealEngineLiveLinkConnector

# Build managed layer
dotnet build src/Managed/SimioUnrealEngineLiveLinkConnector.csproj

# Or build entire solution
dotnet build SimioUnrealEngineLiveLinkConnector.sln
```

### Using Build Scripts
```powershell
# Build managed layer only
.\build\BuildManaged.ps1

# Full build and deployment (when native layer exists)
.\build\BuildAndDeploy.ps1
```

### Visual Studio
1. Open `SimioUnrealEngineLiveLinkConnector.sln`
2. Build → Build Solution (Ctrl+Shift+B)
3. Tests will appear in Test Explorer

---

## Test Instructions

### Current Status (Phase 1)
- **Unit Tests**: ✅ Active (32/33 passing)
- **Integration Tests**: 🚧 Planned for Phase 3
- **E2E Tests**: 🚧 Planned for Phase 4-5
- **Native Tests**: 🚧 Planned for Phase 3

### Running Unit Tests

#### Command Line
```powershell
# Run all unit tests
dotnet test tests/Unit.Tests/Unit.Tests.csproj

# Run with verbose output
dotnet test tests/Unit.Tests/Unit.Tests.csproj --verbosity normal

# Run specific test class
dotnet test tests/Unit.Tests/Unit.Tests.csproj --filter "TestCategory=CoordinateConverter"
```

#### Visual Studio
1. Build → Build Solution
2. Test → Run All Tests (Ctrl+R, A)
3. View results in Test Explorer

#### VS Code
1. Install C# extension
2. Open Command Palette (Ctrl+Shift+P)
3. Run "Test: Run All Tests"

### Test Categories

#### Unit.Tests/ (Active)
- **CoordinateConverterTests.cs** - Mathematical conversion validation
- **LiveLinkManagerTests.cs** - Singleton management and registry operations
- **Expected Status**: 32/33 passing (1 test requires native DLL)

#### Integration.Tests/ (Future)
- P/Invoke marshaling validation
- Native library integration testing
- Requires compiled UnrealLiveLink.Native.dll

#### E2E.Tests/ (Future)  
- Full Simio → Connector → Unreal workflows
- Performance and load testing
- Requires deployed connector and running Unreal Engine

#### Native.Tests/ (Future)
- C++ unit tests for native layer
- Google Test or Catch2 framework
- Memory management and thread safety validation

---

## Project Structure

### Source Code
```
src/
├── Managed/                           # C# .NET Framework 4.8
│   ├── SimioUnrealEngineLiveLinkConnector.csproj
│   ├── Element/                       # Simio element definitions (Phase 2)
│   ├── Steps/                         # Simio step implementations (Phase 2)
│   └── UnrealIntegration/            # Foundation layer (✅ Complete)
│       ├── Types.cs                   # P/Invoke marshaling structs
│       ├── CoordinateConverter.cs     # Simio ↔ Unreal conversions
│       ├── UnrealLiveLinkNative.cs   # P/Invoke interface
│       ├── LiveLinkObjectUpdater.cs  # Per-object wrapper
│       └── LiveLinkManager.cs        # Singleton manager
└── Native/                           # C++ Unreal Engine plugin (Phase 3)
    └── UnrealLiveLink.Native/        # Native library project
```

### Test Structure
```
tests/
├── Unit.Tests/                       # ✅ Component unit tests (Active)
├── Integration.Tests/                # 🚧 P/Invoke integration (Phase 3)
├── E2E.Tests/                       # 🚧 Full workflow tests (Phase 4-5)  
└── Native.Tests/                    # 🚧 C++ unit tests (Phase 3)
```

---

## Build Outputs

### Current (Phase 1)
- `src/Managed/bin/Debug/net48/SimioUnrealEngineLiveLinkConnector.dll`
- `tests/Unit.Tests/bin/Debug/net48/Unit.Tests.dll`

### Future (Phase 3+)
- `lib/native/win-x64/UnrealLiveLink.Native.dll`
- Native test executables
- Packaged Simio extension (.spfx)

---

## Development Workflow

### Phase 1 (✅ Complete)
1. Build managed foundation layer
2. Run unit tests (expect 32/33 passing)
3. Commit changes with test validation

### Phase 2 (Current Target)
1. Implement Simio Element and Steps
2. Add unit tests for new components  
3. Test with mock Simio API integration

### Phase 3 (Future)
1. Implement native C++ layer
2. Add integration and native tests
3. Validate P/Invoke marshaling

### Phase 4-5 (Future)
1. Full system integration
2. Add E2E and performance tests
3. Production deployment validation

---

## Troubleshooting

### Common Issues

#### Build Failures
- Ensure .NET Framework 4.8 SDK is installed
- Check Visual Studio workloads include .NET Framework development

#### Test Failures  
- **Expected**: 1 test fails (native DLL not available in Phase 1)
- **Unexpected failures**: Check dependency references and build order

#### Missing Dependencies
```powershell
# Restore NuGet packages
dotnet restore SimioUnrealEngineLiveLinkConnector.sln
```

### Getting Help
1. Check build output for specific error messages
2. Verify prerequisites are installed
3. Consult phase-specific documentation in `docs/` folder
