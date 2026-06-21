#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using realvirtual.MCP.Serialization;

namespace realvirtual.MCP.Tests
{
    //! Verifies Sensor string and bool fields round-trip correctly
    public class TestMcpSerializeSensorFields : FeatureTestBase
    {
        protected override string TestName => "MCP Serialize Sensor Fields Round-Trip";
        

        private Sensor testSensor;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;
            var go = CreateGameObject("TestSensor");
            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            go.layer = LayerMask.NameToLayer("rvSensor");
            testSensor = go.AddComponent<Sensor>();
            testSensor.LimitSensorToTag = "TestTag";
            testSensor.DisplayStatus = true;

            var json = ComponentSerializer.Serialize(testSensor);
            testSensor.LimitSensorToTag = "";
            testSensor.DisplayStatus = false;
            ComponentDeserializer.Deserialize(testSensor, json);
        }

        protected override string ValidateResults()
        {
            if (testSensor.LimitSensorToTag != "TestTag")
                return $"LimitSensorToTag: expected 'TestTag', got '{testSensor.LimitSensorToTag}'";
            if (!testSensor.DisplayStatus)
                return "DisplayStatus should be true after deserialize";
            return "";
        }
    }
}

#endif
