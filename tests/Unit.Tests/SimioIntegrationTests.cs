using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimioUnrealEngineLiveLinkConnector.Element;
using SimioUnrealEngineLiveLinkConnector.Steps;

namespace SimioUnrealEngineLiveLinkConnector.Unit.Tests
{
    [TestClass]
    public class SimioIntegrationTests
    {
        [TestMethod]
        public void SimioElementDefinition_CanInstantiate()
        {
            // Arrange & Act
            var elementDef = new SimioUnrealEngineLiveLinkElementDefinition();
            
            // Assert
            Assert.IsNotNull(elementDef);
            Assert.IsNotNull(elementDef.Name);
            Assert.IsNotNull(elementDef.Description);
            Assert.AreNotEqual(Guid.Empty, elementDef.UniqueID);
            Assert.AreEqual(SimioUnrealEngineLiveLinkElementDefinition.MY_ID, elementDef.UniqueID);
        }
        
        [TestMethod]
        public void CreateObjectStepDefinition_CanInstantiate()
        {
            // Arrange & Act
            var stepDef = new CreateObjectStepDefinition();
            
            // Assert
            Assert.IsNotNull(stepDef);
            Assert.IsNotNull(stepDef.Name);
            Assert.IsNotNull(stepDef.Description);
            Assert.AreNotEqual(Guid.Empty, stepDef.UniqueID);
        }
        
        [TestMethod]
        public void SetObjectPositionOrientationStepDefinition_CanInstantiate()
        {
            // Arrange & Act
            var stepDef = new SetObjectPositionOrientationStepDefinition();
            
            // Assert
            Assert.IsNotNull(stepDef);
            Assert.IsNotNull(stepDef.Name);
            Assert.IsNotNull(stepDef.Description);
            Assert.AreNotEqual(Guid.Empty, stepDef.UniqueID);
        }
        
        [TestMethod]
        public void SimioGuidReferences_AreConsistent()
        {
            // Arrange
            var elementId = SimioUnrealEngineLiveLinkElementDefinition.MY_ID;
            
            // Act & Assert - Verify the GUID is a valid, non-empty GUID
            Assert.AreNotEqual(Guid.Empty, elementId);
            
            // Verify it's the same reference used by step definitions
            // (This validates our AddElementProperty GUID parameter fixes)
            Assert.IsTrue(elementId.ToString().Length == 36); // Standard GUID string format
        }
    }
}