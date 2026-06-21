// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_MCP
using Newtonsoft.Json.Linq;
using realvirtual;
using realvirtual.MCP;
using UnityEngine;

namespace realvirtual.MCP.Tools
{
    //! MCP tools for interacting with PLC signals.
    //!
    //! Provides commands to list, read, and write PLC signals (bool, int, float).
    //! These tools enable AI agents to interact with the realvirtual PLC signal system.
    public static class SignalTools
    {
        //! Lists all PLC signals in the scene
        [McpTool("List all PLC signals")]
        public static string SignalList()
        {
            var boolSignals = Object.FindObjectsOfType<PLCOutputBool>();
            var intSignals = Object.FindObjectsOfType<PLCOutputInt>();
            var floatSignals = Object.FindObjectsOfType<PLCOutputFloat>();

            var arr = new JArray();

            foreach (var signal in boolSignals)
            {
                arr.Add(new JObject
                {
                    ["name"] = signal.gameObject.name,
                    ["type"] = "bool",
                    ["value"] = signal.Value
                });
            }

            foreach (var signal in intSignals)
            {
                arr.Add(new JObject
                {
                    ["name"] = signal.gameObject.name,
                    ["type"] = "int",
                    ["value"] = signal.Value
                });
            }

            foreach (var signal in floatSignals)
            {
                arr.Add(new JObject
                {
                    ["name"] = signal.gameObject.name,
                    ["type"] = "float",
                    ["value"] = signal.Value
                });
            }

            return new JObject
            {
                ["signals"] = arr,
                ["count"] = boolSignals.Length + intSignals.Length + floatSignals.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Gets the value of a specific signal
        [McpTool("Get signal value")]
        public static string SignalGet(
            [McpParam("Signal name")] string name)
        {
            var boolSignal = FindSignal<PLCOutputBool>(name);
            if (boolSignal != null)
            {
                return new JObject
                {
                    ["name"] = name,
                    ["type"] = "bool",
                    ["value"] = boolSignal.Value
                }.ToString(Newtonsoft.Json.Formatting.None);
            }

            var intSignal = FindSignal<PLCOutputInt>(name);
            if (intSignal != null)
            {
                return new JObject
                {
                    ["name"] = name,
                    ["type"] = "int",
                    ["value"] = intSignal.Value
                }.ToString(Newtonsoft.Json.Formatting.None);
            }

            var floatSignal = FindSignal<PLCOutputFloat>(name);
            if (floatSignal != null)
            {
                return new JObject
                {
                    ["name"] = name,
                    ["type"] = "float",
                    ["value"] = floatSignal.Value
                }.ToString(Newtonsoft.Json.Formatting.None);
            }

            return new JObject { ["error"] = $"Signal '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Sets the value of a boolean signal
        [McpTool("Set bool signal value")]
        public static string SignalSetBool(
            [McpParam("Signal name")] string name,
            [McpParam("Value (true/false)")] bool value)
        {
            var signal = FindSignal<PLCInputBool>(name);
            if (signal == null)
                return new JObject { ["error"] = $"Boolean signal '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            signal.Value = value;

            return new JObject
            {
                ["status"] = "ok",
                ["name"] = name,
                ["type"] = "bool",
                ["value"] = value
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Sets the value of an integer signal
        [McpTool("Set int signal value")]
        public static string SignalSetInt(
            [McpParam("Signal name")] string name,
            [McpParam("Integer value")] int value)
        {
            var signal = FindSignal<PLCInputInt>(name);
            if (signal == null)
                return new JObject { ["error"] = $"Integer signal '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            signal.Value = value;

            return new JObject
            {
                ["status"] = "ok",
                ["name"] = name,
                ["type"] = "int",
                ["value"] = value
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Sets the value of a float signal
        [McpTool("Set float signal value")]
        public static string SignalSetFloat(
            [McpParam("Signal name")] string name,
            [McpParam("Float value")] float value)
        {
            var signal = FindSignal<PLCInputFloat>(name);
            if (signal == null)
                return new JObject { ["error"] = $"Float signal '{name}' not found" }.ToString(Newtonsoft.Json.Formatting.None);

            signal.Value = value;

            return new JObject
            {
                ["status"] = "ok",
                ["name"] = name,
                ["type"] = "float",
                ["value"] = value
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Helper to find a signal component by name
        private static T FindSignal<T>(string name) where T : Component
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var signal = go.GetComponent<T>();
                if (signal != null)
                    return signal;
            }

            var signals = Object.FindObjectsOfType<T>();
            foreach (var signal in signals)
            {
                if (signal.gameObject.name == name)
                    return signal;
            }

            return null;
        }
    }
}
#endif
