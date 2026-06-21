#if REALVIRTUAL_MCP
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using realvirtual.MCP;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace realvirtual.MCP.Tests
{
    //! Validates that drive_to command moves drive towards target position
    public class TestMcpDriveToMovement : FeatureTestBase
    {
        protected override string TestName => "MCP drive_to moves drive towards target position";

        private Drive testDrive;
        private float targetPosition = 500f;

        protected override void SetupTest()
        {
            MinTestTime = 5f;

            var driveGO = CreateGameObject("McpDriveToTest");
            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.LinearX;
            testDrive.TargetSpeed = 200f;

            var cube = CreatePrimitive(PrimitiveType.Cube, "DriveVisual", driveGO.transform);
            cube.transform.localScale = Vector3.one * 0.1f;

            var registry = new McpToolRegistry();
            registry.DiscoverTools();

            var result = registry.CallTool("drive_to", new Dictionary<string, object>
            {
                ["name"] = "McpDriveToTest",
                ["position"] = targetPosition
            });

            LogTest($"drive_to result: {result}");
        }

        protected override string ValidateResults()
        {
            if (testDrive == null)
                return "Test drive was destroyed";

            float pos = testDrive.CurrentPosition;

            if (pos < 50f)
                return $"Drive did not move. Position: {pos:F2}mm";

            if (pos < targetPosition * 0.5f)
                return $"Drive did not reach near target. Position: {pos:F2}mm, Target: {targetPosition}mm";

            LogTest($"Drive reached position {pos:F2}mm (target: {targetPosition}mm)");
            return "";
        }
    }
}

#endif
