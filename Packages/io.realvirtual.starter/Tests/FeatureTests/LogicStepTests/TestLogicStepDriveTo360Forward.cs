// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests LogicStep_DriveTo with Direction=Forward on a 360° continuous rotation drive.
    //! Drives from 300° to 60° by moving forward through the 360/0 boundary.
    //! Uses a start signal and serial container to prevent the LogicStep loop from restarting.
    public class TestLogicStepDriveTo360Forward : FeatureTestBase
    {
        protected override string TestName => "LogicStep DriveTo moves forward from 300 to 60 across 360/0 boundary";

        private Drive testDrive;
        private PLCInputBool doneSignal;
        private float startPosition = 300f;
        private float targetPosition = 60f;

        protected override void SetupTest()
        {
            MinTestTime = 8f;

            // Create drive with 360° rotation and wrap-around
            var driveGO = CreateGameObject("TestDrive360");
            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.RotationX;
            testDrive.TargetSpeed = 200f;
            testDrive.UseLimits = true;
            testDrive.LowerLimit = 0f;
            testDrive.UpperLimit = 360f;
            testDrive.JumpToLowerLimitOnUpperLimit = true;
            testDrive.StartPosition = startPosition;

            // Signals
            var startSignal = CreateTestObject<PLCOutputBool>("StartSignal");
            doneSignal = CreateTestObject<PLCInputBool>("DoneSignal");

            // Build LogicStep hierarchy:
            // SerialContainer
            //   ├── WaitForSignalBool startSignal = true
            //   ├── DriveTo 60° Direction=Forward
            //   ├── SetSignalBool done = true
            //   └── SetSignalBool startSignal = false  (blocks loop restart)

            var serialGO = CreateGameObject("SerialSequence");
            serialGO.AddComponent<LogicStep_SerialContainer>();

            var waitGO = CreateGameObject("WaitForStart");
            waitGO.transform.SetParent(serialGO.transform);
            var wait = waitGO.AddComponent<LogicStep_WaitForSignalBool>();
            wait.Signal = startSignal;
            wait.WaitForTrue = true;

            var stepGO = CreateGameObject("StepDriveTo60");
            stepGO.transform.SetParent(serialGO.transform);
            var driveToStep = stepGO.AddComponent<LogicStep_DriveTo>();
            driveToStep.drive = testDrive;
            driveToStep.Destination = targetPosition;
            driveToStep.Direction = DriveToDirection.Forward;

            var doneGO = CreateGameObject("SetDone");
            doneGO.transform.SetParent(serialGO.transform);
            var setDone = doneGO.AddComponent<LogicStep_SetSignalBool>();
            setDone.Signal = doneSignal;
            setDone.SetToTrue = true;

            var resetGO = CreateGameObject("ResetStart");
            resetGO.transform.SetParent(serialGO.transform);
            var resetStart = resetGO.AddComponent<LogicStep_SetSignalBool>();
            resetStart.Signal = startSignal;
            resetStart.SetToTrue = false;

            // Kick off the sequence
            startSignal.Value = true;

            LogTest($"Setup: Start={startPosition}, Target={targetPosition}, Direction=Forward");
        }

        protected override string ValidateResults()
        {
            if (testDrive == null)
                return "Test drive was destroyed";

            float currentPos = testDrive.CurrentPosition;
            bool done = doneSignal != null && doneSignal.Value;

            LogTest($"Result: Position={currentPos:F2}, IsAtTarget={testDrive.IsAtTarget}, Done={done}");

            if (!done)
                return $"Sequence did not complete. Position: {currentPos:F2}";

            float distanceToTarget = Mathf.Abs(currentPos - targetPosition);
            if (distanceToTarget > 5f)
                return $"Drive not at target position. Position: {currentPos:F2}, Expected: {targetPosition}, Distance: {distanceToTarget:F2}";

            return "";
        }
    }
}
