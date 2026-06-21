// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Tests that Grip finds the nearest free GripTarget and positions the MU there with correct sub-parenting.
    public class TestGripTargetPlacement : FeatureTestBase
    {
        protected override string TestName => "Grip finds GripTarget and places MU with sub-parenting";

        private Grip grip;
        private MU mu;
        private GripTarget gripTarget;

        protected override void SetupTest()
        {
            MinTestTime = 1f;

            // GripTarget at center of test area
            var targetGO = CreateGameObject("PlaceTarget");
            targetGO.transform.position = TestPosition(5f, 0.5f, 5f);
            targetGO.transform.rotation = Quaternion.Euler(0, 45, 0);
            // Add collider so OverlapSphere can detect it
            var targetCol = targetGO.AddComponent<SphereCollider>();
            targetCol.isTrigger = true;
            targetCol.radius = 0.3f;
            gripTarget = targetGO.AddComponent<GripTarget>();
            gripTarget.AlignPosition = true;
            gripTarget.AlignRotation = true;

            // Grip positioned above the target
            var gripGO = CreateGameObject("TestGrip");
            gripGO.transform.position = TestPosition(5f, 1f, 5f);
            var rb = gripGO.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            grip = gripGO.AddComponent<Grip>();
            grip.PlaceMode = PlaceMode.Auto;
            grip.GripTargetSearchRadius = 1000f; // 1000mm = 1m search radius
            grip.OneBitControl = false; // prevent FixedUpdate from auto-placing

            // MU at grip position
            var muGO = CreatePrimitive(PrimitiveType.Cube, "TestMU");
            muGO.transform.position = TestPosition(5f, 1f, 5f);
            muGO.transform.localScale = Vector3.one * 0.05f;
            muGO.layer = LayerMask.NameToLayer("rvMU");
            var muRb = muGO.AddComponent<Rigidbody>();
            muRb.isKinematic = true; // keep kinematic while gripped
            mu = muGO.AddComponent<MU>();
            mu.Rigidbody = muRb;

            // Don't call Fix/Place here — OverlapSphere needs physics frames to register colliders.
            // Both will be called in ValidateResults.
        }

        protected override string ValidateResults()
        {
            if (mu == null) return "MU not found";
            if (gripTarget == null) return "GripTarget not found";

            // Only fix and place once (ValidateResults may be called multiple times)
            if (mu.FixedBy == null)
            {
                Physics.SyncTransforms();
                grip.Fix(mu);
                grip.Place();
            }

            // MU should be at GripTarget position
            float posDist = Vector3.Distance(mu.transform.position, gripTarget.transform.position);
            if (posDist > 0.01f)
                return $"MU not at GripTarget position (distance={posDist:F4}m, expected < 0.01m)";

            // MU should be sub-parented under GripTarget
            if (mu.transform.parent != gripTarget.transform)
                return $"MU parent is '{mu.transform.parent?.name ?? "null"}', expected GripTarget '{gripTarget.name}'";

            // GripTarget should be marked as occupied by this MU
            if (gripTarget.OccupiedBy != mu)
                return $"GripTarget.OccupiedBy is '{gripTarget.OccupiedBy?.name ?? "null"}', expected MU '{mu.name}'";

            return "";
        }
    }
}
