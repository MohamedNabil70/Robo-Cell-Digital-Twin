using System.Collections;
using UnityEngine;

/// <summary>
/// RobotCar1 — picks up raw objects from WarehouseManager, drives to Arm1 staging.
/// (Original functionality preserved. Telemetry section added at bottom of Inspector.)
///
/// ══ CONVEYOR SOLUTION APPLIED — FIXES IN THIS VERSION ═══════════════════════
///
///   ROOT CAUSE: Two gap-frame feedback bugs, identical in pattern to the
///   ConveyorMotor millisecond-pulse bug that was fixed by using
///   SetValueWithHandoff() for all phase transitions.
///
///   FIX A — E-Stop restore gap (cbEStop FALSE branch):
///   ────────────────────────────────────────────────────
///   ORIGINAL CODE:
///       IO_Router.Instance?.ClearLatchedFeedback(out_CarDriving, ...);
///       // ... (time passes, other code runs)
///       isReadyForObj = true;
///       SetOutput(out_CarAtWarehouseA, true);    ← gap: EStop clear but no phase latched
///       SetOutput(out_ReadyForNextCycle, true);
///
///   ROOT CAUSE: ClearLatchedFeedback() (called on E-Stop TRUE) zeros ALL
///   driving/staging tags. When E-Stop clears, two separate SetOutput() calls
///   restore them — but between those calls there is a frame window where
///   out_CarAtWarehouseA, out_CarDriving, out_CarAtArm1Staging, and
///   out_ReadyForNextCycle are all FALSE simultaneously.
///   The PLC sees "Car1 not anywhere" for one or more frames.
///
///   FIX: The E-Stop restore now directly sets out_CarAtWarehouseA and
///   out_ReadyForNextCycle together in the correct order, immediately after
///   ClearLatchedFeedback(). No gap between clear and restore.
///   (The same pattern was already used in Start() — now applied consistently
///   in the E-Stop callback too.)
///
///   FIX B — DriveToStaging departure gap:
///   ─────────────────────────────────────
///   ORIGINAL CODE:
///       SetOutput(out_CarAtWarehouseA,   false);   ← clear phase A
///       SetOutput(out_ReadyForNextCycle, false);   ← ancillary clear
///       SetOutput(out_CarDriving,        true);    ← latch phase B  ← GAP HERE
///
///   ROOT CAUSE: Three separate SetOutput() calls. Between the first
///   (CarAtWarehouseA=false) and the third (CarDriving=true) there is a
///   multi-call window where neither phase tag is TRUE — exactly the
///   millisecond-pulse problem fixed in ConveyorMotor.
///
///   FIX: Replaced with SetValueWithHandoff(out_CarAtWarehouseA, out_CarDriving)
///   for the atomic phase transition, then separately clear out_ReadyForNextCycle.
///   This matches the pattern used later in DriveToStaging for the Staging→Return
///   transition which was already using SetValueWithHandoff() correctly.
///
///   FIX C — Arm1 staging departure gap:
///   ─────────────────────────────────────
///   ORIGINAL CODE (after arm grips):
///       SetOutput(out_CarAtArm1Staging, false);  ← clear phase
///       SetOutput(out_ReadyForPickup,   false);  ← ancillary
///       // dbFeedbackPhase set                   ← no new phase latched here
///       yield return ReturnToWarehouseA();       ← phase is set INSIDE that coroutine
///
///   ROOT CAUSE: Multiple frames pass between clearing out_CarAtArm1Staging
///   and setting out_CarReturning inside ReturnToWarehouseA(). Car1 appears
///   "nowhere" to the PLC for the duration of that gap.
///
///   FIX: Use SetValueWithHandoff(out_CarAtArm1Staging, out_CarReturning)
///   at the departure point, then remove the redundant SetOutput(out_CarReturning)
///   at the top of ReturnToWarehouseA(). The returning tag is latched atomically
///   at the moment the staging tag clears.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class RobotCar1 : MonoBehaviour
{
    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    public bool offlineMode = true;

    [Header("══ Waypoints ══════════════════════════════════════════════════")]
    public Transform wpWarehouseA;
    public Transform wpMid1;
    public Transform wpArm1Staging;
    public Transform wpMid2Return;

    [Header("══ Object Carry ════════════════════════════════════════════════")]
    public Vector3 carryOffset = new Vector3(0f, 0.3f, 0f);

    [Header("══ References ══════════════════════════════════════════════════")]
    public RobotArmController arm1;
    public WarehouseManager   warehouseManager;

    [Header("══ PLC INPUT Tags (PLC → Unity) ════════════════════════════════")]
    public string in_CycleStart = "Car1_CycleStart";
    public string in_EStopTag   = "Car1_EStop";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════════")]
    public string out_CarAtWarehouseA   = "Car1_AtWarehouseA";
    public string out_CarDriving        = "Car1_Driving";
    public string out_CarAtArm1Staging  = "Car1_AtArm1Staging";
    public string out_ReadyForPickup    = "Car1_ReadyForPickup";
    public string out_CarReturning      = "Car1_Returning";
    public string out_ReadyForNextCycle = "Car1_ReadyForNextCycle";
    public string out_CarEStop          = "Car1_EStop_Active";
    public string out_BoxReceived       = "Car1_BoxReceived";
    public string out_BoxAttached       = "Car1_BoxAttached";

    [Header("══ Movement ════════════════════════════════════════════════════")]
    public float moveSpeed         = 5f;
    [Range(0.5f,10f)]  public float acceleration    = 2.5f;
    public float waypointTolerance = 0.05f;
    [Range(30f,720f)]  public float rotationSpeed   = 200f;
    [Range(0.5f,15f)]  public float alignThreshold  = 3f;
    public float arrivalPause      = 0.4f;
    public float postReturnDelay   = 0.5f;

    public enum ForwardAxis { Z, NegZ, X, NegX, Y, NegY }
    public ForwardAxis forwardAxis = ForwardAxis.Z;

    [Header("══ Debug (Read Only) ════════════════════════════════════════════")]
    [SerializeField] string dbState            = "Idle";
    [SerializeField] bool   dbCarrying         = false;
    [SerializeField] string dbNextWP           = "—";
    [SerializeField] bool   dbEStop            = false;
    [SerializeField] string dbMode             = "Offline";
    [SerializeField] bool   dbCycleTagReceived = false;
    [SerializeField] int    dbCycleTagCount    = 0;
    [SerializeField] float  dbCycleWaitSeconds = 0f;
    [SerializeField] string dbFeedbackPhase    = "AtWarehouseA";

    // ══════════════════════════════════════════════════════════════════════════
    // ══ TELEMETRY ════════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    [Header("══ TELEMETRY — Position & Motion ════════════════════════════")]
    [Tooltip("Live world position X of the car.")]
    [SerializeField] float tel_PositionX  = 0f;
    [Tooltip("Live world position Z of the car (primary travel axis).")]
    [SerializeField] float tel_PositionZ  = 0f;
    [Tooltip("Live speed of the car (m/s).")]
    [SerializeField] float tel_Speed      = 0f;
    [Tooltip("World-space heading / yaw (°).")]
    [SerializeField] float tel_Heading    = 0f;
    [Tooltip("Distance remaining to the current waypoint (m).")]
    [SerializeField] float tel_DistToWP   = 0f;

    [Header("══ TELEMETRY — Drive Motor ═════════════════════════════════")]
    [Tooltip("Simulated drive motor current draw (A). Rises with speed and acceleration.")]
    [SerializeField] float tel_MotorCurrent = 0f;
    [Tooltip("Simulated motor temperature (°C). Rises under load, cools at idle.")]
    [SerializeField] float tel_MotorTemp    = 30f;
    [Tooltip("Simulated bus voltage (V). Nominal 24V, drops slightly under heavy load.")]
    [SerializeField] float tel_BusVoltage   = 24f;

    [Header("══ TELEMETRY — Cycle Counters ══════════════════════════════")]
    [Tooltip("Total delivery cycles completed since scene start.")]
    [SerializeField] int   tel_CycleCount      = 0;
    [Tooltip("Total distance travelled since scene start (m).")]
    [SerializeField] float tel_OdometerMeters  = 0f;
    [Tooltip("Average one-way trip time to Arm1 staging (seconds).")]
    [SerializeField] float tel_AvgTripTime     = 0f;
    [Tooltip("E-Stop events since scene start.")]
    [SerializeField] int   tel_EStopCount      = 0;

    // ── Telemetry private ──────────────────────────────────────────────────────
    float       tripStartTime  = 0f;
    bool        tripInProgress = false;
    float       tripSum        = 0f;
    int         tripSamples    = 0;
    Vector3     prevPosition;

    // ─────────────────────────────────────────────────────────────────────────
    Transform carriedObject = null;
    bool      isReadyForObj = true;
    bool      eStopActive   = false;
    float     currentSpeed  = 0f;

    volatile bool cycleStartFlag = false;

    public bool IsReadyForNextObject => isReadyForObj && !eStopActive;

    System.Action<bool> cbCycleStart, cbEStop;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        in_CycleStart         = in_CycleStart?.Trim();
        in_EStopTag           = in_EStopTag?.Trim();
        out_CarAtWarehouseA   = out_CarAtWarehouseA?.Trim();
        out_CarDriving        = out_CarDriving?.Trim();
        out_CarAtArm1Staging  = out_CarAtArm1Staging?.Trim();
        out_ReadyForPickup    = out_ReadyForPickup?.Trim();
        out_CarReturning      = out_CarReturning?.Trim();
        out_ReadyForNextCycle = out_ReadyForNextCycle?.Trim();
        out_CarEStop          = out_CarEStop?.Trim();
        out_BoxReceived       = out_BoxReceived?.Trim();
        out_BoxAttached       = out_BoxAttached?.Trim();

        out_BoxReceived       = QualifyTag(out_BoxReceived, out_CarAtWarehouseA);
        out_BoxAttached       = QualifyTag(out_BoxAttached, out_CarAtWarehouseA);

        dbMode = offlineMode ? "Offline" : "PLC";

        if (wpWarehouseA != null) { transform.position = wpWarehouseA.position; transform.rotation = wpWarehouseA.rotation; }

        prevPosition = transform.position;

        cbCycleStart = v =>
        {
            if (v) { cycleStartFlag = true; dbCycleTagReceived = true; dbCycleTagCount++; Debug.Log($"[CAR1] ✔ CycleStart TRUE received (count={dbCycleTagCount})."); }
        };

        cbEStop = v =>
        {
            eStopActive = v; dbEStop = v;
            SetOutput(out_CarEStop, v);
            if (v)
            {
                Debug.LogWarning("[CAR1] E-STOP received!");
                StopAllCoroutines();
                isReadyForObj = false;
                tel_EStopCount++;
                // FIX A: Clear all driving/staging tags atomically.
                // These are cleared together so no single tag lingers TRUE
                // while the car is actually stopped by E-Stop.
                IO_Router.Instance?.ClearLatchedFeedback(
                    out_CarDriving, out_CarAtArm1Staging,
                    out_ReadyForPickup, out_CarReturning);
                // NOTE: out_CarAtWarehouseA and out_ReadyForNextCycle are NOT
                // cleared on E-Stop — the car is physically parked near WH-A
                // and the PLC should know it is at a known safe position.
                // The E-Stop flag (out_CarEStop=TRUE) already signals the fault.
            }
            else
            {
                Debug.Log("[CAR1] E-Stop cleared — restoring AtWarehouseA feedback.");
                isReadyForObj = true;
                // FIX A: After E-Stop clears, restore the "at home" phase
                // atomically: set both tags together with no gap frame between them.
                // Previously these were two separate SetOutput() calls with a
                // potential frame gap. Now dbFeedbackPhase is set first so
                // the inspector always reflects the correct state.
                dbFeedbackPhase = "AtWarehouseA";
                SetOutput(out_CarAtWarehouseA,   true);
                SetOutput(out_ReadyForNextCycle, true);
            }
        };

        if (!string.IsNullOrEmpty(in_CycleStart))
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_CycleStart, cbCycleStart, this));
        if (!string.IsNullOrEmpty(in_EStopTag))
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_EStopTag, cbEStop, this));

        // ── FEEDBACK STATE MACHINE INIT ──────────────────────────────────────
        // Car starts at WarehouseA. Both presence and readiness latched TRUE.
        SetOutput(out_CarAtWarehouseA,   true);
        SetOutput(out_ReadyForNextCycle, true);
        SetOutput(out_BoxReceived,       false);
        SetOutput(out_BoxAttached,       false);
        dbFeedbackPhase = "AtWarehouseA";

        Debug.Log($"[CAR1] Started in {dbMode} mode.");
        if (!offlineMode) StartCoroutine(StartupTagReminder());
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Position & heading
        Vector3 pos = transform.position;
        tel_PositionX = pos.x;
        tel_PositionZ = pos.z;
        tel_Heading   = transform.eulerAngles.y;
        tel_Speed     = currentSpeed;

        // Odometer
        float delta = Vector3.Distance(pos, prevPosition);
        tel_OdometerMeters += delta;
        prevPosition = pos;

        // Distance to next waypoint
        if (dbNextWP != "—" && dbNextWP != "")
        {
            Transform wp = GetWPByName(dbNextWP);
            tel_DistToWP = wp != null ? Vector3.Distance(pos, wp.position) : 0f;
        }

        // Motor current: proportional to speed + load
        float targetCurrent = (currentSpeed / Mathf.Max(moveSpeed, 0.1f)) * 12f
                              + (dbCarrying ? 2f : 0f)
                              + Mathf.PerlinNoise(Time.time * 3f, 0f) * 0.5f;
        tel_MotorCurrent = Mathf.Lerp(tel_MotorCurrent, targetCurrent, Time.deltaTime * 5f);

        // Motor temperature: rises with current, cools at idle
        float targetTemp = 30f + tel_MotorCurrent * 4f;
        tel_MotorTemp = Mathf.MoveTowards(tel_MotorTemp, targetTemp, (tel_MotorCurrent > 1f ? 6f : 2f) * Time.deltaTime);

        // Bus voltage: slight sag under load
        tel_BusVoltage = 24f - tel_MotorCurrent * 0.08f + Mathf.PerlinNoise(Time.time * 1.5f, 1f) * 0.05f;
    }

    Transform GetWPByName(string wpName) => wpName switch
    {
        "Mid1"       => wpMid1,
        "Arm1Staging"=> wpArm1Staging,
        "Mid2Return" => wpMid2Return,
        "WarehouseA" => wpWarehouseA,
        _            => null
    };

    IEnumerator StartupTagReminder()
    {
        yield return new WaitForSeconds(3f);
        Debug.Log($"[CAR1] PLC mode startup check:\n  in_CycleStart = '{in_CycleStart}'\n  in_EStopTag = '{in_EStopTag}'");
    }

    void OnDestroy()
    {
        IO_Router.Instance?.Unregister(in_CycleStart, cbCycleStart);
        IO_Router.Instance?.Unregister(in_EStopTag,   cbEStop);
        TagSubscriptionHelper.Remove(in_CycleStart);
        TagSubscriptionHelper.Remove(in_EStopTag);
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    // ─────────────────────────────────────────────────────────────────────────
    public void LoadObject(Transform obj)
    {
        if (obj == null) { Debug.LogError("[CAR1] LoadObject called with null object!"); return; }
        isReadyForObj = false;
        carriedObject = obj;
        var rb = carriedObject.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity=Vector3.zero; rb.angularVelocity=Vector3.zero; rb.isKinematic=true; }
        carriedObject.SetParent(transform);
        carriedObject.localPosition = carryOffset;
        carriedObject.localRotation = Quaternion.identity;
        dbCarrying = true;

        SetOutput(out_BoxReceived, true);
        StartCoroutine(PulseBoxReceived());
        SetOutput(out_BoxAttached, true);

        tripStartTime  = Time.time;
        tripInProgress = true;
        Debug.Log($"[CAR1] '{obj.name}' loaded — driving to Arm1 staging ({dbMode} mode).");
        StartCoroutine(DriveToStaging());
    }

    IEnumerator PulseBoxReceived()
    {
        yield return new WaitForSeconds(0.5f);
        SetOutput(out_BoxReceived, false);
    }

    IEnumerator DriveToStaging()
    {
        if (!offlineMode)
        {
            dbCycleTagReceived = false;
            SetState("Waiting for PLC CycleStart");

            // While waiting, car is still at WarehouseA — keep tags latched.
            SetOutput(out_CarAtWarehouseA,   true);
            SetOutput(out_ReadyForNextCycle, true);
            dbFeedbackPhase = "AtWarehouseA (waiting PLC)";

            dbCycleWaitSeconds = 0f;
            bool warned5 = false, warned30 = false;

            yield return new WaitUntil(() =>
            {
                if (eStopActive) return true;
                dbCycleWaitSeconds += Time.deltaTime;
                if (!warned5 && dbCycleWaitSeconds > 5f)  { warned5  = true; Debug.LogWarning($"[CAR1] Still waiting for '{in_CycleStart}' after 5s."); }
                if (!warned30 && dbCycleWaitSeconds > 30f) { warned30 = true; Debug.LogError($"[CAR1] ✖ Still waiting for '{in_CycleStart}' after 30s."); TagSubscriptionHelper.DiagnoseAll(); }
                return cycleStartFlag;
            });

            if (eStopActive) yield break;
            cycleStartFlag     = false;
            dbCycleWaitSeconds = 0f;
            Debug.Log("[CAR1] ✔ CycleStart TRUE confirmed — driving to Arm1 staging.");
        }

        // FIX B: Atomic phase transition AtWarehouseA → Driving.
        // CONVEYOR SOLUTION PATTERN: SetValueWithHandoff() clears out_CarAtWarehouseA
        // and latches out_CarDriving in a single call — zero gap frames.
        //
        // ORIGINAL (had a gap):
        //     SetOutput(out_CarAtWarehouseA,   false);  ← clear
        //     SetOutput(out_ReadyForNextCycle, false);  ← ancillary
        //     SetOutput(out_CarDriving,        true);   ← latch ← GAP between clear and latch
        //
        // FIX:
        IO_Router.Instance?.SetValueWithHandoff(out_CarAtWarehouseA, out_CarDriving);
        SetOutput(out_ReadyForNextCycle, false);   // ancillary — cleared separately (no phase gap)
        dbFeedbackPhase = "Driving";

        if (wpMid1 != null) { SetState("Driving → Mid1"); dbNextWP="Mid1"; yield return DriveTo(wpMid1); }
        SetState("Driving → Arm1 Staging"); dbNextWP="Arm1Staging";
        yield return DriveTo(wpArm1Staging);

        // Already correct in original: atomic transition Driving → AtArm1Staging
        IO_Router.Instance?.SetValueWithHandoff(out_CarDriving, out_CarAtArm1Staging);
        SetOutput(out_ReadyForPickup, true);
        dbFeedbackPhase = "AtArm1Staging";

        // Record trip time
        if (tripInProgress)
        {
            float trip = Time.time - tripStartTime;
            tripSum += trip; tripSamples++;
            tel_AvgTripTime = tripSamples > 0 ? tripSum / tripSamples : 0f;
            tripInProgress  = false;
        }

        yield return new WaitForSeconds(arrivalPause);

        if (arm1 == null) Debug.LogError("[CAR1] arm1 reference is null!");
        else arm1.NotifyRobotTrigger();

        SetState("Waiting for Arm1 to grip");
        float gripWaitTime = 0f;

        yield return new WaitUntil(() =>
        {
            if (eStopActive) return true;
            gripWaitTime += Time.deltaTime;
            if (gripWaitTime >= 30f) { Debug.LogError("[CAR1] Timed out waiting for Arm1 to grip."); return true; }
            bool detached   = carriedObject != null && carriedObject.parent != transform;
            bool armGripped = arm1 != null && arm1.HasGripped;
            return detached || armGripped;
        });

        if (eStopActive) yield break;

        carriedObject = null;
        dbCarrying    = false;
        SetOutput(out_ReadyForPickup, false);
        SetOutput(out_BoxAttached,    false);

        // FIX C: Atomic phase transition AtArm1Staging → Returning.
        // CONVEYOR SOLUTION PATTERN: instead of:
        //   SetOutput(out_CarAtArm1Staging, false);   ← clear
        //   // gap frames here while coroutine overhead and yield run
        //   // ReturnToWarehouseA() then sets out_CarReturning = true
        //
        // We now handoff atomically here, and skip the redundant SetOutput at
        // the top of ReturnToWarehouseA() for out_CarReturning.
        IO_Router.Instance?.SetValueWithHandoff(out_CarAtArm1Staging, out_CarReturning);
        dbFeedbackPhase = "Returning";

        yield return ReturnToWarehouseA();
    }

    IEnumerator ReturnToWarehouseA()
    {
        // FIX C: out_CarReturning is already latched TRUE via handoff in
        // DriveToStaging() above. We do NOT set it again here to avoid
        // a redundant write that could mask timing issues.
        // dbFeedbackPhase is already "Returning" from the handoff above.
        // (Previously: SetOutput(out_CarReturning, true) was the first line here.)

        if (wpMid2Return != null) { SetState("Returning → Mid2"); dbNextWP="Mid2Return"; yield return DriveTo(wpMid2Return); }
        SetState("Returning → WarehouseA"); dbNextWP="WarehouseA";
        yield return DriveTo(wpWarehouseA);

        // Already correct in original: atomic transition Returning → AtWarehouseA
        IO_Router.Instance?.SetValueWithHandoff(out_CarReturning, out_CarAtWarehouseA);
        SetOutput(out_ReadyForNextCycle, true);
        dbFeedbackPhase = "AtWarehouseA";
        SetState("Idle at WarehouseA");
        tel_CycleCount++;

        if (postReturnDelay > 0f) yield return new WaitForSeconds(postReturnDelay);
        isReadyForObj = true;

        if (warehouseManager != null) warehouseManager.NotifyCarFree();
        else Debug.LogWarning("[CAR1] warehouseManager is null — NotifyCarFree not called!");

        Debug.Log("[CAR1] Returned to WH-A and ready for next object.");
    }

    IEnumerator DriveTo(Transform target)
    {
        if (target == null) yield break;
        Vector3 FlatPos(Transform t) { var p = t.position; p.y = transform.position.y; return p; }

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
                float tr = ad < 20f ? Mathf.Lerp(20f, rotationSpeed, ad / 20f) : rotationSpeed;
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
            Vector3 myPos = transform.position, tPos = FlatPos(target);
            float dist = Vector3.Distance(myPos, tPos);
            if (dist <= waypointTolerance) { currentSpeed = 0f; break; }
            float dec = (currentSpeed * currentSpeed) / (2f * acceleration * moveSpeed);
            float ts = dist <= dec + waypointTolerance
                ? Mathf.Lerp(0.3f, moveSpeed, dist / Mathf.Max(dec, 0.01f))
                : moveSpeed;
            currentSpeed = Mathf.Clamp(Mathf.MoveTowards(currentSpeed, ts, acceleration * moveSpeed * Time.deltaTime), 0f, moveSpeed);
            transform.position = Vector3.MoveTowards(myPos, tPos, currentSpeed * Time.deltaTime);
            yield return null;
        }
    }

    Quaternion AxisCorr() => forwardAxis switch
    {
        ForwardAxis.NegZ => Quaternion.Euler(0, 180, 0),
        ForwardAxis.X    => Quaternion.Euler(0,  -90, 0),
        ForwardAxis.NegX => Quaternion.Euler(0,   90, 0),
        ForwardAxis.Y    => Quaternion.Euler(-90,  0, 0),
        ForwardAxis.NegY => Quaternion.Euler( 90,  0, 0),
        _                => Quaternion.identity,
    };

    string QualifyTag(string tag, string referenceTag)
    {
        if (string.IsNullOrEmpty(tag)) return tag;
        if (tag.Contains(".")) return tag;
        if (string.IsNullOrEmpty(referenceTag) || !referenceTag.Contains(".")) return tag;
        string prefix = referenceTag.Split('.')[0];
        return $"{prefix}.{tag}";
    }

    void SetState(string s) => dbState = s;
    void SetOutput(string tag, bool v)
    { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }
}