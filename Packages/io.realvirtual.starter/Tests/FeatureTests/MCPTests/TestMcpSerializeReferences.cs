#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Linq;
using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies that reference fields (Component, GameObject) are handled gracefully with null serializer (skipped or path-only, no errors)
    public class TestMcpSerializeReferences : FeatureTestBase
    {
        protected override string TestName => "MCP Serialize References (Skip Gracefully)";
        

        private Drive testDrive;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestDrive");
            testDrive = go.AddComponent<Drive>();
            testDrive.TargetSpeed = 250f;

            // Should not throw even with reference fields present
            var json = ComponentSerializer.Serialize(testDrive);
            if (json == null)
                LogTestError("Serialize returned null");
            else
                LogTest($"Serialized {json.Properties().Count()} properties");
        }

        protected override string ValidateResults()
        {
            var json = ComponentSerializer.Serialize(testDrive);
            if (json == null)
                return "Serialize returned null";

            // Value fields should be present
            if (json["TargetSpeed"] == null)
                return "TargetSpeed missing from output";

            return "";
        }
    }
}

#endif
