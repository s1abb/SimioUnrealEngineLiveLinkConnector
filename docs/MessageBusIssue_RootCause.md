# LiveLink Architecture Issue - Root Cause Analysis

**Date:** October 20, 2025  
**Issue:** Simio successfully initializes LiveLink and updates objects, but nothing appears in Unreal Engine's LiveLink window

---

## Root Cause

### Current Implementation (Sub-Phase 6.6)
The native DLL uses **`FSimioLiveLinkSource`** which is an **in-process** LiveLink source:

```cpp
class FSimioLiveLinkSource : public ILiveLinkSource
{
    // This directly talks to ILiveLinkClient in the SAME process
    virtual void ReceiveClient(ILiveLinkClient* InClient, FGuid InSourceGuid) override
    {
        Client = InClient;  // Direct pointer to client IN THIS PROCESS
        SourceGuid = InSourceGuid;
    }
};
```

**Problem:** This only works when the DLL is loaded **INSIDE Unreal Engine's process**. Since the DLL is loaded by **Simio's process**, there is NO ILiveLinkClient available!

### Expected Architecture (Not Yet Implemented)
The architecture document specifies using **`FLiveLinkMessageBusSource`** which communicates **between processes** via UDP:

> "Communicates over UDP (port 6666 default) to Unreal Editor"
> "No direct coupling - Editor and DLL run as separate processes"

**Process Architecture:**
```
┌─────────────────┐                          ┌─────────────────┐
│  Simio Process  │                          │   UE Process    │
│                 │                          │                 │
│  ┌───────────┐  │                          │  ┌───────────┐  │
│  │ Managed   │  │                          │  │ LiveLink  │  │
│  │ Layer     │  │                          │  │ Subsystem │  │
│  └─────┬─────┘  │                          │  └─────▲─────┘  │
│        │        │                          │        │        │
│  ┌─────▼─────┐  │   UDP Message Bus        │  ┌─────┴─────┐  │
│  │ Native    │  │   (Port 6666/11111)      │  │ Message   │  │
│  │ DLL       │──┼──────────────────────────┼─▶│ Bus       │  │
│  │ with      │  │   230.0.0.1:6666         │  │ Listener  │  │
│  │ MessageBus│  │   Multicast              │  └───────────┘  │
│  └───────────┘  │                          │                 │
└─────────────────┘                          └─────────────────┘
    EXTERNAL                                      TARGET
    APPLICATION                                   UNREAL ENGINE
```

---

## Evidence

### 1. Simio Trace Shows Success
```
LiveLink connection initialized with source 'SimioSimulation' on localhost:11111
LiveLink object 'ModelEntity1.6' created at position (-5.75, 0.00, -0.00)
LiveLink position updated for 'ModelEntity1.6' (-4.50, 0.00, 0.00)
```
✅ Simio thinks it's working!

### 2. Unreal Engine Shows UDP But No Source
```
LogUdpMessaging: Initializing bridge on interface 0.0.0.0:0 to multicast group 230.0.0.1:6666
LogUdpMessaging: Display: Unicast socket bound to '0.0.0.0:51517'
LogUdpMessaging: Display: Added local interface '10.49.21.79' to multicast group '230.0.0.1:6666'
```
✅ UE is listening on the Message Bus  
❌ No "SimioSimulation" source appears in LiveLink window

### 3. Code Comments Confirm Temporary Status
```cpp
// Temporary minimal source until we can access LiveLinkMessageBusSource
class FSimioLiveLinkSource : public ILiveLinkSource

// Note: Using FSimioLiveLinkSource instead of FLiveLinkMessageBusSource
```

---

## Why Current Code "Works" Locally

The current implementation succeeds in **tests** because integration tests mock the P/Invoke layer:
- Tests validate the **API contract** (function signatures, return codes)
- Tests do NOT validate **actual network communication**
- Tests assume a "working" implementation (which is correct for unit/integration testing)

The managed layer has NO way to know the native layer isn't actually sending data over the network!

---

## Configuration Mismatch

| Component | Protocol | Address | Port | Status |
|-----------|----------|---------|------|--------|
| **Simio Config** | Unicast | localhost | 11111 | ⚠️ Ignored by current impl |
| **Current Native DLL** | In-Process | N/A | N/A | ❌ Not communicating |
| **UE LiveLink** | Multicast | 230.0.0.1 | 6666 | ✅ Listening |

**The Simio configuration (localhost:11111) is being IGNORED** because the current implementation doesn't use Message Bus at all!

---

## Required Implementation (Sub-Phase 6.X)

### Add FLiveLinkMessageBusSource Integration

**Code Location:** `src/Native/UnrealLiveLink.Native/Private/LiveLinkBridge.cpp`

**Current:**
```cpp
void FLiveLinkBridge::EnsureLiveLinkSource()
{
    // Creates FSimioLiveLinkSource (in-process)
    TSharedPtr<ILiveLinkSource> NewSource = 
        MakeShared<FSimioLiveLinkSource>(SourceType, MachineName);
    
    Client->AddSource(NewSource);  // Only works IN Unreal Engine process!
}
```

