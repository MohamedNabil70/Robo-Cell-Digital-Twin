// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests backward compatibility: existing Grip+Sensor setups continue to work as before.
    //! When PartToGrip sensor is assigned, the sensor-based pick has priority over OverlapSphere.
    public class TestGripBackwardCompat : FeatureTestBase
    {
        protected override string TestName => "Grip backward compat - sensor-based pick still works";

        private Grip grip;
        private MU mu;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            // Grip with Sensor (legacy setup) — positioned in center of test area
            var gripGO = CreateGameObject("TestGrip");
            gripGO.transform.position = TestPosition(5f, 0.5f, 5f);
            var rb = gripGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            grip = gripGO.AddComponent<Grip>();
            grip.NoPhysicsWhenPlaced = true; // legacy property still works
            grip.OneBitControl = false; // prevent FixedUpdate from auto-placing

            // Sensor child
            var sensorGO = CreateGameObject("Sensor", gripGO.transform);
            sensorGO.transform.localPosition = Vector3.zero;
            var col = sensorGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = Vector3.one * 0.3f;
            sensorGO.layer = LayerMask.NameToLayer("rvSensor");
            var sensor = sensorGO.AddComponent<Sensor>();
            sensor.LimitSensorToTag = "";
            grip.PartToGrip = sensor;

            // MU placed at sensor position — physics trigger needs frames to detect
            var muGO = CreatePrimitive(PrimitiveType.Cube, "TestMU");
            muGO.transform.position = TestPosition(5f, 0.5f, 5f);
            muGO.transform.localScale = Vector3.one * 0.05f;
            muGO.layer = LayerMask.NameToLayer("rvMU");
            var muRb = muGO.AddComponent<Rigidbody>();
            muRb.isKinematic = true; // keep kinematic so MU stays in sensor range
            mu = muGO.AddComponent<MU>();
            mu.Rigidbody = muRb;

            // Don't call Fix() here — sensor needs physics frames to fire OnTriggerEnter.
            // Pick() will be called in ValidateResults after sensor has detected the MU.
        }

        protected override string ValidateResults()
        {
            if (mu == null) return "MU not found";
            if (grip == null) return "Grip not found";

            // Pick via sensor path — sensor should have detected the MU by now
            grip.Pick();

            // MU should be gripped
            if (mu.FixedBy == null)
                return "MU was not fixed - backward compat broken";

            // MU should be child of grip (kinematic attachment)
            if (!mu.transform.IsChildOf(grip.transform))
                return $"MU is not child of Grip - parent is '{mu.transform.parent?.name ?? "null"}'";

            // Test place — with NoPhysicsWhenPlaced=true and PlaceMode=Auto the old path should apply
            grip.Place();

            // After place with NoPhysicsWhenPlaced=true, MU should be kinematic (old behavior)
            if (mu.Rigidbody != null && !mu.Rigidbody.isKinematic)
                return "NoPhysicsWhenPlaced=true but MU got physics on place - backward compat broken";

            return "";
        }
    }
}
