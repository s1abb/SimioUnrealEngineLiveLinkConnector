# Test and Build Instructions

## Overview
This document provides concise instructions for building, testing, and deploying the SimioUnrealEngineLiveLinkConnector project to Simio.

## Prerequisites

### Development Environment
- **Visual Studio 2019/2022** or **VS Code** with C# extension
- **.NET Framework 4.8 SDK** (required for Simio compatibility)  
- **PowerShell** (for build and deployment scripts)
- **Simio Installed** at `C:\Program Files\Simio LLC\Simio\` ✅ **VALIDATED**

### Future Requirements (Phase 3+)
- **Unreal Engine 5.0+** (for native layer development)
- **Visual Studio C++ workload** (for native compilation)

---

## Build Instructions ✅ **UPDATED**

### Recommended Build Process (Using Scripts)
```powershell
# Navigate to project root
cd C:\repos\SimioUnrealEngineLiveLinkConnector

# Step 1: Build the managed layer
.\build\BuildManaged.ps1

# Step 2: Deploy to Simio (requires admin rights)
.\build\DeployToSimio.ps1
```

### Manual Build (Alternative)
```powershell
# Build managed layer directly
dotnet build src/Managed/SimioUnrealEngineLiveLinkConnector.csproj --configuration Release

# Or build entire solution  
dotnet build SimioUnrealEngineLiveLinkConnector.sln --configuration Release
```

### Visual Studio Build
1. Open `SimioUnrealEngineLiveLinkConnector.sln`
2. Build → Build Solution (Ctrl+Shift+B)
3. Tests will appear in Test Explorer

**Note**: Build succeeds with 0 errors against real Simio APIs ✅

## Deployment to Simio ✅ **NEW**

### Automated Deployment
The `DeployToSimio.ps1` script handles copying the built DLL to Simio's UserExtensions directory:

```powershell
# Deploy to Simio (will request UAC elevation)
.\build\DeployToSimio.ps1
```

**Target Directory**: `C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\`

**Files Deployed**:
- `SimioUnrealEngineLiveLinkConnector.dll` (main extension)
- `UnrealLiveLink.Native.dll` (if available)
- `System.Drawing.Common.dll` (dependency)

### Manual Deployment (Alternative)
1. Build the project first
2. Copy from: `src\Managed\bin\Release\net48\SimioUnrealEngineLiveLinkConnector.dll`
3. Copy to: `C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\`
4. Requires administrator rights

---

## Test Instructions

### Current Status (Phase 2 Complete)
- **Unit Tests**: ✅ Active (32/33 passing - 97% success rate)
- **Simio Integration Tests**: ✅ Added (validation tests)
- **Compilation Tests**: ✅ Build succeeds against real Simio APIs
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
- **CoordinateConverterTests.cs** - Mathematical conversion validation ✅
- **LiveLinkManagerTests.cs** - Singleton management and registry operations ✅
- **SimioIntegrationTests.cs** - Simio component instantiation validation ✅ **NEW**
- **Expected Status**: 32/33 passing (1 test requires native DLL architecture fix)

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
│   ├── SimioUnrealEngineLiveLinkConnector.csproj  # ✅ Validated against real Simio APIs
│   ├── Element/                       # ✅ Simio element definitions (Phase 2 Complete)
│   │   ├── SimioUnrealEngineLiveLinkElement.cs         # Connection lifecycle management
│   │   └── SimioUnrealEngineLiveLinkElementDefinition.cs  # Element schema definition
│   ├── Steps/                         # ✅ Core Simio step implementations (Phase 2 Complete)
│   │   ├── CreateObjectStep.cs + StepDefinition.cs              # Object creation
│   │   ├── SetObjectPositionOrientationStep.cs + StepDefinition.cs  # Transform updates
│   │   ├── TransmitValuesStep.cs + StepDefinition.cs (🚧 Planned)   # Data streaming
│   │   └── DestroyObjectStep.cs + StepDefinition.cs (🚧 Planned)    # Object cleanup
│   └── UnrealIntegration/            # ✅ Foundation layer (Complete)
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
├── Unit.Tests/                       # ✅ Component unit tests (Active - 32/33 passing)
│   ├── CoordinateConverterTests.cs   # Mathematical validation
│   ├── LiveLinkManagerTests.cs       # Singleton and registry tests  
│   └── SimioIntegrationTests.cs      # ✅ NEW: Simio component validation
├── Integration.Tests/                # 🚧 P/Invoke integration (Phase 3)
├── E2E.Tests/                       # 🚧 Full workflow tests (Phase 4-5)  
└── Native.Tests/                    # 🚧 C++ unit tests (Phase 3)
```

