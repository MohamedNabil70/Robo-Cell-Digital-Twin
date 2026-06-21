#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies SerializeAll returns all components keyed by type name
    public class TestMcpSerializeAllComponents : FeatureTestBase
    {
        protected override string TestName => "MCP SerializeAll Components on GameObject";
        

        private GameObject testGo;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            testGo = CreateGameObject("TestMulti");
            var drive = testGo.AddComponent<Drive>();
            drive.TargetSpeed = 250f;
            var collider = testGo.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            testGo.layer = LayerMask.NameToLayer("rvSensor");
            var sensor = testGo.AddComponent<Sensor>();
            sensor.DisplayStatus = true;
        }

        protected override string ValidateResults()
        {
            var json = ComponentSerializer.SerializeAll(testGo);
            if (json == null)
                return "SerializeAll returned null";
            if (json["Drive"] == null)
                return "Drive component missing from SerializeAll output";
            if (json["Sensor"] == null)
                return "Sensor component missing from SerializeAll output";
            if (json["Drive"]["TargetSpeed"] == null)
                return "Drive.TargetSpeed missing";
            if (Mathf.Abs((float)json["Drive"]["TargetSpeed"] - 250f) > 0.01f)
                return $"Drive.TargetSpeed wrong: {json["Drive"]["TargetSpeed"]}";
            return "";
        }
    }
}

#endif
