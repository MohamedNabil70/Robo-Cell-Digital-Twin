// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Regression test: DriveTo within limits should work normally regardless of JumpToLowerLimitOnUpperLimit setting.
    //! Ensures the wrap-around fix does not affect normal operation within limits.
    public class TestDriveToWithinLimits : FeatureTestBase
    {
        protected override string TestName => "DriveTo reaches target within limits without triggering wrap-around";

        private Drive testDrive;
        private float targetPosition = 180f;
        private float startPosition = 50f;
        private float driveSpeed = 500f; // deg/s

        protected override void SetupTest()
        {
            MinTestTime = 2f; // 500 deg/s over 130° needs < 1s

            // Create drive GameObject
            var driveGO = CreateGameObject("TestDriveWithinLimits");

            // Add Drive component with jump enabled but target within limits
            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.RotationX;
            testDrive.TargetSpeed = driveSpeed;
            testDrive.UseLimits = true;
            testDrive.LowerLimit = 0f;
            testDrive.UpperLimit = 360f;
            testDrive.JumpToLowerLimitOnUpperLimit = true;

            // Set starting position
            testDrive.CurrentPosition = startPosition;

            // DriveTo a position within limits (should not trigger jump)
            testDrive.DriveTo(targetPosition);

            LogTest($"Setup: Start={startPosition}, DriveTo={targetPosition}");
        }

        protected override string ValidateResults()
        {
            if (testDrive == null)
                return "Test drive was destroyed";

            float currentPos = testDrive.CurrentPosition;
            bool isAtTarget = testDrive.IsAtTarget;

            LogTest($"Result: Position={currentPos:F2}, IsAtTarget={isAtTarget}");

            // Test 1: Drive must have reached target
            if (!isAtTarget)
                return $"Drive did not reach target. Position: {currentPos:F2}, Expected: {targetPosition}";

            // Test 2: Position must be at target (within tolerance)
            float tolerance = 1f;
            if (Mathf.Abs(currentPos - targetPosition) > tolerance)
                return $"Drive position {currentPos:F2} not at expected target {targetPosition} (tolerance {tolerance})";

            // Test 3: Position must be within limits
            if (currentPos < testDrive.LowerLimit || currentPos > testDrive.UpperLimit)
                return $"Drive position {currentPos:F2} outside limits [{testDrive.LowerLimit}, {testDrive.UpperLimit}]";

            // Test 4: Position should not have wrapped (should be close to direct path)
            if (currentPos < startPosition)
                return $"Drive position {currentPos:F2} appears to have wrapped unexpectedly (started at {startPosition})";

            return ""; // Test passed
        }
    }
}
