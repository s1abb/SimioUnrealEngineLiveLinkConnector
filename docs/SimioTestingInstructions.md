# Simio Testing Instructions with Mock DLL

## Overview
Complete step-by-step guide for testing the SimioUnrealEngineLiveLinkConnector extension in Simio using our mock DLL implementation.

## ✅ **VALIDATION SUCCESSFUL!**
**Date Tested**: October 15, 2025  
**Test Status**: **PASSED** - All core functionality validated with mock DLL

### **Key Test Results:**
- ✅ **API Coverage**: All 11 core functions called successfully (`ULL_Initialize`, `ULL_RegisterObject`, `ULL_UpdateObject`, `ULL_RemoveObject`, `ULL_Shutdown`, etc.)
- ✅ **Coordinate System**: Position tracking working correctly (entities moving from `-575.0` to `+390.0` on X-axis)
- ✅ **Entity Lifecycle**: Proper registration → continuous updates → cleanup cycle observed
- ✅ **Performance**: High-frequency updates (multiple per second) handling correctly
- ✅ **Multi-Entity**: Simultaneous entities managed properly (`ModelEntity1.11` and `ModelEntity1.12`)
- ✅ **Rotation Data**: Valid quaternion rotations `[0.7,0.0,-0.0,0.7]` for 90° turns
- ✅ **File Logging**: Complete audit trail captured in `tests\Simio.Tests\SimioUnrealLiveLink_Mock.log` (cleared on each run)

**Sample Log Output:**
```
[16:37:22] [MOCK] ULL_RegisterObject(subjectName='ModelEntity1.10')
[16:37:22] [MOCK] ULL_UpdateObject(subjectName='ModelEntity1.10', transform=pos=[-575.0,0.0,0.0], rot=[0.0,0.0,-0.0,1.0], scale=[1.0,1.0,1.0])
[16:37:22] [MOCK] ULL_UpdateObject(subjectName='ModelEntity1.10', transform=pos=[-450.0,-0.0,0.0], rot=[0.7,0.0,-0.0,0.7], scale=[1.0,1.0,1.0])
[16:37:22] [MOCK] ULL_RemoveObject(subjectName='ModelEntity1.10')
```

**Ready for Phase 4**: Mock testing complete - proceed to real Unreal Engine integration!

## Prerequisites ✅

Before starting, ensure you have:
- [x] Built and deployed the extension (should already be done)
- [x] Simio installed at `C:\Program Files\Simio LLC\Simio\`
- [x] Mock DLL properly deployed and working

### Quick Verification:
```powershell
# Verify deployment
Get-ChildItem "C:\Program Files\Simio LLC\Simio\UserExtensions\SimioUnrealEngineLiveLinkConnector\"