---

## Build Outputs

### Current (Phase 2 Complete)
**Release Build**:
- `src/Managed/bin/Release/net48/SimioUnrealEngineLiveLinkConnector.dll` ✅ **Main extension DLL**
- `src/Managed/bin/Release/net48/System.Drawing.Common.dll` (dependency)

**Test Build**:
- `tests/Unit.Tests/bin/Debug/net48/Unit.Tests.dll`

**Deployment Target**:
- `C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\` ✅

### Future (Phase 3+)  
- `lib/native/win-x64/UnrealLiveLink.Native.dll`
- Native test executables
- Packaged Simio extension (.spfx)

---

## Development Workflow

### Phase 1 (✅ Complete)  
1. ✅ Build managed foundation layer
2. ✅ Run unit tests (32/33 passing)
3. ✅ Validate coordinate conversion mathematics

### Phase 2 (✅ Complete - ACCELERATED)
1. ✅ Implement Simio Element and core Steps
2. ✅ Add Simio integration unit tests
3. ✅ **BREAKTHROUGH**: Compile against real Simio APIs (not mock)
4. ✅ **NEW**: Build and deployment automation

### **Current Workflow** (Simio Validation)
1. **Build**: `.\build\BuildManaged.ps1`
2. **Deploy**: `.\build\DeployToSimio.ps1` (requires admin)
3. **Validate**: Launch Simio and verify extension appears
4. **Report**: Provide feedback on Element/Steps visibility

### Phase 3 (Next Target)
1. Complete remaining Step definitions (TransmitValues, DestroyObject)
2. Implement native C++ layer  
3. Add integration and native tests
4. Validate P/Invoke marshaling

### Phase 4-5 (Future)
1. Full system integration with Unreal Engine
2. Add E2E and performance tests
3. Production deployment validation

---

## Simio Validation Checklist ✅ **NEW**

After running the deployment script, verify the extension is properly registered:

### 1. Launch Simio
- Open Simio application
- Create a new model or open existing model

### 2. Check Elements Toolbox
- Look for **"UnrealEngineLiveLinkConnector"** element
- Should appear in Elements panel/toolbox
- **Expected**: Element icon and description visible

### 3. Check Steps Toolbox  
- Look for **"CreateObject"** step
- Look for **"SetObjectPositionOrientation"** step
- Should appear in Process Steps panel
- **Expected**: Step icons and descriptions visible

### 4. Test Element Creation
- Try adding UnrealEngineLiveLinkConnector element to model
- Check if properties panel shows "Source Name" property
- **Expected**: Element instantiates without errors

### 5. Report Results
Please report back with:
- ✅ **Element appears in toolbox**: YES/NO
- ✅ **Steps appear in toolbox**: YES/NO  
- ✅ **Element can be created**: YES/NO
- ❌ **Any error messages**: (if any)

---

## Troubleshooting

### Common Issues

#### Build Failures
- ✅ **RESOLVED**: Project compiles against real Simio APIs  
- Ensure .NET Framework 4.8 SDK is installed
- Check Visual Studio workloads include .NET Framework development

#### Deployment Issues
- **UAC Prompt**: Accept administrator elevation when prompted
- **Access Denied**: Run PowerShell as Administrator manually
- **Simio Not Found**: Verify Simio installed at `C:\Program Files\Simio LLC\Simio\`

#### Test Failures  
- **Expected**: 1 test fails (native DLL architecture issue - unrelated to Simio)
- **Current Status**: 32/33 tests passing (97% success rate)
- **New Tests**: Simio integration tests may fail outside Simio environment (expected)

#### Missing Dependencies
```powershell
# Restore NuGet packages
dotnet restore SimioUnrealEngineLiveLinkConnector.sln
```

### System.Drawing.Common Warnings
**Expected warnings** during build about version conflicts - these are resolved automatically by selecting version 6.0.0 for .NET Framework 4.8 compatibility.

### Getting Help
1. Check build output for specific error messages
2. Verify prerequisites are installed  
3. For Simio validation issues, check Windows Event Viewer for extension loading errors
4. Consult phase-specific documentation in `docs/` folder
