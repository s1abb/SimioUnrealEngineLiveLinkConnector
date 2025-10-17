#include "UnrealLiveLink.Native.h"
#include <cstdio>
#include "Windows/WindowsHWrapper.h"
#include "CoreGlobals.h"

DEFINE_LOG_CATEGORY(LogUnrealLiveLinkNative);

// Define required UE globals with proper types
TCHAR GInternalProjectName[64] = TEXT("UnrealLiveLinkNative");
const TCHAR* GForeignEngineDir = TEXT("");

// Windows entry point for Program target
int WINAPI WinMain(HINSTANCE hInst, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    // Basic printf instead of UE_LOG to avoid complications
    printf("UnrealLiveLinkNative starting...\n");
    
    // TODO: Implement LiveLink bridge functionality in later sub-phases
    
    printf("UnrealLiveLinkNative completed\n");
    return 0;
}