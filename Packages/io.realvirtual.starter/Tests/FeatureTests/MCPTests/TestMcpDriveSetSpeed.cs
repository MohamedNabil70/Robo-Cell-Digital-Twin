#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using realvirtual.MCP;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace realvirtual.MCP.Tests
{
    //! Validates that drive_set_speed changes target speed and rejects negative values
    public class TestMcpDriveSetSpeed : FeatureTestBase
    {
        protected override string TestName => "MCP drive_set_speed changes speed and rejects negative values";

        private Drive testDrive;
        private string setSpeedResult;
        private string negativeSpeedResult;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            var driveGO = CreateGameObject("McpSpeedTest");
            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.LinearX;
            testDrive.TargetSpeed = 100f;

            var registry = new McpToolRegistry();
            registry.DiscoverTools();

            // Setze positive Geschwindigkeit
            setSpeedResult = registry.CallTool("drive_set_speed", new Dictionary<string, object>
            {
                ["name"] = "McpSpeedTest",
                ["speed"] = 300f
            });

            // Versuche negative Geschwindigkeit
            negativeSpeedResult = registry.CallTool("drive_set_speed", new Dictionary<string, object>
            {
                ["name"] = "McpSpeedTest",
                ["speed"] = -50f
            });
        }

        protected override string ValidateResults()
        {
            // Positive Speed sollte erfolgreich sein
            if (string.IsNullOrEmpty(setSpeedResult))
                return "set_speed returned empty";

            JObject parsed;
            try { parsed = JObject.Parse(setSpeedResult); }
            catch (System.Exception e) { return $"set_speed JSON parse error: {e.Message}"; }

            if (parsed["error"] != null)
                return $"set_speed error: {parsed["error"]}";
            if (parsed["status"]?.ToString() != "ok")
                return $"set_speed status not 'ok': {parsed["status"]}";

            if (testDrive.TargetSpeed < 299f || testDrive.TargetSpeed > 301f)
                return $"TargetSpeed not updated. Expected ~300, got {testDrive.TargetSpeed}";

            // Negative Speed sollte Error liefern
            if (string.IsNullOrEmpty(negativeSpeedResult))
                return "Negative speed returned empty";

            JObject negParsed;
            try { negParsed = JObject.Parse(negativeSpeedResult); }
            catch (System.Exception e) { return $"Negative speed JSON parse error: {e.Message}"; }

            if (negParsed["error"] == null)
                return "Negative speed should return error";

            return "";
        }
    }
}

#endif
