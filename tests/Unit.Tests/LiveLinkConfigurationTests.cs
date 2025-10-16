using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;
using SimioUnrealEngineLiveLinkConnector.Utils;
using SimioUnrealEngineLiveLinkConnector.Element;

namespace SimioUnrealEngineLiveLinkConnector.Unit.Tests
{
    [TestClass]
    public class LiveLinkConfigurationTests
    {
        [TestMethod]
        public void LiveLinkConfiguration_Constructor_ShouldSetDefaults()
        {
            var config = new LiveLinkConfiguration();
            Assert.IsNotNull(config);
            Assert.IsFalse(string.IsNullOrWhiteSpace(config.SourceName));
            // Default EnableLogging is false
            Assert.IsFalse(config.EnableLogging);
        }

        [TestMethod]
        public void LiveLinkConfiguration_CreateValidated_ValidData_ShouldCreateConfig()
        {
            var config = new LiveLinkConfiguration {
                SourceName = "TestSource",
                EnableLogging = true,
                LogFilePath = "C:/temp/log.txt",
                UnrealEnginePath = "C:/UE",
                Host = "localhost",
                Port = 8080,
                ConnectionTimeout = System.TimeSpan.FromSeconds(5.0),
                RetryAttempts = 3
            };
            var validated = config.CreateValidated();
            Assert.IsNotNull(validated);
            Assert.AreEqual("TestSource", validated.SourceName);
            Assert.AreEqual(8080, validated.Port);
        }

        [TestMethod]
        public void LiveLinkConfiguration_CreateValidated_InvalidData_ShouldSanitize()
        {
            var config = new LiveLinkConfiguration {
                SourceName = "",
                EnableLogging = true,
                LogFilePath = "",
                UnrealEnginePath = "",
                Host = "",
                Port = -1,
                ConnectionTimeout = System.TimeSpan.FromSeconds(-5.0),
                RetryAttempts = -3
            };
            var validated = config.CreateValidated();
            Assert.IsNotNull(validated);
            Assert.AreEqual("SimioSimulation", validated.SourceName);
            Assert.AreEqual(11111, validated.Port);
            Assert.AreEqual(3, validated.RetryAttempts);
        }

        [TestMethod]
        public void LiveLinkConfiguration_ToString_ShouldProvideReadableOutput()
        {
            var config = new LiveLinkConfiguration {
                SourceName = "TestSource",
                EnableLogging = true,
                LogFilePath = "C:/temp/log.txt",
                UnrealEnginePath = "C:/UE",
                Host = "localhost",
                Port = 8080,
                ConnectionTimeout = System.TimeSpan.FromSeconds(5.0),
                RetryAttempts = 3
            };
            var validated = config.CreateValidated();
            Assert.IsNotNull(validated);
            var output = validated.ToString();
            Assert.IsTrue(output.Contains("TestSource"));
            Assert.IsTrue(output.Contains("8080"));
        }
    }
}
