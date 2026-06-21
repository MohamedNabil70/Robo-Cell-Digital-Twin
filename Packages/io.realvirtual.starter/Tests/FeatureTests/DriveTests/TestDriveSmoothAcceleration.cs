// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
#if REALVIRTUAL_PROFESSIONAL
    //! Tests drive movement to target position using smooth S-curve acceleration profile
    public class TestDriveSmoothAcceleration : FeatureTestBase
    {
        protected override string TestName => "Drive moves to target using smooth S-curve acceleration (Professional)";

        private Drive testDrive;
        private float targetPosition = 2000f; // mm
        private float targetSpeed = 500f; // mm/s
        private float acceleration = 200f; // mm/s²
        private float jerk = 1000f; // mm/s³

        protected override void SetupTest()
        {
            MinTestTime = 25f; // Smooth S-curve profile is significantly slower than linear ramp

            var driveGO = CreateGameObject("TestDriveSmoothAccel");

            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.LinearX;
            testDrive.Acceleration = acceleration;
            testDrive.UseAcceleration = true;
            testDrive.SmoothAcceleration = true;
            testDrive.Jerk = jerk;

            var destinationMotor = driveGO.AddComponent<Drive_DestinationMotor>();

            var speedSignal = CreateTestObject<PLCOutputFloat>("SpeedSignal");
            speedSignal.Value = targetSpeed;
            destinationMotor.TargetSpeed = speedSignal;

            var destinationSignal = CreateTestObject<PLCOutputFloat>("DestinationSignal");
            destinationSignal.Value = targetPosition;
            destinationMotor.Destination = destinationSignal;

            var startSignal = CreateTestObject<PLCOutputBool>("StartSignal");
            destinationMotor.StartDrive = startSignal;

            startSignal.Value = true;

            LogTest($"Setup: Target={targetPosition}mm, Speed={targetSpeed}mm/s, Accel={acceleration}mm/s², Jerk={jerk}mm/s³, SmoothAcceleration=true");
        }

        protected override string ValidateResults()
        {
            if (testDrive == null)
                return "Test drive was destroyed";

            float currentPosition = testDrive.CurrentPosition;
            float currentSpeed = testDrive.CurrentSpeed;

            LogTest($"Result: Position={currentPosition:F2}mm, Speed={currentSpeed:F2}mm/s");

            if (currentPosition < 100f)
                return $"Drive did not move significantly. Position: {currentPosition:F2}mm";

            if (!testDrive.UseAcceleration)
                return "UseAcceleration should be true for this test";

            if (!testDrive.SmoothAcceleration)
                return "SmoothAcceleration should be true for this test";

            if (!testDrive.IsAtTarget)
                return $"Drive did not reach target. Position: {currentPosition:F2}mm, Target: {targetPosition}mm";

            float distanceToTarget = Mathf.Abs(currentPosition - targetPosition);
            if (distanceToTarget > 5f)
                return $"Drive not at target position. Distance: {distanceToTarget:F2}mm";

            if (currentSpeed > 10f)
                return $"Drive at target but still moving: {currentSpeed:F2} mm/s";

            return "";
        }
    }
#endif
}
