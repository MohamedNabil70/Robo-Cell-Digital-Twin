// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

namespace realvirtual
{
    [System.Serializable]
    public class EventMUGrip : UnityEvent<MU, bool>
    {
    }

    //! Place mode used by Grip's auto-place system.
    public enum PlaceMode
    {
        Auto,    //!< Raycast: GripTarget → align+parent; MU below → LoadMu; TransportSurface → Physics; else → kinematic
        Static,  //!< MU stays kinematic where the gripper left it
        Physics  //!< MU gets physics and falls/lands on surface
    }

    [AddComponentMenu("realvirtual/Gripping/Grip")]
    [SelectionBase]
    [RequireComponent(typeof(Rigidbody))]
    #region doc
    //! Grip component for attaching and transporting MUs with moving mechanisms like robots or grippers.

    //! The Grip component is a fundamental part of realvirtual's material handling system, enabling dynamic
    //! pick-and-place operations in industrial automation simulations. It provides flexible attachment mechanisms
    //! for securely gripping MUs (Material Units) and transporting them through the production system.
    //! The component works by detecting MUs through a sensor or automatic OverlapSphere search, fixing them
    //! kinematically or with physics joints, and maintaining the attachment while the parent object moves.
    //!
    //! Key Features:
    //! - Sensor-based MU detection OR automatic OverlapSphere pick (no sensor required)
    //! - Kinematic attachment for stable, physics-free transportation
    //! - Optional physics joint connection for dynamic simulations
    //! - Auto-Place Raycast: GripTarget → LoadMu on MU → Physics on TransportSurface → Kinematic
    //! - GripTarget marker support for precise pick placement
    //! - Alignment control for precise positioning during pick and place operations
    //! - Direct gripping mode for immediate attachment on sensor detection
    //! - Support for loading MUs as subcomponents onto other MUs
    //! - Single-bit or dual-bit PLC control modes
    //! - Unity events for grip and ungrip notifications
    //!
    //! Common Applications:
    //! - Robotic end effectors and tool changers
    //! - Conveyor transfer mechanisms
    //! - AGV loading/unloading systems
    //! - Palletizing and depalletizing operations
    //! - Assembly line pick-and-place stations
    //! - Material sorting and distribution systems
    //!
    //! Integration Points:
    //! The Grip component integrates seamlessly with other realvirtual components through the sensor system
    //! for MU detection, the MU system for material tracking, and Drive_Cylinder for automated gripping
    //! based on cylinder positions. It can be controlled through PLC signals (PLCOutputBool) for industrial
    //! control system integration or directly through Unity Inspector properties for simulation control.
    //!
    //! Performance Considerations:
    //! The component uses kinematic attachment by default, which is more performant than physics-based
    //! joints. OverlapSphere and Raycast are called only once per Pick/Place event (not per frame).
    //! Pre-allocated Collider buffer avoids GC allocations in the pick/place code path.
    //!
    //! Events and Signals:
    //! The EventMUGrip Unity event provides real-time notifications of grip operations, passing the MU
    //! reference and grip state (true for grip, false for ungrip). This enables custom logic execution
    //! during material handling operations, such as updating production tracking systems or triggering
    //! dependent automation sequences.
    //!
    //! For detailed documentation and examples, visit:
    //! https://doc.realvirtual.io/components-and-scripts/grip
    #endregion
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/grip")]
    [Icon("Packages/io.realvirtual.starter/Editor/EditorAssets/EditorScriptIcons/Grip Icon.png")]
    public class Grip : BaseGrip, IFix, IMultiPlayer
    {
        [Tooltip("Toggle between Simple and Advanced inspector mode")]
        public bool AdvancedMode = false; //!< If true all advanced properties are shown in the inspector

        [Header("Pick Detection")]
        [Tooltip("Search radius in millimeters for automatic MU detection when no Sensor is assigned")]
        public float GripRange = 10f; //!< Search radius in millimeters for automatic MU detection when no Sensor is assigned

        [ShowIf("AdvancedMode")]
        [Tooltip("Sensor that identifies which MUs should be gripped")]
        public Sensor PartToGrip; //!< Identifies the MU to be gripped. When null the OverlapSphere auto-detection uses GripRange.

