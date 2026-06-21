// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests interaction between sensors and drives using signals
    public class TestSensorDriveInteraction : FeatureTestBase
    {
        protected override string TestName => "Sensor detects passing object and triggers second drive via signal chain";

        private Drive drive1;
        private Drive drive2;
        private Sensor sensor;
        private PLCInputBool sensorOccupiedSignal;
        private float targetPosition1 = 200f; // mm
        private float targetPosition2 = 300f; // mm

        protected override void SetupTest()
        {
            // Drive 1 - triggers the sensor
            var drive1GO = CreateGameObject("Drive1");
            drive1GO.transform.position = TestPosition(0, 0, 0);
            drive1 = drive1GO.AddComponent<Drive>();
            drive1.Direction = DIRECTION.LinearX;

            var destMotor1 = drive1GO.AddComponent<Drive_DestinationMotor>();

            var speed1 = CreateTestObject<PLCOutputFloat>("Speed1");
            speed1.Value = 300f;
            destMotor1.TargetSpeed = speed1;

            var dest1 = CreateTestObject<PLCOutputFloat>("Destination1");
            dest1.Value = targetPosition1;
            destMotor1.Destination = dest1;

            var start1 = CreateTestObject<PLCOutputBool>("Start1");
            destMotor1.StartDrive = start1;

            // Visual cube tracked via CreatePrimitive - needs Rigidbody for OnTriggerEnter
            var cube1 = CreatePrimitive(PrimitiveType.Cube, "Drive1Visual", drive1.transform);
            cube1.transform.localPosition = Vector3.zero;
            cube1.transform.localScale = Vector3.one * 0.1f;
            var rb = cube1.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // Sensor at 150mm position
            var sensorGO = CreateGameObject("SensorPosition");
            sensorGO.transform.position = TestPosition(0.15f, 0, 0);

            var sensorCollider = sensorGO.AddComponent<BoxCollider>();
            sensorCollider.isTrigger = true;
            sensorCollider.size = new Vector3(0.05f, 0.2f, 0.2f);

            sensorGO.layer = LayerMask.NameToLayer("rvSensor");

            sensor = sensorGO.AddComponent<Sensor>();
            sensor.DisplayStatus = true;
            sensor.LimitSensorToTag = ""; // Ensure empty string, not null (no serialization at runtime)

            sensorOccupiedSignal = CreateTestObject<PLCInputBool>("SensorOccupied");
            sensor.SensorOccupied = sensorOccupiedSignal;

            // Drive 2 - triggered by sensor
            var drive2GO = CreateGameObject("Drive2");
            drive2GO.transform.position = TestPosition(0, 0, 0.5f);
            drive2 = drive2GO.AddComponent<Drive>();
            drive2.Direction = DIRECTION.LinearX;

            var destMotor2 = drive2GO.AddComponent<Drive_DestinationMotor>();

            var speed2 = CreateTestObject<PLCOutputFloat>("Speed2");
            speed2.Value = 200f;
            destMotor2.TargetSpeed = speed2;

            var dest2 = CreateTestObject<PLCOutputFloat>("Destination2");
            dest2.Value = targetPosition2;
            destMotor2.Destination = dest2;

            var start2 = CreateTestObject<PLCOutputBool>("Start2");
            destMotor2.StartDrive = start2;

            // Visual cube tracked via CreatePrimitive
            var cube2 = CreatePrimitive(PrimitiveType.Cube, "Drive2Visual", drive2.transform);
            cube2.transform.localPosition = Vector3.zero;
            cube2.transform.localScale = Vector3.one * 0.1f;
            cube2.GetComponent<Renderer>().material.color = Color.blue;

            // Controller that starts drive2 when sensor fires
            var driveScript = drive2GO.AddComponent<SimpleDriveController>();
            driveScript.sensorSignal = sensorOccupiedSignal;
            driveScript.startPLC = start2;

            // Set cube1 layer to rvMU so sensor's collision matrix allows detection
            cube1.layer = LayerMask.NameToLayer("rvMU");

            start1.Value = true;
        }

        protected override string ValidateResults()
        {
            if (drive1 == null || drive2 == null)
                return "One or more drives were destroyed";

            if (sensor == null)
                return "Sensor was destroyed";

            if (drive1.CurrentPosition < 140f)
                return $"Drive1 has not reached sensor yet: {drive1.CurrentPosition:F2}mm";

            if (!sensor.Occupied)
                return "Sensor did not detect passing object";

            if (!sensorOccupiedSignal.Value)
                return "Sensor signal not set to true";

            if (drive2.CurrentSpeed < 1f && drive2.CurrentPosition < 10f)
                return "Drive2 did not start when sensor was triggered";

            if (drive1.CurrentPosition < targetPosition1 * 0.8f)
                return $"Drive1 did not move enough: {drive1.CurrentPosition:F2}mm";

            return "";
        }

        //! Helper component that starts a drive when a sensor signal becomes true.
        //! Uses FixedUpdate for signal-based logic per project conventions.
        private class SimpleDriveController : MonoBehaviour
        {
            public PLCInputBool sensorSignal;
            public PLCOutputBool startPLC;
            private bool started;

            void FixedUpdate()
            {
                if (!started && sensorSignal != null && sensorSignal.Value && startPLC != null)
                {
                    startPLC.Value = true;
                    started = true;
                }
            }
        }
    }
}
