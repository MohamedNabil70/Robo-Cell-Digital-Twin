// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests that with nothing below the grip on rvMU/rvTransport layers, the MU stays kinematic (no physics).
    public class TestGripAutoPlaceKinematic : FeatureTestBase
    {
        protected override string TestName => "Grip Auto-Place Kinematic - nothing below stays kinematic";

        private MU mu;
        private Vector3 placePosition;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            // Grip high in the air — nothing below on rvMU or rvTransport layer
            var gripGO = CreateGameObject("TestGrip");
            gripGO.transform.position = TestPosition(5f, 5f, 5f);
            var rb = gripGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var grip = gripGO.AddComponent<Grip>();
            grip.PlaceMode = PlaceMode.Auto;
            grip.GripRange = 10f;

            // MU at grip position
            var muGO = CreatePrimitive(PrimitiveType.Cube, "TestMU");
            muGO.transform.position = TestPosition(5f, 5f, 5f);
            muGO.transform.localScale = Vector3.one * 0.05f;
            muGO.layer = LayerMask.NameToLayer("rvMU");
            var muRb = muGO.AddComponent<Rigidbody>();
            muRb.isKinematic = false;
            mu = muGO.AddComponent<MU>();
            mu.Rigidbody = muRb;

            // Manually fix and place to test AutoPlace fallback
            grip.Fix(mu);
            placePosition = mu.transform.position;
            grip.Place();
        }

        protected override string ValidateResults()
        {
            if (mu == null) return "MU not found";
            var rb = mu.Rigidbody;
            if (rb == null) return "MU has no Rigidbody";

            // MU should be kinematic (Auto fallback when nothing below)
            if (!rb.isKinematic)
                return "MU is not kinematic - should stay in place since nothing is below";

            return "";
        }
    }
}
