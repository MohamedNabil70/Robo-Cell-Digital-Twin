// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    //! Tests LogicStep serial and parallel containers working together.
    //! Sequence: Drive1 to 500mm (serial) -> Drive1 to 1000mm + Drive2 to 800mm (parallel) -> signal done.
    //! Validates that serial steps execute in order and parallel steps run simultaneously.
    public class TestLogicStepSerialParallel : FeatureTestBase
    {
        protected override string TestName => "Serial container executes steps in order with embedded parallel container";

        private Drive drive1;
        private Drive drive2;
        private PLCInputBool doneSignal;
        private float serialTarget = 500f; // mm
        private float parallelTarget1 = 1000f; // mm
        private float parallelTarget2 = 800f; // mm

        protected override void SetupTest()
        {
            MinTestTime = 15f;

            // Create two linear drives
            var drive1GO = CreateGameObject("Drive1");
            drive1 = drive1GO.AddComponent<Drive>();
            drive1.Direction = DIRECTION.LinearX;
            drive1.TargetSpeed = 300f;

            var drive2GO = CreateGameObject("Drive2");
            drive2 = drive2GO.AddComponent<Drive>();
            drive2.Direction = DIRECTION.LinearX;
            drive2.TargetSpeed = 300f;

            // Signals
            var startSignal = CreateTestObject<PLCOutputBool>("StartSignal");
            doneSignal = CreateTestObject<PLCInputBool>("DoneSignal");

            // Build LogicStep hierarchy:
            // SerialContainer (root)
            //   ├── Step0: WaitForSignalBool startSignal = true
            //   ├── Step1: DriveTo drive1 -> 500
            //   ├── ParallelContainer
            //   │     ├── Step2a: DriveTo drive1 -> 1000
            //   │     └── Step2b: DriveTo drive2 -> 800
            //   ├── Step3: SetSignalBool done = true
            //   └── Step4: SetSignalBool startSignal = false  (blocks loop restart)

            var serialGO = CreateGameObject("SerialSequence");
            var serial = serialGO.AddComponent<LogicStep_SerialContainer>();

            // Step 0: Wait for start signal
            var step0GO = CreateGameObject("Step0_WaitForStart");
            step0GO.transform.SetParent(serialGO.transform);
            var step0 = step0GO.AddComponent<LogicStep_WaitForSignalBool>();
            step0.Signal = startSignal;
            step0.WaitForTrue = true;

            // Step 1: Drive1 to 500mm
            var step1GO = CreateGameObject("Step1_DriveTo500");
            step1GO.transform.SetParent(serialGO.transform);
            var step1 = step1GO.AddComponent<LogicStep_DriveTo>();
            step1.drive = drive1;
            step1.Destination = serialTarget;

            // Step 2: Parallel container
            var parallelGO = CreateGameObject("Step2_Parallel");
            parallelGO.transform.SetParent(serialGO.transform);
            var parallel = parallelGO.AddComponent<LogicStep_ParallelContainer>();

            // Step 2a: Drive1 to 1000mm (parallel)
            var step2aGO = CreateGameObject("Step2a_DriveTo1000");
            step2aGO.transform.SetParent(parallelGO.transform);
            var step2a = step2aGO.AddComponent<LogicStep_DriveTo>();
            step2a.drive = drive1;
            step2a.Destination = parallelTarget1;

            // Step 2b: Drive2 to 800mm (parallel)
            var step2bGO = CreateGameObject("Step2b_DriveTo800");
            step2bGO.transform.SetParent(parallelGO.transform);
            var step2b = step2bGO.AddComponent<LogicStep_DriveTo>();
            step2b.drive = drive2;
            step2b.Destination = parallelTarget2;

            // Step 3: Set done signal
            var step3GO = CreateGameObject("Step3_SetDone");
            step3GO.transform.SetParent(serialGO.transform);
            var step3 = step3GO.AddComponent<LogicStep_SetSignalBool>();
            step3.Signal = doneSignal;
            step3.SetToTrue = true;

            // Step 4: Reset start signal (blocks the loop from restarting)
            var step4GO = CreateGameObject("Step4_ResetStart");
            step4GO.transform.SetParent(serialGO.transform);
            var step4 = step4GO.AddComponent<LogicStep_SetSignalBool>();
            step4.Signal = startSignal;
            step4.SetToTrue = false;

            // Kick off the sequence
            startSignal.Value = true;

            LogTest($"Setup: Serial[WaitStart, DriveTo({serialTarget}), Parallel[DriveTo({parallelTarget1}), DriveTo({parallelTarget2})], SetDone, ResetStart]");
        }

        protected override string ValidateResults()
        {
            if (drive1 == null || drive2 == null)
                return "One or more drives were destroyed";

            float pos1 = drive1.CurrentPosition;
            float pos2 = drive2.CurrentPosition;
            bool done = doneSignal != null && doneSignal.Value;

            LogTest($"Result: Drive1={pos1:F2}mm, Drive2={pos2:F2}mm, Done={done}");

            // Check that the done signal was set (entire sequence completed)
            if (!done)
                return "Sequence did not complete - done signal not set";

            // Check Drive1 reached parallel target (1000mm), not just serial target (500mm)
            if (Mathf.Abs(pos1 - parallelTarget1) > 10f)
                return $"Drive1 not at parallel target. Position: {pos1:F2}mm, Expected: {parallelTarget1}mm";

            // Check Drive2 reached its parallel target (800mm)
            if (Mathf.Abs(pos2 - parallelTarget2) > 10f)
                return $"Drive2 not at parallel target. Position: {pos2:F2}mm, Expected: {parallelTarget2}mm";

            // Verify Drive1 passed through serial target (it should be beyond 500 now)
            if (pos1 < serialTarget)
                return $"Drive1 did not pass serial target. Position: {pos1:F2}mm, Serial target was: {serialTarget}mm";

            return "";
        }
    }
}