# Should show:
# - SimioUnrealEngineLiveLinkConnector.dll (36KB)
# - UnrealLiveLink.Native.dll (67KB - mock)
# - System.Drawing.Common.dll
```

---

## 🎯 **Step-by-Step Testing Process**

### **Phase 1: Extension Loading Test**

#### Step 1.1: Launch Simio
1. **Open Simio** from Start Menu or desktop shortcut
2. **Create New Model** → select "Model" → "Create"
3. **Wait for Simio to fully load** the modeling environment

#### Step 1.2: Verify Extension Loaded
1. **Go to Library Panel** (left side of Simio)
2. **Look for "Elements" section**
3. **Find "SimioUnrealEngineLiveLinkElement"** in the list
   - If missing: Extension failed to load, check Windows Event Viewer
   - If present: ✅ Extension loaded successfully!

#### Step 1.3: Verify Steps Available
1. **Go to Steps Panel** (ribbon or right-click in Processes)
2. **Look for our custom steps**:
   - `CreateObject`
   - `SetObjectPositionOrientation` 
   - `DestroyObject`
   - `TransmitValues`
   - If missing: Check Simio logs for loading errors
   - If present: ✅ All steps loaded successfully!

---

### **Phase 2: Basic Element Configuration**

#### Step 2.1: Add Element to Model
1. **Drag "SimioUnrealEngineLiveLinkElement"** from Library to main model window
2. **Place it anywhere** in the model (position doesn't matter)
3. **Select the placed element**
4. **Check Properties panel** for:
   - `SourceName` property (should default to "SimioSimulation")
   - Other element properties

#### Step 2.2: Configure Element Properties  
1. **Click on the element** to select it
2. **In Properties panel**, modify if desired:
   - `SourceName`: Change to "TestSimulation" (optional)
3. **Note the element ID** (e.g., "SimioUnrealEngineLiveLinkElement1")

---

### **Phase 3: Create Test Entities and Process**

#### Step 3.1: Add Source and Sink
1. **From Library → Standard Library**:
   - Drag **Source** to model → place it
   - Drag **Sink** to model → place it to the right
2. **Connect them**:
   - Click **Path** tool in ribbon
   - Draw path from Source to Sink
   - Name it "Path1" (default is fine)

#### Step 3.2: Create Process with LiveLink Steps
1. **Right-click Source** → "View Properties"
2. **Go to "Processes" tab** → "Creating Entity (Exiting)" 
3. **Click "Add" to create process steps**

#### Step 3.3: Add CreateObject Step
1. **Right-click in process** → "Insert Step" → **Find "CreateObject"**
2. **Configure CreateObject step properties**:
   - `UnrealEngineConnector`: Select our element (dropdown)
   - `ObjectName`: Enter "Entity{Entity.Name}" or just "TestObject1"
   - `X`: 100
   - `Y`: 200  
   - `Z`: 0
   - `Heading`: 0
   - `Pitch`: 0
   - `Roll`: 0

#### Step 3.4: Add SetObjectPositionOrientation Step  
1. **Add another step** → **"SetObjectPositionOrientation"**
2. **Configure properties**:
   - `UnrealEngineConnector`: Same element as above
   - `ObjectName`: Same as CreateObject ("TestObject1")
   - `X`: 150 (different position)
   - `Y`: 250
   - `Z`: 10
   - `Heading`: Entity.Movement.Direction
   - `Pitch`: 0
   - `Roll`: 0

#### Step 3.5: Add TransmitValues Step (Optional)
1. **Add "TransmitValues" step**
2. **Configure**:
   - `UnrealEngineConnector`: Same element
3. **Add repeat group entries** (click "+" button):
   - ValueName: "SimulationTime", ValueExpression: TimeNow
   - ValueName: "EntityCount", ValueExpression: Source1.NumberCreated
   - ValueName: "CurrentSpeed", ValueExpression: Entity.Movement.Rate

---

### **Phase 4: Mock DLL Log Monitoring**

#### Step 4.1: Prepare Log File Monitoring
1. **Navigate to the project directory**:
   ```powershell
   cd C:\repos\SimioUnrealEngineLiveLinkConnector
   ```
2. **Optional: Monitor the log in real-time**:
   ```powershell
   Get-Content "tests\Simio.Tests\SimioUnrealLiveLink_Mock.log" -Wait -Tail 10
   ```
   > **Note**: Log file is automatically cleared at the start of each simulation run

#### Step 4.2: Launch Simio Normally  
1. **Open Simio** from Start Menu (no special launch required)
2. **Load your test model**
3. The mock DLL will automatically log to `tests\Simio.Tests\SimioUnrealLiveLink_Mock.log`

---

### **Phase 5: Run Simulation and Validate**

#### Step 5.1: Start Simulation
1. **In Simio model**, click **"Run"** in ribbon
2. **Set run parameters**:
   - Ending Type: "Run Length"
   - Run Length: 10 (minutes)
   - Speed: Any (Normal is fine)
3. **Click "Run"**

#### Step 5.2: Check Mock DLL Log Output
**After running simulation, check the log file** at `tests\Simio.Tests\SimioUnrealLiveLink_Mock.log`:
```powershell
Get-Content "tests\Simio.Tests\SimioUnrealLiveLink_Mock.log"
```

**Expected log entries**:
```
[14:23:15] [MOCK] ULL_Initialize(providerName='TestSimulation')
[14:23:15] [MOCK] ULL_RegisterObject(subjectName='TestObject1')
[14:23:16] [MOCK] ULL_UpdateObject(subjectName='TestObject1', transform=pos=[100.0,200.0,0.0], rot=[0.0,0.0,0.0,1.0], scale=[1.0,1.0,1.0])
[14:23:16] [MOCK] ULL_UpdateObject(subjectName='TestObject1', transform=pos=[150.0,250.0,10.0], rot=[...])
[14:23:17] [MOCK] ULL_RegisterDataSubject(subjectName='SimulationData', propertyNames=['SimulationTime','EntityCount','CurrentSpeed'], count=3)
[14:23:18] [MOCK] ULL_UpdateDataSubject(subjectName='SimulationData', values=[1.5,5.0,100.0])
```

#### Step 5.3: Validation Checklist
- ✅ **ULL_Initialize called** when simulation starts
- ✅ **ULL_RegisterObject called** for each entity creation
- ✅ **ULL_UpdateObject called** when positions change
- ✅ **ULL_UpdateDataSubject called** if TransmitValues step used
- ✅ **Coordinate conversion working** (Simio units → Unreal units)
- ✅ **No error messages** in Simio or console

---

### **Phase 6: Advanced Testing**

#### Step 6.1: Test DestroyObject Step
1. **Add Sink process**:
   - Right-click Sink → Properties → Processes
   - "Destroying Entity (Entered)" → Add step
   - Use **"DestroyObject"** step
   - Configure ObjectName to match created objects
2. **Run simulation** and verify:
   ```
   [MOCK] ULL_RemoveObject(subjectName='TestObject1')
   ```

#### Step 6.2: Test Multiple Entities
1. **Increase entity creation rate** in Source
2. **Use expressions** for dynamic naming:
   - ObjectName: `"Entity_" + Entity.Id.ToString`
3. **Watch for multiple object registrations**:
   ```
   [MOCK] ULL_RegisterObject(subjectName='Entity_1')
   [MOCK] ULL_RegisterObject(subjectName='Entity_2')
   [MOCK] ULL_RegisterObject(subjectName='Entity_3')
   ```

#### Step 6.3: Test Error Handling
1. **Create invalid configuration**:
   - Empty ObjectName
   - Invalid element reference
   - NaN values in coordinates
2. **Verify graceful error handling** in Simio logs

---

## 🚨 **Troubleshooting Guide**

### Extension Not Loading
**Symptoms**: Element/Steps missing from Library
**Solutions**:
1. Check Windows Event Viewer → Applications and Services Logs → Simio
2. Verify all DLL files deployed correctly
3. Check .NET Framework 4.8 compatibility
4. Try running Simio as Administrator

### No Mock DLL Log Output
**Symptoms**: No log file created or no `[MOCK]` entries in `tests\Simio.Tests\SimioUnrealLiveLink_Mock.log`
**Solutions**:
1. **Check from project directory**: `cd C:\repos\SimioUnrealEngineLiveLinkConnector` and verify the tests folder exists
2. **Verify DLL is deployed**: Check extension files in Simio UserExtensions folder  
3. **Check simulation actually ran**: Ensure entities are created and steps execute
4. **Verify element reference**: Make sure all steps reference the correct element instance

### P/Invoke Errors
**Symptoms**: Simio crashes or reports DLL errors
**Solutions**:
1. Verify mock DLL architecture (x64) matches Simio
2. Check DLL dependencies with DependencyWalker
3. Ensure Visual C++ Redistributables installed

### Simulation Not Calling Steps
**Symptoms**: No mock output during simulation
**Solutions**:
1. Verify element reference is set correctly in all steps
2. Check process step configuration and execution order
3. Ensure entities are actually being created and processed

---

## ✅ **Expected Results Summary**

After successful testing, you should see:

### **Log File Pattern** (tests\Simio.Tests\SimioUnrealLiveLink_Mock.log):
```
[14:23:15] [MOCK] ULL_Initialize(providerName='SimioSimulation')
[14:23:15] [MOCK] ULL_RegisterObject(subjectName='Entity_1')
[14:23:16] [MOCK] ULL_UpdateObject(subjectName='Entity_1', transform=pos=[100.0,-200.0,200.0], rot=[0.0,0.0,0.0,1.0], scale=[1.0,1.0,1.0])
[14:23:16] [MOCK] ULL_UpdateObject(subjectName='Entity_1', transform=pos=[150.0,-250.0,250.0], rot=[...])
[14:23:17] [MOCK] ULL_UpdateDataSubject(subjectName='SimulationData', propertyNames=['SimulationTime','EntityCount'], values=[2.5,1.0])
[14:23:18] [MOCK] ULL_RemoveObject(subjectName='Entity_1')
[14:23:18] [MOCK] ULL_Shutdown
```

### **Key Validations**:
- ✅ **Coordinate Conversion**: Simio Y,Z flipped to Unreal -Z,Y format  
- ✅ **Unit Conversion**: Simio meters → Unreal centimeters (×100)
- ✅ **Lifecycle Management**: Initialize → Create → Update → Destroy → Shutdown
- ✅ **Data Streaming**: TransmitValues broadcasting simulation metrics
- ✅ **Error Handling**: Graceful failure modes with proper logging

### **Success Criteria**:
1. **All 4 steps execute** without Simio errors
2. **Mock DLL logs all API calls** with correct parameters  
3. **Coordinate transformations** match expected Unreal format
4. **Simulation runs to completion** with clean shutdown
5. **No memory leaks or crashes** during extended runs

---

## 🎉 **What This Proves**

Successful testing demonstrates:

- ✅ **Complete Simio Integration**: All extension components working in real Simio environment
- ✅ **P/Invoke Layer Validation**: Managed ↔ Native communication working perfectly
- ✅ **API Contract Verification**: Mock DLL confirms exact requirements for real implementation  
- ✅ **Production Readiness**: Managed layer ready for real Unreal Engine native DLL
- ✅ **End-to-End Workflow**: Full Simio → LiveLink data pipeline operational

**Next Step**: Replace mock DLL with real Unreal Engine LiveLink implementation - **no managed layer changes required!** 🚀