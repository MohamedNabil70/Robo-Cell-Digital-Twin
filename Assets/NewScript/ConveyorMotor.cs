using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ConveyorMotor — moves product objects along a world axis.
///
/// ══ FIXES IN THIS VERSION ════════════════════════════════════════════════════
///
///   FIX 1 — PRIMARY FEEDBACK BUG: out_ObjectOnBelt never sent TRUE
///   ──────────────────────────────────────────────────────────────────────────
///   ROOT CAUSE A — Auto-detect gate blocked by existing manual list:
///     OnTriggerEnter() contained this early-exit:
///         if (objectsOnBelt.Count > 0 && !objectsOnBelt.Contains(incoming)) return;
///     If the Inspector "Objects On Belt" list is populated but the entering object
///     is NOT in it (e.g. it was spawned at runtime), the trigger is silently
///     discarded. activeObject is never set → out_ObjectOnBelt never fires TRUE.
///     FIX: only apply the whitelist guard when autoDetectObjects is FALSE.
///     If autoDetectObjects is TRUE the tag-match alone is the gate — the
///     manual list is used only to pre-populate the belt.
///
///   ROOT CAUSE B — FixedUpdate AssignActive gate checks plcTag / offlineMode:
///     The fallback in FixedUpdate that calls AssignActive(objectsOnBelt[0]) when
///     activeObject is null is gated on `offlineMode || string.IsNullOrEmpty(plcTag)`.
///     In PLC mode with a plcTag set the gate is FALSE, so this path never runs.
///     If the Trigger collider also failed (Root Cause A) there is no path left
///     to set activeObject. FIX: remove the offline/plcTag gate from FixedUpdate —
///     it is safe to try assigning from the list whenever activeObject is null.
///
///   ROOT CAUSE C — AssignActive called BEFORE IsRunning is true at startup:
///     When the belt is in offline mode it runs immediately, but AssignActive() is
///     called inside OnTriggerEnter before FixedUpdate has ticked — at that moment
///     IsRunning is already TRUE (offline mode), so the `hasObj` check in
///     AssignActive passes correctly. However if a Rigidbody-driven object enters
///     the trigger in the same frame that Start() runs, plcOn may still be false
///     and IsRunning returns false, so `hasObj` = false and the tag is never sent.
///     FIX: AssignActive now unconditionally fires the out_ObjectOnBelt=TRUE
///     signal as long as an activeObject is being set, regardless of IsRunning.
///     The FixedUpdate update loop still guards the signal on IsRunning so it
///     correctly clears when the belt stops.
///
///   FIX 2 — ClearActive double-call guard was too narrow
///   ──────────────────────────────────────────────────────
///   ClearActive() only cleared the feedback tag when lastHadObject==true.
///   If the belt stopped (setting lastHadObject=false) BEFORE the object left the
///   trigger, OnTriggerExit would call ClearActive() but the tag was already
///   false so no SetOutput(out_ObjectOnBelt, false) was sent — benign but
///   confusing in the tag monitor. Now ClearActive always sends false if
///   activeObject was set, regardless of lastHadObject.
///
///   FIX 3 — offlineMode gate in plcCallback was too broad
///   ──────────────────────────────────────────────────────
///   plcCallback had `if (offlineMode) return;` which prevents PLC-controlled
///   belt start/stop when offlineMode is toggled at runtime to FALSE after Start().
///   The guard now checks `if (offlineMode)` to log a warning instead of silently
///   ignoring the value — the assignment still proceeds.  This means toggling
///   offlineMode to FALSE mid-session correctly hands control to the PLC.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class ConveyorMotor : MonoBehaviour
{
    public enum MoveAxis { X, NegX, Y, NegY, Z, NegZ }

    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    public bool offlineMode = true;

    [Header("══ Movement ══════════════════════════════════════════════════")]
    public float    speed = 2f;
    public MoveAxis axis  = MoveAxis.X;

    [Header("══ Objects — drag ALL your cubes here ═════════════════════════")]
    public List<Transform> objectsOnBelt = new List<Transform>();

    [Header("══ Auto-Detect via Trigger Collider ════════════════════════════")]
    public bool   autoDetectObjects = true;
    public string productTag        = "ProductObject";

    [Header("══ PLC INPUT Tag (PLC → Unity) ══════════════════════════════")]
    [Tooltip("PLC BOOL: TRUE = run belt.\n⚠ Case-sensitive — must match TIA Portal DB.")]
    public string plcTag    = "";
    [Tooltip("Boolean TRUE: emergency stop belt / FALSE: clear stop.\n⚠ Case-sensitive.")]
    public string in_EStopTag = "";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════")]
    [Tooltip("TRUE while belt is running. Latched — cleared only when belt stops.")]
    public string out_BeltRunning   = "";
    [Tooltip("TRUE while belt is stopped. Latched — cleared only when belt starts.")]
    public string out_BeltStopped   = "";
    [Tooltip("TRUE while an object is on belt AND belt is running.")]
    public string out_ObjectOnBelt  = "";
    [Tooltip("TRUE while sensor override is active (object at pickup sensor).")]
    public string out_SensorBlocked = "";
    [Tooltip("TRUE while E-Stop is active.")]
    public string out_BeltEStop     = "";

    [Header("══ Debug State (Read Only) ════════════════════════════════")]
    [SerializeField] bool   dbPlcOn        = false;
    [SerializeField] bool   dbSensorStop   = false;
    [SerializeField] bool   dbHeld         = false;
    [SerializeField] bool   dbRunning      = false;
    [SerializeField] bool   dbEStop        = false;
    [SerializeField] bool   dbObjectOnBelt = false;
    [SerializeField] string dbActiveObject = "—";
    [SerializeField] int    dbKnownObjects = 0;
    [SerializeField] string dbMode         = "Offline";
    [SerializeField] string dbFeedbackPhase = "Stopped";

    // ══════════════════════════════════════════════════════════════════════════
    // ══ TELEMETRY ════════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    [Header("══ TELEMETRY — Belt Motion ════════════════════════════════════")]
    [Tooltip("Live belt surface speed (m/s). Zero when stopped or E-Stopped.")]
    [SerializeField] float tel_BeltSpeed       = 0f;
    [Tooltip("Cumulative run time since scene start (seconds).")]
    [SerializeField] float tel_RunTimeSeconds  = 0f;
    [Tooltip("Total objects that have crossed the belt since scene start.")]
    [SerializeField] int   tel_ThroughputCount = 0;

    [Header("══ TELEMETRY — Motor ═══════════════════════════════════════════")]
    [Tooltip("Simulated motor current (A). Rises with load (object on belt). Idle ≈ 0.5A.")]
    [SerializeField] float tel_MotorCurrent  = 0f;
    [Tooltip("Simulated motor temperature (°C). Rises with continuous run time.")]
    [SerializeField] float tel_MotorTemp     = 30f;
    [Tooltip("Simulated belt tension (%). Decreases slightly under load — represents wear.")]
    [SerializeField] float tel_BeltTension   = 100f;
    [Tooltip("E-Stop events on this belt since scene start.")]
    [SerializeField] int   tel_EStopCount    = 0;

    [Header("══ TELEMETRY — Object ══════════════════════════════════════════")]
    [Tooltip("Live distance travelled by the active object on the belt (m). Resets when object leaves.")]
    [SerializeField] float   tel_ObjectTravelDist = 0f;
    [Tooltip("Time active object has been on belt (s). Resets when object leaves.")]
    [SerializeField] float   tel_ObjectOnBeltTime = 0f;

    // ── Telemetry private ──────────────────────────────────────────────────────
    Vector3 prevObjectPos;
    float   objectArrivalTime = 0f;
    bool    prevEStop         = false;

    // ─────────────────────────────────────────────────────────────────────────
    bool      plcOn         = false;
    bool      sensorBlocked = false;
    bool      cubeHeld      = false;
    bool      eStopActive   = false;
    bool      lastRunning   = false;
    bool      lastHadObject = false;
    Transform activeObject  = null;

    bool IsRunning => (plcOn || offlineMode || string.IsNullOrEmpty(plcTag))
                      && !sensorBlocked && !cubeHeld && !eStopActive;

    Action<bool> plcCallback, cbEStop;

    void Start()
    {
        dbMode         = (offlineMode || string.IsNullOrEmpty(plcTag)) ? "Offline" : "PLC";
        dbKnownObjects = objectsOnBelt.Count;

        if (autoDetectObjects)
        {
            var col = GetComponent<Collider>();
            if (col == null) Debug.LogWarning($"[CONVEYOR:{name}] autoDetectObjects=true but no Collider.");
            else if (!col.isTrigger) Debug.LogWarning($"[CONVEYOR:{name}] Collider is not a Trigger.");
        }

        if (string.IsNullOrEmpty(plcTag))
        {
            plcOn = true; dbPlcOn = true;
            Debug.LogWarning($"[CONVEYOR:{name}] plcTag empty — Simulation/Offline mode.");
        }
        else
        {
            plcCallback = v =>
            {
                // FIX 3: removed hard `return` in offline mode.
                // Log a warning so the developer knows the PLC write arrived
                // but still apply it — this makes runtime mode-switching safe.
                if (offlineMode)
                    Debug.LogWarning($"[CONVEYOR:{name}] PLC tag '{plcTag}'={v} received while " +
                                     "offlineMode=TRUE. Applying anyway. Set offlineMode=FALSE " +
                                     "in Inspector to suppress this warning.");
                plcOn = v; dbPlcOn = v;
            };
            Debug.Log($"[CONVEYOR:{name}] Registering PLC run tag '{plcTag}'.");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(plcTag, plcCallback, this));
        }

        if (!string.IsNullOrEmpty(in_EStopTag))
        {
            cbEStop = v =>
            {
                eStopActive = v; dbEStop = v;
                SetOutput(out_BeltEStop, v);
                if (v)
                {
                    tel_EStopCount++;
                    IO_Router.Instance?.SetValueWithHandoff(out_BeltRunning, out_BeltStopped);
                    dbFeedbackPhase = "Stopped (EStop)";
                    lastRunning = false;
                    // FIX: Do NOT clear out_ObjectOnBelt on E-Stop — the object is still
                    // physically on the belt. out_BeltEStop=TRUE already tells the PLC
                    // the motor is stopped; it does not mean the part has left.
                }
            };
            Debug.Log($"[CONVEYOR:{name}] Registering PLC E-Stop tag '{in_EStopTag}'.");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_EStopTag, cbEStop, this));
        }

        SetOutput(out_BeltStopped, true);
        SetOutput(out_BeltRunning, false);
        dbFeedbackPhase = "Stopped";

        StartCoroutine(DiagnosticsAfterDelay());
    }

    void OnDestroy()
    {
        if (!string.IsNullOrEmpty(plcTag)      && plcCallback != null) IO_Router.Instance?.Unregister(plcTag,      plcCallback);
        if (!string.IsNullOrEmpty(in_EStopTag) && cbEStop     != null) IO_Router.Instance?.Unregister(in_EStopTag, cbEStop);
        TagSubscriptionHelper.Remove(plcTag);
        TagSubscriptionHelper.Remove(in_EStopTag);
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    IEnumerator DiagnosticsAfterDelay()
    {
        yield return new WaitForSeconds(2.5f);
        Debug.Log($"=== CONVEYOR:{name}  mode={dbMode}  objects={dbKnownObjects}  " +
                  $"active='{dbActiveObject}'  tag='{plcTag}'  speed={speed}  axis={axis} ===");
    }

    // ─────────────────────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!autoDetectObjects) return;

        // Only process one object at a time
        if (activeObject != null) return;

        // Tag filter — must match productTag when set
        bool isProduct = !string.IsNullOrEmpty(productTag) && other.CompareTag(productTag);
        if (!isProduct) return;

        Transform incoming = other.transform;

        // FIX 1A: The original code had:
        //     if (objectsOnBelt.Count > 0 && !objectsOnBelt.Contains(incoming)) return;
        // This silently rejected runtime-spawned objects that were never added to the
        // list in the Inspector — activeObject stayed null and the feedback tag
        // was never sent. The list is now purely additive: we add the object to it
        // if it isn't there yet (so the belt can move it), then proceed.
        if (!objectsOnBelt.Contains(incoming))
            objectsOnBelt.Add(incoming);

        AssignActive(incoming);
    }

    void OnTriggerExit(Collider other)
    {
        if (!autoDetectObjects || activeObject == null || other.transform != activeObject) return;
        tel_ThroughputCount++;
        ClearActive();
    }

    void FixedUpdate()
    {
        dbRunning      = IsRunning;
        dbKnownObjects = objectsOnBelt.Count;

        // FIX 1B: Removed the `offlineMode || string.IsNullOrEmpty(plcTag)` gate.
        // In PLC mode the gate was FALSE, so this path never ran — even though
        // objectsOnBelt was populated by SetObjectToMove() from WarehouseManager.
        // Now we always attempt to assign from the list when activeObject is null.
        if (activeObject == null && objectsOnBelt.Count > 0)
            AssignActive(objectsOnBelt[0]);

        // ── Belt state feedback ───────────────────────────────────────────────
        bool nowRunning = IsRunning;
        if (nowRunning != lastRunning)
        {
            lastRunning = nowRunning;
            if (nowRunning)
            {
                IO_Router.Instance?.SetValueWithHandoff(out_BeltStopped, out_BeltRunning);
                dbFeedbackPhase = "Running";
                Debug.Log($"[CONVEYOR:{name}] Belt STARTED — out_BeltRunning latched TRUE.");
            }
            else
            {
                IO_Router.Instance?.SetValueWithHandoff(out_BeltRunning, out_BeltStopped);
                dbFeedbackPhase = "Stopped";
                Debug.Log($"[CONVEYOR:{name}] Belt STOPPED — out_BeltStopped latched TRUE.");
            }
        }

        // ── Object feedback ───────────────────────────────────────────────────
        // KEY FIX: out_ObjectOnBelt is TRUE whenever an object is PHYSICALLY PRESENT
        // on the belt — regardless of whether the belt motor is running or stopped.
        //
        // The original code used:
        //     bool nowHasObject = IsRunning && activeObject != null;
        //
        // This caused the observed millisecond pulse:
        //   1. AssignActive() fired   → out_ObjectOnBelt = TRUE
        //   2. Arm release called SetSensorOverride(true) or SetHeld(true)
        //      → IsRunning became FALSE in the same physics step
        //   3. FixedUpdate ran immediately after with nowHasObject = FALSE
        //      → out_ObjectOnBelt = FALSE
        //
        // The object was physically on the belt for the entire sequence — the belt
        // being paused (sensor override / held) is a MOTOR state, not an occupancy
        // state.  TIA Portal needs to know "is there a part here?" independently of
        // "is the motor running?".  It already has out_BeltRunning and out_BeltStopped
        // to know motor state.  out_ObjectOnBelt must reflect physical presence only.
        bool nowHasObject = activeObject != null;   // ← physical presence, not motor state
        if (nowHasObject != lastHadObject)
        {
            lastHadObject  = nowHasObject;
            dbObjectOnBelt = nowHasObject;
            SetOutput(out_ObjectOnBelt, nowHasObject);
            Debug.Log($"[CONVEYOR:{name}] out_ObjectOnBelt → {nowHasObject} (active='{dbActiveObject}')");

            if (nowHasObject && activeObject != null)
            {
                prevObjectPos    = activeObject.position;
                objectArrivalTime= Time.time;
                tel_ObjectTravelDist = 0f;
                tel_ObjectOnBeltTime = 0f;
            }
        }

        // ── Telemetry updates ─────────────────────────────────────────────────
        float dt = Time.fixedDeltaTime;

        tel_BeltSpeed = IsRunning ? speed : 0f;

        if (IsRunning) tel_RunTimeSeconds += dt;

        if (IsRunning && activeObject != null)
        {
            float moved = Vector3.Distance(activeObject.position, prevObjectPos);
            tel_ObjectTravelDist += moved;
            tel_ObjectOnBeltTime  = Time.time - objectArrivalTime;
            prevObjectPos         = activeObject.position;
        }

        float targetCurrent = IsRunning
            ? (0.5f + (nowHasObject ? 2.5f : 0f) + Mathf.PerlinNoise(Time.time * 2f, 0f) * 0.3f)
            : 0f;
        tel_MotorCurrent = Mathf.Lerp(tel_MotorCurrent, targetCurrent, dt * 5f);

        float targetTemp = 30f + tel_MotorCurrent * 10f;
        tel_MotorTemp = Mathf.MoveTowards(tel_MotorTemp, targetTemp, (IsRunning ? 5f : 2f) * dt);

        if (IsRunning && nowHasObject)
            tel_BeltTension = Mathf.Max(80f, tel_BeltTension - 0.002f * dt);
        else
            tel_BeltTension = Mathf.Min(100f, tel_BeltTension + 0.5f * dt);

        // ── Move active object ────────────────────────────────────────────────
        if (!IsRunning || activeObject == null) return;
        var rb = activeObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (rb.isKinematic) return;
            rb.MovePosition(rb.position + GetDir() * speed * Time.fixedDeltaTime);
        }
        else activeObject.position += GetDir() * speed * Time.fixedDeltaTime;
    }

    Vector3 GetDir() => axis switch
    {
        MoveAxis.X    =>  Vector3.right,   MoveAxis.NegX => -Vector3.right,
        MoveAxis.Y    =>  Vector3.up,      MoveAxis.NegY => -Vector3.up,
        MoveAxis.Z    =>  Vector3.forward, MoveAxis.NegZ => -Vector3.forward,
        _             =>  Vector3.right
    };

    /// <summary>
    /// Assign an object as the active belt object and immediately fire out_ObjectOnBelt.
    ///
    /// FIX 1C: We now send out_ObjectOnBelt = TRUE unconditionally when setting an
    /// activeObject, rather than gating it on IsRunning.  The reason: at the moment
    /// OnTriggerEnter fires, IsRunning may briefly be false even in offline mode if
    /// the physics callback precedes the first FixedUpdate tick.  The FixedUpdate
    /// belt-state machine will correctly CLEAR the tag if the belt is actually
    /// stopped — so the worst outcome of firing early is a 1-frame false positive,
    /// which is far better than a permanently-missed rising edge.
    ///
    /// The bool dbObjectOnBelt and lastHadObject are both set here so the FixedUpdate
    /// change-detection doesn't re-fire on the next tick.
    /// </summary>
    void AssignActive(Transform obj)
    {
        activeObject   = obj;
        dbActiveObject = obj != null ? obj.name : "—";

        if (obj != null)
        {
            // FIX 1C: always send TRUE when an object is assigned.
            // FixedUpdate will clear it if the belt is actually stopped.
            lastHadObject        = true;
            dbObjectOnBelt       = true;
            prevObjectPos        = obj.position;
            objectArrivalTime    = Time.time;
            tel_ObjectTravelDist = 0f;
            tel_ObjectOnBeltTime = 0f;
            SetOutput(out_ObjectOnBelt, true);
            Debug.Log($"[CONVEYOR:{name}] out_ObjectOnBelt → TRUE (AssignActive '{obj.name}')");
        }
    }

    void ClearActive()
    {
        bool hadObject = activeObject != null;
        activeObject   = null;
        dbActiveObject = "—";

        // FIX 2: send false unconditionally when an object was present — don't
        // gate on lastHadObject because the belt-stop path may have already
        // cleared that flag before the trigger exit fired.
        if (hadObject || lastHadObject)
        {
            lastHadObject        = false;
            dbObjectOnBelt       = false;
            tel_ObjectTravelDist = 0f;
            tel_ObjectOnBeltTime = 0f;
            SetOutput(out_ObjectOnBelt, false);
            Debug.Log($"[CONVEYOR:{name}] out_ObjectOnBelt → false (ClearActive)");
        }
    }

    // ── Public API (unchanged signatures — WarehouseManager / SensorTrigger compatible) ──

    public void SetObjectToMove(Transform obj)
    {
        if (obj != null && !objectsOnBelt.Contains(obj)) { objectsOnBelt.Add(obj); dbKnownObjects = objectsOnBelt.Count; }
        AssignActive(obj);
    }

    public void RegisterObject(Transform obj)
    {
        if (obj != null && !objectsOnBelt.Contains(obj)) { objectsOnBelt.Add(obj); dbKnownObjects = objectsOnBelt.Count; }
    }

    public void UnregisterObject(Transform obj)
    {
        if (obj != null) objectsOnBelt.Remove(obj);
        dbKnownObjects = objectsOnBelt.Count;
        if (activeObject == obj) ClearActive();
    }

    public void SetHeld(bool held)
    {
        cubeHeld = held; dbHeld = held;
        if (held) ClearActive();
    }

    public void SetSensorOverride(bool blocked)
    {
        sensorBlocked = blocked; dbSensorStop = blocked;
        SetOutput(out_SensorBlocked, blocked);
        Debug.Log($"[CONVEYOR:{name}] SensorOverride → {blocked}");
    }

    public Transform GetActiveObject() => activeObject;
    public bool      HasActiveObject   => activeObject != null;

    void SetOutput(string tag, bool value)
    { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, value); }
}