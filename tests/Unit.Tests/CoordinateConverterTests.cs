using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;

namespace SimioUnrealEngineLiveLinkConnector.Unit.Tests
{
    [TestClass]
    public class TypesTests
    {
        [TestMethod]
        public void ULL_Transform_StructSize_ShouldMatchExpected()
        {
            // Ensure struct size is correct for P/Invoke marshaling
            // Expected: 3*8 + 4*8 + 3*8 = 80 bytes (10 doubles)
            int actualSize = Marshal.SizeOf<ULL_Transform>();
            int expectedSize = 10 * sizeof(double); // 10 doubles = 80 bytes
            
            Assert.AreEqual(expectedSize, actualSize, 
                $"ULL_Transform size mismatch. Expected {expectedSize} bytes, got {actualSize} bytes");
        }

        [TestMethod]
        public void ULL_Transform_Identity_ShouldCreateValidTransform()
        {
            var transform = ULL_Transform.Identity();
            
            Assert.IsTrue(transform.IsValid(), "Identity transform should be valid");
            
            // Check position is at origin
            Assert.AreEqual(0.0, transform.position[0], "Identity X position should be 0");
            Assert.AreEqual(0.0, transform.position[1], "Identity Y position should be 0");
            Assert.AreEqual(0.0, transform.position[2], "Identity Z position should be 0");
            
            // Check rotation is identity quaternion [0,0,0,1]
            Assert.AreEqual(0.0, transform.rotation[0], "Identity quaternion X should be 0");
            Assert.AreEqual(0.0, transform.rotation[1], "Identity quaternion Y should be 0");
            Assert.AreEqual(0.0, transform.rotation[2], "Identity quaternion Z should be 0");
            Assert.AreEqual(1.0, transform.rotation[3], "Identity quaternion W should be 1");
            
            // Check scale is unit
            Assert.AreEqual(1.0, transform.scale[0], "Identity scale X should be 1");
            Assert.AreEqual(1.0, transform.scale[1], "Identity scale Y should be 1");
            Assert.AreEqual(1.0, transform.scale[2], "Identity scale Z should be 1");
        }

        [TestMethod]
        public void ULL_Transform_Create_ShouldCreateValidTransform()
        {
            var transform = ULL_Transform.Create(
                100.0, 200.0, 300.0,  // position
                0.1, 0.2, 0.3, 0.9,   // quaternion (not normalized, but valid)
                2.0, 3.0, 4.0);       // scale
            
            Assert.IsTrue(transform.IsValid(), "Created transform should be valid");
            
            Assert.AreEqual(100.0, transform.position[0]);
            Assert.AreEqual(200.0, transform.position[1]);
            Assert.AreEqual(300.0, transform.position[2]);
            
            Assert.AreEqual(0.1, transform.rotation[0]);
            Assert.AreEqual(0.2, transform.rotation[1]);
            Assert.AreEqual(0.3, transform.rotation[2]);
            Assert.AreEqual(0.9, transform.rotation[3]);
            
            Assert.AreEqual(2.0, transform.scale[0]);
            Assert.AreEqual(3.0, transform.scale[1]);
            Assert.AreEqual(4.0, transform.scale[2]);
        }

        [TestMethod]
        public void ULL_Transform_IsValid_ShouldDetectInvalidArrays()
        {
            var transform = new ULL_Transform();
            Assert.IsFalse(transform.IsValid(), "Uninitialized transform should be invalid");
            
            // Test with null arrays
            transform.position = null;
            transform.rotation = new double[4];
            transform.scale = new double[3];
            Assert.IsFalse(transform.IsValid(), "Transform with null position should be invalid");
            
            // Test with wrong array sizes
            transform.position = new double[2]; // Should be 3
            transform.rotation = new double[4];
            transform.scale = new double[3];
            Assert.IsFalse(transform.IsValid(), "Transform with wrong position size should be invalid");
        }

        [TestMethod]
        public void ULL_Transform_ToString_ShouldProvideReadableOutput()
        {
            var transform = ULL_Transform.Create(1.5, 2.5, 3.5, 0.0, 0.0, 0.0, 1.0);
            string result = transform.ToString();
            
            Assert.IsTrue(result.Contains("1.50"), "ToString should include position X");
            Assert.IsTrue(result.Contains("2.50"), "ToString should include position Y");
            Assert.IsTrue(result.Contains("3.50"), "ToString should include position Z");
            Assert.IsTrue(result.Contains("1.000"), "ToString should include quaternion W");
        }
    }

    [TestClass]
    public class CoordinateConverterTests
    {
        private const double TOLERANCE = 1e-10;

