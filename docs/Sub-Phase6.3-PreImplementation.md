# Sub-Phase 6.3 Pre-Implementation Analysis

**Date:** October 17, 2025  
**Status:** ✅ READY TO IMPLEMENT  
**Confidence Level:** HIGH

---

## Understanding Confirmation

### ✅ **Objective is Clear**

Export all 12 C API functions with stub implementations (logging only, no LiveLink integration yet).

**What this means:**
- Create function declarations in header file
- Implement function bodies that only:
  - Log the call with UE_LOG
  - Validate parameters (null checks)
  - Return appropriate codes
  - Do NOT actually connect to LiveLink (that's Sub-Phase 6.5+)

---

## ✅ **Critical Information Gathered**

### 1. **12 Functions to Implement - SIGNATURES CONFIRMED**

Source: `src/Native/Mock/MockLiveLink.h` (lines 1-200)

**Lifecycle (4 functions):**
```cpp
int ULL_Initialize(const char* providerName);
void ULL_Shutdown();
int ULL_GetVersion();
int ULL_IsConnected();
```

**Transform Subjects (5 functions):**
```cpp
void ULL_RegisterObject(const char* subjectName);
void ULL_RegisterObjectWithProperties(const char* subjectName, const char** propertyNames, int propertyCount);
void ULL_UpdateObject(const char* subjectName, const ULL_Transform* transform);
void ULL_UpdateObjectWithProperties(const char* subjectName, const ULL_Transform* transform, const float* propertyValues, int propertyCount);
void ULL_RemoveObject(const char* subjectName);
```

**Data Subjects (3 functions):**
```cpp
void ULL_RegisterDataSubject(const char* subjectName, const char** propertyNames, int propertyCount);
void ULL_UpdateDataSubject(const char* subjectName, const char** propertyNames, const float* propertyValues, int propertyCount);
void ULL_RemoveDataSubject(const char* subjectName);
```

**✅ VERIFIED** against `src/Managed/UnrealIntegration/UnrealLiveLinkNative.cs` - P/Invoke declarations match exactly.

---

### 2. **DLL vs EXE Configuration - SOLUTION FOUND** 🎯

**Current State:**
- Target.cs has: `Type = TargetType.Program;` and `LinkType = TargetLinkType.Monolithic;`
- This produces: `UnrealLiveLinkNative.exe` (25MB)
- Need: `UnrealLiveLink.Native.dll` for P/Invoke

**Solution (from reference project):**
Add to `UnrealLiveLinkNative.Target.cs`:
```csharp
bShouldCompileAsDLL = true;
```

**Reference Evidence:**
- File: `examples/LiveLinkCInterface/UnrealLiveLinkCInterface.Target.cs.md`
- Line 11: `bShouldCompileAsDLL = true;`
- This is THE key setting that changes output from .exe to .dll

**Configuration to Apply:**
```csharp
public UnrealLiveLinkNativeTarget(TargetInfo Target) : base(Target)
{
    Type = TargetType.Program;
    bShouldCompileAsDLL = true;        // ← ADD THIS LINE
    LinkType = TargetLinkType.Monolithic;
    LaunchModuleName = "UnrealLiveLinkNative";
    
    // ... rest of configuration
}
```

---

### 3. **Export Decoration - ALREADY CONFIGURED** ✅

**Current Build.cs has:**
```csharp
PublicDefinitions.Add("ULL_API=__declspec(dllexport)");
```

**Usage Pattern:**
```cpp
// In header:
extern "C" {
    __declspec(dllexport) int ULL_Initialize(const char* providerName);
    // ... or with macro:
    ULL_API int ULL_Initialize(const char* providerName);
}
```

**Decision:** Use explicit `__declspec(dllexport)` for clarity (matches mock pattern).

---

### 4. **File Locations - CONFIRMED**

**Files to Create:**
1. ✅ `src/Native/UnrealLiveLink.Native/Public/UnrealLiveLinkAPI.h` (declarations)
2. ✅ `src/Native/UnrealLiveLink.Native/Private/UnrealLiveLinkAPI.cpp` (implementations)

**Files to Modify:**
1. ✅ `src/Native/UnrealLiveLink.Native/UnrealLiveLinkNative.Target.cs` (add bShouldCompileAsDLL)

**Existing Infrastructure:**
- ✅ Types.h already exists with ULL_Transform and return codes
- ✅ Build.cs already has ULL_API export definition
- ✅ UnrealLiveLink.Native.h exists (can include for UE_LOG)

---

### 5. **Stub Implementation Pattern - CLEAR**

**Example from Mock (simplified for stubs):**
```cpp
int ULL_Initialize(const char* providerName) {
    // 1. Validate parameters
    if (!providerName || providerName[0] == '\0') {
        UE_LOG(LogUnrealLiveLinkNative, Error, TEXT("ULL_Initialize: providerName is null or empty"));
        return ULL_ERROR;
    }
    
    // 2. Log the call
    UE_LOG(LogUnrealLiveLinkNative, Log, TEXT("ULL_Initialize called with providerName: %s"), 
           UTF8_TO_TCHAR(providerName));
    
    // 3. Stub behavior (no actual LiveLink yet)
    // TODO: Sub-Phase 6.5 will add actual FLiveLinkMessageBusSource creation
    
    // 4. Return success
    return ULL_OK;
}
```

**Key Points:**
- ✅ Use UE_LOG for debugging (already have LogUnrealLiveLinkNative declared)
- ✅ UTF8_TO_TCHAR() for string conversion
- ✅ Return ULL_OK, ULL_ERROR, etc. (from Types.h)
- ✅ NO actual LiveLink code yet (stubs only)

---

### 6. **Return Code Strategy - CONFIRMED**

**From Types.h:**
```cpp
#define ULL_OK                  0
#define ULL_ERROR              -1
#define ULL_NOT_CONNECTED      -2
#define ULL_NOT_INITIALIZED    -3
```

**Stub Behavior:**
- `ULL_Initialize()` → return `ULL_OK` (simulate success)
- `ULL_GetVersion()` → return `1` (API version)
- `ULL_IsConnected()` → return `ULL_NOT_CONNECTED` (not implemented yet)
- `ULL_Shutdown()` → void (just log)
- All others → void (just log and validate)

---

### 7. **Testing Strategy - DEFINED**

**Verification Steps:**
1. ✅ Build produces `.dll` not `.exe`
2. ✅ Use `dumpbin /EXPORTS UnrealLiveLink.Native.dll` to verify 12 exports
3. ✅ Function names match P/Invoke expectations exactly
4. ✅ Copy DLL to Simio UserExtensions folder
5. ✅ Run managed layer test that calls Initialize/GetVersion/Shutdown
6. ✅ Check UE log file for debug messages

**Expected Results:**
- DLL loads without DllNotFoundException
- Functions callable from C#
- Log messages appear in UE log
- Returns expected codes (ULL_OK = 0, version = 1)

---

## ✅ **Dependencies Available**

### Already Implemented:
- ✅ ULL_Transform struct (Types.h)
- ✅ Return code constants (Types.h)
- ✅ LogUnrealLiveLinkNative declared (UnrealLiveLink.Native.h)
- ✅ Build system working (BuildNative.ps1)
- ✅ UE 5.6 environment set up

### Not Needed Yet:
- ❌ LiveLink modules (Sub-Phase 6.5)
- ❌ LiveLinkBridge class (Sub-Phase 6.4)
- ❌ Actual LiveLink functionality (Sub-Phase 6.5+)

---

## ✅ **Blockers - ALL RESOLVED**

### Previously Identified Blocker:
**"DLL vs EXE Output"** - Status: ✅ RESOLVED

**Solution Found:**
- Add `bShouldCompileAsDLL = true;` to Target.cs
- Confirmed from reference project example
- Simple one-line change

### No Remaining Blockers

---

## 📋 **Implementation Checklist**

### Step 1: Modify Target Configuration
- [ ] Edit `UnrealLiveLinkNative.Target.cs`
- [ ] Add `bShouldCompileAsDLL = true;`
- [ ] Build to verify DLL output

### Step 2: Create API Header
- [ ] Create `UnrealLiveLinkAPI.h` in Public folder
- [ ] Include Types.h for ULL_Transform
- [ ] Declare all 12 functions with extern "C" and __declspec(dllexport)
- [ ] Add comprehensive documentation comments

### Step 3: Create API Implementation
- [ ] Create `UnrealLiveLinkAPI.cpp` in Private folder
- [ ] Include UnrealLiveLinkAPI.h
- [ ] Include UnrealLiveLink.Native.h for UE_LOG
- [ ] Implement all 12 stub functions
- [ ] Add parameter validation
- [ ] Add logging for each call

### Step 4: Build and Verify
- [ ] Run BuildNative.ps1
- [ ] Verify output is .dll not .exe
- [ ] Check file size and location
- [ ] Run dumpbin to verify exports

### Step 5: Test Integration
- [ ] Copy DLL to test location
- [ ] Run C# P/Invoke test
- [ ] Verify no DllNotFoundException
- [ ] Check UE log for messages

---

## 🎯 **Success Criteria (from Plan)**

1. ✅ DLL exports exactly 12 functions (verify with `dumpbin /EXPORTS`)
2. ✅ Function names match P/Invoke declarations in C#
3. ✅ Calling convention is `__cdecl` (default for extern "C")
4. ✅ Can call from managed layer without DllNotFoundException
5. ✅ Stub functions log to Unreal log file
6. ✅ Returns ULL_OK for Initialize, version 1 for GetVersion

---

## 📊 **Estimated Effort**

**Time Estimate:** 1-2 hours

**Breakdown:**
- Target.cs modification: 5 minutes
- API header creation: 20 minutes (12 function declarations + docs)
- API implementation: 45 minutes (12 stub functions + validation)
- Build and verify: 10 minutes
- Testing and validation: 20 minutes

**Complexity:** LOW-MEDIUM
- Simple stub implementations
- No LiveLink logic required
- Clear patterns from mock DLL
- Build configuration solution known

---

## ✅ **READY TO PROCEED**

**All prerequisites met:**
- ✅ Objective understood
- ✅ Function signatures confirmed
- ✅ DLL configuration solution identified
- ✅ File locations known
- ✅ Implementation patterns clear
- ✅ Testing strategy defined
- ✅ No blocking unknowns

**Recommendation:** Proceed with Sub-Phase 6.3 implementation.

**Risk Level:** LOW

---

**Prepared by:** AI Assistant  
**Date:** October 17, 2025  
**Next Action:** Begin Sub-Phase 6.3 implementation with Target.cs modification
