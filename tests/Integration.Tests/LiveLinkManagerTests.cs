using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;
using System;

namespace SimioUnrealEngineLiveLinkConnector.Tests.UnrealIntegration
{
    [TestClass]
    public class LiveLinkManagerTests
    {
        [TestMethod]
        public void LiveLinkManager_Singleton_ShouldReturnSameInstance()
        {
            var instance1 = LiveLinkManager.Instance;
            var instance2 = LiveLinkManager.Instance;
            
            Assert.AreSame(instance1, instance2, "LiveLinkManager should return the same singleton instance");
        }

        [TestMethod]
        public void LiveLinkManager_InitialState_ShouldBeNotInitialized()
        {
            var manager = LiveLinkManager.Instance;
            
            Assert.IsFalse(manager.IsInitialized, "Manager should not be initialized initially");
            Assert.IsNull(manager.SourceName, "Source name should be null initially");
            Assert.AreEqual(0, manager.ObjectCount, "Object count should be 0 initially");
        }

        [TestMethod]
        public void LiveLinkManager_GetOrCreateObject_WithoutInitialization_ShouldThrow()
        {
            var manager = LiveLinkManager.Instance;
            
            // Ensure manager is not initialized (would need Shutdown() if it was)
            Assert.ThrowsException<InvalidOperationException>(() => 
                manager.GetOrCreateObject("TestObject"));
        }

        [TestMethod]
        public void LiveLinkObjectUpdater_Constructor_ValidName_ShouldSucceed()
        {
            var updater = new LiveLinkObjectUpdater("TestObject");
            
            Assert.AreEqual("TestObject", updater.ObjectName);
            Assert.IsFalse(updater.IsRegistered);
            Assert.IsFalse(updater.HasProperties);
            Assert.IsNull(updater.PropertyNames);
        }

        [TestMethod]
        public void LiveLinkObjectUpdater_Constructor_InvalidName_ShouldThrow()
        {
            Assert.ThrowsException<ArgumentException>(() => new LiveLinkObjectUpdater(null));
            Assert.ThrowsException<ArgumentException>(() => new LiveLinkObjectUpdater(""));
            Assert.ThrowsException<ArgumentException>(() => new LiveLinkObjectUpdater("   "));
        }

        [TestMethod]
        public void UnrealLiveLinkNative_GetExpectedApiVersion_ShouldReturnPositive()
        {
            int version = UnrealLiveLinkNative.GetExpectedApiVersion();
            Assert.IsTrue(version > 0, "Expected API version should be positive");
        }

        [TestMethod]
        public void UnrealLiveLinkNative_IsDllAvailable_ShouldHandleGracefully()
        {
            // This test checks that the method doesn't throw, regardless of DLL availability
            bool isAvailable = UnrealLiveLinkNative.IsDllAvailable();
            
            // We don't assert the result since the DLL may or may not be available during testing
            // But the method should not throw exceptions
            Assert.IsTrue(true, "IsDllAvailable() should complete without throwing exceptions");
        }

        [TestMethod]
        public void UnrealLiveLinkNative_ReturnCodes_ShouldHaveCorrectValues()
        {
            Assert.AreEqual(0, UnrealLiveLinkNative.ULL_OK);
            Assert.AreEqual(-1, UnrealLiveLinkNative.ULL_ERROR);
            Assert.AreEqual(-2, UnrealLiveLinkNative.ULL_NOT_CONNECTED);
            Assert.AreEqual(-3, UnrealLiveLinkNative.ULL_NOT_INITIALIZED);
        }

