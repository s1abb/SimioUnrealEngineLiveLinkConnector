using System;

namespace SimioUnrealEngineLiveLinkConnector.UnrealIntegration
{
    /// <summary>
    /// Static utility class for converting between Simio and Unreal Engine coordinate systems.
    /// 
    /// Simio: Right-handed, Y-up, meters, Euler angles in degrees
    /// Unreal: Left-handed, Z-up, centimeters, Quaternions
    /// </summary>
    public static class CoordinateConverter
    {
        // Conversion constants
        private const double METERS_TO_CENTIMETERS = 100.0;
        private const double DEGREES_TO_RADIANS = Math.PI / 180.0;

        /// <summary>
        /// Converts Simio position to Unreal Engine position
        /// Simio (X,Y,Z) meters → Unreal (X,-Z,Y) centimeters
        /// </summary>
        /// <param name="simioX">Simio X coordinate in meters</param>
        /// <param name="simioY">Simio Y coordinate in meters</param>
        /// <param name="simioZ">Simio Z coordinate in meters</param>
        /// <returns>Array [unrealX, unrealY, unrealZ] in centimeters</returns>
        public static double[] SimioPositionToUnreal(double simioX, double simioY, double simioZ)
        {
            // Handle NaN/infinity
            if (!IsFinite(simioX) || !IsFinite(simioY) || !IsFinite(simioZ))
            {
                return new double[] { 0.0, 0.0, 0.0 };
            }

            // Axis remapping: Simio(X,Y,Z) → Unreal(X,-Z,Y) and convert to centimeters
            return new double[]
            {
                simioX * METERS_TO_CENTIMETERS,  // X stays X
                -simioZ * METERS_TO_CENTIMETERS, // Z becomes -Y (flip for handedness)
                simioY * METERS_TO_CENTIMETERS   // Y becomes Z (up axis change)
            };
        }

        /// <summary>
        /// Converts Simio scale to Unreal Engine scale
        /// Simio (X,Y,Z) → Unreal (X,Z,Y) - axis remapping only, no unit conversion
        /// </summary>
        /// <param name="simioX">Simio X scale factor</param>
        /// <param name="simioY">Simio Y scale factor</param>
        /// <param name="simioZ">Simio Z scale factor</param>
        /// <returns>Array [unrealX, unrealY, unrealZ] scale factors</returns>
        public static double[] SimioScaleToUnreal(double simioX, double simioY, double simioZ)
        {
            // Handle invalid scale values
            if (!IsFinite(simioX) || !IsFinite(simioY) || !IsFinite(simioZ) ||
                simioX <= 0.0 || simioY <= 0.0 || simioZ <= 0.0)
            {
                return new double[] { 1.0, 1.0, 1.0 }; // Default to unit scale
            }

            // Axis remapping: Simio(X,Y,Z) → Unreal(X,Z,Y)
            return new double[]
            {
                simioX, // X stays X
                simioZ, // Z becomes Y
                simioY  // Y becomes Z
            };
        }

