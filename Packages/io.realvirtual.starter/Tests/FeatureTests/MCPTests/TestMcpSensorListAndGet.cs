#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using realvirtual.MCP;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace realvirtual.MCP.Tests
{
    //! Validates sensor_list finds sensors and sensor_get returns occupancy status
    public class TestMcpSensorListAndGet : FeatureTestBase
    {
        protected override string TestName => "MCP sensor_list finds sensors and sensor_get returns status";

        private Sensor testSensor;
        private McpToolRegistry registry;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            var sensorGO = CreateGameObject("McpTestSensor");
            var collider = sensorGO.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            testSensor = sensorGO.AddComponent<Sensor>();
            testSensor.DisplayStatus = true;
            testSensor.LimitSensorToTag = "";

            registry = new McpToolRegistry();
            registry.DiscoverTools();
        }

        protected override string ValidateResults()
        {
            // Test sensor_list
            var listResult = registry.CallTool("sensor_list", null);
            if (string.IsNullOrEmpty(listResult))
                return "sensor_list returned empty";

            JObject listParsed;
            try { listParsed = JObject.Parse(listResult); }
            catch (System.Exception e) { return $"sensor_list JSON parse error: {e.Message}"; }

            if (listParsed["sensors"] == null)
                return "sensor_list missing 'sensors' key";

            var sensors = listParsed["sensors"] as JArray;
            bool foundTestSensor = false;
            foreach (var s in sensors)
            {
                if (s["name"]?.ToString() == "McpTestSensor")
                {
                    foundTestSensor = true;
                    break;
                }
            }
            if (!foundTestSensor)
                return "sensor_list did not find 'McpTestSensor'";

            // Test sensor_get
            var getResult = registry.CallTool("sensor_get",
                new Dictionary<string, object> { ["name"] = "McpTestSensor" });
            if (string.IsNullOrEmpty(getResult))
                return "sensor_get returned empty";

            JObject getParsed;
            try { getParsed = JObject.Parse(getResult); }
            catch (System.Exception e) { return $"sensor_get JSON parse error: {e.Message}"; }

            if (getParsed["error"] != null)
                return $"sensor_get error: {getParsed["error"]}";
            if (getParsed["name"]?.ToString() != "McpTestSensor")
                return $"sensor_get name mismatch: {getParsed["name"]}";
            if (getParsed["occupied"] == null)
                return "sensor_get missing 'occupied' field";

            return "";
        }
    }
}

#endif
