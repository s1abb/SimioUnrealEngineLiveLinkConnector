using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimioUnrealEngineLiveLinkConnector.Utils;
using SimioAPI;

namespace SimioUnrealEngineLiveLinkConnector.Unit.Tests
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void PathUtils_SafeCombine_ShouldCombinePaths()
        {
            var part1 = "C:\\repos";
            var part2 = "SimioUnrealEngineLiveLinkConnector";
            var combined = PathUtils.SafeCombine(part1, part2);
            Assert.IsTrue(combined.EndsWith("SimioUnrealEngineLiveLinkConnector"));
        }

        [TestMethod]
        public void PathUtils_EnsureDirectoryExists_ShouldCreateDirectory()
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UtilsTestDir");
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
            var result = PathUtils.EnsureDirectoryExists(tempDir);
            Assert.IsTrue(result);
            Assert.IsTrue(System.IO.Directory.Exists(tempDir));
            System.IO.Directory.Delete(tempDir, true);
        }

        // Context-dependent PropertyValidation tests are omitted to match established patterns

        [TestMethod]
        public void NetworkUtils_FormatEndpoint_ShouldFormatCorrectly()
        {
            var host = "localhost";
            var port = 1234;
            var formatted = NetworkUtils.FormatEndpoint(host, port);
            Assert.AreEqual("localhost:1234", formatted);
        }
    }
        // Dummy context for PropertyValidation tests
        // No custom dummy Simio context needed; follow patterns from other tests
}
