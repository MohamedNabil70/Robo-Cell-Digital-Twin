using System.Collections;
using UnityEngine;

/// <summary>
/// RobotCar2 — receives processed object from Arm3, drives to WH-B.
/// (Original functionality preserved. Telemetry section added at bottom of Inspector.)
///
/// ══ CONVEYOR SOLUTION APPLIED — FIXES IN THIS VERSION ═══════════════════════
///
///   ROOT CAUSE: Three gap-frame feedback bugs in MainLoop(), identical in
///   pattern to the ConveyorMotor millisecond-pulse bug. Each gap occurs when
///   the "current phase" tag is cleared with SetOutput()=false before the
///   "next phase" tag is set with SetOutput()=true, leaving one or more
///   frames where ALL phase tags are FALSE simultaneously.
///
///   The TIA Portal PLC polls tags every scan cycle (~10ms). Any gap of
///   even one Unity frame (~16ms at 60fps) is visible to the PLC as an
///   invalid "car is nowhere" state, which can trigger false alarms,
///   missed rising edges, or incorrect sequence logic.
///
///   FIX A — BoxLoaded departure gap (AtConv2 → Driving transition):
///   ─────────────────────────────────────────────────────────────────
///   ORIGINAL CODE:
///       SetOutput(out_CarAtConv2,   false);  ← clear phase A
///       SetOutput(out_ReadyForNext, false);  ← ancillary
///       dbFeedbackPhase = "BoxLoaded";       ← inspector only
///       // ... conveyor releases, PLC wait ...
///       SetOutput(out_CarDriving, true);     ← latch phase B ← BIG GAP HERE
///
///   ROOT CAUSE: Multiple frames (and potentially seconds in PLC wait) pass
///   between clearing out_CarAtConv2 and latching out_CarDriving. The PLC
///   sees Car2 as "nowhere" for this entire window.
///
///   FIX: Handoff out_CarAtConv2 → out_CarDriving atomically at departure.
///   The PLC wait in offline mode still occurs but the CAR now has a valid
///   phase tag (Driving) set from the moment it leaves Conv2 — even while
///   waiting for the PLC proceed signal (which is a logical state, not a
///   physical location change).
///
///   FIX B — AtWarehouseB departure gap (AtWarehouseB → Returning transition):
///   ──────────────────────────────────────────────────────────────────────────
///   ORIGINAL CODE:
///       SetOutput(out_CarAtWarehouseB, false);   ← clear phase
///       SetOutput(out_ObjectDelivered, true);    ← ancillary pulse
///       // ... yield 0.2f ...
///       SetOutput(out_ObjectDelivered, false);   ← pulse clear
///       SetOutput(out_CarReturning,    true);    ← latch phase ← GAP
///
///   ROOT CAUSE: out_CarAtWarehouseB is cleared but out_CarReturning is
///   not latched until AFTER the 0.2s ObjectDelivered pulse. The PLC
///   sees Car2 as "nowhere" for 200ms + frame overhead.
///
///   FIX: SetValueWithHandoff(out_CarAtWarehouseB, out_CarReturning) at
///   the departure point. The ObjectDelivered pulse still fires afterward
///   as a separate ancillary signal — it does not affect the phase latch.
///
///   FIX C — Return arrival gap (Returning → AtConv2 transition):
///   ─────────────────────────────────────────────────────────────
///   This was ALREADY CORRECT in the original — SetValueWithHandoff() was
///   already used here. Preserved unchanged and documented for clarity.
///
///   FIX D — E-Stop restore: out_CarAtConv2 / out_ReadyForNext gap:
///   ────────────────────────────────────────────────────────────────
///   ClearLatchedFeedback() zeroes all driving/delivery tags on E-Stop.
///   The E-Stop restore (else branch) previously had no path to re-latch
///   out_CarAtConv2 or out_ReadyForNext — those were only set at the top
///   of MainLoop(). After E-Stop clears, RestartMainLoopAfterEStop() yields
///   until eStopActive=false then restarts MainLoop(), which sets them. But
///   between "eStopActive=false" and "MainLoop() top" there is a coroutine
///   scheduling gap. FIX: set them immediately in the cbEStop FALSE branch
///   before yielding to the restart coroutine.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class RobotCar2 : MonoBehaviour
{
    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    public bool offlineMode = true;

    [Header("══ Waypoints ══════════════════════════════════════════════════")]
    public Transform wpConv2Exit;
    public Transform wpMid1;
    public Transform wpWarehouseB;
    public Transform wpMid2Return;

    [Header("══ References ══════════════════════════════════════════════════")]
    public WarehouseManager warehouseManager;
    public ConveyorMotor    secondConveyor;

    [Header("══ Object Carry ════════════════════════════════════════════════")]
    public Vector3 carryOffset = new Vector3(0f, 0.3f, 0f);

    [Header("══ PLC INPUT Tags (PLC → Unity) ════════════════════════════════")]
    public string in_ProceedToDrop = "Car2_ProceedToDrop";
    public string in_EStopTag      = "Car2_EStop";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════════")]
    public string out_CarAtConv2      = "Car2_AtConv2";
    public string out_CarDriving      = "Car2_Driving";
    public string out_CarAtWarehouseB = "Car2_AtWarehouseB";
    public string out_ObjectDelivered = "Car2_ObjectDelivered";
    public string out_CarReturning    = "Car2_Returning";
    public string out_ReadyForNext    = "Car2_ReadyForNext";
    public string out_BoxReceived     = "Car2_BoxReceived";
    public string out_CarEStop        = "Car2_EStop_Active";
    public string out_BoxAttached       = "Car2_BoxAttached";

    [Header("══ Movement ════════════════════════════════════════════════════")]
    public float moveSpeed         = 5f;
    [Range(0.5f,10f)]  public float acceleration    = 2.5f;
    public float waypointTolerance = 0.05f;
    [Range(30f,720f)]  public float rotationSpeed   = 200f;
    [Range(0.5f,15f)]  public float alignThreshold  = 3f;
    public float arrivalPause      = 0.5f;

    public enum ForwardAxis { Z, NegZ, X, NegX, Y, NegY }
    public ForwardAxis forwardAxis = ForwardAxis.Z;

    [Header("══ Debug (Read Only) ════════════════════════════════════════════")]
    [SerializeField] string dbState         = "Idle";
    [SerializeField] bool   dbCarrying      = false;
    [SerializeField] string dbNextWP        = "—";
    [SerializeField] bool   dbEStop         = false;
    [SerializeField] string dbMode          = "Offline";
    [SerializeField] string dbFeedbackPhase = "AtConv2";

    // ══════════════════════════════════════════════════════════════════════════
    // ══ TELEMETRY ════════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    [Header("══ TELEMETRY — Position & Motion ════════════════════════════")]
    [Tooltip("Live world position X of the car.")]
    [SerializeField] float tel_PositionX = 0f;
    [Tooltip("Live world position Z of the car (primary travel axis).")]
    [SerializeField] float tel_PositionZ = 0f;
    [Tooltip("Live speed of the car (m/s).")]
    [SerializeField] float tel_Speed     = 0f;
    [Tooltip("World-space heading / yaw (°).")]
    [SerializeField] float tel_Heading   = 0f;
    [Tooltip("Distance remaining to the current waypoint (m).")]
    [SerializeField] float tel_DistToWP  = 0f;

    [Header("══ TELEMETRY — Drive Motor ═════════════════════════════════")]
    [Tooltip("Simulated drive motor current draw (A).")]
    [SerializeField] float tel_MotorCurrent = 0f;
    [Tooltip("Simulated motor temperature (°C). Rises under load, cools at idle.")]
    [SerializeField] float tel_MotorTemp    = 30f;
    [Tooltip("Simulated bus voltage (V). Nominal 24V, slight sag under load.")]
    [SerializeField] float tel_BusVoltage   = 24f;

    [Header("══ TELEMETRY — Delivery Counters ═══════════════════════════")]
    [Tooltip("Total WH-B deliveries completed since scene start.")]
    [SerializeField] int   tel_DeliveryCount    = 0;
    [Tooltip("Total distance travelled since scene start (m).")]
    [SerializeField] float tel_OdometerMeters   = 0f;
    [Tooltip("Average one-way trip time to WH-B (seconds).")]
    [SerializeField] float tel_AvgDeliveryTime  = 0f;
    [Tooltip("E-Stop events since scene start.")]
    [SerializeField] int   tel_EStopCount       = 0;
    [Tooltip("Payload currently on board (bool — TRUE while carrying).")]
    [SerializeField] bool  tel_PayloadOnBoard   = false;

    // ── Telemetry private ──────────────────────────────────────────────────────
    float   tripStartTime = 0f;
    bool    tripActive    = false;
    float   tripSum       = 0f;
    int     tripSamples   = 0;
    Vector3 prevPosition;

    // ─────────────────────────────────────────────────────────────────────────
    Transform carriedObject = null;
    bool      boxLoadedFlag = false;
    bool      proceedFlag   = false;
    bool      eStopActive   = false;
    float     currentSpeed  = 0f;

    System.Action<bool> cbProceed, cbEStop;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        out_BoxReceived       = out_BoxReceived?.Trim();
        out_BoxAttached       = out_BoxAttached?.Trim();

        out_BoxReceived       = QualifyTag(out_BoxReceived, out_CarAtConv2);
        out_BoxAttached       = QualifyTag(out_BoxAttached, out_CarAtConv2);

        dbMode = offlineMode ? "Offline" : "PLC";

        if (wpConv2Exit != null) { transform.position = wpConv2Exit.position; transform.rotation = wpConv2Exit.rotation; }

        prevPosition = transform.position;

        cbProceed = v => { if (v) { proceedFlag = true; Debug.Log("[CAR2] ProceedToDrop TRUE received from PLC."); } };

        cbEStop = v =>
        {
            eStopActive = v; dbEStop = v;
            SetOutput(out_CarEStop, v);
            if (v)
            {
                Debug.LogWarning("[CAR2] E-STOP!");
                StopAllCoroutines();
                tel_EStopCount++;
                // Clear all in-transit tags — car is stopped
                IO_Router.Instance?.ClearLatchedFeedback(
                    out_CarDriving, out_CarAtWarehouseB, out_CarReturning);
                StartCoroutine(RestartMainLoopAfterEStop());
            }
            else
            {
                Debug.Log("[CAR2] E-Stop cleared.");
                // FIX D: Immediately restore AtConv2/ReadyForNext so the PLC
                // knows where the car is during the coroutine restart gap.
                // MainLoop() will also set these at its top, but this prevents
                // the gap between eStopActive=false and MainLoop() rescheduling.
                dbFeedbackPhase = "AtConv2";
                SetOutput(out_CarAtConv2,   true);
                SetOutput(out_ReadyForNext, true);
            }
        };

        if (!string.IsNullOrEmpty(in_ProceedToDrop))
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_ProceedToDrop, cbProceed, this));
        if (!string.IsNullOrEmpty(in_EStopTag))
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_EStopTag, cbEStop, this));

        StartCoroutine(MainLoop());

        // ── FEEDBACK STATE MACHINE INIT ──────────────────────────────────────
        SetOutput(out_CarAtConv2,   true);
        SetOutput(out_ReadyForNext, true);
        SetOutput(out_BoxReceived,   false);
        SetOutput(out_BoxAttached,   false);
        dbFeedbackPhase = "AtConv2";

        Debug.Log($"[CAR2] Started in {dbMode} mode.");
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        Vector3 pos = transform.position;
        tel_PositionX = pos.x;
        tel_PositionZ = pos.z;
        tel_Heading   = transform.eulerAngles.y;
        tel_Speed     = currentSpeed;
        tel_PayloadOnBoard = dbCarrying;

        float delta = Vector3.Distance(pos, prevPosition);
        tel_OdometerMeters += delta;
        prevPosition = pos;

        Transform wp = GetWPByName(dbNextWP);
        tel_DistToWP = wp != null ? Vector3.Distance(pos, wp.position) : 0f;

        float targetCurrent = (currentSpeed / Mathf.Max(moveSpeed, 0.1f)) * 12f
                              + (dbCarrying ? 2.5f : 0f)
                              + Mathf.PerlinNoise(Time.time * 3.1f, 0.5f) * 0.4f;
        tel_MotorCurrent = Mathf.Lerp(tel_MotorCurrent, targetCurrent, Time.deltaTime * 5f);

        float targetTemp = 30f + tel_MotorCurrent * 4f;
        tel_MotorTemp = Mathf.MoveTowards(tel_MotorTemp, targetTemp, (tel_MotorCurrent > 1f ? 6f : 2f) * Time.deltaTime);

        tel_BusVoltage = 24f - tel_MotorCurrent * 0.08f + Mathf.PerlinNoise(Time.time * 1.3f, 2f) * 0.05f;
    }

    Transform GetWPByName(string wpName) => wpName switch
    {
        "Mid1"       => wpMid1,
        "WarehouseB" => wpWarehouseB,
        "Mid2Return" => wpMid2Return,
        "Conv2Exit"  => wpConv2Exit,
        _            => null
    };

    IEnumerator RestartMainLoopAfterEStop()
    {
        yield return new WaitUntil(() => !eStopActive);
        Debug.Log("[CAR2] E-Stop cleared — restarting main loop.");
        StartCoroutine(MainLoop());
    }

    void OnDestroy()
    {
        if (!string.IsNullOrEmpty(in_ProceedToDrop)) IO_Router.Instance?.Unregister(in_ProceedToDrop, cbProceed);
        if (!string.IsNullOrEmpty(in_EStopTag))      IO_Router.Instance?.Unregister(in_EStopTag,      cbEStop);
        TagSubscriptionHelper.Remove(in_ProceedToDrop);
        TagSubscriptionHelper.Remove(in_EStopTag);
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MainLoop()
    {
        while (true)
        {
            SetState("Waiting at Conv2 exit");
            // ── FEEDBACK: latch AtConv2 and ReadyForNext at top of loop ──────
            SetOutput(out_CarAtConv2,   true);
            SetOutput(out_ReadyForNext, true);
            dbFeedbackPhase = "AtConv2";
            boxLoadedFlag   = false;
            proceedFlag     = false;

            yield return new WaitUntil(() => boxLoadedFlag && !eStopActive);
            boxLoadedFlag = false;

            // FIX A: Atomic phase transition AtConv2 → Driving.
            // CONVEYOR SOLUTION PATTERN: SetValueWithHandoff() clears out_CarAtConv2
            // and latches out_CarDriving in a single call — zero gap frames.
            //
            // ORIGINAL (had a large gap):
            //   SetOutput(out_CarAtConv2,   false);   ← clear
            //   SetOutput(out_ReadyForNext, false);   ← ancillary
            //   dbFeedbackPhase = "BoxLoaded";
            //   // ... conveyor.SetSensorOverride + PLC wait (potentially seconds) ...
            //   SetOutput(out_CarDriving, true);      ← latch ← BIG GAP
            //
            // FIX: handoff immediately, clear ancillary separately.
            IO_Router.Instance?.SetValueWithHandoff(out_CarAtConv2, out_CarDriving);
            SetOutput(out_ReadyForNext, false);
            dbFeedbackPhase = "Driving";

            secondConveyor?.SetSensorOverride(false);
            secondConveyor?.SetHeld(false);

            if (!offlineMode && !string.IsNullOrEmpty(in_ProceedToDrop))
            {
                // While waiting for PLC proceed: car is physically en-route or staged.
                // out_CarDriving stays TRUE — this is the correct physical state
                // (we are committed to the delivery run, just awaiting PLC clearance).
                SetState("Waiting for ProceedToDrop from PLC");
                dbFeedbackPhase = "WaitingProceed (Driving)";
                float waitTime = 0f; bool warned = false;

                yield return new WaitUntil(() =>
                {
                    if (eStopActive) return true;
                    waitTime += Time.deltaTime;
                    if (!warned && waitTime > 5f) { warned = true; Debug.LogWarning($"[CAR2] Waiting >5s for '{in_ProceedToDrop}'."); }
                    return proceedFlag;
                });

                if (eStopActive) { yield return new WaitUntil(() => !eStopActive); continue; }
                proceedFlag = false;
                Debug.Log("[CAR2] ✔ ProceedToDrop confirmed — driving to WH-B.");
                dbFeedbackPhase = "Driving";
            }

            tripStartTime = Time.time;
            tripActive    = true;

            // out_CarDriving already latched TRUE via handoff above — no redundant set needed.

            if (wpMid1 != null) { SetState("Driving → Mid1"); dbNextWP="Mid1"; yield return DriveTo(wpMid1); }
            SetState("Driving → WarehouseB"); dbNextWP="WarehouseB";
            yield return DriveTo(wpWarehouseB);

            // Already correct in original: atomic transition Driving → AtWarehouseB
            IO_Router.Instance?.SetValueWithHandoff(out_CarDriving, out_CarAtWarehouseB);
            dbFeedbackPhase = "AtWarehouseB";

            if (tripActive)
            {
                float trip = Time.time - tripStartTime;
                tripSum += trip; tripSamples++;
                tel_AvgDeliveryTime = tripSamples > 0 ? tripSum / tripSamples : 0f;
                tripActive = false;
            }

            SetState("Depositing at WarehouseB");
            yield return new WaitForSeconds(arrivalPause);

            Transform delivered = carriedObject;
            DetachObject();

            if (warehouseManager != null) warehouseManager.NotifyDelivered(delivered);
            else Debug.LogWarning("[CAR2] warehouseManager is null — NotifyDelivered not called!");

            // FIX B: Atomic phase transition AtWarehouseB → Returning.
            // CONVEYOR SOLUTION PATTERN: handoff clears AtWarehouseB and latches
            // Returning atomically. ObjectDelivered pulse fires as a separate
            // ancillary signal — it does not participate in the phase FSM.
            //
            // ORIGINAL (had a gap):
            //   SetOutput(out_CarAtWarehouseB, false);    ← clear phase
            //   SetOutput(out_ObjectDelivered, true);     ← ancillary pulse starts
            //   yield return new WaitForSeconds(0.2f);    ← 200ms GAP
            //   SetOutput(out_ObjectDelivered, false);    ← ancillary pulse ends
            //   SetOutput(out_CarReturning,    true);     ← latch phase ← GAP
            //
            // FIX: handoff AtWarehouseB→Returning immediately, pulse ObjectDelivered separately.
            IO_Router.Instance?.SetValueWithHandoff(out_CarAtWarehouseB, out_CarReturning);
            dbFeedbackPhase = "Returning";
            tel_DeliveryCount++;

            // Ancillary delivered pulse — fires after phase transition, not before.
            SetOutput(out_ObjectDelivered, true);
            yield return new WaitForSeconds(0.2f);
            SetOutput(out_ObjectDelivered, false);

            if (wpMid2Return != null) { SetState("Returning → Mid2"); dbNextWP="Mid2Return"; yield return DriveTo(wpMid2Return); }
            SetState("Returning → Conv2Exit"); dbNextWP="Conv2Exit";
            yield return DriveTo(wpConv2Exit);

            // FIX C: This was ALREADY CORRECT in the original — preserved unchanged.
            // Atomic transition Returning → AtConv2.
            IO_Router.Instance?.SetValueWithHandoff(out_CarReturning, out_CarAtConv2);
            dbFeedbackPhase = "AtConv2";
            SetState("Idle — ready at Conv2");
            // Loop continues to top — out_ReadyForNext set at top of next iteration.
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void NotifyBoxLoaded(Transform obj)
    {
        if (obj == null) { Debug.LogWarning("[CAR2] NotifyBoxLoaded called with null object!"); return; }

        carriedObject = obj;
        var rb = carriedObject.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity=Vector3.zero; rb.angularVelocity=Vector3.zero; rb.isKinematic=true; }
        carriedObject.SetParent(transform);
        carriedObject.localPosition = carryOffset;
        carriedObject.localRotation = Quaternion.identity;
        dbCarrying    = true;
        boxLoadedFlag = true;

        SetOutput(out_BoxReceived, true);
        StartCoroutine(PulseBoxReceived());
        SetOutput(out_BoxAttached, true);

        Debug.Log($"[CAR2] '{obj.name}' loaded — driving to WH-B.");
    }

    IEnumerator PulseBoxReceived()
    {
        yield return new WaitForSeconds(0.5f);
        SetOutput(out_BoxReceived, false);
    }

    void DetachObject()
    {
        if (carriedObject == null) return;
        carriedObject.SetParent(null);
        carriedObject = null;
        dbCarrying    = false;
        SetOutput(out_BoxAttached, false);
    }

    IEnumerator DriveTo(Transform target)
    {
        if (target == null) yield break;
        Vector3 FlatPos(Transform t) { var p=t.position; p.y=transform.position.y; return p; }

        Vector3 toTarget = FlatPos(target) - transform.position;
        if (toTarget.sqrMagnitude > waypointTolerance * waypointTolerance)
        {
            Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up) * AxisCorr();
            float rs = 0f;
            while (Quaternion.Angle(transform.rotation, desired) > alignThreshold)
            {
                if (eStopActive) yield break;
                toTarget = FlatPos(target) - transform.position;
                if (toTarget.sqrMagnitude < 0.0001f) break;
                desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up) * AxisCorr();
                float ad = Quaternion.Angle(transform.rotation, desired);
                float tr = ad < 20f ? Mathf.Lerp(20f, rotationSpeed, ad/20f) : rotationSpeed;
                rs = Mathf.MoveTowards(rs, tr, rotationSpeed * acceleration * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, rs * Time.deltaTime);
                yield return null;
            }
            transform.rotation = desired;
        }

        currentSpeed = 0f;
        while (true)
        {
            if (eStopActive) yield break;
            Vector3 myPos=transform.position, tPos=FlatPos(target);
            float dist=Vector3.Distance(myPos,tPos);
            if (dist<=waypointTolerance) { currentSpeed=0f; break; }
            float dec=(currentSpeed*currentSpeed)/(2f*acceleration*moveSpeed);
            float ts=dist<=dec+waypointTolerance?Mathf.Lerp(0.3f,moveSpeed,dist/Mathf.Max(dec,0.01f)):moveSpeed;
            currentSpeed=Mathf.Clamp(Mathf.MoveTowards(currentSpeed,ts,acceleration*moveSpeed*Time.deltaTime),0f,moveSpeed);
            transform.position=Vector3.MoveTowards(myPos,tPos,currentSpeed*Time.deltaTime);
            yield return null;
        }
    }

    Quaternion AxisCorr() => forwardAxis switch
    {
        ForwardAxis.NegZ=>Quaternion.Euler(0,180,0), ForwardAxis.X  =>Quaternion.Euler(0,-90,0),
        ForwardAxis.NegX=>Quaternion.Euler(0, 90,0), ForwardAxis.Y  =>Quaternion.Euler(-90,0,0),
        ForwardAxis.NegY=>Quaternion.Euler(90,  0,0), _=>Quaternion.identity,
    };

    string QualifyTag(string tag, string referenceTag)
    {
        if (string.IsNullOrEmpty(tag)) return tag;
        if (tag.Contains(".")) return tag;
        if (string.IsNullOrEmpty(referenceTag) || !referenceTag.Contains(".")) return tag;
        string prefix = referenceTag.Split('.')[0];
        return $"{prefix}.{tag}";
    }

    void SetState(string s) => dbState=s;
    void SetOutput(string tag, bool v) { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }
}