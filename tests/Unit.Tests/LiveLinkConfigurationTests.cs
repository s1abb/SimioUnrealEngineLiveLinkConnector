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
                EnableLogging = true
            };
            var validated = config.CreateValidated();
            Assert.IsNotNull(validated);
            Assert.AreEqual("TestSource", validated.SourceName);
            Assert.IsTrue(validated.EnableLogging);
        }

        [TestMethod]
        public void LiveLinkConfiguration_CreateValidated_InvalidData_ShouldSanitize()
        {
            var config = new LiveLinkConfiguration {
                SourceName = "",
                EnableLogging = false
            };
            var validated = config.CreateValidated();
            Assert.IsNotNull(validated);
            // Empty source name should default to "SimioSimulation"
            Assert.AreEqual("SimioSimulation", validated.SourceName);
            Assert.IsFalse(validated.EnableLogging);
        }

        [TestMethod]
        public void LiveLinkConfiguration_ToString_ShouldProvideReadableOutput()
        {
            var config = new LiveLinkConfiguration {
                SourceName = "TestSource",
                EnableLogging = true
            };
            var validated = config.CreateValidated();
            Assert.IsNotNull(validated);
            var output = validated.ToString();
            // Should contain source name in output
            Assert.IsTrue(output.Contains("TestSource"));
        }

        [TestMethod]
        public void LiveLinkConfiguration_Validate_EmptySourceName_ShouldReturnError()
        {
            var config = new LiveLinkConfiguration {
                SourceName = "",
                EnableLogging = false
            };
            var errors = config.Validate();
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Length > 0);
            Assert.IsTrue(errors[0].Contains("Source Name"));
        }

        [TestMethod]
        public void LiveLinkConfiguration_Validate_ValidConfig_ShouldReturnNoErrors()
        {
            var config = new LiveLinkConfiguration {
                SourceName = "TestSource",
                EnableLogging = true
            };
            var errors = config.Validate();
            Assert.IsNotNull(errors);
            Assert.AreEqual(0, errors.Length);
        }
    }
}