        [ShowIf("AdvancedMode")]
        [Tooltip("Automatically grip MUs when detected by PartToGrip sensor")]
        public bool DirectlyGrip = false; //!< If set to true the MU is directly gripped when Sensor PartToGrip detects a Part

        [ShowIf("AdvancedMode")]
        [Tooltip("GameObject to align MUs with before picking (optional)")]
        public GameObject PickAlignWithObject;

        [ShowIf("AdvancedMode")]
        [Tooltip("Align rotation with PickAlignWithObject when picking")]
        public bool AlignRotation = true; //!<  If not null the MUs are aligned with this object before picking.

        [ShowIf("AdvancedMode")]
        [Tooltip("Trigger picking when this sensor is occupied (optional)")]
        public Sensor PickBasedOnSensor; //!< Picking is started when this sensor is occupied (optional)

        [ShowIf("AdvancedMode")]
        [Tooltip("Trigger picking based on cylinder position (optional)")]
        public Drive_Cylinder PickBasedOnCylinder; //!< Picking is stared when Cylinder is Max or Min (optional)

        [ShowIf("AdvancedMode")]
        [Tooltip("Pick when cylinder reaches maximum position, otherwise pick at minimum")]
        public bool PickOnCylinderMax; //!< Picking is started when Cylinderis Max

        [Header("Place Mode")]
        [ShowIf("AdvancedMode")]
        [Tooltip("Controls how MUs are released: Auto=Raycast cascade, Static=kinematic in place, Physics=gravity enabled")]
        public PlaceMode PlaceMode = PlaceMode.Auto; //!< Controls how MUs are released on Place()

        [ShowIf("AdvancedMode")]
        [Tooltip("Search radius in millimeters for finding the nearest free GripTarget on Place")]
        public float GripTargetSearchRadius = 500f; //!< Search radius in millimeters for finding the nearest GripTarget on Place

        [ShowIf("AdvancedMode")]
        [Tooltip("Raycast distance in millimeters for Auto-Place surface detection")]
        public float RaycastDistance = 1000f; //!< Raycast distance in millimeters for Auto-Place surface detection

        [ShowIf("AdvancedMode")]
        [Tooltip("Keep objects kinematic (no physics) after placing")]
        public bool NoPhysicsWhenPlaced = false; //!< Object remains kinematic (no phyisics) when placed

        [ShowIf("AdvancedMode")]
        [Tooltip("GameObject to align MUs with when placing (optional)")]
        public GameObject PlaceAlignWithObject; //!<  If not null the MUs are aligned with this object after placing. Useful for positioning near Fixers that will take over.

        [ShowIf("AdvancedMode")]
        [Tooltip("Load placed components onto another MU as subcomponents")]
        public bool PlaceLoadOnMU = false; //!<  When placing the components they should be loaded onto an MU as subcomponent.

        [ShowIf(EConditionOperator.And, "AdvancedMode", "PlaceLoadOnMU")]
        [Tooltip("Sensor that identifies the target MU for loading placed components")]
        public Sensor PlaceLoadOnMUSensor; //!<  Sensor defining the MU where the picked MUs should be loaded to.

        [ShowIf("AdvancedMode")]
        [Tooltip("Should be usually kept empty, for very special cases where joint should be used for gripping")]
        public UnityEngine.Joint ConnectToJoint; //< Should be usually kept empty, for very special cases where joint should be used for gripping

        [Header("Pick & Place Control")]
        [Tooltip("Enable picking of MUs identified by the sensor")]
        public bool PickObjects = false; //!< true for picking MUs identified by the sensor.

        [Tooltip("Enable placing of currently gripped MUs")]
        public bool PlaceObjects = false; //!< //!< true for placing the loaded MUs.

        [ShowIf("AdvancedMode")]
        [Tooltip("Use single bit control (true) or two separate bits for pick/place (false)")]
        public bool OneBitControl = true; //!< If true the grip is controlled by one bit. If false the grip is controlled by two bits.

        [Tooltip("PLC signal to control picking operation")]
        public PLCOutputBool SignalPick;