        [TestMethod]
        public void UnrealLiveLinkNative_IsSuccess_ShouldWorkCorrectly()
        {
            Assert.IsTrue(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_OK));
            Assert.IsFalse(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_ERROR));
            Assert.IsFalse(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_NOT_CONNECTED));
            Assert.IsFalse(UnrealLiveLinkNative.IsSuccess(UnrealLiveLinkNative.ULL_NOT_INITIALIZED));
        }

        [TestMethod]
        public void UnrealLiveLinkNative_GetReturnCodeDescription_ShouldProvideDescriptions()
        {
            string successDesc = UnrealLiveLinkNative.GetReturnCodeDescription(UnrealLiveLinkNative.ULL_OK);
            string errorDesc = UnrealLiveLinkNative.GetReturnCodeDescription(UnrealLiveLinkNative.ULL_ERROR);
            
            Assert.IsFalse(string.IsNullOrEmpty(successDesc), "Success description should not be empty");
            Assert.IsFalse(string.IsNullOrEmpty(errorDesc), "Error description should not be empty");
            Assert.AreNotEqual(successDesc, errorDesc, "Different return codes should have different descriptions");
        }

        [TestMethod]
        public void UnrealLiveLinkNative_ValidateArraySize_ShouldWorkCorrectly()
        {
            double[] validArray = new double[3];
            
            // Should not throw for correct size
            Assert.IsTrue(UnrealLiveLinkNative.ValidateArraySize(validArray, 3, "testArray"));
            
            // Should throw for null array
            Assert.ThrowsException<ArgumentNullException>(() =>
                UnrealLiveLinkNative.ValidateArraySize(null, 3, "testArray"));
            
            // Should throw for wrong size
            Assert.ThrowsException<ArgumentException>(() =>
                UnrealLiveLinkNative.ValidateArraySize(validArray, 5, "testArray"));
        }
    }

    [TestClass] 
    public class LiveLinkObjectUpdaterTests
    {
        [TestMethod]
        public void LiveLinkObjectUpdater_UpdateTransform_WithoutNativeDll_ShouldNotCrash()
        {
            var updater = new LiveLinkObjectUpdater("TestObject");
            
            // This should not crash even if native DLL is not available
            // The P/Invoke call will fail, but we're testing the managed wrapper logic
            try
            {
                updater.UpdateTransform(1.0, 2.0, 3.0, 0.0, 0.0, 0.0);
                // If it succeeds, that's fine (mock DLL might be present)
                Assert.IsTrue(true);
            }
            catch (DllNotFoundException)
            {
                // Expected if no DLL present
                Assert.IsTrue(true);
            }
            catch (Exception ex)
            {
                // Other exceptions indicate issues with our managed code
                Assert.Fail($"Unexpected exception: {ex.Message}");
            }
            finally
            {
                updater.Dispose();
            }
        }

        [TestMethod]
        public void LiveLinkObjectUpdater_UpdateWithProperties_InvalidArrays_ShouldThrow()
        {
            var updater = new LiveLinkObjectUpdater("TestObject");
            
            try
            {
                // Null property names
                Assert.ThrowsException<ArgumentNullException>(() =>
                    updater.UpdateWithProperties(0, 0, 0, 0, 0, 0, null, new float[1]));
                
                // Null property values
                Assert.ThrowsException<ArgumentNullException>(() =>
                    updater.UpdateWithProperties(0, 0, 0, 0, 0, 0, new string[1], null));
                
                // Mismatched array lengths
                Assert.ThrowsException<ArgumentException>(() =>
                    updater.UpdateWithProperties(0, 0, 0, 0, 0, 0, new string[2], new float[1]));
            }
            finally
            {
                updater.Dispose();
            }
        }

        [TestMethod]
        public void LiveLinkObjectUpdater_Dispose_ShouldHandleMultipleCalls()
        {
            var updater = new LiveLinkObjectUpdater("TestObject");
            
            // Should not throw on multiple dispose calls
            updater.Dispose();
            updater.Dispose();
            updater.Dispose();
            
            Assert.IsTrue(true, "Multiple Dispose() calls should be safe");
        }

        [TestMethod]
        public void LiveLinkObjectUpdater_ToString_ShouldProvideDebugInfo()
        {
            var updater = new LiveLinkObjectUpdater("TestObject");
            
            string result = updater.ToString();
            
            Assert.IsTrue(result.Contains("TestObject"), "ToString should include object name");
            Assert.IsTrue(result.Contains("NOT_REGISTERED"), "ToString should include registration status");
            
            updater.Dispose();
            
            string disposedResult = updater.ToString();
            Assert.IsTrue(disposedResult.Contains("DISPOSED"), "ToString should indicate disposed state");
        }
    }
}