# Architecture

## Overview
SimioUnrealEngineLiveLinkConnector is a Simio extension that streams real-time object transforms and simulation data from Simio simulations to Unreal Engine via LiveLink. It consists of two layers: a C# managed layer integrating with Simio's API, and a C++ native bridge communicating with Unreal's LiveLink Message Bus.

**Key Capabilities:**
1. **Transform Streaming:** Real-time 3D object position, rotation, and scale
2. **Data Streaming:** Simulation metrics, KPIs, and system state (NEW!)
3. **Property Streaming:** Custom float properties attached to objects or data subjects
4. **Dynamic Management:** Register, update, and remove objects during simulation

## Repo Structure
C:.
|   README.md
|   SimioUnrealEngineLiveLinkConnector.sln
|   
+---build
|       BuildAndDeploy.ps1
|       BuildManaged.ps1
|       BuildNative.ps1
|       DeployToSimio.ps1
|       
+---docs
|       Architecture.md
|       BuildInstructions.md
|       CoordinateSystems.md
|       ManagedLayerDevelopment.md
|       NativeLayerDevelopment.md
|       SimioInstructions.md
|       TestInstructions.md
|       UnrealSetup.md
|
+---examples
|   \---OmniverseConnector
|           CreatePrimStep.md
|           CreatePrimStepDefinition.md
|           DestroyPrimStep.md
|           DestroyPrimStepDefinition.md
|           OmniverseElement.md
|           OmniverseElementDefinition.md
|           SetPrimPositionAndOrientationStep.md
|           SetPrimPositionAndOrientationStepDefinition.md
|
+---lib
|   \---native
|       \---win-x64
|               <!-- Note: the mock native DLL is produced locally by build scripts and is intentionally untracked -->
|               UnrealLiveLink.Native.pdb
|               VERSION.txt
|
+---src
|   +---Managed
|   |   |   SimioUnrealEngineLiveLinkConnector.csproj
|   |   |
|   |   +---Element
|   |   |       SimioUnrealEngineLiveLinkElement.cs
|   |   |       SimioUnrealEngineLiveLinkElementDefinition.cs
|   |   |
|   |   +---Steps
|   |   |       CreateObjectStep.cs
# Architecture (Concise)

Purpose
- Provide a short, accurate description of the system architecture for contributors and reviewers.

Current status (short)
- Managed layer (C#): implemented and validated. Provides Simio Element and 4 Steps (Create, SetPositionOrientation, TransmitValues, Destroy).
- Native layer (C++): mock native DLL implemented for development/testing. A real Unreal Engine LiveLink plugin is scaffolded but not yet implemented.
- Tests: unit tests and integration tests run locally with the mock; unit tests are passing after test-run fixes.
- Build: scripts exist for environment setup, mock native build, managed build, test and deployment. Deployment script requests elevation when writing to Program Files.

High-level components

1) Managed layer (Simio integration)

- Purpose: Implement Simio Element and Steps, coordinate conversion, and marshal data to the native bridge.
- Key classes/files:
    - `Element/SimioUnrealEngineLiveLinkElementDefinition.cs` — element schema. Exposes 7 essential properties (SourceName, EnableLogging, LogFilePath, UnrealEnginePath, LiveLinkHost, LiveLinkPort, ConnectionTimeout, RetryAttempts). Note: `EnableLogging` is an ExpressionProperty (default True).
    - `Element/SimioUnrealEngineLiveLinkElement.cs` — reads configuration into `LiveLinkConfiguration`, validates it, and manages lifecycle via `LiveLinkManager`.
    - `UnrealIntegration/LiveLinkManager.cs` — singleton that owns the native integration and object registry.
    - `UnrealIntegration/LiveLinkObjectUpdater.cs` — per-object wrapper, lazy registration, property management.
    - `UnrealIntegration/Types.cs` and `UnrealIntegration/UnrealLiveLinkNative.cs` — P/Invoke types and declarations.
    - `UnrealIntegration/CoordinateConverter.cs` — Simio ↔ Unreal conversion utilities.

2) Native layer (C++ bridge)

- Purpose: Bridge from managed P/Invoke into Unreal's LiveLink APIs (subject registration and frame updates).
- Current state:
    - Mock DLL (`build/BuildMockDLL.ps1` → `lib/native/win-x64/UnrealLiveLink.Native.dll`) exists and is used for testing.
    - The Unreal plugin scaffold is present under `src/Native/UnrealLiveLink.Native/` for future implementation.
- Design notes:
    - Minimal, C-compatible API for P/Invoke (`ULL_Initialize`, `ULL_RegisterObject`, `ULL_UpdateObject`, `ULL_UpdateDataSubject`, `ULL_RemoveObject`, `ULL_Shutdown`).
    - Two subject types: Transform (3D) and Data (named float properties).

Data flow (summary)
- Simio Step (Create/Set/Transmit) → LiveLinkObjectUpdater / LiveLinkManager → CoordinateConverter → P/Invoke call → native bridge → Unreal LiveLink provider → LiveLink message bus → Unreal actors/components or Blueprints.

Key decisions & rationale
- Mock-first approach: The mock native DLL enables rapid development and CI-friendly integration tests without requiring a full Unreal Engine environment.
- Untracked mock binary: The mock DLL is produced locally and intentionally untracked to avoid committing generated binaries.
- Configuration object: `LiveLinkConfiguration` centralizes element properties and validation, improving testability and clarity.
- TraceInformation policy: initialization writes a trace line. Shutdown does not write a final TraceInformation to avoid overwriting Simio trace files at simulation end.
- Test runtime behavior: the test project copies Simio runtime DLLs into test output when available and uses an `App.config` redirect for System.Drawing.Common to avoid FileLoadExceptions.

Build & test notes
- Run `.
\build\SetupVSEnvironment.ps1` (once per session) to prepare toolchain.
- Build mock native: `.
\build\BuildMockDLL.ps1` (produces local mock DLL).
- Build managed: `.
\build\BuildManaged.ps1` or `dotnet build`.
- Run unit tests: `dotnet test tests/Unit.Tests/Unit.Tests.csproj` (post-build copies Simio DLLs if present).

Next priorities (top 3)
1. Native: implement the real Unreal LiveLink plugin and validate P/Invoke compatibility (high priority).
2. CI: add a GitHub Actions workflow to run restore → build → test (medium-high). Consider self-hosted runner for integration tests or provide Simio runtime artifacts to CI.
3. Packaging: create a reproducible UserExtensions package and optionally an installer for easier deployment (medium).

Notes for contributors
- Do not commit generated binaries. Use `build/BuildMockDLL.ps1` to produce the mock locally.
- If tests fail with System.Drawing.Common, ensure the test project's `App.config` is present or run tests on a machine with matching System.Drawing.Common.

Contact / ownership
- Managed layer: primary maintained in `src/Managed/` (C# team / dev)
- Native layer: `src/Native/UnrealLiveLink.Native/` (native lead)
- CI & packaging: devops / release owner

---

This file replaces the previous long-form architecture doc and focuses on current, accurate facts and next steps. If you want, I will commit the tidy and push it to `main`.