        [TestMethod]
        public void SimioPositionToUnreal_Origin_ShouldReturnOrigin()
        {
            var result = CoordinateConverter.SimioPositionToUnreal(0.0, 0.0, 0.0);
            
            Assert.AreEqual(0.0, result[0], TOLERANCE, "Origin X should remain 0");
            Assert.AreEqual(0.0, result[1], TOLERANCE, "Origin Y should remain 0");
            Assert.AreEqual(0.0, result[2], TOLERANCE, "Origin Z should remain 0");
        }

        [TestMethod]
        public void SimioPositionToUnreal_KnownValues_ShouldConvertCorrectly()
        {
            // Test case: Simio (1m, 2m, 3m) → Unreal (100cm, -300cm, 200cm)
            var result = CoordinateConverter.SimioPositionToUnreal(1.0, 2.0, 3.0);
            
            Assert.AreEqual(100.0, result[0], TOLERANCE, "X: 1m should become 100cm");
            Assert.AreEqual(-300.0, result[1], TOLERANCE, "Y: 3m should become -300cm (Z→-Y)");
            Assert.AreEqual(200.0, result[2], TOLERANCE, "Z: 2m should become 200cm (Y→Z)");
        }

        [TestMethod]
        public void SimioPositionToUnreal_InvalidValues_ShouldReturnOrigin()
        {
            var result1 = CoordinateConverter.SimioPositionToUnreal(double.NaN, 0.0, 0.0);
            var result2 = CoordinateConverter.SimioPositionToUnreal(double.PositiveInfinity, 0.0, 0.0);
            
            Assert.AreEqual(0.0, result1[0], "NaN input should return 0");
            Assert.AreEqual(0.0, result2[0], "Infinity input should return 0");
        }

        [TestMethod]
        public void SimioScaleToUnreal_UnitScale_ShouldRemainUnit()
        {
            var result = CoordinateConverter.SimioScaleToUnreal(1.0, 1.0, 1.0);
            
            Assert.AreEqual(1.0, result[0], TOLERANCE);
            Assert.AreEqual(1.0, result[1], TOLERANCE);
            Assert.AreEqual(1.0, result[2], TOLERANCE);
        }

        [TestMethod]
        public void SimioScaleToUnreal_KnownValues_ShouldRemapAxes()
        {
            // Test axis remapping: Simio(X,Y,Z) → Unreal(X,Z,Y)
            var result = CoordinateConverter.SimioScaleToUnreal(2.0, 3.0, 4.0);
            
            Assert.AreEqual(2.0, result[0], TOLERANCE, "X should stay X");
            Assert.AreEqual(4.0, result[1], TOLERANCE, "Z should become Y");
            Assert.AreEqual(3.0, result[2], TOLERANCE, "Y should become Z");
        }

        [TestMethod]
        public void SimioScaleToUnreal_InvalidValues_ShouldReturnUnitScale()
        {
            var result1 = CoordinateConverter.SimioScaleToUnreal(0.0, 1.0, 1.0); // Zero scale
            var result2 = CoordinateConverter.SimioScaleToUnreal(-1.0, 1.0, 1.0); // Negative scale
            var result3 = CoordinateConverter.SimioScaleToUnreal(double.NaN, 1.0, 1.0); // NaN
            
            Assert.AreEqual(1.0, result1[0], "Zero scale should default to 1.0");
            Assert.AreEqual(1.0, result2[0], "Negative scale should default to 1.0");
            Assert.AreEqual(1.0, result3[0], "NaN scale should default to 1.0");
        }

        [TestMethod]
        public void EulerToQuaternion_ZeroRotation_ShouldReturnIdentityQuaternion()
        {
            var result = CoordinateConverter.EulerToQuaternion(0.0, 0.0, 0.0);
            
            Assert.AreEqual(0.0, result[0], TOLERANCE, "Identity quaternion X should be 0");
            Assert.AreEqual(0.0, result[1], TOLERANCE, "Identity quaternion Y should be 0");
            Assert.AreEqual(0.0, result[2], TOLERANCE, "Identity quaternion Z should be 0");
            Assert.AreEqual(1.0, result[3], TOLERANCE, "Identity quaternion W should be 1");
            
            Assert.IsTrue(CoordinateConverter.IsQuaternionNormalized(result), 
                "Identity quaternion should be normalized");
        }

