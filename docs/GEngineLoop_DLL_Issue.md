# Critical Issue: GEngineLoop in DLL vs Standalone Program

**Date:** October 20, 2025  
**Status:** 🔴 **BLOCKER** - GEngineLoop causes host process termination  
**Severity:** High - Makes connector unusable

---

## Observed Behavior

### What Works ✅
1. **Simio loads without crash** - Major progress from previous crash!
2. **Simio trace shows success:**
   - `LiveLink connection initialized with source 'SimioSimulation' on localhost:11111`
   - `LiveLink object 'ModelEntity1.6' created at position (-5.75, 0.00, -0.00)`
   - `LiveLink position updated for 'ModelEntity1.6' (-4.50, 0.00, 0.00)`
   - `LiveLink object 'ModelEntity1.6' destroyed successfully`
3. **UE listening on Message Bus:**
   - `LogUdpMessaging: Initializing bridge on interface 0.0.0.0:0 to multicast group 230.0.0.1:6666`
   - `LogUdpMessaging: Display: Added local interface '10.49.21.79' to multicast group '230.0.0.1:6666'`

### What Doesn't Work ❌
1. **No "SimioSimulation" source in UE LiveLink window** - Primary failure
2. **Simio closes when user presses Stop** - Unacceptable UX
3. **No native DLL logs visible** - Can't verify if GEngineLoop.PreInit() succeeded
4. **No UDP packets observed** - ILiveLinkProvider may not be broadcasting

---

## Root Cause Analysis

### Problem 1: GEngineLoop Owns the Process

```cpp
void FLiveLinkBridge::Shutdown()
{
    // THIS SHUTS DOWN THE ENTIRE PROCESS!
    RequestEngineExit(TEXT("UnrealLiveLinkNative shutting down"));
    FEngineLoop::AppPreExit();
    FModuleManager::Get().UnloadModulesAtShutdown();
    FEngineLoop::AppExit();  // ← Terminates Simio.exe
}
```

**Why:** `GEngineLoop` and `FEngineLoop::AppExit()` are designed for **standalone programs** (like UnrealEditor.exe, UnrealPak.exe), not DLLs loaded by other processes.

**Effect:** When Simio calls `ULL_Shutdown()` → `FLiveLinkBridge::Shutdown()` → `FEngineLoop::AppExit()`, it **terminates the Simio process entirely**.

### Problem 2: Uncertain GEngineLoop Initialization

We don't know if `GEngineLoop.PreInit()` actually succeeded because:
1. No logs visible (UE_LOG uses OutputDebugString, not files)
2. No way to verify from Simio trace
3. Silent failure possible if modules can't load

### Problem 3: Reference Implementation Context

**UnrealLiveLinkCInterface** uses GEngineLoop because it's:
- A **standalone executable** that happens to be compiled as DLL
- Called from **external processes** (Python, C programs)
- **Owns its own process** - safe to call AppExit()

**Our situation is different:**
- We're a **true DLL** loaded into Simio's process
- Simio **owns the process** - we can't shut it down!
- Need lightweight initialization without process ownership

---

## Evidence

### Simio Trace (First 100 lines)
```
Time(Hours),EntityID,ObjectName,ProcessID,StepName,Action
0,--,--,--,--,LiveLink connection initialized with source 'SimioSimulation' on localhost:11111
0,ModelEntity1.6,Model,0.Source1_CreatedEntity,[CreateObject] CreateObject1,"LiveLink object 'ModelEntity1.6' created at position (-5.75, 0.00, -0.00)"
0,ModelEntity1.6,ModelEntity1.6,3.Process1,[SetObjectPositionOrientation] SetObjectPositionOrientation3,"LiveLink position updated for 'ModelEntity1.6' (-4.50, 0.00, 0.00)"
0.0016865079365079366,ModelEntity1.6,Model,2.Sink1_DestroyingEntity,[DestroyObject] DestroyObject1,LiveLink object 'ModelEntity1.6' destroyed successfully
```
✅ Managed layer working perfectly!

### UE Log Analysis
```
LogUdpMessaging: Initializing bridge on interface 0.0.0.0:0 to multicast group 230.0.0.1:6666
LogUdpMessaging: Display: Unicast socket bound to '0.0.0.0:51782'
LogUdpMessaging: Display: Added local interface '10.49.21.79' to multicast group '230.0.0.1:6666'
```
✅ UE Message Bus listening correctly!

**But:** No log entries for:
- ❌ `SimioSimulation` source
- ❌ `LiveLinkProvider` creation
- ❌ Subject registration
- ❌ Frame data updates

---

## Possible Solutions

### Option A: Minimal GEngineLoop (No AppExit)

Don't call `RequestEngineExit()` or `AppExit()` in Shutdown:

