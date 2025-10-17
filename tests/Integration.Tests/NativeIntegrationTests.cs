using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SimioUnrealEngineLiveLinkConnector.Integration.Tests
{
    /// <summary>
    /// Integration tests for the native DLL layer.
    /// These tests validate P/Invoke marshaling and native function behavior.
    /// 
    /// Test Scope:
    /// - DLL loading and function resolution
    /// - Lifecycle functions (Initialize, Shutdown, GetVersion, IsConnected)
    /// - Transform subject operations (Register, Update, Remove)
    /// - Data subject operations
    /// - Struct marshaling (ULL_Transform)
    /// - Error handling and return codes
    /// 
    /// Expected Behavior (Sub-Phase 6.3 - Stub Functions):
    /// - All functions execute without exceptions
    /// - Initialize returns ULL_OK (0)
    /// - GetVersion returns 1
    /// - IsConnected returns ULL_NOT_CONNECTED (-2) - no LiveLink yet
    /// - Subject operations complete without errors
    /// - Log messages generated (if accessible)
    /// </summary>
    [TestClass]
    public class NativeIntegrationTests
    {
        private static bool _isInitialized = false;
        private static string? _testProviderName;

        #region Test Infrastructure

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            context.WriteLine("=== Native Integration Tests - Class Setup ===");
            
            // Verify native DLL exists
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnrealLiveLink.Native.dll");
            context.WriteLine($"DLL Path: {dllPath}");
            
            Assert.IsTrue(File.Exists(dllPath), 
                $"Native DLL not found at: {dllPath}. Ensure native layer is built.");
            
            FileInfo dllInfo = new FileInfo(dllPath);
            context.WriteLine($"DLL Size: {dllInfo.Length:N0} bytes");
            context.WriteLine($"DLL Modified: {dllInfo.LastWriteTime}");
            
            // Set test provider name
            _testProviderName = "IntegrationTest";
            
            context.WriteLine("✅ Class setup complete");
        }

        [ClassCleanup]
        public static void ClassTeardown()
        {
            // Ensure cleanup
            if (_isInitialized)
            {
                try
                {
                    UnrealLiveLinkNative.ULL_Shutdown();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [TestInitialize]
        public void TestSetup()
        {
            // Each test may need to handle initialization state
            // Some tests verify behavior before initialization
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Individual test cleanup if needed
        }

        #endregion

        #region 1. DLL Loading & Availability Tests

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("DLL")]
        public void DLL_ShouldBeAvailable()
        {
            // Verify the DLL can be accessed
            bool isAvailable = UnrealLiveLinkNative.IsDllAvailable();
            
            Assert.IsTrue(isAvailable, "Native DLL should be available for P/Invoke");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("DLL")]
        public void GetVersion_ShouldReturnExpectedValue()
        {
            // This should work even before initialization
            int version = UnrealLiveLinkNative.ULL_GetVersion();
            
            Assert.AreEqual(1, version, "API version should be 1 (ULL_API_VERSION)");
        }

        #endregion

        #region 2. Lifecycle Function Tests

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        public void Initialize_WithValidProviderName_ShouldReturnSuccess()
        {
            // Arrange
            string providerName = _testProviderName ?? "TestProvider";
            
            // Act
            int result = UnrealLiveLinkNative.ULL_Initialize(providerName);
            _isInitialized = (result == UnrealLiveLinkNative.ULL_OK);
            
            // Assert
            Assert.AreEqual(UnrealLiveLinkNative.ULL_OK, result, 
                "Initialize should return ULL_OK (0) for valid provider name");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        [TestCategory("ErrorHandling")]
        public void Initialize_WithNullProviderName_ShouldReturnError()
        {
            // Act
            int result = UnrealLiveLinkNative.ULL_Initialize(null!);
            
            // Assert
            Assert.AreEqual(UnrealLiveLinkNative.ULL_ERROR, result,
                "Initialize should return ULL_ERROR (-1) for null provider name");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        [TestCategory("ErrorHandling")]
        public void Initialize_WithEmptyProviderName_ShouldReturnError()
        {
            // Act
            int result = UnrealLiveLinkNative.ULL_Initialize(string.Empty);
            
            // Assert
            Assert.AreEqual(UnrealLiveLinkNative.ULL_ERROR, result,
                "Initialize should return ULL_ERROR (-1) for empty provider name");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        public void Initialize_CalledTwice_ShouldSucceedBothTimes()
        {
            // Arrange
            string providerName = _testProviderName ?? "TestProvider";
            
            // Act - First call
            int result1 = UnrealLiveLinkNative.ULL_Initialize(providerName);
            _isInitialized = (result1 == UnrealLiveLinkNative.ULL_OK);
            
            // Act - Second call (idempotent behavior)
            int result2 = UnrealLiveLinkNative.ULL_Initialize(providerName);
            
            // Assert
            Assert.AreEqual(UnrealLiveLinkNative.ULL_OK, result1, "First Initialize should succeed");
            Assert.AreEqual(UnrealLiveLinkNative.ULL_OK, result2, "Second Initialize should succeed (idempotent)");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        public void IsConnected_AfterInitialization_ShouldReturnNotConnected()
        {
            // Arrange - Initialize first
            int initResult = UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = (initResult == UnrealLiveLinkNative.ULL_OK);
            
            // Act
            int result = UnrealLiveLinkNative.ULL_IsConnected();
            
            // Assert
            // In Sub-Phase 6.3 (stubs), should return NOT_CONNECTED since no LiveLink integration yet
            Assert.AreEqual(UnrealLiveLinkNative.ULL_NOT_CONNECTED, result,
                "IsConnected should return ULL_NOT_CONNECTED (-2) in stub phase (no LiveLink integration yet)");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        public void IsConnected_BeforeInitialization_ShouldReturnNotInitialized()
        {
            // Ensure we're not initialized (may need to shutdown first)
            UnrealLiveLinkNative.ULL_Shutdown();
            _isInitialized = false;
            
            // Act
            int result = UnrealLiveLinkNative.ULL_IsConnected();
            
            // Assert
            Assert.AreEqual(UnrealLiveLinkNative.ULL_NOT_INITIALIZED, result,
                "IsConnected should return ULL_NOT_INITIALIZED (-3) before initialization");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        public void Shutdown_ShouldCompleteWithoutException()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_Shutdown();
            _isInitialized = false;
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Lifecycle")]
        public void Shutdown_CalledMultipleTimes_ShouldNotCrash()
        {
            // Act & Assert - Multiple shutdowns should be safe
            UnrealLiveLinkNative.ULL_Shutdown();
            UnrealLiveLinkNative.ULL_Shutdown();
            UnrealLiveLinkNative.ULL_Shutdown();
            
            _isInitialized = false;
        }

        #endregion

        #region 3. Transform Subject Operation Tests

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("TransformSubjects")]
        public void RegisterObject_WithValidName_ShouldNotThrow()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_RegisterObject("TestObject_001");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("TransformSubjects")]
        [TestCategory("ErrorHandling")]
        public void RegisterObject_WithNullName_ShouldNotCrash()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            // Act & Assert - Should handle gracefully (log error but not crash)
            UnrealLiveLinkNative.ULL_RegisterObject(null!);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("TransformSubjects")]
        public void UpdateObject_WithValidTransform_ShouldNotThrow()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            ULL_Transform transform = new ULL_Transform
            {
                position = new double[] { 100.0, 200.0, 300.0 },
                rotation = new double[] { 0.0, 0.0, 0.0, 1.0 },
                scale = new double[] { 1.0, 1.0, 1.0 }
            };
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_UpdateObject("TestObject_002", ref transform);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("TransformSubjects")]
        public void RegisterObjectWithProperties_WithValidData_ShouldNotThrow()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            string[] properties = new string[] { "Speed", "Load", "Battery" };
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_RegisterObjectWithProperties("TestObject_003", properties, properties.Length);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("TransformSubjects")]
        public void UpdateObjectWithProperties_WithValidData_ShouldNotThrow()
        {
            // Arrange - Initialize and register first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            string[] propertyNames = new string[] { "Speed", "Load" };
            UnrealLiveLinkNative.ULL_RegisterObjectWithProperties("TestObject_004", propertyNames, propertyNames.Length);
            
            ULL_Transform transform = new ULL_Transform
            {
                position = new double[] { 50.0, 60.0, 70.0 },
                rotation = new double[] { 0.0, 0.0, 0.707, 0.707 },
                scale = new double[] { 1.0, 1.0, 1.0 }
            };
            
            float[] propertyValues = new float[] { 25.5f, 100.0f };
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_UpdateObjectWithProperties("TestObject_004", ref transform, propertyValues, propertyValues.Length);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("TransformSubjects")]
        public void RemoveObject_WithValidName_ShouldNotThrow()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            // Act & Assert - Should not throw (safe to call on non-existent object)
            UnrealLiveLinkNative.ULL_RemoveObject("TestObject_005");
        }

        #endregion

        #region 4. Data Subject Operation Tests

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("DataSubjects")]
        public void RegisterDataSubject_WithValidData_ShouldNotThrow()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            string[] properties = new string[] { "Temperature", "Pressure", "FlowRate" };
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_RegisterDataSubject("DataSubject_001", properties, properties.Length);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("DataSubjects")]
        public void UpdateDataSubject_WithValidData_ShouldNotThrow()
        {
            // Arrange - Initialize and register first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            string[] propertyNames = new string[] { "Metric1", "Metric2" };
            UnrealLiveLinkNative.ULL_RegisterDataSubject("DataSubject_002", propertyNames, propertyNames.Length);
            
            float[] propertyValues = new float[] { 42.0f, 3.14f };
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_UpdateDataSubject("DataSubject_002", propertyNames, propertyValues, propertyValues.Length);
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("DataSubjects")]
        public void RemoveDataSubject_WithValidName_ShouldNotThrow()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            // Act & Assert - Should not throw
            UnrealLiveLinkNative.ULL_RemoveDataSubject("DataSubject_003");
        }

        #endregion

        #region 5. Struct Marshaling Tests

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Marshaling")]
        public void ULL_Transform_ShouldBe80Bytes()
        {
            // Verify struct size matches native expectations
            int size = Marshal.SizeOf<ULL_Transform>();
            
            Assert.AreEqual(80, size, 
                "ULL_Transform should be exactly 80 bytes for binary compatibility with native code");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Marshaling")]
        public void ULL_Transform_ShouldHaveCorrectFieldLayout()
        {
            // Create a test transform
            ULL_Transform transform = new ULL_Transform
            {
                position = new double[] { 1.0, 2.0, 3.0 },
                rotation = new double[] { 0.0, 0.0, 0.0, 1.0 },
                scale = new double[] { 1.0, 1.0, 1.0 }
            };
            
            // Verify arrays are correct size
            Assert.AreEqual(3, transform.position.Length, "position should have 3 elements");
            Assert.AreEqual(4, transform.rotation.Length, "rotation should have 4 elements");
            Assert.AreEqual(3, transform.scale.Length, "scale should have 3 elements");
            
            // Verify we can set and read values
            transform.position[0] = 100.0;
            Assert.AreEqual(100.0, transform.position[0], "Should be able to set and read position values");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Marshaling")]
        public void ULL_Transform_ShouldPassThroughPInvokeCorrectly()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            // Create transform with specific values
            ULL_Transform transform = new ULL_Transform
            {
                position = new double[] { 123.45, 678.90, 111.22 },
                rotation = new double[] { 0.1, 0.2, 0.3, 0.9 },
                scale = new double[] { 2.0, 3.0, 4.0 }
            };
            
            // Act - Pass through P/Invoke (should not crash or corrupt memory)
            UnrealLiveLinkNative.ULL_UpdateObject("MarshalTest", ref transform);
            
            // Assert - Verify struct wasn't corrupted by P/Invoke
            Assert.AreEqual(123.45, transform.position[0], 0.001, "position[0] should be unchanged after P/Invoke");
            Assert.AreEqual(678.90, transform.position[1], 0.001, "position[1] should be unchanged after P/Invoke");
            Assert.AreEqual(2.0, transform.scale[0], 0.001, "scale[0] should be unchanged after P/Invoke");
        }

        #endregion

        #region 6. Error Handling Tests

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("ErrorHandling")]
        public void ReturnCodes_ShouldHaveCorrectValues()
        {
            // Verify return code constants match native layer
            Assert.AreEqual(0, UnrealLiveLinkNative.ULL_OK, "ULL_OK should be 0");
            Assert.AreEqual(-1, UnrealLiveLinkNative.ULL_ERROR, "ULL_ERROR should be -1");
            Assert.AreEqual(-2, UnrealLiveLinkNative.ULL_NOT_CONNECTED, "ULL_NOT_CONNECTED should be -2");
            Assert.AreEqual(-3, UnrealLiveLinkNative.ULL_NOT_INITIALIZED, "ULL_NOT_INITIALIZED should be -3");
        }

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("ErrorHandling")]
        public void IsSuccess_ShouldCorrectlyIdentifySuccessCodes()
        {
            Assert.IsTrue(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_OK));
            Assert.IsFalse(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_ERROR));
            Assert.IsFalse(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_NOT_CONNECTED));
            Assert.IsFalse(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_NOT_INITIALIZED));
        }

        #endregion

        #region 7. High-Frequency Update Test

        [TestMethod]
        [TestCategory("Integration")]
        [TestCategory("Performance")]
        [Timeout(5000)] // 5 second timeout
        public void UpdateObject_HighFrequency_ShouldNotCrashOrBlock()
        {
            // Arrange - Initialize first
            UnrealLiveLinkNative.ULL_Initialize(_testProviderName ?? "TestProvider");
            _isInitialized = true;
            
            ULL_Transform transform = new ULL_Transform
            {
                position = new double[] { 0.0, 0.0, 0.0 },
                rotation = new double[] { 0.0, 0.0, 0.0, 1.0 },
                scale = new double[] { 1.0, 1.0, 1.0 }
            };
            
            // Act - Simulate 60Hz updates for 1 second (60 updates)
            // Note: Stub implementation throttles logging to every 60th call
            for (int i = 0; i < 60; i++)
            {
                transform.position[0] = i * 10.0;
                UnrealLiveLinkNative.ULL_UpdateObject("HighFreqTest", ref transform);
            }
            
            // Assert - If we get here without exception or timeout, test passes
            Assert.IsTrue(true, "High-frequency updates should complete without issues");
        }

        #endregion
    }
}
