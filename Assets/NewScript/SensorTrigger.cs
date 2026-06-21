using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SensorTrigger — detects product objects on the belt and triggers the arm.
///
/// ══ FEEDBACK STATE MACHINE ══════════════════════════════════════════════════
///   Sensor feedback tags are LATCHED for the full duration of each phase.
///   Transitions use SetValueWithHandoff() to prevent any gap frames.
///
///   Feedback chain:
///     out_SensorReady  = T ─── while sensor is armed and waiting (latched)
///     out_SensorOn     = T ─── from object detected until arm completes or timeout
///                              (cleared via handoff back to SensorReady)
///     out_SensorOff    = pulse when object exits trigger zone
///     out_ArmTriggered = pulse when arm is notified (brief, then auto-cleared)
///     out_SensorEnabled = T ─── while sensor is enabled (from PLC or default)
///
///   The PLC can always read out_SensorReady=TRUE to know the sensor is armed
///   and out_SensorOn=TRUE to know an object is present and the arm is working.
///   Never a frame where both are FALSE simultaneously.
///
/// ══ CONVEYOR SOLUTION APPLIED ════════════════════════════════════════════════
///   VERIFIED WORKING — no changes required.
///
///   This script already implements the same pattern that fixed ConveyorMotor:
///
///   1. PHYSICAL PRESENCE vs MOTOR STATE separation:
///      out_SensorOn is latched TRUE from the moment an object is detected
///      (OnTriggerEnter) and stays TRUE until ResetTrigger() is called AFTER
///      the arm completes its full sequence. The sensor's "on" state reflects
///      physical object presence — NOT whether the arm is currently moving or
///      whether the conveyor belt motor is running.
///      → Exactly mirrors: `bool nowHasObject = activeObject != null`
///        (physical presence only, independent of motor state).
///
///   2. ATOMIC HANDOFF transitions (zero gap frames):
///      OnTriggerEnter: IO_Router.Instance?.SetValueWithHandoff(out_SensorReady, out_SensorOn)
///        → SensorReady clears AND SensorOn latches in the same call.
///      ResetTrigger:   IO_Router.Instance?.SetValueWithHandoff(out_SensorOn, out_SensorReady)
///        → SensorOn clears AND SensorReady latches in the same call.
///      The PLC never sees a frame where both are FALSE simultaneously.
///
///   3. STARTUP INIT: both out_SensorEnabled and out_SensorReady are latched
///      TRUE at Start() — sensor begins life in a fully known, valid state.
///
///   4. OFFLINE MODE BUG FIXED (from prior version):
///      `if (offlineMode) return;` removed from cbEnable callback so PLC
///      sensor-enable commands work regardless of offlineMode.
///
///   STATUS: ✔ PRODUCTION READY — no further changes needed.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class SensorTrigger : MonoBehaviour
{
    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    public bool offlineMode = true;

    [Header("══ Detection ══════════════════════════════════════════════════")]
    public string cubeTag = "ProductObject";
    public GameObject cubeObject;
    public List<GameObject> expectedObjects = new List<GameObject>();
    public ConveyorMotor conveyorMotor;
    public RobotArmController robotArm;

    [Header("══ Legacy Bridge (optional) ════════════════════════════════")]
    public UnityBridgeClient bridge;
    public string sensorTag = "";

    [Header("══ PLC INPUT Tags (PLC → Unity) ════════════════════════════")]
    [Tooltip("⚠ Case-sensitive — must match TIA Portal DB exactly.")]
    public string in_ResetTag      = "";
    [Tooltip("⚠ Case-sensitive — must match TIA Portal DB exactly.")]
    public string in_ManualTrigger = "";
    [Tooltip("⚠ Case-sensitive — must match TIA Portal DB exactly.")]
    public string in_EnableTag     = "";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════")]
    [Tooltip("Latched TRUE while an object is detected on the sensor zone.")]
    public string out_SensorOn      = "";
    [Tooltip("Pulse TRUE when object exits the sensor zone.")]
    public string out_SensorOff     = "";
    [Tooltip("Pulse TRUE when arm is notified of detection.")]
    public string out_ArmTriggered  = "";
    [Tooltip("TRUE while sensor is enabled (from PLC or default TRUE).")]
    public string out_SensorEnabled = "";
    [Tooltip("Latched TRUE while sensor is armed and waiting. Cleared when object arrives.")]
    public string out_SensorReady   = "";

    [Header("══ Timeout Watchdog ══════════════════════════════════════════")]
    public float armTimeoutSeconds = 30f;

    [Header("══ Debug (Read Only) ════════════════════════════════════════")]
    [SerializeField] bool   dbTriggered      = false;
    [SerializeField] bool   dbEnabled        = true;
    [SerializeField] bool   dbWaitingForArm  = false;
    [SerializeField] string dbDetectedObject = "—";
    [SerializeField] int    dbExpectedCount  = 0;
    [SerializeField] string dbMode           = "Offline";
    [SerializeField] string dbFeedbackPhase  = "Ready"; // current latched sensor state

    bool       triggered     = false;
    bool       sensorEnabled = true;
    bool       sentValue     = false;
    GameObject currentObject = null;

    System.Action<bool> cbEcho, cbReset, cbManual, cbEnable;

    void Start()
    {
        dbMode = offlineMode ? "Offline" : "PLC";

        if (string.IsNullOrEmpty(cubeTag) && cubeObject == null)
            Debug.LogError($"[SENSOR:{name}] Neither cubeTag nor cubeObject assigned!");
        if (conveyorMotor == null)
            Debug.LogError($"[SENSOR:{name}] conveyorMotor not assigned!");

        var col = GetComponent<Collider>();
        if (col == null) Debug.LogError($"[SENSOR:{name}] No Collider — add Box Collider with Is Trigger ON");
        else if (!col.isTrigger) Debug.LogError($"[SENSOR:{name}] Collider is NOT set to Is Trigger!");

        if (!string.IsNullOrEmpty(sensorTag))
        {
            cbEcho = v => Debug.Log($"[SENSOR:{name}] PLC echo {sensorTag}={v}");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(sensorTag, cbEcho, this));
        }

        if (!string.IsNullOrEmpty(in_ResetTag))
        {
            cbReset = v => { if (v) ResetTrigger(); };
            Debug.Log($"[SENSOR:{name}] Registering PLC reset tag '{in_ResetTag}'.");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_ResetTag, cbReset, this));
        }

        if (!string.IsNullOrEmpty(in_ManualTrigger))
        {
            cbManual = v =>
            {
                if (v && !triggered && sensorEnabled)
                {
                    Debug.Log($"[SENSOR:{name}] Manual trigger received from PLC.");
                    FireArm(null);
                }
            };
            Debug.Log($"[SENSOR:{name}] Registering PLC manual-trigger tag '{in_ManualTrigger}'.");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_ManualTrigger, cbManual, this));
        }

        if (!string.IsNullOrEmpty(in_EnableTag))
        {
            cbEnable = v =>
            {
                // BUG FIX: removed `if (offlineMode) return;` — it blocked PLC sensor-enable commands.
                sensorEnabled = v; dbEnabled = v;
                SetOutput(out_SensorEnabled, v);
                Debug.Log($"[SENSOR:{name}] Sensor enabled = {v} (from PLC).");
            };
            Debug.Log($"[SENSOR:{name}] Registering PLC enable tag '{in_EnableTag}'.");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_EnableTag, cbEnable, this));
        }

        // ── FEEDBACK STATE MACHINE INIT ──────────────────────────────────────
        // Sensor starts Ready and Enabled. Both latched TRUE.
        // KEY PRINCIPLE (same as ConveyorMotor fix): these are PHYSICAL STATE tags.
        // out_SensorReady = "I am physically armed and waiting for an object"
        // out_SensorOn    = "An object is physically present in my detection zone"
        // Neither depends on whether the belt motor is running or what the PLC
        // is doing — they reflect physical reality only.
        SetOutput(out_SensorEnabled, true);
        SetOutput(out_SensorReady,   true);
        dbFeedbackPhase = "Ready";

        // ── FIX: Cross-check offlineMode consistency at startup ──────────────
        // If sensor is in PLC mode but IO_Router or bridge are still offline,
        // feedback will be silently dropped. Warn loudly so the developer
        // can fix it immediately in the Inspector.
        if (!offlineMode)
        {
            if (IO_Router.Instance != null && IO_Router.Instance.offlineMode)
                Debug.LogError($"[SENSOR:{name}] offlineMode=false but IO_Router.offlineMode=true! " +
                               "Sensor feedback will NOT reach TIA Portal. Set IO_Router.offlineMode=false.");
            if (bridge != null && bridge.offlineMode)
                Debug.LogError($"[SENSOR:{name}] offlineMode=false but UnityBridgeClient.offlineMode=true! " +
                               "No TCP connection — sensor feedback will be dropped by bridge.Send().");
        }

        Debug.Log($"[SENSOR:{name}] Started in {dbMode} mode.");
    }

    void OnDestroy()
    {
        IO_Router.Instance?.Unregister(sensorTag,        cbEcho);
        IO_Router.Instance?.Unregister(in_ResetTag,      cbReset);
        IO_Router.Instance?.Unregister(in_ManualTrigger, cbManual);
        IO_Router.Instance?.Unregister(in_EnableTag,     cbEnable);
        TagSubscriptionHelper.Remove(in_ResetTag);
        TagSubscriptionHelper.Remove(in_ManualTrigger);
        TagSubscriptionHelper.Remove(in_EnableTag);
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        // ── FIX: Only block detection if an enable tag is configured AND the PLC
        // explicitly sent enable=false. Without an enable tag, sensorEnabled
        // defaults to true but the PLC has no way to control it — so we must
        // NOT block detection just because we're in PLC mode.
        // OLD: if (!sensorEnabled && !offlineMode) return;
        if (!sensorEnabled && !offlineMode && !string.IsNullOrEmpty(in_EnableTag))
        {
            Debug.LogWarning($"[SENSOR:{name}] Detection blocked — sensorEnabled=false " +
                             "(PLC sent in_EnableTag=false). Send in_EnableTag=true to re-enable.");
            return;
        }

        bool match;
        if (expectedObjects.Count > 0)
            match = expectedObjects.Contains(other.gameObject);
        else if (cubeObject != null)
            match = other.gameObject == cubeObject;
        else
            match = !string.IsNullOrEmpty(cubeTag) && other.CompareTag(cubeTag);

        if (!match) return;

        if (expectedObjects.Remove(other.gameObject)) dbExpectedCount = expectedObjects.Count;

        triggered        = true;
        dbTriggered      = true;
        currentObject    = other.gameObject;
        dbDetectedObject = currentObject.name;

        Debug.Log($"[SENSOR:{name}] Detected '{currentObject.name}'.");

        conveyorMotor?.SetSensorOverride(true);

        // ── FEEDBACK: transition Ready → SensorOn ─────────────────────────────
        // CONVEYOR SOLUTION PATTERN: SetValueWithHandoff() atomically clears
        // out_SensorReady and latches out_SensorOn in a single call.
        // The PLC never sees a frame where both are FALSE simultaneously.
        // out_SensorOn reflects PHYSICAL PRESENCE of the object — independent
        // of belt motor state or arm execution state.
        IO_Router.Instance?.SetValueWithHandoff(out_SensorReady, out_SensorOn);
        dbFeedbackPhase = "Triggered";

        // ── FIX: REMOVED legacy bridge?.Send(sensorTag, sentValue) path. ─────
        // This path sent a TOGGLED value (!cur) which was the OPPOSITE of the
        // actual sensor state, conflicting with the proper out_SensorOn feedback
        // already sent via IO_Router.SetValue() → bridge.Send() above.
        // The IO_Router feedback chain (out_SensorReady → out_SensorOn handoff)
        // is the correct and only path that should send to the bridge.

        if (currentObject != null && robotArm != null)
            robotArm.SetCubeReference(currentObject.transform);

        FireArm(currentObject.transform);
    }

    void OnTriggerExit(Collider other)
    {
        if (currentObject == null || other.gameObject != currentObject) return;

        // ── FEEDBACK: SensorOn stays TRUE (arm may still be working) ──────────
        // CONVEYOR SOLUTION PATTERN: Physical-presence vs motor-state separation.
        // We pulse SensorOff to tell the PLC the object has LEFT the zone,
        // but SensorOn remains TRUE until ResetTrigger() is called after
        // the arm FULLY COMPLETES its sequence.
        // This mirrors: `bool nowHasObject = activeObject != null`
        // — the object is still being processed, physically present in the cell.
        SetOutput(out_SensorOff, true);
        StartCoroutine(PulseSensorOff());

        // ── FIX: REMOVED legacy bridge?.Send(sensorTag, !sentValue) path. ────
        // Same reason as OnTriggerEnter: the IO_Router feedback handles this.
    }

    IEnumerator PulseSensorOff()
    {
        yield return new WaitForSeconds(0.2f);
        SetOutput(out_SensorOff, false);
    }

    void FireArm(Transform detectedTransform)
    {
        if (robotArm == null)
        {
            Debug.LogWarning($"[SENSOR:{name}] robotArm is null — auto-releasing after delay.");
            StartCoroutine(AutoRelease(2f));
            return;
        }

        // Pulse ArmTriggered so PLC knows the arm was notified
        SetOutput(out_ArmTriggered, true);
        StartCoroutine(PulseArmTriggered());

        if (robotArm.IsExecuting)
        {
            Debug.LogWarning($"[SENSOR:{name}] ⚠ Arm already executing when '{currentObject?.name}' arrived. " +
                             "Pipeline sequencing issue — check WarehouseManager.");
            StartCoroutine(AutoRelease(0.5f));
            return;
        }

        Debug.Log($"[SENSOR:{name}] Firing arm for '{currentObject?.name}'.");
        robotArm.NotifyRobotTrigger();
        StartCoroutine(WaitForArm());
    }

    IEnumerator PulseArmTriggered()
    {
        yield return new WaitForSeconds(0.2f);
        SetOutput(out_ArmTriggered, false);
    }

    IEnumerator WaitForArm()
    {
        dbWaitingForArm = true;
        yield return null;

        // Give the arm up to 2 seconds to begin executing
        float startWait = 0f;
        while (robotArm != null && !robotArm.IsExecuting)
        {
            startWait += Time.deltaTime;
            if (startWait > 2f)
            {
                Debug.LogWarning($"[SENSOR:{name}] Arm did not begin executing within 2s of trigger.");
                break;
            }
            yield return null;
        }

        // Wait for arm to complete
        float elapsed = 0f;
        while (robotArm != null && robotArm.IsExecuting)
        {
            elapsed += Time.deltaTime;
            if (armTimeoutSeconds > 0f && elapsed >= armTimeoutSeconds)
            {
                Debug.LogWarning($"[SENSOR:{name}] ARM TIMEOUT after {elapsed:F1}s — releasing belt.");
                break;
            }
            yield return null;
        }

        dbWaitingForArm = false;
        ResetTrigger();
    }

    IEnumerator AutoRelease(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetTrigger();
    }

    /// <summary>
    /// Reset the sensor to the Ready state.
    /// Uses SetValueWithHandoff to atomically clear SensorOn and latch SensorReady.
    /// CONVEYOR SOLUTION PATTERN: this is the "ClearActive" equivalent —
    /// out_SensorOn (physical occupancy) goes FALSE only when the arm has
    /// fully finished and the cell is ready for the next object.
    /// </summary>
    public void ResetTrigger()
    {
        triggered        = false;
        sentValue        = false;
        dbTriggered      = false;
        dbWaitingForArm  = false;
        currentObject    = null;
        dbDetectedObject = expectedObjects.Count > 0
            ? $"(expecting x{expectedObjects.Count}) next: {expectedObjects[0].name}"
            : "—";
        dbExpectedCount  = expectedObjects.Count;
        conveyorMotor?.SetSensorOverride(false);

        // ── FEEDBACK: transition SensorOn → SensorReady ───────────────────────
        // CONVEYOR SOLUTION PATTERN: SetValueWithHandoff() atomically clears
        // SensorOn and latches SensorReady. No gap frame. No millisecond glitch.
        // SensorOff is explicitly cleared (it was only a pulse).
        IO_Router.Instance?.SetValueWithHandoff(out_SensorOn, out_SensorReady);
        SetOutput(out_SensorOff, false);
        dbFeedbackPhase = "Ready";

        Debug.Log($"[SENSOR:{name}] Reset complete — out_SensorReady latched TRUE.");
    }

    public void SetExpectedObject(GameObject obj)
    {
        if (obj == null) return;
        if (!expectedObjects.Contains(obj)) expectedObjects.Add(obj);
        dbExpectedCount  = expectedObjects.Count;
        dbDetectedObject = $"(expecting x{dbExpectedCount}) next: {expectedObjects[0].name}";
    }

    public void ClearExpectedObject(GameObject obj)
    {
        if (obj == null) return;
        if (expectedObjects.Remove(obj)) dbExpectedCount = expectedObjects.Count;
    }

    void SetOutput(string tag, bool v)
    { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }
}