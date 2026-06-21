// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests DriveTo wrap-around behavior when JumpToLowerLimitOnUpperLimit is enabled.
    //! Verifies that DriveTo can cross the 360->0 boundary (forward) and the 0->360 boundary (backward).
    public class TestDriveTo360Wrap : FeatureTestBase
    {
        protected override string TestName => "DriveTo wraps across 360/0 boundary in both forward and backward directions";

        private Drive forwardDrive;
        private Drive backwardDrive;
        private bool forwardJumpEventFired;
        private bool backwardJumpEventFired;

        private readonly float forwardTarget = 60f;
        private readonly float forwardStart = 300f;
        private readonly float backwardTarget = 300f;
        private readonly float backwardStart = 60f;
        private readonly float driveSpeed = 500f; // deg/s

        protected override void SetupTest()
        {
            MinTestTime = 2f; // 500 deg/s wrapping ~120° needs < 1s

            // === Forward wrap: 300° -> 360°/0° -> 60° ===
            var forwardGO = CreateGameObject("TestDriveWrap360Forward");
            forwardDrive = forwardGO.AddComponent<Drive>();
            forwardDrive.Direction = DIRECTION.RotationX;
            forwardDrive.TargetSpeed = driveSpeed;
            forwardDrive.UseLimits = true;
            forwardDrive.LowerLimit = 0f;
            forwardDrive.UpperLimit = 360f;
            forwardDrive.JumpToLowerLimitOnUpperLimit = true;

            forwardJumpEventFired = false;
            forwardDrive.OnJumpToLowerLimit += (drive) => { forwardJumpEventFired = true; };

            forwardDrive.CurrentPosition = forwardStart;
            float range = forwardDrive.UpperLimit - forwardDrive.LowerLimit;
            float forwardDestination = forwardTarget + range; // 60 + 360 = 420
            forwardDrive.DriveTo(forwardDestination);

            LogTest($"Forward Setup: Start={forwardStart}, DriveTo={forwardDestination}, Expected final={forwardTarget}");

            // === Backward wrap: 60° -> 0°/360° -> 300° ===
            var backwardGO = CreateGameObject("TestDriveWrap360Backward");
            backwardDrive = backwardGO.AddComponent<Drive>();
            backwardDrive.Direction = DIRECTION.RotationX;
            backwardDrive.TargetSpeed = driveSpeed;
            backwardDrive.UseLimits = true;
            backwardDrive.LowerLimit = 0f;
            backwardDrive.UpperLimit = 360f;
            backwardDrive.JumpToLowerLimitOnUpperLimit = true;

            backwardJumpEventFired = false;
            backwardDrive.OnJumpToUpperLimit += (drive) => { backwardJumpEventFired = true; };

            backwardDrive.CurrentPosition = backwardStart;
            float backwardDestination = backwardTarget - range; // 300 - 360 = -60
            backwardDrive.DriveTo(backwardDestination);

            LogTest($"Backward Setup: Start={backwardStart}, DriveTo={backwardDestination}, Expected final={backwardTarget}");
        }

        protected override string ValidateResults()
        {
            if (forwardDrive == null)
                return "Forward test drive was destroyed";
            if (backwardDrive == null)
                return "Backward test drive was destroyed";

            float tolerance = 1f;

            // === Forward wrap validation ===
            float forwardPos = forwardDrive.CurrentPosition;
            bool forwardAtTarget = forwardDrive.IsAtTarget;

            LogTest($"Forward Result: Position={forwardPos:F2}, IsAtTarget={forwardAtTarget}, JumpEventFired={forwardJumpEventFired}");

            if (!forwardAtTarget)
                return $"Forward: Drive did not reach target. Position: {forwardPos:F2}, Expected: {forwardTarget}";

            if (Mathf.Abs(forwardPos - forwardTarget) > tolerance)
                return $"Forward: Drive position {forwardPos:F2} not at expected target {forwardTarget} (tolerance {tolerance})";

            if (forwardPos < forwardDrive.LowerLimit || forwardPos > forwardDrive.UpperLimit)
                return $"Forward: Drive position {forwardPos:F2} outside limits [{forwardDrive.LowerLimit}, {forwardDrive.UpperLimit}]";

            if (!forwardJumpEventFired)
                return "Forward: OnJumpToLowerLimit event was not fired during wrap";

            // === Backward wrap validation ===
            float backwardPos = backwardDrive.CurrentPosition;
            bool backwardAtTarget = backwardDrive.IsAtTarget;

            LogTest($"Backward Result: Position={backwardPos:F2}, IsAtTarget={backwardAtTarget}, JumpEventFired={backwardJumpEventFired}");

            if (!backwardAtTarget)
                return $"Backward: Drive did not reach target. Position: {backwardPos:F2}, Expected: {backwardTarget}";

            if (Mathf.Abs(backwardPos - backwardTarget) > tolerance)
                return $"Backward: Drive position {backwardPos:F2} not at expected target {backwardTarget} (tolerance {tolerance})";

            if (backwardPos < backwardDrive.LowerLimit || backwardPos > backwardDrive.UpperLimit)
                return $"Backward: Drive position {backwardPos:F2} outside limits [{backwardDrive.LowerLimit}, {backwardDrive.UpperLimit}]";

            if (!backwardJumpEventFired)
                return "Backward: OnJumpToUpperLimit event was not fired during wrap";

            return ""; // Both tests passed
        }
    }
}