        [TestMethod]
        public void EulerToQuaternion_90DegreeRotations_ShouldProduceKnownQuaternions()
        {
            // Test 90-degree rotation around X-axis
            var resultX = CoordinateConverter.EulerToQuaternion(90.0, 0.0, 0.0);
            Assert.IsTrue(CoordinateConverter.IsQuaternionNormalized(resultX), 
                "90° X rotation should be normalized");
            
            // Test 90-degree rotation around Y-axis
            var resultY = CoordinateConverter.EulerToQuaternion(0.0, 90.0, 0.0);
            Assert.IsTrue(CoordinateConverter.IsQuaternionNormalized(resultY), 
                "90° Y rotation should be normalized");
            
            // Test 90-degree rotation around Z-axis
            var resultZ = CoordinateConverter.EulerToQuaternion(0.0, 0.0, 90.0);
            Assert.IsTrue(CoordinateConverter.IsQuaternionNormalized(resultZ), 
                "90° Z rotation should be normalized");
        }

        [TestMethod]
        public void EulerToQuaternion_InvalidValues_ShouldReturnIdentityQuaternion()
        {
            var result = CoordinateConverter.EulerToQuaternion(double.NaN, 0.0, 0.0);
            
            Assert.AreEqual(0.0, result[0], TOLERANCE);
            Assert.AreEqual(0.0, result[1], TOLERANCE);
            Assert.AreEqual(0.0, result[2], TOLERANCE);
            Assert.AreEqual(1.0, result[3], TOLERANCE);
        }

        [TestMethod]
        public void SimioToUnrealTransform_CompleteTransformation_ShouldWork()
        {
            var transform = CoordinateConverter.SimioToUnrealTransform(
                1.0, 2.0, 3.0,     // position in meters
                90.0, 0.0, 0.0,    // rotation in degrees
                2.0, 2.0, 2.0);    // scale

            Assert.IsTrue(transform.IsValid(), "Complete transform should be valid");
            
            // Check position conversion
            Assert.AreEqual(100.0, transform.position[0], TOLERANCE, "X: 1m → 100cm");
            Assert.AreEqual(-300.0, transform.position[1], TOLERANCE, "Y: 3m → -300cm");
            Assert.AreEqual(200.0, transform.position[2], TOLERANCE, "Z: 2m → 200cm");
            
            // Check rotation is normalized
            Assert.IsTrue(CoordinateConverter.IsQuaternionNormalized(transform.rotation),
                "Quaternion should be normalized");
            
            // Check scale remapping
            Assert.AreEqual(2.0, transform.scale[0], TOLERANCE, "X scale");
            Assert.AreEqual(2.0, transform.scale[1], TOLERANCE, "Y scale (remapped)");
            Assert.AreEqual(2.0, transform.scale[2], TOLERANCE, "Z scale (remapped)");
        }

        [TestMethod]
        public void IsQuaternionNormalized_ValidQuaternion_ShouldReturnTrue()
        {
            // Identity quaternion
            Assert.IsTrue(CoordinateConverter.IsQuaternionNormalized(
                new double[] { 0.0, 0.0, 0.0, 1.0 }));
            
            // 45-degree rotation quaternion (approximately)
            double cos45 = Math.Cos(Math.PI / 8);
            double sin45 = Math.Sin(Math.PI / 8);
            Assert.IsTrue(CoordinateConverter.IsQuaternionNormalized(
                new double[] { sin45, 0.0, 0.0, cos45 }));
        }

        [TestMethod]
        public void IsQuaternionNormalized_InvalidQuaternion_ShouldReturnFalse()
        {
            // Not normalized
            Assert.IsFalse(CoordinateConverter.IsQuaternionNormalized(
                new double[] { 1.0, 1.0, 1.0, 1.0 }));
            
            // Wrong size
            Assert.IsFalse(CoordinateConverter.IsQuaternionNormalized(
                new double[] { 0.0, 0.0, 1.0 }));
            
            // Null
            Assert.IsFalse(CoordinateConverter.IsQuaternionNormalized(null));
        }

        [TestMethod]
        public void GetVectorMagnitude_KnownVectors_ShouldReturnCorrectMagnitude()
        {
            // Unit vectors
            Assert.AreEqual(1.0, CoordinateConverter.GetVectorMagnitude(
                new double[] { 1.0, 0.0, 0.0 }), TOLERANCE);
            Assert.AreEqual(1.0, CoordinateConverter.GetVectorMagnitude(
                new double[] { 0.0, 1.0, 0.0 }), TOLERANCE);
            Assert.AreEqual(1.0, CoordinateConverter.GetVectorMagnitude(
                new double[] { 0.0, 0.0, 1.0 }), TOLERANCE);
            
            // 3-4-5 triangle
            Assert.AreEqual(5.0, CoordinateConverter.GetVectorMagnitude(
                new double[] { 3.0, 4.0, 0.0 }), TOLERANCE);
            
            // Zero vector
            Assert.AreEqual(0.0, CoordinateConverter.GetVectorMagnitude(
                new double[] { 0.0, 0.0, 0.0 }), TOLERANCE);
        }
    }
}