```cpp
void FLiveLinkBridge::Shutdown()
{
    if (LiveLinkProvider.IsValid())
    {
        LiveLinkProvider.Reset();
    }
    
    // DON'T call RequestEngineExit or AppExit!
    // Just clean up our resources
    // FModuleManager::Get().UnloadModulesAtShutdown();  // ← Also risky!
    
    bInitialized = false;
}
```

**Pros:**
- Doesn't terminate host process
- Keeps UE runtime active for future reinitialization

**Cons:**
- May leak resources
- Modules stay loaded in Simio process
- Uncertain if safe

### Option B: Check if GEngineLoop Actually Works

Before assuming it works, verify:
1. Does `GEngineLoop.PreInit()` return success?
2. Does `ILiveLinkProvider::CreateLiveLinkProvider()` succeed?
3. Can we call `LiveLinkProvider->UpdateSubjectStaticData()` without crash?

Add comprehensive logging:
```cpp
UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: Calling GEngineLoop.PreInit..."));
int32 PreInitResult = GEngineLoop.PreInit(TEXT("UnrealLiveLinkNative -Messaging"));
UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Initialize: GEngineLoop.PreInit returned %d"), PreInitResult);

// Check if it actually worked
if (PreInitResult != 0)
{
    UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("Initialize: GEngineLoop.PreInit FAILED!"));
    return false;
}
```

### Option C: Abandon GEngineLoop for DLL

**Reference the ILiveLinkProvider documentation** - maybe it doesn't actually require full GEngineLoop?

Try minimal initialization:
```cpp
// Just load the module, don't initialize full engine
FModuleManager::Get().LoadModule(TEXT("UdpMessaging"));
FModuleManager::Get().LoadModule(TEXT("LiveLinkMessageBusFramework"));

// Try to create provider
LiveLinkProvider = ILiveLinkProvider::CreateLiveLinkProvider(ProviderName);
```

### Option D: Raw UDP Message Bus Protocol

Bypass `ILiveLinkProvider` entirely and implement raw UDP protocol:
- Send UDP packets directly to 230.0.0.1:6666
- Format as UE Message Bus protocol
- More control, but complex implementation

---

## Next Steps

### Immediate (Test Current State)
1. ✅ **Remove `RequestEngineExit` and `AppExit` from Shutdown** - Quick fix to prevent Simio termination
2. ✅ **Add return value checking** for `GEngineLoop.PreInit()`
3. ✅ **Add comprehensive logging** for each initialization step
4. ✅ **Rebuild and test** - See if provider actually gets created

### Short Term (Debug)
1. Use **DebugView++** to capture UE_LOG output from native DLL
2. Verify `ILiveLinkProvider::CreateLiveLinkProvider()` succeeds
3. Check if UDP packets are being sent (Wireshark on 230.0.0.1:6666)

### Medium Term (Alternatives)
1. Research if `ILiveLinkProvider` actually requires GEngineLoop
2. Investigate minimal initialization approach
3. Consider raw UDP protocol implementation if necessary

---

## Questions to Answer

1. **Does GEngineLoop.PreInit() succeed in a DLL context?**
   - Return value check needed
   - May fail silently

2. **Can ILiveLinkProvider work without full engine initialization?**
   - Documentation research needed
   - May only need module loading

3. **Is there a "DLL-safe" version of engine initialization?**
   - Check other UE DLL plugins
   - May be an established pattern

4. **Why isn't any native logging visible?**
   - UE_LOG may require additional setup
   - OutputDebugString may not work from DLL
   - Need DebugView++ or file logging

---

## Status

**Current State:** DLL loads, Simio thinks it works, but:
- ❌ No LiveLink source in UE
- ❌ Shutdown terminates Simio
- ❓ Uncertain if GEngineLoop actually initialized
- ❓ Unknown if ILiveLinkProvider was created

**Blocker:** Cannot ship with current Shutdown behavior (terminates host application).

**Priority:** HIGH - Fix Shutdown issue immediately, then debug provider creation.

---

## Related Files

- `src/Native/UnrealLiveLink.Native/Private/LiveLinkBridge.cpp` - Initialize() and Shutdown()
- `src/Native/UnrealLiveLink.Native/Private/UnrealLiveLinkNativeMain.cpp` - IMPLEMENT_APPLICATION
- `tests/Simio.Tests/Model_Model_trace.csv` - Simio execution trace
- `C:\projects\UE\SUELLCTest\Saved\Logs\SUELLCTest.log` - UE log (no SimioSimulation activity)

---

## Next Action

**IMMEDIATE FIX REQUIRED:** Remove `RequestEngineExit` and `AppExit` calls, add logging, rebuild, test.