**Required:**
```cpp
#include "LiveLinkMessageBusSource.h"  // Add this header

void FLiveLinkBridge::EnsureLiveLinkSource()
{
    // Create Message Bus Source (cross-process)
    TSharedPtr<FLiveLinkMessageBusSource> NewSource = 
        FLiveLinkMessageBusSource::CreateSource(SourceType, MachineName);
    
    // This will broadcast over UDP Message Bus to ANY listening UE instance!
    // No direct ILiveLinkClient needed - completely decoupled
}
```

### Module Dependencies

**Update:** `src/Native/UnrealLiveLink.Native/UnrealLiveLinkNative.Build.cs`

```csharp
PrivateDependencyModuleNames.AddRange(new string[]
{
    "Core",
    "CoreUObject",
    "ApplicationCore",
    "LiveLinkInterface",
    "LiveLinkMessageBusFramework",  // ✅ Already added
    "UdpMessaging",                  // ✅ Already added
    "Messaging",                     // ⚠️ May need to add
});
```

**Note:** `LiveLinkMessageBusFramework` and `UdpMessaging` are already in Build.cs, so the dependencies should be available!

---

## Network Configuration

### Current Unreal Engine Default
- **Protocol:** UDP Multicast
- **Address:** 230.0.0.1 (multicast group)
- **Port:** 6666

### Simio Configuration (Currently Ignored)
- **Host:** localhost (Element property)
- **Port:** 11111 (Element property)

### Implementation Options

**Option A: Use UE Defaults (Recommended)**
- Hardcode to 230.0.0.1:6666 in native layer
- Ignore Simio's Host/Port properties (or use for validation only)
- Most compatible with standard UE LiveLink setup

**Option B: Configurable Endpoint**
- Pass Host/Port through P/Invoke to `ULL_Initialize(sourceName, host, port)`
- Create MessageBusSource with custom endpoint
- More flexible but requires testing with UE Message Bus configuration

---

## Testing Strategy

### Phase 1: Verify Message Bus Dependencies
```powershell
# Check if LiveLinkMessageBusSource is available
dumpbin /exports "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\UnrealLiveLink.Native.dll" | Select-String -Pattern "LiveLink"
```

### Phase 2: Add Logging
```cpp
UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Using FLiveLinkMessageBusSource for cross-process communication"));
UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Broadcasting to Message Bus: %s:%d"), *Host, Port);
```

### Phase 3: Monitor UE Output Log
Look for:
```
LogLiveLink: New source 'SimioSimulation' added via Message Bus
```

### Phase 4: Verify in LiveLink Window
- Source "SimioSimulation" appears
- Status shows green
- Subjects appear as Simio entities are created

---

## Development Plan

### Sub-Phase 6.6.1: Message Bus Source Implementation (CRITICAL)
**Priority:** URGENT - Blocking real testing  
**Effort:** 2-4 hours  
**Risk:** Low (dependencies already available, reference implementation exists)

**Tasks:**
1. Replace `FSimioLiveLinkSource` with `FLiveLinkMessageBusSource`
2. Update `EnsureLiveLinkSource()` to create Message Bus source
3. Add network endpoint configuration (use UE defaults or pass through API)
4. Add comprehensive logging for Message Bus operations
5. Update `LiveLinkBridge.h` if new members needed
6. Rebuild native DLL
7. Redeploy with dependencies
8. Test Simio → UE LiveLink connection

**Success Criteria:**
- ✅ "SimioSimulation" source appears in UE LiveLink window
- ✅ Subjects appear as Simio entities are created
- ✅ Transform data streams in real-time
- ✅ No crashes or errors in either Simio or UE

**Reference:**
- UnrealLiveLinkCInterface example project
- Architecture.md: "Message Bus Architecture" section
- UE Documentation: LiveLink Message Bus Source

---

## Impact Assessment

### What's Working
- ✅ Simio managed layer (100% of tests passing)
- ✅ P/Invoke layer (API contract validated)
- ✅ Native DLL lifecycle (Initialize/Shutdown/IsConnected)
- ✅ Subject registration and update logic
- ✅ Coordinate conversion
- ✅ Deployment system with dependencies

### What's NOT Working
- ❌ **Actual network communication (Message Bus)**
- ❌ Cross-process data streaming
- ❌ Visibility in Unreal Engine LiveLink window
- ❌ Real-world end-to-end testing

### Why Tests Passed
Integration tests validate **API correctness**, not **network functionality**. This is actually GOOD test design - it allows us to develop the managed layer independently. But now we need to complete the native implementation.

---

## Next Steps

1. **Immediate:** Implement `FLiveLinkMessageBusSource` integration (Sub-Phase 6.6.1)
2. **Short-term:** Test and validate cross-process communication
3. **Medium-term:** Continue with Sub-Phase 6.7+ as originally planned

**This is the ONLY blocker for real Unreal Engine testing!**

---

## References

- `docs/Architecture.md` - Message Bus architecture specification
- `docs/DevelopmentPlan.md` - Sub-Phase 6.6 completion notes
- `examples/LiveLinkCInterface/` - Reference implementation
- UnrealLiveLinkCInterface GitHub repository
- Unreal Engine LiveLink Documentation

