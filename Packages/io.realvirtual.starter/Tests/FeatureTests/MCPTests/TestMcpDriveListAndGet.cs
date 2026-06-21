#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using realvirtual.MCP;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace realvirtual.MCP.Tests
{
    //! Validates that drive_list finds created drives and drive_get returns detailed information
    public class TestMcpDriveListAndGet : FeatureTestBase
    {
        protected override string TestName => "MCP drive_list finds drives and drive_get returns details";

        private Drive drive1;
        private Drive drive2;
        private McpToolRegistry registry;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            // Erstelle 2 Test-Drives
            var drive1GO = CreateGameObject("McpTestDrive1");
            drive1 = drive1GO.AddComponent<Drive>();
            drive1.Direction = DIRECTION.LinearX;
            drive1.TargetSpeed = 100f;

            var drive2GO = CreateGameObject("McpTestDrive2");
            drive2 = drive2GO.AddComponent<Drive>();
            drive2.Direction = DIRECTION.RotationZ;
            drive2.TargetSpeed = 200f;

            registry = new McpToolRegistry();
            registry.DiscoverTools();
        }

        protected override string ValidateResults()
        {
            // Test drive_list
            var listResult = registry.CallTool("drive_list", null);
            if (string.IsNullOrEmpty(listResult))
                return "drive_list returned empty";

            JObject listParsed;
            try { listParsed = JObject.Parse(listResult); }
            catch { return $"drive_list not valid JSON: {listResult}"; }

            if (listParsed["drives"] == null)
                return "drive_list missing 'drives' key";

            var drives = listParsed["drives"] as JArray;
            if (drives == null)
                return "'drives' is not an array";

            // Pruefe ob beide Test-Drives gefunden wurden
            int foundCount = 0;
            foreach (var d in drives)
            {
                var name = d["name"]?.ToString();
                if (name == "McpTestDrive1" || name == "McpTestDrive2")
                    foundCount++;
            }

            if (foundCount < 2)
                return $"Expected 2 test drives in list, found {foundCount}";

            // Test drive_get
            var getResult = registry.CallTool("drive_get",
                new Dictionary<string, object> { ["name"] = "McpTestDrive1" });
            if (string.IsNullOrEmpty(getResult))
                return "drive_get returned empty";

            JObject getParsed;
            try { getParsed = JObject.Parse(getResult); }
            catch { return $"drive_get not valid JSON: {getResult}"; }

            if (getParsed["error"] != null)
                return $"drive_get returned error: {getParsed["error"]}";
            if (getParsed["name"]?.ToString() != "McpTestDrive1")
                return $"drive_get name mismatch: {getParsed["name"]}";
            if (getParsed["direction"]?.ToString() != "LinearX")
                return $"drive_get direction mismatch: {getParsed["direction"]}";

            return "";
        }
    }
}

#endif
