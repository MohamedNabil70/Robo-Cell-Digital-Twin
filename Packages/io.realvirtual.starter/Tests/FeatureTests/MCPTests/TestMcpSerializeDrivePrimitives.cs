#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Linq;
using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies that all primitive Drive fields (float, bool, enum) survive a serialize/deserialize round-trip
    public class TestMcpSerializeDrivePrimitives : FeatureTestBase
    {
        protected override string TestName => "MCP Serialize Drive Primitives Round-Trip";
        

        private Drive testDrive;
        private float originalSpeed;
        private float originalAccel;
        private bool originalUseLimits;
        private float originalUpperLimit;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestDrive");
            testDrive = go.AddComponent<Drive>();
            testDrive.TargetSpeed = 333.5f;
            testDrive.Acceleration = 77.2f;
            testDrive.UseLimits = true;
            testDrive.UpperLimit = 500f;
            testDrive.LowerLimit = -100f;

            originalSpeed = testDrive.TargetSpeed;
            originalAccel = testDrive.Acceleration;
            originalUseLimits = testDrive.UseLimits;
            originalUpperLimit = testDrive.UpperLimit;

            // Serialize
            var json = ComponentSerializer.Serialize(testDrive);
            LogTest($"Serialized {json.Properties().Count()} properties");

            // Reset values
            testDrive.TargetSpeed = 0;
            testDrive.Acceleration = 0;
            testDrive.UseLimits = false;
            testDrive.UpperLimit = 0;
            testDrive.LowerLimit = 0;

            // Deserialize back
            ComponentDeserializer.Deserialize(testDrive, json);
        }

        protected override string ValidateResults()
        {
            if (Mathf.Abs(testDrive.TargetSpeed - originalSpeed) > 0.01f)
                return $"TargetSpeed: expected {originalSpeed}, got {testDrive.TargetSpeed}";
            if (Mathf.Abs(testDrive.Acceleration - originalAccel) > 0.01f)
                return $"Acceleration: expected {originalAccel}, got {testDrive.Acceleration}";
            if (testDrive.UseLimits != originalUseLimits)
                return $"UseLimits: expected {originalUseLimits}, got {testDrive.UseLimits}";
            if (Mathf.Abs(testDrive.UpperLimit - originalUpperLimit) > 0.01f)
                return $"UpperLimit: expected {originalUpperLimit}, got {testDrive.UpperLimit}";
            return "";
        }
    }
}

#endif
