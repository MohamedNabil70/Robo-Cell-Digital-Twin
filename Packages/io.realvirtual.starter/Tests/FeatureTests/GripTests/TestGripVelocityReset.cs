// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests that velocity is zeroed when MU is placed with PlaceMode.Physics (prevents flying MUs).
    public class TestGripVelocityReset : FeatureTestBase
    {
        protected override string TestName => "Grip Velocity Reset on Place - MU does not fly away";

        private MU mu;
        private Grip grip;
        private GameObject gripGO;

        protected override void SetupTest()
        {
            MinTestTime = 0.5f;

            // Floor to catch MU after place (prevents unlimited freefall)
            var floor = CreatePrimitive(PrimitiveType.Cube, "Floor");
            floor.transform.position = TestPosition(5f, 0f, 5f);
            floor.transform.localScale = new Vector3(5f, 0.1f, 5f);
            floor.layer = LayerMask.NameToLayer("rvTransport");

            // Grip above the floor
            gripGO = CreateGameObject("TestGrip");
            gripGO.transform.position = TestPosition(5f, 2f, 5f);
            var gripRb = gripGO.AddComponent<Rigidbody>();
            gripRb.isKinematic = true;
            grip = gripGO.AddComponent<Grip>();
            grip.PlaceMode = PlaceMode.Physics;
            grip.GripRange = 10f;
            grip.OneBitControl = false; // prevent FixedUpdate interference

            // MU at grip position
            var muGO = CreatePrimitive(PrimitiveType.Cube, "TestMU");
            muGO.transform.position = TestPosition(5f, 2f, 5f);
            muGO.transform.localScale = Vector3.one * 0.05f;
            muGO.layer = LayerMask.NameToLayer("rvMU");
            var muRb = muGO.AddComponent<Rigidbody>();
            muRb.isKinematic = false;
            mu = muGO.AddComponent<MU>();
            mu.Rigidbody = muRb;

            // Fix now, move grip, place in ValidateResults
            grip.Fix(mu);
            gripGO.transform.position = TestPosition(5f, 2f, 5f);
        }

        protected override string ValidateResults()
        {
            if (mu == null) return "MU not found";

            // Place now — velocity reset should zero out any kinematic-derived velocity
            grip.Place();

            var rb = mu.Rigidbody;
            if (rb == null) return "MU has no Rigidbody";

            // Immediately after Place, velocity should be near zero (reset worked)
            float speed = rb.linearVelocity.magnitude;
            if (speed > 1.0f)
                return $"MU velocity too high after place: {speed:F2} m/s (expected near zero - velocity reset failed)";

            return "";
        }
    }
}