        [ShowIf("showSignalPlace")]
        [Tooltip("PLC signal to control placing operation (when using two-bit control)")]
        public PLCOutputBool SignalPlace;

        [Header("Events")]
        [ShowIf("AdvancedMode")]
        [Tooltip("Unity event triggered on grip (true) and ungrip (false) with MU reference")]
        public EventMUGrip EventMUGrip; //!<  Unity event which is called for MU grip and ungrip. On grip it passes MU and true. On ungrip it passes MU and false.

        [Header("Gizmos")]
        [Tooltip("Show grip range gizmo in scene view")]
        public bool ShowGizmo = true; //!< If true the grip range gizmo is displayed in the scene view

        [Header("Status")]
        [ReadOnly]
        public List<GameObject> PickedMUs = new List<GameObject>();

        // Helper for compound ShowIf: AdvancedMode && !OneBitControl
        private bool showSignalPlace => AdvancedMode && !OneBitControl;

        // Pre-allocated buffer for OverlapSphere — avoids GC allocation in Pick/Place code path
        private readonly Collider[] _overlapBuffer = new Collider[32];

        private bool _issignalpicknotnull;
        private bool _issignalplacenotnull;
        private bool Deactivated = false;
        private bool _pickobjectsbefore = false;
        private bool _placeobjectsbefore = false;
        private List<FixedJoint> _fixedjoints;
        private bool _ismultiplayeclient = false;

        //! Deactivates or activates the grip component.
        public void DeActivate(bool activate)
        {
            Deactivated = activate;
        }

        public void OnMultiplayer(bool isclient, bool isstart)
        {
            if (isclient && isstart)
                _ismultiplayeclient = true;
            else
                _ismultiplayeclient = false;
        }

        //! Fixes (grips) the given MU to this grip.
        public void Fix(MU mu)
        {
            if (Deactivated || _ismultiplayeclient)
                return;

            var obj = mu.gameObject;
            if (PickedMUs.Contains(obj) == false)
            {
                if (mu == null)
                {
                    ErrorMessage("MUs which should be picked need to have the MU script attached!");
                    return;
                }

                // Clear GripTarget if MU was placed on one
                var gripTarget = mu.GetComponentInParent<GripTarget>();
                if (gripTarget != null && gripTarget.OccupiedBy == mu)
                    gripTarget.ClearOccupied();

                if (ConnectToJoint == null)
                    mu.Fix(this.gameObject);

                if (PickAlignWithObject != null)
                {
                    obj.transform.position = PickAlignWithObject.transform.position;
                    if (AlignRotation)
                          obj.transform.rotation = PickAlignWithObject.transform.rotation;
                }

                if (ConnectToJoint != null)
                    ConnectToJoint.connectedBody = mu.Rigidbody;

                PickedMUs.Add(obj);
                if (EventMUGrip != null)
                    EventMUGrip.Invoke(mu, true);
            }
        }

        //! Releases (ungrips) the given MU. Does NOT trigger AutoPlace — use internal Place() flank for auto-placement.
        public void Unfix(MU mu)
        {
            if (Deactivated || _ismultiplayeclient)
                return;

            var obj = mu.gameObject;
            var tmpfixedjoints = _fixedjoints;
            var rb = mu.Rigidbody;
            if (EventMUGrip != null)
                EventMUGrip.Invoke(mu, false);

            if (PlaceAlignWithObject != null)
            {
                obj.transform.position = PlaceAlignWithObject.transform.position;
                obj.transform.rotation = PlaceAlignWithObject.transform.rotation;
            }

            if (ConnectToJoint == null)
                mu.Unfix();

            if (ConnectToJoint != null)
                ConnectToJoint.connectedBody = null;

            if (PlaceLoadOnMUSensor == null)
            {
                if (!NoPhysicsWhenPlaced)
                {
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        // Velocity-reset: prevent MU from flying away when physics is re-enabled
                        ResetVelocity(rb);
                    }
                    else
                        Warning("No Rigidbody for MU which is unfixed", this);
                }
                else
                {
                    // NoPhysicsWhenPlaced: override mu.Unfix()'s physics-on and keep kinematic
                    if (rb != null)
                        rb.isKinematic = true;
                }
            }

