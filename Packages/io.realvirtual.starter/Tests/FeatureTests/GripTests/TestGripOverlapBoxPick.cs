// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests that Grip finds and picks the nearest MU via OverlapSphere when no Sensor is assigned.
    public class TestGripOverlapBoxPick : FeatureTestBase
    {
        protected override string TestName => "Grip OverlapSphere picks nearest MU without Sensor";

        private Grip grip;
        private MU nearMU;
        private MU farMU;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            // Grip without Sensor assigned — positioned in center of test area
            var gripGO = CreateGameObject("TestGrip");
            gripGO.transform.position = TestPosition(5f, 0.5f, 5f);
            var rb = gripGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            grip = gripGO.AddComponent<Grip>();
            grip.GripRange = 300f; // 300mm search radius
            grip.OneBitControl = false; // prevent FixedUpdate interference
            // PartToGrip intentionally left null — OverlapSphere mode

            // Near MU (should be picked): 5cm away
            var nearGO = CreatePrimitive(PrimitiveType.Cube, "NearMU");
            nearGO.transform.position = TestPosition(5f, 0.5f, 5.05f);
            nearGO.transform.localScale = Vector3.one * 0.05f;
            nearGO.layer = LayerMask.NameToLayer("rvMU");
            var nearRb = nearGO.AddComponent<Rigidbody>();
            nearRb.isKinematic = true; // keep kinematic so MU doesn't fall before pick
            nearMU = nearGO.AddComponent<MU>();
            nearMU.Rigidbody = nearRb;

            // Far MU (should NOT be picked): 20cm away — within GripRange but farther
            var farGO = CreatePrimitive(PrimitiveType.Cube, "FarMU");
            farGO.transform.position = TestPosition(5f, 0.5f, 5.2f);
            farGO.transform.localScale = Vector3.one * 0.05f;
            farGO.layer = LayerMask.NameToLayer("rvMU");
            var farRb = farGO.AddComponent<Rigidbody>();
            farRb.isKinematic = true; // keep kinematic so MU doesn't fall before pick
            farMU = farGO.AddComponent<MU>();
            farMU.Rigidbody = farRb;

            // Don't set PickObjects here — OverlapSphere needs physics to register colliders first.
            // Pick() will be called in ValidateResults after physics frames have run.
        }

        protected override string ValidateResults()
        {
            if (grip == null) return "Grip not found";

            // Only pick once (ValidateResults may be called multiple times)
            if (nearMU.FixedBy == null)
            {
                Physics.SyncTransforms();
                grip.Pick();
            }

            // Near MU should be gripped
            if (nearMU.FixedBy == null)
                return "Near MU was not picked (FixedBy is null)";

            // Far MU should NOT be gripped
            if (farMU.FixedBy != null)
                return $"Far MU was also picked - FixedBy='{farMU.FixedBy.name}', parent='{farMU.transform.parent?.name ?? "null"}', grip picked {grip.PickedMUs.Count} MUs";

            return "";
        }
    }
}
