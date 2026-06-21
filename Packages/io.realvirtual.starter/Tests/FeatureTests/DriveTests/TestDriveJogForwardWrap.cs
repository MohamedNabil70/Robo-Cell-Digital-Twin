// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Regression test: Jog-Forward wrap-around should still work correctly after the DriveTo fix.
    //! Ensures the DriveTo changes do not break existing Jog functionality.
    public class TestDriveJogForwardWrap : FeatureTestBase
    {
        protected override string TestName => "Jog forward wraps correctly from upper limit back to lower limit (360 to 0)";

        private Drive testDrive;
        private bool jumpEventFired;
        private float startPosition = 350f;
        private float jogSpeed = 200f; // deg/s
        private float expectedFinalPosition = 10f; // After wrapping from 350 to ~10

        protected override void SetupTest()
        {
            MinTestTime = 2f; // 200 deg/s jogging ~20° past boundary needs < 1s

            // Create drive GameObject
            var driveGO = CreateGameObject("TestJogWrap");

            // Add Drive component with 360 rotation limits
            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.RotationX;
            testDrive.TargetSpeed = jogSpeed;
            testDrive.UseLimits = true;
            testDrive.LowerLimit = 0f;
            testDrive.UpperLimit = 360f;
            testDrive.JumpToLowerLimitOnUpperLimit = true;

            // Track OnJumpToLowerLimit event
            jumpEventFired = false;
            testDrive.OnJumpToLowerLimit += (drive) => { jumpEventFired = true; };

            // Set starting position near upper limit (use StartPosition so Drive.Start() initializes correctly)
            testDrive.StartPosition = startPosition;

            // Start jogging forward (will cross UpperLimit and wrap)
            testDrive.JogForward = true;

            LogTest($"Setup: Start={startPosition}, JogForward=true, Expected wrap to ~{expectedFinalPosition}");
        }

        protected override string ValidateResults()
        {
            if (testDrive == null)
                return "Test drive was destroyed";

            float currentPos = testDrive.CurrentPosition;

            LogTest($"Result: Position={currentPos:F2}, JumpEventFired={jumpEventFired}");

            // Test 1: Jump event must have fired (wrap occurred)
            if (!jumpEventFired)
                return "OnJumpToLowerLimit event was not fired during jog wrap";

            // Test 2: Position must be within limits (wrapped correctly)
            if (currentPos < testDrive.LowerLimit || currentPos > testDrive.UpperLimit)
                return $"Drive position {currentPos:F2} outside limits [{testDrive.LowerLimit}, {testDrive.UpperLimit}]";

            return ""; // Test passed
        }
    }
}
