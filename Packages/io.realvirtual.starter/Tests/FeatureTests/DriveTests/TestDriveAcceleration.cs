// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests drive acceleration and deceleration behavior with Drive_DestinationMotor (non-smooth / linear ramp)
    public class TestDriveAcceleration : FeatureTestBase
    {
        protected override string TestName => "Drive moves to target using linear acceleration ramp (non-smooth)";

        private Drive testDrive;
        private float targetPosition = 2000f; // mm
        private float targetSpeed = 500f; // mm/s
        private float acceleration = 200f; // mm/s²

        protected override void SetupTest()
        {
            MinTestTime = 10f; // 500 mm/s with 200 mm/s² accel over 2000mm needs ~6s

            var driveGO = CreateGameObject("TestDriveAccel");

            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.LinearX;
            testDrive.Acceleration = acceleration;
            testDrive.UseAcceleration = true;
            testDrive.SmoothAcceleration = false;

            var destinationMotor = driveGO.AddComponent<Drive_DestinationMotor>();

            var speedSignal = CreateTestObject<PLCOutputFloat>("SpeedSignal");
            speedSignal.Value = targetSpeed;
            destinationMotor.TargetSpeed = speedSignal;

            var destinationSignal = CreateTestObject<PLCOutputFloat>("DestinationSignal");
            destinationSignal.Value = targetPosition;
            destinationMotor.Destination = destinationSignal;

            var startSignal = CreateTestObject<PLCOutputBool>("StartSignal");
            destinationMotor.StartDrive = startSignal;

            // Visual cube tracked via CreatePrimitive helper
            var cube = CreatePrimitive(PrimitiveType.Cube, "DriveVisual", driveGO.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one * 0.1f;

            startSignal.Value = true;
        }

        protected override string ValidateResults()
        {
            if (testDrive == null)
                return "Test drive was destroyed";

            float currentPosition = testDrive.CurrentPosition;
            float currentSpeed = testDrive.CurrentSpeed;

            LogTest($"Drive test completed - Position: {currentPosition:F2}mm, Speed: {currentSpeed:F2}mm/s");

            if (currentPosition < 100f)
                return $"Drive did not move significantly. Position: {currentPosition:F2}mm";

            float distanceToTarget = Mathf.Abs(currentPosition - targetPosition);

            if (currentPosition < targetPosition * 0.8f)
                return $"Drive did not move enough. Position: {currentPosition:F2}mm, Expected closer to: {targetPosition}mm";

            if (!testDrive.UseAcceleration)
                return "UseAcceleration should be true for this test";

            if (testDrive.SmoothAcceleration)
                return "SmoothAcceleration should be false for this test";

            if (distanceToTarget < 50f && currentSpeed > 10f)
                return $"Drive near target but still moving fast: {currentSpeed:F2} mm/s";

            return "";
        }
    }
}
