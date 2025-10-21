//=============================================================================
// UnrealLiveLinkNativeMain.cpp
//=============================================================================
// Main entry point for the UnrealLiveLinkNative Program DLL.
// Implements the required IMPLEMENT_APPLICATION macro for Program targets.
//
// Sub-Phase 6.6.2: GEngineLoop initialization support
// Based on reference: UnrealLiveLinkCInterface
//=============================================================================

#include "RequiredProgramMainCPPInclude.h"

// Implement the application (required for Program target type)
IMPLEMENT_APPLICATION(UnrealLiveLinkNative, "UnrealLiveLinkNative");
