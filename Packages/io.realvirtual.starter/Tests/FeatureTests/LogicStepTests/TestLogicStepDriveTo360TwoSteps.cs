// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests the customer scenario: DriveTo300 (Automatic) followed by DriveTo200 (Forward).
    //! After reaching 300, the drive must go forward through the 360/0 boundary to reach 200,
    //! not backward from 300 to 200.
    public class TestLogicStepDriveTo360TwoSteps : FeatureTestBase
    {
        protected override string TestName =>
            "LogicStep DriveTo300 Automatic then DriveTo200 Forward wraps through 360/0 boundary";

        private Drive testDrive;
        private PLCInputBool doneSignal;
        private bool jumpToLowerLimitFired;

        protected override void SetupTest()
        {
            // Step1: 300° at 200°/s = 1.5s, Step2: 260° forward at 200°/s = 1.3s → ~3s, 8s safety
            MinTestTime = 8f;

            // Create drive: 0-360 with JumpToLowerLimitOnUpperLimit
            var driveGO = CreateGameObject("MasterDrive360");
            testDrive = driveGO.AddComponent<Drive>();
            testDrive.Direction = DIRECTION.RotationX;
            testDrive.TargetSpeed = 200f;
            testDrive.UseLimits = true;
            testDrive.LowerLimit = 0f;
            testDrive.UpperLimit = 360f;
            testDrive.JumpToLowerLimitOnUpperLimit = true;
            testDrive.StartPosition = 0f;

            // Track wrap-around event (proves the drive went forward through 360/0, not backward)
            jumpToLowerLimitFired = false;
            testDrive.OnJumpToLowerLimit += (d) => { jumpToLowerLimitFired = true; };

            // Signals
            var startSignal = CreateTestObject<PLCOutputBool>("StartSignal");
            doneSignal = CreateTestObject<PLCInputBool>("DoneSignal");

            // Build LogicStep hierarchy:
            // SerialContainer
            //   ├── WaitForSignalBool startSignal = true
            //   ├── DriveTo 300° Direction=Automatic
            //   ├── DriveTo 200° Direction=Forward  (must go 300→360→0→200)
            //   ├── SetSignalBool done = true
            //   └── SetSignalBool startSignal = false  (blocks loop restart)

            var serialGO = CreateGameObject("SerialSequence");
            serialGO.AddComponent<LogicStep_SerialContainer>();

            // Step: Wait for start
            var waitGO = CreateGameObject("WaitForStart");
            waitGO.transform.SetParent(serialGO.transform);
            var wait = waitGO.AddComponent<LogicStep_WaitForSignalBool>();
            wait.Signal = startSignal;
            wait.WaitForTrue = true;

            // Step 1: DriveTo 300° (Automatic)
            var step1GO = CreateGameObject("DriveTo300_Automatic");
            step1GO.transform.SetParent(serialGO.transform);
            var step1 = step1GO.AddComponent<LogicStep_DriveTo>();
            step1.drive = testDrive;
            step1.Destination = 300f;
            step1.Direction = DriveToDirection.Automatic;

            // Step 2: DriveTo 200° (Forward) - must wrap through 360→0
            var step2GO = CreateGameObject("DriveTo200_Forward");
            step2GO.transform.SetParent(serialGO.transform);
            var step2 = step2GO.AddComponent<LogicStep_DriveTo>();
            step2.drive = testDrive;
            step2.Destination = 200f;
            step2.Direction = DriveToDirection.Forward;

            // Step: Set done signal
            var doneGO = CreateGameObject("SetDone");
            doneGO.transform.SetParent(serialGO.transform);
            var setDone = doneGO.AddComponent<LogicStep_SetSignalBool>();
            setDone.Signal = doneSignal;
            setDone.SetToTrue = true;

            // Step: Reset start to prevent loop
            var resetGO = CreateGameObject("ResetStart");
            resetGO.transform.SetParent(serialGO.transform);
            var resetStart = resetGO.AddComponent<LogicStep_SetSignalBool>();
            resetStart.Signal = startSignal;
            resetStart.SetToTrue = false;

            // Kick off
            startSignal.Value = true;

            LogTest("Setup: Start=0, Step1=DriveTo300(Automatic), Step2=DriveTo200(Forward)");
        }

        protected override string ValidateResults()
        {
            if (testDrive == null)
                return "Test drive was destroyed";

            float currentPos = testDrive.CurrentPosition;
            bool done = doneSignal != null && doneSignal.Value;

            LogTest($"Result: Position={currentPos:F2}, IsAtTarget={testDrive.IsAtTarget}, Done={done}, " +
                    $"JumpEvent={jumpToLowerLimitFired}");

            if (!done)
                return $"Sequence did not complete. Position: {currentPos:F2}, JumpEvent: {jumpToLowerLimitFired}";

            // The drive MUST have wrapped through 360→0 (proving it went forward, not backward)
            if (!jumpToLowerLimitFired)
                return "OnJumpToLowerLimit was NOT fired - drive went backward (300->200) instead of forward (300->360->0->200)";

            // Final position should be 200
            float distanceToTarget = Mathf.Abs(currentPos - 200f);
            if (distanceToTarget > 5f)
                return $"Drive not at target 200. Position: {currentPos:F2}, Distance: {distanceToTarget:F2}";

            return "";
        }
    }
}
