// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using NaughtyAttributes;
using UnityEngine;

namespace realvirtual
{
    [AddComponentMenu("realvirtual/Gripping/GripTarget")]
    #region doc
    //! Marker component for precise MU placement by a Grip component.

    //! GripTarget is a lightweight marker that defines where MUs should be placed by a Grip.
    //! Place this component on a GameObject at the desired placement position and rotation.
    //! The Grip component automatically searches for the nearest free GripTarget within its
    //! GripTargetSearchRadius when Place() is called in Auto mode.
    //!
    //! Key Features:
    //! - Zero-configuration marker - position and rotation come from the Transform
    //! - OccupiedBy tracking: knows which MU is currently placed here
    //! - Automatic stale-reference cleanup when the occupying MU is destroyed
    //! - Auto-creates a SphereCollider trigger in Reset() so OverlapSphere can detect it
    //! - Thread-safe SetOccupied() prevents two Grips from racing for the same target
    //!
    //! Common Applications:
    //! - Fixture positions in CNC machines or assembly stations
    //! - Precise placement slots on rotating tables or AGVs
    //! - Pick-and-place output trays with defined positions
    //! - Pallet loading positions for ordered stacking
    //!
    //! Integration Points:
    //! - Grip.PlaceMode = Auto with GripTargetSearchRadius > 0 automatically finds free targets
    //! - When OccupiedBy is set, IsFree returns false and other Grips skip this target
    //! - OccupiedBy is cleared automatically when the MU is destroyed or picked up
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/components-and-scripts/griptarget
    #endregion
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/griptarget")]
    public class GripTarget : MonoBehaviour
    {
        [Tooltip("Align the MU position to the GripTarget position when placing")]
        public bool AlignPosition = false; //!< If true the MU position is snapped to the GripTarget position

        [Tooltip("Align the MU rotation to the GripTarget rotation when placing")]
        public bool AlignRotation = false; //!< If true the MU rotation is aligned to the GripTarget rotation when placed

        [ReadOnly]
        public MU OccupiedBy; //!< Currently placed MU at this target position (null = free)

        //! Returns true when no MU is currently occupying this target.
        public bool IsFree => OccupiedBy == null;

        //! Sets the occupying MU and notifies the MU via EventMUPlacedOnTarget.
        public void SetOccupied(MU mu)
        {
            OccupiedBy = mu;
            if (mu != null)
                mu.EventMUPlacedOnTarget(this);
        }

        //! Clears the occupying MU and notifies the MU via EventMURemovedFromTarget.
        public void ClearOccupied()
        {
            var mu = OccupiedBy;
            OccupiedBy = null;
            if (mu != null)
                mu.EventMURemovedFromTarget(this);
        }

        private void Reset()
        {
            // Auto-create a SphereCollider trigger so Physics.OverlapSphere can detect this target.
            // Only add if no collider exists yet.
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.05f; // 5cm default — small enough not to interfere with physics
            }
        }

        private void Update()
        {
            // Stale-reference cleanup: MU may have been destroyed externally (e.g. Sink).
            // Unity's null check on a destroyed MonoBehaviour returns true for the == null test.
            if (OccupiedBy != null && OccupiedBy.gameObject == null)
                OccupiedBy = null;
        }

        private void OnDrawGizmosSelected()
        {
            float scale = GetScaleValue();
            float size = 30f / scale; // 30mm
            float dot = 5f / scale;   // 5mm center dot
            float axisLen = 40f / scale; // 40mm axis lines

            // Flat placement indicator
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.zero, new Vector3(size * 2, dot * 0.5f, size * 2));
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size * 2, dot * 0.5f, size * 2));
            Gizmos.matrix = Matrix4x4.identity;

            // Center dot
            Gizmos.color = IsFree ? new Color(0.2f, 0.8f, 1f, 0.6f) : new Color(1f, 0.4f, 0.1f, 0.6f);
            Gizmos.DrawSphere(transform.position, dot);

            // Cross-hair
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(transform.position - transform.right * size, transform.position + transform.right * size);
            Gizmos.DrawLine(transform.position - transform.forward * size, transform.position + transform.forward * size);

            // Up axis indicator
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * axisLen);
        }

        private float GetScaleValue()
        {
            var ctrl = GetComponentInParent<realvirtualController>();
            if (ctrl != null && ctrl.Scale > 0f)
                return ctrl.Scale;
            ctrl = FindFirstObjectByType<realvirtualController>();
            if (ctrl != null && ctrl.Scale > 0f)
                return ctrl.Scale;
            return 1000f;
        }
    }
}
