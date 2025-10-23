# Message Bus Stability Fix - Development Plan

**Purpose:** Address connection errors in successive start/stop/pause/run cycles  
**Root Cause:** Message Bus cleanup timing, idempotent initialization issues, and subject removal order  
**Target Completion:** Phase 6.8 Enhancement  
**Priority:** HIGH - Critical for production stability

**Date Created:** October 23, 2025  
**Last Updated:** October 23, 2025  
**Status:** Proposed (Pending Sub-Phase 6.6.1 Implementation)  
**Integration:** Will be implemented as Sub-Phase 6.6.1 in DevelopmentPlan.md

---

## Executive Summary

The LiveLinkBridge exhibits Message Bus connection errors when users perform rapid start/stop/run cycles in Simio. Analysis reveals four critical issues:

1. **Idempotent initialization** returns early without validating provider state
2. **Shutdown order** destroys provider before removing subjects
3. **No Message Bus cooldown** between shutdown and initialization
4. **No connection recovery** mechanism for lost connections

These fixes will make the system robust to user workflows involving rapid simulation control.

---

## Current Implementation Status

**What's Already Implemented:**
- ✅ GEngineLoop initialization with static flag (prevents double-init crash)
- ✅ DLL-safe shutdown (no AppExit - doesn't terminate Simio)
- ✅ Basic connection health checks (2 out of 4 Steps have IsConnectionHealthy validation)
- ✅ Idempotent Initialize() behavior (returns true if already initialized)

**What Needs Implementation (Sub-Phase 6.6.1):**
- ❌ InternalShutdown() helper method
- ❌ Proper subject removal order (subjects before provider)
- ❌ Message Bus cooldown (500ms after shutdown)
- ❌ LastShutdownTime tracking
- ❌ Auto-recovery in GetConnectionStatus()
- ❌ Health checks in TransmitValuesStep and DestroyObjectStep
- ❌ Error message throttling in all Steps

**Why These Fixes Are Needed:**
Current implementation has GEngineLoop restart optimization (21ms → 1ms) but lacks Message Bus cleanup timing, proper shutdown sequencing, and connection recovery. These gaps cause issues in rapid start/stop scenarios common in Simio workflows.

---

## Problem Analysis

### Issue #1: Idempotent Initialize() - CRITICAL ⚠️

**Location:** `LiveLinkBridge.cpp:58-66`

**Current Implementation:**
The existing code DOES have idempotent behavior that returns early:

```cpp
if (bInitialized)
{
    // Returns early without validating LiveLinkProvider state
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Initialize: Already initialized with provider '%s', returning true (idempotent)"), 
           *ProviderName);
    return true;  // ← Returns early
}
```

**However,** the issue is NOT with this check itself, but with the lack of provider state validation. The fix should not remove idempotent behavior entirely, but rather:
1. Validate provider state even when already initialized
2. Recreate provider if missing/invalid
3. Ensure clean shutdown occurred before allowing reinit

**Problem Scenario:**
```
1. User: START → Initialize() sets bInitialized=true, creates provider
2. User: STOP  → Shutdown() resets provider, sets bInitialized=false
3. User: START → Initialize() should recreate provider
4. BUG: If bInitialized somehow stays true → returns early without provider
```

**Root Cause:** Race condition or missed reset leaves `bInitialized=true` but `LiveLinkProvider=null`

**Impact:** Message Bus connection fails silently, no subjects appear in Unreal

---

### Issue #2: Subject Removal Order - HIGH ⚠️

**Location:** `LiveLinkBridge.cpp:174-194`

**Current Code:**
```cpp
// 1. Reset provider FIRST
if (bLiveLinkSourceCreated && LiveLinkProvider.IsValid())
{
    LiveLinkProvider.Reset();  // ← Destroys Message Bus connection
    bLiveLinkSourceCreated = false;
}

// 2. Clear subjects AFTER (but provider is gone!)
TransformSubjects.Empty();
DataSubjects.Empty();
```

**Problem:** Subjects should be removed FROM the provider BEFORE destroying the provider

**Verification Against Code (LiveLinkBridge.cpp:159-205):**
✅ Confirmed: Current Shutdown() method does NOT have:
- LastShutdownTime tracking
- Subject removal from provider before provider destruction
- InternalShutdown() helper

**Impact:** This confirms Fix #1 and Fix #2 are both needed.
- Message Bus may hold stale subject references
- Potential memory leaks in LiveLink subsystem
- Subjects may persist in Unreal after shutdown

---

### Issue #3: No Message Bus Cooldown - HIGH ⚠️

**Current Behavior:**
```
User: STOP  → Shutdown() destroys provider via Message Bus (UDP multicast)
       ↓ (0ms delay)
User: START → Initialize() creates new provider via Message Bus
              ↑ Provider broadcasts on 230.0.0.1:6666
              ↑ BUT old provider may not have finished unregistering
```

**Problem:** UDP multicast takes time to propagate. Rapid restart causes:
- Duplicate provider registrations
- Port conflicts (multiple providers on same multicast address)
- Connection instability

**Typical UDP multicast cleanup time:** 200-500ms

**Impact:** Message Bus errors, duplicate sources in Unreal LiveLink window

---

### Issue #4: No Connection Recovery - MEDIUM ⚠️

**Current Behavior:**
- If Message Bus connection drops during operation
- `bLiveLinkSourceCreated=true` and `LiveLinkProvider.IsValid()=true`
- But underlying UDP socket is dead
- No automatic reconnection

**Scenarios:**
- User pauses simulation for extended period (Message Bus timeout)
- Network interruption
- Unreal Editor restart

**Impact:** Updates fail silently, subjects stop moving in Unreal

---

## Proposed Fixes

### Fix #1: Remove Idempotent Behavior - Always Ensure Clean State

**Priority:** CRITICAL  
**Effort:** 2 hours  
**Risk:** Low (improves reliability)

**Strategy:** Always perform internal shutdown before initialization to guarantee clean state.

**Implementation:**

```cpp
bool FLiveLinkBridge::Initialize(const FString& InProviderName)
{
    FScopeLock Lock(&CriticalSection);
    
    // ✅ ALWAYS shutdown first if initialized (ensures clean state)
    if (bInitialized)
    {
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("Initialize: Already initialized, performing internal shutdown first"));
        
        // Internal shutdown (without lock - we already have it)
        InternalShutdown();
    }
    
    // GEngineLoop initialization (unchanged)
    if (!bGEngineLoopInitialized)
    {
        // ... existing GEngineLoop initialization code ...
        bGEngineLoopInitialized = true;
    }
    
    // Store provider name and mark as initialized
    ProviderName = InProviderName;
    bInitialized = true;
    bLiveLinkReady = true;
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Initialize: Ready for LiveLink integration with provider '%s'"), 
           *ProviderName);
    
    // Create LiveLink provider
    EnsureLiveLinkSource();
    
    return bLiveLinkSourceCreated;
}
```

**Add Helper Method:**

```cpp
// In LiveLinkBridge.h (private section)
void InternalShutdown();

// In LiveLinkBridge.cpp
void FLiveLinkBridge::InternalShutdown()
{
    // Note: Caller must hold CriticalSection lock
    
    if (!bInitialized)
    {
        return;
    }
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("InternalShutdown: Cleaning up %d transform subjects, %d data subjects"), 
           TransformSubjects.Num(), 
           DataSubjects.Num());
    
    // 1. Remove all subjects from provider FIRST (before destroying provider)
    if (bLiveLinkSourceCreated && LiveLinkProvider.IsValid())
    {
        // Remove transform subjects
        for (const auto& SubjectPair : TransformSubjects)
        {
            LiveLinkProvider->RemoveSubject(SubjectPair.Key);
            UE_LOG(LogUnrealLiveLinkNative, Verbose, 
                   TEXT("InternalShutdown: Removed transform subject '%s'"), 
                   *SubjectPair.Key.ToString());
        }
        
        // Remove data subjects
        for (const auto& SubjectPair : DataSubjects)
        {
            LiveLinkProvider->RemoveSubject(SubjectPair.Key);
            UE_LOG(LogUnrealLiveLinkNative, Verbose, 
                   TEXT("InternalShutdown: Removed data subject '%s'"), 
                   *SubjectPair.Key.ToString());
        }
    }
    
    // 2. Clear subject maps
    TransformSubjects.Empty();
    DataSubjects.Empty();
    NameCache.Empty();
    
    // 3. Reset provider LAST (after subjects removed)
    if (bLiveLinkSourceCreated && LiveLinkProvider.IsValid())
    {
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("InternalShutdown: Destroying LiveLink Message Bus Provider '%s'"), 
               *ProviderName);
        
        LiveLinkProvider.Reset();
        bLiveLinkSourceCreated = false;
        
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("InternalShutdown: ✅ Provider destroyed"));
    }
    
    ProviderName.Empty();
    bInitialized = false;
    bLiveLinkReady = false;
    
    // Record shutdown time for cooldown calculation
    LastShutdownTime = FPlatformTime::Seconds();
}
```

**Update Shutdown():**

```cpp
void FLiveLinkBridge::Shutdown()
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized)
    {
        UE_LOG(LogUnrealLiveLinkNative, Warning, 
               TEXT("Shutdown: Not initialized, nothing to do"));
        return;
    }
    
    UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("Shutdown: Beginning shutdown sequence"));
    
    InternalShutdown();
    
    UE_LOG(LogUnrealLiveLinkNative, Log, 
           TEXT("Shutdown: Complete (resources released, modules remain loaded)"));
}
```

**Testing:**
- Run simulation 20 times in succession (rapid START/STOP cycles)
- Verify no "already initialized" early returns
- Verify provider recreated each time
- Check for memory leaks (subjects properly cleaned)

---

### Fix #2: Add Message Bus Cooldown Period

**Priority:** HIGH  
**Effort:** 1 hour  
**Risk:** Low (simple timing addition)

**Strategy:** Wait 500ms after shutdown before allowing reinitialization to give UDP multicast time to propagate.

**Implementation:**

**Add to LiveLinkBridge.h:**
```cpp
// In private section
static double LastShutdownTime;  // Track last shutdown for cooldown
static constexpr double MESSAGE_BUS_COOLDOWN_SECONDS = 0.5;
```

**Add to LiveLinkBridge.cpp:**
```cpp
// Static member initialization
double FLiveLinkBridge::LastShutdownTime = 0.0;
```

**Update Initialize():**
```cpp
bool FLiveLinkBridge::Initialize(const FString& InProviderName)
{
    FScopeLock Lock(&CriticalSection);
    
    // ✅ Message Bus cooldown - wait for UDP multicast cleanup
    double TimeSinceLastShutdown = FPlatformTime::Seconds() - LastShutdownTime;
    if (LastShutdownTime > 0.0 && TimeSinceLastShutdown < MESSAGE_BUS_COOLDOWN_SECONDS)
    {
        double WaitTime = MESSAGE_BUS_COOLDOWN_SECONDS - TimeSinceLastShutdown;
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("Initialize: Waiting %.3f seconds for Message Bus cooldown..."), 
               WaitTime);
        
        // Release lock during sleep to prevent deadlock
        CriticalSection.Unlock();
        FPlatformProcess::Sleep(WaitTime);
        CriticalSection.Lock();
        
        UE_LOG(LogUnrealLiveLinkNative, Log, 
               TEXT("Initialize: ✅ Message Bus cooldown complete"));
    }
    
    // ... rest of initialization (unchanged)
}
```

**Update InternalShutdown():**
```cpp
void FLiveLinkBridge::InternalShutdown()
{
    // ... existing shutdown code ...
    
    // Record shutdown time for cooldown calculation
    LastShutdownTime = FPlatformTime::Seconds();
    
    UE_LOG(LogUnrealLiveLinkNative, Verbose, 
           TEXT("InternalShutdown: Cooldown timer started (%.3fs required before next init)"), 
           MESSAGE_BUS_COOLDOWN_SECONDS);
}
```

**Testing:**
- Measure time between STOP and START (should add 500ms delay)
- Rapid START/STOP/START cycles should not cause duplicate providers
- Check Unreal LiveLink window - should see clean source removal/addition
- No port conflicts or connection errors

**User Impact:**
- Slight delay (0-500ms) on simulation restart
- Only applies if user clicks START within 500ms of STOP
- Improves stability significantly

---

### Fix #3: Enhanced Connection Health Check with Auto-Recovery

**Priority:** MEDIUM  
**Effort:** 3 hours  
**Risk:** Medium (requires careful state management)

**Strategy:** Add health check validation and attempt recovery when connection lost.

**Implementation:**

**Update GetConnectionStatus():**
```cpp
int FLiveLinkBridge::GetConnectionStatus() const
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized)
    {
        return ULL_NOT_INITIALIZED;
    }
    
    // ✅ Validate provider exists and is valid
    if (!bLiveLinkSourceCreated || !LiveLinkProvider.IsValid())
    {
        UE_LOG(LogUnrealLiveLinkNative, Warning, 
               TEXT("GetConnectionStatus: Provider missing (created=%d, valid=%d)"), 
               bLiveLinkSourceCreated, 
               LiveLinkProvider.IsValid());
        
        // Attempt recovery if we should have a provider
        if (bInitialized)
        {
            UE_LOG(LogUnrealLiveLinkNative, Log, 
                   TEXT("GetConnectionStatus: Attempting provider recovery..."));
            
            const_cast<FLiveLinkBridge*>(this)->EnsureLiveLinkSource();
            
            if (bLiveLinkSourceCreated && LiveLinkProvider.IsValid())
            {
                UE_LOG(LogUnrealLiveLinkNative, Log, 
                       TEXT("GetConnectionStatus: ✅ Provider recovered successfully"));
                return ULL_OK;
            }
            else
            {
                UE_LOG(LogUnrealLiveLinkNative, Error, 
                       TEXT("GetConnectionStatus: ❌ Provider recovery failed"));
                return ULL_NOT_CONNECTED;
            }
        }
        
        return ULL_NOT_CONNECTED;
    }
    
    // Provider exists and is valid
    return ULL_OK;
}
```

**Add Periodic Health Check to Update Methods:**
```cpp
void FLiveLinkBridge::UpdateTransformSubject(
    const FName& SubjectName,
    const FTransform& Transform)
{
    FScopeLock Lock(&CriticalSection);
    
    if (!bInitialized)
    {
        static int32 NotInitializedCount = 0;
        if (++NotInitializedCount % 60 == 1)
        {
            UE_LOG(LogUnrealLiveLinkNative, Warning, 
                   TEXT("UpdateTransformSubject: Not initialized (count: %d)"), 
                   NotInitializedCount);
        }
        return;
    }
    
    // ✅ Check LiveLink source health before update
    if (!bLiveLinkSourceCreated)
    {
        static int32 NoSourceCount = 0;
        if (++NoSourceCount % 60 == 1)
        {
            UE_LOG(LogUnrealLiveLinkNative, Warning, 
                   TEXT("UpdateTransformSubject: LiveLink source not available, attempting recovery (count: %d)"), 
                   NoSourceCount);
            
            EnsureLiveLinkSource();
        }
        return;
    }
    
    // ✅ Validate provider before use
    if (!LiveLinkProvider.IsValid())
    {
        UE_LOG(LogUnrealLiveLinkNative, Error, 
               TEXT("UpdateTransformSubject: Provider invalid, cannot update '%s'"), 
               *SubjectName.ToString());
        bLiveLinkSourceCreated = false;  // Mark as needing recovery
        return;
    }
    
    // ... rest of update logic (unchanged)
}
```

**Testing:**
- Simulate connection loss (close Unreal Editor during simulation)
- Verify auto-recovery when Unreal reopened
- Test pause for 60+ seconds (potential timeout scenario)
- Monitor logs for recovery attempts

---

### Fix #4: Managed Layer Connection Validation

**Priority:** MEDIUM  
**Effort:** 2 hours  
**Risk:** Low (defensive programming)

**Strategy:** Add connection health checks in Steps before operations.

**Current Status (Verified Against Code):**

**Already Implemented:**
- ✅ CreateObjectStep.cs (line 35): Has IsConnectionHealthy check
- ✅ SetObjectPositionOrientationStep.cs (line 37): Has IsConnectionHealthy check

**Missing Implementation:**
- ❌ TransmitValuesStep.cs: No health check found
- ❌ DestroyObjectStep.cs: No health check found
- ❌ No error message throttling in any Step
- ❌ Error messages currently report "not in a healthy state" (not very actionable)

**Improvement Needed:**
Even Steps with health checks need:
1. More actionable error messages: "LiveLink connection lost. Stop and restart the simulation."
2. Error throttling: `_lastHealthCheckFailTime` field to prevent spam
3. Recovery detection: Stop showing errors when connection restored

**Implementation:**

**Update SetPositionOrientationStep.cs:**
```csharp
public ExitType Execute(IStepExecutionContext context)
{
    // Get connector element reference
    var connectorElement = ((IElementProperty)_readers.GetProperty("UnrealEngineConnector"))
        .GetElement(context) as SimioUnrealEngineLiveLinkElement;

    if (connectorElement == null)
    {
        context.ExecutionInformation.ReportError(
            "SetPositionOrientation: Failed to resolve 'Unreal Engine Connector' element reference");
        return ExitType.FirstExit;
    }

    // ✅ NEW: Check connection health before operation
    if (!connectorElement.IsConnectionHealthy)
    {
        // Throttle error messages (once per second)
        if (!_lastHealthCheckFailTime.HasValue || 
            (DateTime.Now - _lastHealthCheckFailTime.Value).TotalSeconds >= 1.0)
        {
            context.ExecutionInformation.ReportError(
                "SetPositionOrientation: LiveLink connection lost. Please stop and restart the simulation.");
            _lastHealthCheckFailTime = DateTime.Now;
        }
        return ExitType.FirstExit;
    }

    // Get object name
    string objectName = GetStringProperty("ObjectName", context);
    if (string.IsNullOrWhiteSpace(objectName))
    {
        context.ExecutionInformation.ReportError(
            "SetPositionOrientation: Object name is required");
        return ExitType.FirstExit;
    }

    // ... rest of step logic (unchanged)
}

// Add field for throttling
private DateTime? _lastHealthCheckFailTime = null;
```

**Apply same pattern to:**
- CreateObjectStep.cs
- TransmitValuesStep.cs
- DestroyObjectStep.cs

**Testing:**
- Stop Unreal during simulation → verify error message appears
- Restart Unreal → verify error message stops (connection recovered)
- Error messages should be throttled (not spam trace window)

---

## Implementation Plan

### Phase 1: Core Stability (Week 1)

**Day 1-2: Fix #1 - InternalShutdown Refactor**
- [ ] Add `InternalShutdown()` helper method
- [ ] Update `Initialize()` to call `InternalShutdown()` if already initialized
- [ ] Update `Shutdown()` to call `InternalShutdown()`
- [ ] Fix subject removal order (subjects before provider)
- [ ] Add unit tests for multiple init/shutdown cycles
- [ ] Manual test: 20 consecutive simulation runs

**Day 3: Fix #2 - Message Bus Cooldown**
- [ ] Add `LastShutdownTime` static member
- [ ] Add cooldown check in `Initialize()`
- [ ] Add cooldown timer update in `InternalShutdown()`
- [ ] Test rapid START/STOP cycles (< 500ms apart)
- [ ] Verify no duplicate providers in Unreal LiveLink window

**Day 4-5: Testing & Validation**
- [ ] Integration tests with mock DLL
- [ ] E2E tests with real Simio + Unreal
- [ ] Performance testing (verify cooldown doesn't impact normal use)
- [ ] Stress test: 100 consecutive simulation runs
- [ ] Document timing characteristics

### Phase 2: Enhanced Reliability (Week 2)

**Day 1-2: Fix #3 - Connection Health & Recovery**
- [ ] Update `GetConnectionStatus()` with provider validation
- [ ] Add auto-recovery logic in `GetConnectionStatus()`
- [ ] Add health checks in `UpdateTransformSubject()`
- [ ] Add health checks in `UpdateTransformSubjectWithProperties()`
- [ ] Test connection loss scenarios (close/reopen Unreal)

**Day 3: Fix #4 - Managed Layer Validation**
- [ ] Add health checks to all 4 Steps
- [ ] Add error message throttling
- [ ] Test error reporting during connection loss
- [ ] Verify error messages stop after recovery

**Day 4-5: Testing & Documentation**
- [ ] Full regression test suite
- [ ] Update Architecture.md with new patterns
- [ ] Update NativeLayerDevelopment.md
- [ ] Create troubleshooting guide

---

## Testing Strategy

### Unit Tests (Mock DLL)

### Automated Tests (Can Run in CI/CD)
- ✅ Multiple Initialize/Shutdown cycles (20+ iterations)
- ✅ Connection health status transitions
- ✅ Subject cleanup verification (count tracking)
- ✅ Message Bus cooldown timing (stopwatch validation)

### Manual Tests (Require Unreal Editor)
- ⚠️ Provider recovery after Unreal restart
- ⚠️ Visual verification of subjects in LiveLink window
- ⚠️ Duplicate provider detection
- ⚠️ 50 consecutive simulation runs (E2E stability)

### Mock vs Real Testing
- **Mock DLL:** Can validate API contract, call sequences, parameter validation
- **Real DLL:** Required for actual Message Bus behavior, cooldown timing, provider lifecycle

**Test: Multiple Initialize/Shutdown Cycles**
```csharp
[TestMethod]
public void NativeLayer_RapidRestartCycles_ShouldNotCrash()
{
    for (int i = 0; i < 20; i++)
    {
        int result = UnrealLiveLinkNative.ULL_Initialize("RestartTest");
        Assert.AreEqual(UnrealLiveLinkNative.ULL_OK, result);
        
        // Use LiveLink briefly
        UnrealLiveLinkNative.ULL_RegisterObject("TestObject");
        var transform = new ULL_Transform { /* ... */ };
        UnrealLiveLinkNative.ULL_UpdateObject("TestObject", ref transform);
        
        UnrealLiveLinkNative.ULL_Shutdown();
        
        // No delay - test worst case
    }
}
```

**Test: Connection Health After Shutdown**
```csharp
[TestMethod]
public void NativeLayer_ConnectionHealthAfterShutdown_ShouldBeNotInitialized()
{
    UnrealLiveLinkNative.ULL_Initialize("HealthTest");
    Assert.AreEqual(UnrealLiveLinkNative.ULL_OK, 
                    UnrealLiveLinkNative.ULL_IsConnected());
    
    UnrealLiveLinkNative.ULL_Shutdown();
    Assert.AreEqual(UnrealLiveLinkNative.ULL_NOT_INITIALIZED, 
                    UnrealLiveLinkNative.ULL_IsConnected());
}
```

**Test: Subject Cleanup on Shutdown**
```csharp
[TestMethod]
public void NativeLayer_SubjectCleanup_ShouldRemoveAllSubjects()
{
    UnrealLiveLinkNative.ULL_Initialize("CleanupTest");
    
    // Register multiple subjects
    for (int i = 0; i < 10; i++)
    {
        UnrealLiveLinkNative.ULL_RegisterObject($"Object_{i}");
    }
    
    UnrealLiveLinkNative.ULL_Shutdown();
    
    // Reinitialize - should start clean
    UnrealLiveLinkNative.ULL_Initialize("CleanupTest");
    
    // Verify subjects don't persist
    // (Would need mock DLL enhancement to track subject count)
}
```

### Integration Tests (Real Native DLL)

**Test: Message Bus Cooldown Timing**
```csharp
[TestMethod]
public void RealNative_MessageBusCooldown_ShouldEnforce500ms()
{
    UnrealLiveLinkNative.ULL_Initialize("CooldownTest");
    UnrealLiveLinkNative.ULL_Shutdown();
    
    var stopwatch = Stopwatch.StartNew();
    UnrealLiveLinkNative.ULL_Initialize("CooldownTest");
    stopwatch.Stop();
    
    // Should have at least 500ms delay (cooldown period)
    Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 450, 
                  $"Expected >= 450ms cooldown, got {stopwatch.ElapsedMilliseconds}ms");
    Assert.IsTrue(stopwatch.ElapsedMilliseconds <= 600, 
                  $"Expected <= 600ms total, got {stopwatch.ElapsedMilliseconds}ms");
}
```

**Test: Provider Recovery After Loss**
```
Manual test (cannot automate):
1. Start Simio simulation
2. Verify objects appear in Unreal
3. Close Unreal Editor (connection lost)
4. Verify GetConnectionStatus returns NOT_CONNECTED
5. Reopen Unreal Editor
6. Verify GetConnectionStatus returns OK (auto-recovery)
7. Verify objects continue updating
```

### E2E Tests (Simio + Unreal)

**Test: 50 Consecutive Simulation Runs**
```
Manual test:
1. Open Simio model with LiveLink connector
2. Run simulation 50 times (START → wait 5 sec → STOP)
3. Verify no errors in Simio trace
4. Verify no crashes in Unreal
5. Check Unreal LiveLink window - should see clean source removal each time
6. Monitor memory usage - should be stable (no leaks)
```

**Test: Rapid Start/Stop Stress Test**
```
Manual test:
1. START simulation
2. Immediately STOP (within 100ms)
3. Immediately START again
4. Repeat 20 times rapidly
5. Verify cooldown prevents connection conflicts
6. Verify no duplicate providers in Unreal
```

---

## Validation Criteria

**Fix #1 Success Metrics:**
- ✅ 20 consecutive runs without errors
- ✅ No "already initialized" early returns in logs
- ✅ Clean subject removal on each shutdown
- ✅ No memory leaks (stable memory over 50 runs)

**Fix #2 Success Metrics:**
- ✅ Rapid START/STOP cycles add 0-500ms delay
- ✅ No duplicate providers in Unreal LiveLink window
- ✅ No port conflicts in logs
- ✅ Message Bus cleanup completes before reinit

**Fix #3 Success Metrics:**
- ✅ Connection loss detected via GetConnectionStatus()
- ✅ Auto-recovery succeeds when Unreal reopened
- ✅ Updates resume after recovery
- ✅ No silent failures

**Fix #4 Success Metrics:**
- ✅ Clear error messages when connection lost
- ✅ Error messages throttled (1 per second max)
- ✅ Errors stop after connection recovered
- ✅ User knows to restart simulation

---

## Risk Assessment

### Low Risk Changes
- Fix #1 (InternalShutdown refactor) - Improves existing logic
- Fix #2 (Cooldown) - Simple timing addition
- Fix #4 (Managed validation) - Defensive checks

### Medium Risk Changes
- Fix #3 (Auto-recovery) - Modifies connection state management
  - **Mitigation:** Extensive testing with connection loss scenarios
  - **Fallback:** Can disable auto-recovery if issues arise

### Deployment Risk
- **Risk:** Cooldown delay may surprise users
  - **Probability:** Medium (if restart within 500ms)
  - **Impact:** Low (0-500ms delay)
  - **Mitigation:** Document expected behavior, consider making configurable
  - **User Communication:** Add tooltip to Element property explaining auto-discovery delay

### Known Limitations
- **Cannot detect pause:** Simio API doesn't expose pause callbacks
  - **Impact:** Connection may timeout during long pause (> 60 sec)
  - **Mitigation:** User must restart simulation after long pause
  - **Future:** Consider keepalive heartbeat if pause becomes issue

---

## Rollback Plan

If issues arise after deployment:

**Option 1: Disable Specific Fix**
- Each fix is independent
- Can disable cooldown by setting `MESSAGE_BUS_COOLDOWN_SECONDS = 0.0`
- Can disable auto-recovery by commenting out recovery code in GetConnectionStatus()

**Option 2: Revert to Previous Version**
- Keep previous LiveLinkBridge.cpp as `.cpp.bak`
- Rebuild native DLL from backup
- Managed layer unchanged (compatible with both versions)

**Option 3: Emergency Hotfix**
- If critical bug found, can release hotfix with single fix disabled
- Git branch strategy: each fix on separate branch for easy cherry-pick

---

## Documentation Updates

**Files to Update:**

1. **Architecture.md**
   - Add section on "Connection Stability Patterns"
   - Document Message Bus cooldown behavior
   - Update ADRs with new decisions

2. **NativeLayerDevelopment.md**
   - Add "Connection Recovery" section
   - Document InternalShutdown() pattern
   - Update troubleshooting guide

3. **ManagedLayerDevelopment.md**
   - Add connection health check pattern
   - Update Step implementation examples

4. **New: ConnectionStabilityGuide.md**
   - Troubleshooting rapid restart issues
   - Understanding Message Bus cooldown
   - User guidance for connection loss scenarios

---

## Success Metrics

**Stability Improvements:**
- Zero connection errors in 100 consecutive simulation runs
  - **Measured by:** Integration test suite, E2E manual test
- Clean startup/shutdown in < 1 second (including cooldown)
  - **Measured by:** Stopwatch in unit tests
- Automatic recovery from connection loss
  - **Measured by:** Manual test with Unreal Editor restart

**User Experience:**
- No manual intervention needed for rapid restarts
- Clear error messages when connection lost
- Simulation continues even if visualization fails

**Code Quality:**
- 100% test coverage for new code paths
  - **Measured by:** Code coverage tools (dotCover, OpenCover)
- Zero compiler warnings
  - **Measured by:** Build output analysis
- Comprehensive logging for debugging
  - **Measured by:** Log review with DebugView++

---

## Timeline

**Week 1 (Core Stability):**
- Day 1-2: Implement Fix #1 (InternalShutdown)
- Day 3: Implement Fix #2 (Cooldown)
- Day 4-5: Testing & validation

**Week 2 (Enhanced Reliability):**
- Day 1-2: Implement Fix #3 (Health check & recovery)
- Day 3: Implement Fix #4 (Managed validation)
- Day 4-5: Full regression testing & documentation

**Total Effort:** 10 days (2 weeks)

---

## Next Steps

1. **Review this plan** with team/stakeholders
2. **Create feature branch:** `feature/message-bus-stability-fix`
3. **Begin Phase 1, Day 1:** InternalShutdown refactor
4. **Daily standup:** Report progress and blockers
5. **Code review:** After each fix completion
6. **Deploy to test environment:** After Phase 1 complete
7. **Production deployment:** After Phase 2 validation

---

## Questions for Discussion

1. **Cooldown duration:** Is 500ms acceptable for user experience?
   - Alternative: Make configurable via Element property
   
2. **Auto-recovery aggressiveness:** Should we retry connection automatically?
   - Current: Single retry on GetConnectionStatus()
   - Alternative: Periodic background retry thread
   
3. **Error handling:** Should connection loss stop simulation?
   - Current: Simulation continues, visualization fails
   - Alternative: Make configurable (fail vs. continue)

4. **Logging verbosity:** Is current logging sufficient?
   - Consider: Add performance metrics (connection time, update latency)

---

## Related Issues

- **Issue #12:** "Duplicate providers in LiveLink window" → Fixed by cooldown
- **Issue #15:** "Objects stop moving after pause" → Partially addressed by health check
- **Issue #18:** "Crash on rapid restart" → Fixed by InternalShutdown

---

## Integration with Development Plan

This stability fix will be implemented as **Sub-Phase 6.6.1** in the main DevelopmentPlan.md.

**Sequencing:**
- Sub-Phase 6.6 (COMPLETE): GEngineLoop initialization, transform subjects
- **Sub-Phase 6.6.1 (THIS FIX)**: Message Bus stability enhancements
- Sub-Phase 6.7 (NEXT): Additional property support
- Sub-Phase 6.8: Data subjects

**Rationale for Sequencing:**
Stability fixes should be implemented BEFORE adding more features (properties, data subjects). This ensures a solid foundation for subsequent development and prevents compounding issues.

**Impact on Timeline:**
- Adds 2 weeks to Phase 6
- Delays Sub-Phase 6.7 by 2 weeks
- **Worth the delay:** Production stability is critical for user adoption

---

## Appendix A: Code Review Checklist

Before marking each fix complete:

**General:**
- [ ] Code compiles without warnings
- [ ] All new code has logging
- [ ] Thread safety verified (FScopeLock usage)
- [ ] No memory leaks (valgrind/sanitizer clean)

**Fix #1 Specific:**
- [ ] InternalShutdown() always called when needed
- [ ] Subjects removed before provider destroyed
- [ ] State fully reset between cycles
- [ ] No static state persists incorrectly

**Fix #2 Specific:**
- [ ] Cooldown timer accurate (measured)
- [ ] Lock released during sleep (no deadlock)
- [ ] Cooldown only applies when needed
- [ ] Configurable duration (if decided)

**Fix #3 Specific:**
- [ ] Recovery logic doesn't infinite loop
- [ ] Recovery failure handled gracefully
- [ ] Const correctness (const_cast documented)
- [ ] Recovery logged adequately

**Fix #4 Specific:**
- [ ] Error throttling works correctly
- [ ] All 4 Steps updated consistently
- [ ] Error messages actionable for user
- [ ] No performance impact from checks

---

## Appendix B: Performance Impact Analysis

**Current Performance:**
- First init: 21ms (GEngineLoop) + 5ms (managed) = 26ms
- Subsequent init: 1ms (native) + 2ms (managed) = 3ms

**After Fixes:**
- First init: 21ms + 5ms = 26ms (unchanged)
- Subsequent init (no cooldown): 1ms + 2ms = 3ms (unchanged)
- Subsequent init (with cooldown): 500ms + 1ms + 2ms = 503ms

**Analysis:**
- Cooldown only applies if restart within 500ms of shutdown
- Typical user workflow: Wait 5-10 seconds between runs
- **Impact:** Minimal - cooldown rarely triggered in normal use
- **Benefit:** Prevents connection corruption worth the delay

**Optimization Options (if needed):**
- Reduce cooldown to 200ms (minimum UDP multicast propagation)
- Make cooldown adaptive (longer after errors, shorter after success)
- Skip cooldown if Message Bus confirms cleanup complete

---

## Appendix C: Alternative Approaches Considered

### Alternative 1: Keep Provider Alive Across Shutdowns
**Idea:** Don't destroy provider on shutdown, reuse for next run

**Pros:**
- No cooldown needed
- Faster restart

**Cons:**
- Subjects persist in Unreal (confusing)
- Provider name can't change between runs
- Harder to reset clean state
- **Rejected:** Clean slate approach more robust

### Alternative 2: Use LiveLink Connection Events
**Idea:** Subscribe to LiveLink connection events for status

**Pros:**
- Accurate connection status
- Event-driven recovery

**Cons:**
- LiveLink API may not expose these events in Message Bus mode
- Adds complexity
- **Rejected:** Current health check sufficient

### Alternative 3: Background Keepalive Thread
**Idea:** Send periodic heartbeat to keep connection alive

**Pros:**
- Prevents timeout during pause
- Proactive connection maintenance

**Cons:**
- Threading complexity
- Overhead during idle time
- **Deferred:** Consider if pause timeout becomes issue

---

## End of Development Plan

**Document Version:** 1.0  
**Last Updated:** October 23, 2025  
**Author:** Development Team  
**Status:** Ready for Implementation