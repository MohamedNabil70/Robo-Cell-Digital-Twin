#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using realvirtual.MCP;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace realvirtual.MCP.Tests
{
    //! Validates signal tools for listing, reading and writing signals
    public class TestMcpSignalReadWrite : FeatureTestBase
    {
        protected override string TestName => "MCP signal tools list, read and write signals correctly";

        private PLCOutputBool boolOut;
        private PLCInputFloat floatIn;
        private McpToolRegistry registry;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            // Erstelle verschiedene Signal-Typen
            boolOut = CreateTestObject<PLCOutputBool>("McpTestBoolSignal");
            boolOut.Value = true;

            floatIn = CreateTestObject<PLCInputFloat>("McpTestFloatSignal");
            floatIn.Value = 0f;

            registry = new McpToolRegistry();
            registry.DiscoverTools();
        }

        protected override string ValidateResults()
        {
            // Test signal_list
            var listResult = registry.CallTool("signal_list", null);
            if (string.IsNullOrEmpty(listResult))
                return "signal_list returned empty";

            JObject listParsed;
            try { listParsed = JObject.Parse(listResult); }
            catch (System.Exception e) { return $"signal_list JSON parse error: {e.Message}"; }

            var signals = listParsed["signals"] as JArray;
            if (signals == null)
                return "signal_list missing 'signals' array";

            bool foundBool = false;
            foreach (var s in signals)
            {
                if (s["name"]?.ToString() == "McpTestBoolSignal")
                {
                    foundBool = true;
                    if (s["type"]?.ToString() != "bool")
                        return $"Bool signal type mismatch: {s["type"]}";
                    break;
                }
            }
            if (!foundBool)
                return "signal_list did not find 'McpTestBoolSignal'";

            // Test signal_get
            var getResult = registry.CallTool("signal_get",
                new Dictionary<string, object> { ["name"] = "McpTestBoolSignal" });

            JObject getParsed;
            try { getParsed = JObject.Parse(getResult); }
            catch (System.Exception e) { return $"signal_get JSON parse error: {e.Message}"; }

            if (getParsed["error"] != null)
                return $"signal_get error: {getParsed["error"]}";
            if (getParsed["value"]?.ToObject<bool>() != true)
                return $"signal_get value mismatch: expected true, got {getParsed["value"]}";

            // Test signal_set_float
            var setResult = registry.CallTool("signal_set_float", new Dictionary<string, object>
            {
                ["name"] = "McpTestFloatSignal",
                ["value"] = 42.5f
            });

            JObject setParsed;
            try { setParsed = JObject.Parse(setResult); }
            catch (System.Exception e) { return $"signal_set_float JSON parse error: {e.Message}"; }

            if (setParsed["error"] != null)
                return $"signal_set_float error: {setParsed["error"]}";

            if (floatIn.Value < 42f || floatIn.Value > 43f)
                return $"Float signal not updated. Expected ~42.5, got {floatIn.Value}";

            return "";
        }
    }
}

#endif