            if (PlaceLoadOnMUSensor != null)
            {
                if (PlaceLoadOnMUSensor.LastTriggeredBy != null)
                {
                    var loadmu = PlaceLoadOnMUSensor.LastTriggeredBy.GetComponent<MU>();
                    if (loadmu == null)
                    {
                        ErrorMessage("You can only load parts on parts which are of type MU, please add to part [" +
                                     PlaceLoadOnMUSensor.LastTriggeredBy.name + "] MU script");
                    }

                    loadmu.LoadMu(mu);
                }
            }

            PickedMUs.Remove(obj);
        }

        //! Picks all MUs: uses sensor (PartToGrip) if assigned, otherwise auto-detects via OverlapSphere.
        public void Pick()
        {
            if (Deactivated || _ismultiplayeclient)
                return;

            if (PartToGrip != null)
            {
                // Sensor-based pick — backward compatible path
                foreach (GameObject obj in PartToGrip.CollidingObjects)
                {
                    var pickobj = GetTopOfMu(obj);
                    if (pickobj == null)
                        Warning("No MU on object for gripping detected", obj);
                    else
                        Fix(pickobj);
                }
            }
            else
            {
                // OverlapSphere auto-detection — no sensor required
                var mu = FindNearestMU();
                if (mu != null)
                    Fix(mu);
                else
                    Warning("Grip: No MU found in GripRange (" + GripRange + "mm) for auto-pick", this);
            }
        }

        //! Places all currently gripped MUs. Applies AutoPlace logic if PlaceMode requires it.
        public void Place()
        {
            if (Deactivated || _ismultiplayeclient)
                return;

            var tmppicked = PickedMUs.ToArray();
            foreach (var muObj in tmppicked)
            {
                if (muObj == null) continue;
                var mu = muObj.GetComponent<MU>();
                if (mu == null) continue;

                // Auto-place logic: only triggered from Place(), NOT from external Unfix() calls.
                // This ensures Gripper.cs calling grip.Unfix() on finger-open does NOT trigger AutoPlace.
                if (ShouldUseAutoPlace())
                    AutoPlace(mu);
                else
                    Unfix(mu);
            }
        }

        private void Reset()
        {
            GetComponent<Rigidbody>().isKinematic = true;
        }

        // Use this for initialization
        private void Start()
        {
            _issignalpicknotnull = SignalPick != null;
            _issignalplacenotnull = SignalPlace != null;

            // PartToGrip == null is now a valid state (OverlapSphere auto-detection mode)
            // Only warn if GripRange is also zero/negative (then no detection is possible)
            if (PartToGrip == null && GripRange <= 0f)
            {
                Warning("Grip: PartToGrip sensor is null and GripRange <= 0. No MU detection possible. Assign a sensor or set GripRange > 0.", this);
            }

            _fixedjoints = new List<FixedJoint>();
            GetComponent<Rigidbody>().isKinematic = true;

            if (PickBasedOnSensor != null)
            {
                PickBasedOnSensor.EventEnter += PickBasedOnSensorOnEventEnter;
            }

            if (DirectlyGrip == true && PartToGrip != null)
            {
                PartToGrip.EventEnter += PickBasedOnSensorOnEventEnter;
            }

            if (PickBasedOnSensor != null)
            {
                PickBasedOnSensor.EventExit += PickBasedOnSensorOnEventExit;
            }

            if (PickBasedOnCylinder != null)
            {
                if (PickOnCylinderMax)
                {
                    PickBasedOnCylinder.EventOnMin += Place;
                    PickBasedOnCylinder.EventOnMax += Pick;
                }
                else
                {
                    PickBasedOnCylinder.EventOnMin += Pick;
                    PickBasedOnCylinder.EventOnMax += Place;
                }
            }
        }

        private void PickBasedOnSensorOnEventExit(GameObject obj)
        {
            var mu = obj.GetComponent<MU>();
            if (mu != null)
                Unfix(mu);
        }

        private void PickBasedOnSensorOnEventEnter(GameObject obj)
        {
            var mu = obj.GetComponent<MU>();
            if (mu != null)
                Fix(mu);
        }