        /// <summary>
        /// Converts Euler angles (degrees) to quaternion [X,Y,Z,W]
        /// Applies axis remapping to account for coordinate system differences
        /// </summary>
        /// <param name="simioRotX">Rotation around X-axis in degrees</param>
        /// <param name="simioRotY">Rotation around Y-axis in degrees</param>
        /// <param name="simioRotZ">Rotation around Z-axis in degrees</param>
        /// <returns>Quaternion array [X,Y,Z,W] for Unreal coordinate system</returns>
        public static double[] EulerToQuaternion(double simioRotX, double simioRotY, double simioRotZ)
        {
            // Handle NaN/infinity
            if (!IsFinite(simioRotX) || !IsFinite(simioRotY) || !IsFinite(simioRotZ))
            {
                return new double[] { 0.0, 0.0, 0.0, 1.0 }; // Identity quaternion
            }

            // Convert degrees to radians
            double radX = simioRotX * DEGREES_TO_RADIANS;
            double radY = simioRotY * DEGREES_TO_RADIANS;
            double radZ = simioRotZ * DEGREES_TO_RADIANS;

            // Remap axes for coordinate system conversion
            // Simio: X,Y,Z → Unreal: X,-Z,Y (same remapping as position)
            double unrealRotX = radX;  // X rotation stays X
            double unrealRotY = radZ;  // Z rotation becomes Y
            double unrealRotZ = -radY; // Y rotation becomes -Z (flip for handedness)

            // Convert Euler angles to quaternion using ZYX order (yaw-pitch-roll)
            double cosX = Math.Cos(unrealRotX * 0.5);
            double sinX = Math.Sin(unrealRotX * 0.5);
            double cosY = Math.Cos(unrealRotY * 0.5);
            double sinY = Math.Sin(unrealRotY * 0.5);
            double cosZ = Math.Cos(unrealRotZ * 0.5);
            double sinZ = Math.Sin(unrealRotZ * 0.5);

            // Compute quaternion components
            double qw = cosX * cosY * cosZ + sinX * sinY * sinZ;
            double qx = sinX * cosY * cosZ - cosX * sinY * sinZ;
            double qy = cosX * sinY * cosZ + sinX * cosY * sinZ;
            double qz = cosX * cosY * sinZ - sinX * sinY * cosZ;

            // Normalize quaternion
            double magnitude = Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            if (magnitude < 1e-10) // Avoid division by zero
            {
                return new double[] { 0.0, 0.0, 0.0, 1.0 }; // Identity quaternion
            }

            return new double[]
            {
                qx / magnitude,
                qy / magnitude,
                qz / magnitude,
                qw / magnitude
            };
        }

        /// <summary>
        /// Creates a complete ULL_Transform from Simio coordinates
        /// </summary>
        /// <param name="simioX">Simio X position in meters</param>
        /// <param name="simioY">Simio Y position in meters</param>
        /// <param name="simioZ">Simio Z position in meters</param>
        /// <param name="simioRotX">Simio X rotation in degrees</param>
        /// <param name="simioRotY">Simio Y rotation in degrees</param>
        /// <param name="simioRotZ">Simio Z rotation in degrees</param>
        /// <param name="simioScaleX">Simio X scale factor (default 1.0)</param>
        /// <param name="simioScaleY">Simio Y scale factor (default 1.0)</param>
        /// <param name="simioScaleZ">Simio Z scale factor (default 1.0)</param>
        /// <returns>ULL_Transform ready for native P/Invoke</returns>
        public static ULL_Transform SimioToUnrealTransform(
            double simioX, double simioY, double simioZ,
            double simioRotX, double simioRotY, double simioRotZ,
            double simioScaleX = 1.0, double simioScaleY = 1.0, double simioScaleZ = 1.0)
        {
            var position = SimioPositionToUnreal(simioX, simioY, simioZ);
            var rotation = EulerToQuaternion(simioRotX, simioRotY, simioRotZ);
            var scale = SimioScaleToUnreal(simioScaleX, simioScaleY, simioScaleZ);

            return new ULL_Transform
            {
                position = position,
                rotation = rotation,
                scale = scale
            };
        }

        /// <summary>
        /// Checks if a double value is finite (not NaN or infinity)
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <returns>True if value is finite</returns>
        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// Validates that a quaternion is properly normalized
        /// </summary>
        /// <param name="quaternion">Quaternion array [X,Y,Z,W]</param>
        /// <returns>True if quaternion is normalized within tolerance</returns>
        public static bool IsQuaternionNormalized(double[] quaternion, double tolerance = 1e-6)
        {
            if (quaternion == null || quaternion.Length != 4)
                return false;

            double magnitude = Math.Sqrt(
                quaternion[0] * quaternion[0] +
                quaternion[1] * quaternion[1] +
                quaternion[2] * quaternion[2] +
                quaternion[3] * quaternion[3]);

            return Math.Abs(magnitude - 1.0) < tolerance;
        }

        /// <summary>
        /// Gets the magnitude of a 3D vector
        /// </summary>
        /// <param name="vector">3D vector array [X,Y,Z]</param>
        /// <returns>Vector magnitude</returns>
        public static double GetVectorMagnitude(double[] vector)
        {
            if (vector == null || vector.Length != 3)
                return 0.0;

            return Math.Sqrt(vector[0] * vector[0] + vector[1] * vector[1] + vector[2] * vector[2]);
        }
    }
}
