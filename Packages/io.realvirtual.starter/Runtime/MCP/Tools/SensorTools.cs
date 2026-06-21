// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_MCP
using Newtonsoft.Json.Linq;
using realvirtual;
using realvirtual.MCP;
using UnityEngine;

namespace realvirtual.MCP.Tools
{
    //! MCP tools for querying Sensor components.
    //!
    //! Provides commands to list sensors and query their occupied/not occupied status.
    //! These tools enable AI agents to interact with the realvirtual sensor system.
    public static class SensorTools
    {
        //! Lists all Sensor components in the scene
        [McpTool("List all sensors")]
        public static string SensorList()
        {
            var sensors = Object.FindObjectsOfType<Sensor>();
            var arr = new JArray();

            foreach (var sensor in sensors)
            {
                var pos = sensor.transform.position;
                arr.Add(new JObject
                {
                    ["name"] = sensor.gameObject.name,
                    ["path"] = GetGameObjectPath(sensor.gameObject),
                    ["occupied"] = sensor.Occupied,
                    ["globalPosition"] = new JObject
                    {
                        ["x"] = pos.x,
                        ["y"] = pos.y,
                        ["z"] = pos.z
                    }
                });
            }

            return new JObject
            {
                ["sensors"] = arr,
                ["count"] = sensors.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Gets the status of a specific sensor
        [McpTool("Get sensor status")]
        public static string SensorGet(
            [McpParam("Sensor name or path")] string name)
        {
            var sensor = FindSensor(name);
            if (sensor == null)
                return new JObject { ["error"] = $"Sensor '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            var sensorPos = sensor.transform.position;
            var obj = new JObject
            {
                ["name"] = sensor.gameObject.name,
                ["path"] = GetGameObjectPath(sensor.gameObject),
                ["occupied"] = sensor.Occupied,
                ["displayStatus"] = sensor.DisplayStatus,
                ["globalPosition"] = new JObject
                {
                    ["x"] = sensorPos.x,
                    ["y"] = sensorPos.y,
                    ["z"] = sensorPos.z
                }
            };

            if (sensor.Occupied && sensor.CollidingMus != null && sensor.CollidingMus.Count > 0)
            {
                var detected = new JArray();
                foreach (var mu in sensor.CollidingMus)
                {
                    if (mu != null)
                        detected.Add(mu.gameObject.name);
                }
                obj["detectedObjects"] = detected;
            }

            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Gets all occupied sensors
        [McpTool("Get occupied sensors")]
        public static string SensorGetOccupied()
        {
            var sensors = Object.FindObjectsOfType<Sensor>();
            var arr = new JArray();

            foreach (var sensor in sensors)
            {
                if (sensor.Occupied)
                {
                    var occPos = sensor.transform.position;
                    arr.Add(new JObject
                    {
                        ["name"] = sensor.gameObject.name,
                        ["path"] = GetGameObjectPath(sensor.gameObject),
                        ["globalPosition"] = new JObject
                        {
                            ["x"] = occPos.x,
                            ["y"] = occPos.y,
                            ["z"] = occPos.z
                        }
                    });
                }
            }

            return new JObject
            {
                ["sensors"] = arr,
                ["count"] = arr.Count
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Helper to find a sensor by name or path
        private static Sensor FindSensor(string nameOrPath)
        {
            var go = GameObject.Find(nameOrPath);
            if (go != null)
            {
                var sensor = go.GetComponent<Sensor>();
                if (sensor != null)
                    return sensor;
            }

            var sensors = Object.FindObjectsOfType<Sensor>();
            foreach (var sensor in sensors)
            {
                if (sensor.gameObject.name == nameOrPath)
                    return sensor;
                if (GetGameObjectPath(sensor.gameObject) == nameOrPath)
                    return sensor;
            }

            return null;
        }

        //! Gets the full hierarchy path of a GameObject
        private static string GetGameObjectPath(GameObject obj)
        {
            var path = obj.name;
            var parent = obj.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
#endif