        private void FixedUpdate()
        {
            if (Deactivated || _ismultiplayeclient)
                return;

            if (_issignalpicknotnull)
            {
                PickObjects = SignalPick.Value;
            }

            if (_issignalplacenotnull)
            {
                PlaceObjects = SignalPlace.Value;
            }

            if (OneBitControl)
                PlaceObjects = !PickObjects;

            if (_pickobjectsbefore == false && PickObjects)
            {
                Pick();
            }

            if (_placeobjectsbefore == false && PlaceObjects)
            {
                Place();
            }

            _pickobjectsbefore = PickObjects;
            _placeobjectsbefore = PlaceObjects;
        }

        private void OnDrawGizmosSelected()
        {
            if (!ShowGizmo) return;
            float scale = GetScaleValue();

            // Draw cross-hair and small sphere at the grip center
            float crossSize = 30f / scale; // 30mm cross-hair
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.9f);
            Gizmos.DrawLine(transform.position - transform.right * crossSize, transform.position + transform.right * crossSize);
            Gizmos.DrawLine(transform.position - transform.up * crossSize, transform.position + transform.up * crossSize);
            Gizmos.DrawLine(transform.position - transform.forward * crossSize, transform.position + transform.forward * crossSize);
            // Solid center dot
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.5f);
            Gizmos.DrawSphere(transform.position, 5f / scale); // 5mm dot

            // Draw grip range sphere when using auto-detection (no sensor)
            if (PartToGrip == null && GripRange > 0f)
            {
                Gizmos.color = new Color(0f, 1f, 0.3f, 0.15f);
                Gizmos.DrawSphere(transform.position, GripRange / scale);
                Gizmos.color = new Color(0f, 1f, 0.3f, 0.7f);
                Gizmos.DrawWireSphere(transform.position, GripRange / scale);
            }
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        /// <summary>Returns true when AutoPlace should be used instead of plain Unfix.</summary>
        private bool ShouldUseAutoPlace()
        {
            // Legacy backward-compat: if NoPhysicsWhenPlaced is set and PlaceMode is Auto,
            // skip raycast and fall back to kinematic/static behaviour (matches old Unfix path).
            if (NoPhysicsWhenPlaced && PlaceMode == PlaceMode.Auto)
                return false;

            // PlaceLoadOnMUSensor uses old path
            if (PlaceLoadOnMUSensor != null)
                return false;

            return true;
        }

        /// <summary>
        /// Executes the AutoPlace cascade:
        ///   Priority 0: GripTarget in GripTargetSearchRadius  → align + sub-parent
        ///   Priority 1: MU below (Raycast rvMU layer)         → LoadMu()
        ///   Priority 2: TransportSurface below                → Physics
        ///   Fallback:   Nothing recognized                    → Kinematic + reparent to StandardParent
        ///
        /// IMPORTANT: Called ONLY from Place() internal flank logic.
        /// External Unfix() calls (e.g. Gripper.cs) bypass this method entirely.
        /// </summary>
        private void AutoPlace(MU mu)
        {
            if (PlaceMode == PlaceMode.Auto)
            {
                // Priority 0: GripTarget in search radius
                var gripTarget = FindNearestGripTarget();
                if (gripTarget != null)
                {
                    mu.Unfix();
                    if (gripTarget.AlignPosition)
                        mu.transform.position = gripTarget.transform.position;
                    if (gripTarget.AlignRotation)
                        mu.transform.rotation = gripTarget.transform.rotation;
                    mu.transform.SetParent(gripTarget.transform);
                    gripTarget.SetOccupied(mu);
                    mu.PhysicsOff();
                    PickedMUs.Remove(mu.gameObject);
                    return;
                }

                // Priority 1 & 2: Raycast straight down in world space (placing follows gravity).
                // Note: NOT -transform.up — a robot gripper's local up axis is arbitrarily oriented
                // (e.g. horizontal), which would fire the ray sideways and miss the surface below.
                float scale = GetScaleValue();
                float rayDist = RaycastDistance / scale;
                var rayDirection = Vector3.down;
                var layermask = LayerMask.GetMask("rvMU", "rvTransport");
                if (Physics.Raycast(mu.transform.position, rayDirection, out var hit, rayDist, layermask))
                {
                    // Priority 1: MU below (AGV, palette, carrier)
                    var targetMU = GetTopOfMu(hit.collider.gameObject);
                    if (targetMU != null && targetMU != mu)
                    {
                        mu.Unfix();
                        ResetVelocity(mu.Rigidbody);
                        targetMU.LoadMu(mu);
                        PickedMUs.Remove(mu.gameObject);
                        return;
                    }

                    // Priority 2: TransportSurface below (conveyor)
                    var surface = hit.collider.GetComponentInParent<TransportSurface>();
                    if (surface != null)
                    {
                        mu.Unfix();
                        ResetVelocity(mu.Rigidbody);
                        mu.PhysicsOn();
                        ReparentToStandardParent(mu);
                        PickedMUs.Remove(mu.gameObject);
                        return;
                    }
                }

                // Fallback: kinematic, reparent to StandardParent/root
                mu.Unfix();
                mu.PhysicsOff();
                ReparentToStandardParent(mu);
                PickedMUs.Remove(mu.gameObject);
            }
            else if (PlaceMode == PlaceMode.Physics)
            {
                mu.Unfix();
                ResetVelocity(mu.Rigidbody);
                mu.PhysicsOn();
                ReparentToStandardParent(mu);
                PickedMUs.Remove(mu.gameObject);
            }
            else // Static
            {
                mu.Unfix();
                mu.PhysicsOff();
                ReparentToStandardParent(mu);
                PickedMUs.Remove(mu.gameObject);
            }
        }

        /// <summary>
        /// Finds the nearest free MU within GripRange using Physics.OverlapSphereNonAlloc.
        /// Only picks MUs that are not already gripped (FixedBy == null).
        /// </summary>
        private MU FindNearestMU()
        {
            var layerMask = LayerMask.GetMask("rvMU");
            float scale = GetScaleValue();
            float radiusInMeters = GripRange / scale;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radiusInMeters, _overlapBuffer, layerMask);

            MU nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var mu = GetTopOfMu(_overlapBuffer[i].gameObject);
                if (mu != null && mu.FixedBy == null)
                {
                    float dist = Vector3.Distance(transform.position, mu.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = mu;
                    }
                }
            }
            return nearest;
        }

        /// <summary>
        /// Finds the nearest free GripTarget within GripTargetSearchRadius using Physics.OverlapSphereNonAlloc.
        /// Reuses the same pre-allocated _overlapBuffer (called only from AutoPlace, never concurrently).
        /// </summary>
        private GripTarget FindNearestGripTarget()
        {
            float scale = GetScaleValue();
            float radiusInMeters = GripTargetSearchRadius / scale;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radiusInMeters, _overlapBuffer);

            GripTarget nearest = null;
            float minDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var target = _overlapBuffer[i].GetComponentInParent<GripTarget>();
                if (target != null && target.IsFree)
                {
                    float dist = Vector3.Distance(transform.position, target.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = target;
                    }
                }
            }
            return nearest;
        }

        /// <summary>
        /// Safety reparent: if MU is still under the gripper after Unfix(), move it to StandardParent.
        /// MU.Unfix() already handles parent restoration in most cases; this is a fallback.
        /// </summary>
        private void ReparentToStandardParent(MU mu)
        {
            if (mu.transform.IsChildOf(this.transform))
            {
                var standardParent = mu.StandardParent;
                mu.transform.SetParent(standardParent != null ? standardParent.transform : null);
            }
        }

        /// <summary>Resets linear and angular velocity to prevent MU from flying away on physics re-enable.</summary>
        private void ResetVelocity(Rigidbody rb)
        {
            if (rb == null) return;
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>Returns the realvirtualController scale (mm-to-m conversion factor). Works in edit mode too.</summary>
        private float GetScaleValue()
        {
            if (realvirtualController != null && realvirtualController.Scale > 0f)
                return realvirtualController.Scale;
            // Edit-mode fallback: find controller in scene
            var ctrl = FindFirstObjectByType<realvirtualController>();
            if (ctrl != null && ctrl.Scale > 0f)
                return ctrl.Scale;
            return 1000f; // fallback: 1000mm = 1m
        }
    }
}
