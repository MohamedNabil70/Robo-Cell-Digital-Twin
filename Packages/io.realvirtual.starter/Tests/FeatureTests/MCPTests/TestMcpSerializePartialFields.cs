#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies SerializeFields() with field name filter only includes requested fields
    public class TestMcpSerializePartialFields : FeatureTestBase
    {
        protected override string TestName => "MCP Serialize Partial Fields (Filter)";
        

        private Drive testDrive;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestDrive");
            testDrive = go.AddComponent<Drive>();
            testDrive.TargetSpeed = 500f;
            testDrive.Acceleration = 200f;
            testDrive.UseLimits = true;
        }

        protected override string ValidateResults()
        {
            var json = ComponentSerializer.SerializeFields(testDrive,
                new[] { "TargetSpeed", "Acceleration" });

            if (json == null)
                return "SerializeFields returned null";
            if (json["TargetSpeed"] == null)
                return "TargetSpeed missing from partial serialization";
            if (json["Acceleration"] == null)
                return "Acceleration missing from partial serialization";
            if (json["UseLimits"] != null)
                return "UseLimits should NOT be in partial serialization";
            if (Mathf.Abs((float)json["TargetSpeed"] - 500f) > 0.01f)
                return $"TargetSpeed value wrong: {json["TargetSpeed"]}";

            return "";
        }
    }
}

#endif
