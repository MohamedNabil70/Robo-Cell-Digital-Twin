#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies enum fields serialize as string and deserialize back correctly
    public class TestMcpEnumRoundTrip : FeatureTestBase
    {
        protected override string TestName => "MCP Enum Fields Round-Trip";
        

        private Drive testDrive;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestDrive");
            testDrive = go.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.RotationY;

            var json = ComponentSerializer.Serialize(testDrive);
            testDrive.Direction = DIRECTION.LinearX; // Change
            ComponentDeserializer.Deserialize(testDrive, json);
        }

        protected override string ValidateResults()
        {
            if (testDrive.Direction != DIRECTION.RotationY)
                return $"Enum not restored: expected RotationY, got {testDrive.Direction}";
            return "";
        }
    }
}

#endif
