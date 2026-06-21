#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using Newtonsoft.Json.Linq;
using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies that deserializing partial JSON only changes specified fields, others stay unchanged
    public class TestMcpDeserializePartialUpdate : FeatureTestBase
    {
        protected override string TestName => "MCP Deserialize Partial Update";
        

        private Drive testDrive;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestDrive");
            testDrive = go.AddComponent<Drive>();
            testDrive.TargetSpeed = 100f;
            testDrive.Acceleration = 50f;
            testDrive.UseLimits = false;
        }

        protected override string ValidateResults()
        {
            var json = new JObject
            {
                ["TargetSpeed"] = 999f,
                ["UseLimits"] = true
            };

            ComponentDeserializer.Deserialize(testDrive, json);

            if (Mathf.Abs(testDrive.TargetSpeed - 999f) > 0.01f)
                return $"TargetSpeed should be 999, got {testDrive.TargetSpeed}";
            if (!testDrive.UseLimits)
                return "UseLimits should be true after deserialize";
            if (Mathf.Abs(testDrive.Acceleration - 50f) > 0.01f)
                return $"Acceleration should remain 50 (unchanged), got {testDrive.Acceleration}";

            return "";
        }
    }
}

#endif
