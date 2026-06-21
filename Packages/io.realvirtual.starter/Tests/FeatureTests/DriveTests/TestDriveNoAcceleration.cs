// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests drive movement to target position without acceleration (constant speed)
    public class TestDriveNoAcceleration : FeatureTestBase
    {
        protected override string TestName => "Drive moves to target position at constant speed without acceleration";

        private Drive testDrive;
        private float targetPosition = 1000f; // mm
        private float targetSpeed = 500f; // mm/s

        protected override void SetupTest()
        {
            MinTestTime = 5f; // 500 mm/s over 1000mm = 2s + margin

            var driveGO = CreateGameObject("TestDriveNoAccel");

            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.LinearX;
            testDrive.UseAcceleration = false;

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

            LogTest($"Setup: Target={targetPosition}mm, Speed={targetSpeed}mm/s, UseAcceleration=false");
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

            if (testDrive.UseAcceleration)
                return "UseAcceleration should be false for this test";

            if (!testDrive.IsAtTarget)
                return $"Drive did not reach target. Position: {currentPosition:F2}mm, Target: {targetPosition}mm";

            float distanceToTarget = Mathf.Abs(currentPosition - targetPosition);
            if (distanceToTarget > 5f)
                return $"Drive not at target position. Distance: {distanceToTarget:F2}mm";

            return "";
        }
    }
}
