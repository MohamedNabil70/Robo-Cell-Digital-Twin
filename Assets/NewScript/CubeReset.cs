using UnityEngine;
using System.Collections;

/// <summary>
/// CubeReset — safety fallback if an object reaches the end of Conv2 without
/// being picked by Arm3.
///
/// ══ FEEDBACK STATE MACHINE ══════════════════════════════════════════════════
///   CubeReset feedback tags reflect the reset sequence state clearly.
///
///   Feedback chain:
///     out_ResetTriggered = pulse TRUE when reset fires (detected missed object)
///     out_ResetComplete  = latched FALSE while reset in progress,
///                          latched TRUE once reset is fully complete.
///                          Stays TRUE until next reset fires.
///     out_FallbackUsed   = pulse TRUE if teleport fallback was needed.
///
///   On startup out_ResetComplete initialises TRUE (no reset in progress).
///   Each reset cycle sets it FALSE at start and TRUE at end.
///   The PLC can monitor out_ResetComplete=FALSE to know a recovery is underway.
///
/// ══ TRIGGER VERIFICATION ════════════════════════════════════════════════════
///   DoReset() null-checks arm3, both conveyors, and fallbackSpawnPoint.
///   E-Stop flag gates all reset actions.
///   resetInProgress prevents re-entrant reset coroutines.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class CubeReset : MonoBehaviour
{
    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    [Tooltip("TRUE = reset logic runs without PLC enable/disable. Output tags still fire.")]
    public bool offlineMode = true;

    [Header("══ Object Detection ═══════════════════════════════════════════")]
    [Tooltip("Tag of product objects. Any object with this tag entering the zone triggers reset.")]
    public string productTag = "ProductObject";

    [Header("══ Conveyors ═══════════════════════════════════════════════════")]
    public ConveyorMotor firstConveyor;
    public ConveyorMotor secondConveyor;

    [Header("══ Sensors ════════════════════════════════════════════════════")]
    public SensorTrigger sensor1;
    public SensorTrigger sensor2;

    [Header("══ References ══════════════════════════════════════════════════")]
    public RobotArmController arm3;
    public WarehouseManager   warehouseManager;

    [Header("══ Fallback Teleport (when Arm3 not assigned) ══════════════════")]
    public Transform fallbackSpawnPoint;

    [Header("══ PLC INPUT Tags (PLC → Unity) ════════════════════════════════")]
    [Tooltip("Boolean TRUE: force a reset cycle from PLC or HMI button.\n⚠ Case-sensitive.")]
    public string in_ManualReset = "";
    [Tooltip("Boolean TRUE: E-Stop active — inhibit any reset action. FALSE: cleared.\n⚠ Case-sensitive.")]
    public string in_EStopTag    = "";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════════")]
    [Tooltip("Pulse TRUE when a missed-object reset fires.")]
    public string out_ResetTriggered = "CubeReset_Triggered";
    [Tooltip("Latched TRUE when reset is complete. FALSE while reset is in progress.")]
    public string out_ResetComplete  = "CubeReset_Complete";
    [Tooltip("Pulse TRUE if the teleport fallback was used (Arm3 not available).")]
    public string out_FallbackUsed   = "CubeReset_FallbackUsed";

    [Header("══ Debug (Read Only) ════════════════════════════════════════════")]
    [SerializeField] int    dbResetCount    = 0;
    [SerializeField] bool   dbInProgress    = false;
    [SerializeField] bool   dbEStopActive   = false;
    [SerializeField] string dbLastResetType = "—";
    [SerializeField] string dbMode          = "Offline";
    [SerializeField] string dbFeedbackPhase = "Idle"; // current reset state

    bool resetInProgress = false;
    bool eStopActive     = false;

    System.Action<bool> cbManualReset, cbEStop;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        dbMode = offlineMode ? "Offline" : "PLC";

        var col = GetComponent<Collider>();
        if (col == null || !col.isTrigger)
            Debug.LogWarning($"[CUBE RESET:{name}] Needs a Collider with Is Trigger = ON.");

        if (!string.IsNullOrEmpty(in_ManualReset))
        {
            // Direct boolean: TRUE = manual reset commanded.
            // Guard: resetInProgress and eStopActive prevent re-entrant resets.
            cbManualReset = v =>
            {
                if (v && !resetInProgress && !eStopActive)
                    Debug.LogWarning($"[CUBE RESET:{name}] Manual reset fired from PLC — " +
                                     "physically place an object in the trigger zone first.");
            };
            Debug.Log($"[CUBE RESET:{name}] Registering PLC manual-reset tag '{in_ManualReset}'.");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_ManualReset, cbManualReset, this));
        }

        if (!string.IsNullOrEmpty(in_EStopTag))
        {
            cbEStop = v =>
            {
                eStopActive   = v;
                dbEStopActive = v;
                if (v) Debug.LogWarning($"[CUBE RESET:{name}] E-Stop active — reset inhibited.");
                else   Debug.Log($"[CUBE RESET:{name}] E-Stop cleared.");
            };
            Debug.Log($"[CUBE RESET:{name}] Registering PLC E-Stop tag '{in_EStopTag}'.");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_EStopTag, cbEStop, this));
        }

        // ── FEEDBACK STATE MACHINE INIT ──────────────────────────────────────
        // Start with ResetComplete = TRUE (no reset in progress at startup).
        SetOutput(out_ResetComplete, true);
        dbFeedbackPhase = "Idle";

        Debug.Log($"[CUBE RESET:{name}] Started in {dbMode} mode. out_ResetComplete=TRUE (latched).");
    }

    void OnDestroy()
    {
        IO_Router.Instance?.Unregister(in_ManualReset, cbManualReset);
        IO_Router.Instance?.Unregister(in_EStopTag,    cbEStop);
        TagSubscriptionHelper.Remove(in_ManualReset);
        TagSubscriptionHelper.Remove(in_EStopTag);
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    // ─────────────────────────────────────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (resetInProgress || eStopActive) return;
        if (string.IsNullOrEmpty(productTag) || !other.CompareTag(productTag)) return;
        StartCoroutine(DoReset(other.transform));
    }

    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator DoReset(Transform target)
    {
        if (target == null) yield break;

        resetInProgress = true;
        dbInProgress    = true;
        dbResetCount++;
        dbFeedbackPhase = "InProgress";

        Debug.LogWarning($"[CUBE RESET:{name}] ⚠ Reset #{dbResetCount} — object: '{target.name}'");

        // ── FEEDBACK: latch ResetComplete=FALSE while reset is in progress ────
        // The PLC monitors this becoming FALSE to know a recovery is underway.
        SetOutput(out_ResetComplete, false);

        // Pulse ResetTriggered so PLC can latch the rising edge
        SetOutput(out_ResetTriggered, true);
        yield return new WaitForSeconds(0.1f);
        SetOutput(out_ResetTriggered, false);

        // Stop Conv2 and reset both sensors BEFORE re-triggering arm
        secondConveyor?.SetSensorOverride(true);
        secondConveyor?.SetHeld(false);
        sensor1?.ResetTrigger();
        sensor2?.ResetTrigger();

        if (arm3 != null && !arm3.IsExecuting)
        {
            dbLastResetType = "Arm3 re-trigger";
            arm3.SetCubeReference(target);
            arm3.NotifyRobotTrigger();
            Debug.Log($"[CUBE RESET:{name}] Arm3 re-triggered for '{target.name}'.");
            yield return new WaitForSeconds(0.5f);
        }
        else if (arm3 != null && arm3.IsExecuting)
        {
            dbLastResetType = "Arm3 queued retry";
            Debug.Log($"[CUBE RESET:{name}] Arm3 busy — waiting for it to become idle...");

            float wait = 0f;
            yield return new WaitUntil(() =>
            {
                wait += Time.deltaTime;
                return arm3 == null || !arm3.IsExecuting || wait > 30f;
            });

            if (arm3 != null && !arm3.IsExecuting)
            {
                sensor2?.ResetTrigger();
                arm3.SetCubeReference(target);
                arm3.NotifyRobotTrigger();
                Debug.Log($"[CUBE RESET:{name}] Arm3 re-triggered after wait.");
            }
            else
            {
                Debug.LogWarning($"[CUBE RESET:{name}] Arm3 still busy after 30s — using teleport fallback.");
                TeleportFallback(target);
            }
        }
        else
        {
            dbLastResetType = "Teleport fallback";
            Debug.LogWarning($"[CUBE RESET:{name}] arm3 is null — using teleport fallback.");
            TeleportFallback(target);
        }

        resetInProgress = false;
        dbInProgress    = false;
        dbFeedbackPhase = "Idle";

        // ── FEEDBACK: latch ResetComplete=TRUE when sequence finishes ─────────
        // The PLC sees this transition from FALSE to TRUE to confirm recovery done.
        SetOutput(out_ResetComplete, true);
        Debug.Log($"[CUBE RESET:{name}] Reset complete. out_ResetComplete=TRUE (latched).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    void TeleportFallback(Transform target)
    {
        if (fallbackSpawnPoint == null)
        {
            Debug.LogWarning($"[CUBE RESET:{name}] No fallbackSpawnPoint assigned — object left in place.");
            PulseFallbackTag();
            return;
        }

        if (target == null)
        {
            Debug.LogWarning($"[CUBE RESET:{name}] TeleportFallback called with null target.");
            return;
        }

        var rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
        }

        target.SetParent(null);
        target.position = fallbackSpawnPoint.position;
        target.rotation = fallbackSpawnPoint.rotation;

        if (rb != null)
        {
            rb.isKinematic     = false;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        firstConveyor?.SetHeld(false);
        firstConveyor?.SetSensorOverride(false);
        secondConveyor?.SetSensorOverride(false);
        secondConveyor?.SetHeld(false);

        // Restore raw appearance on the recycled object
        var cp = target.GetComponent<CubeProcessor>();
        if (cp != null)
        {
            cp.Restore();
            Debug.Log($"[CUBE RESET:{name}] CubeProcessor.Restore() called on '{target.name}'.");
        }

        Debug.Log($"[CUBE RESET:{name}] Teleport fallback complete — '{target.name}' → spawn point.");
        PulseFallbackTag();
    }

    void PulseFallbackTag()
    {
        SetOutput(out_FallbackUsed, true);
        StartCoroutine(PulseFallback());
    }

    IEnumerator PulseFallback()
    {
        yield return new WaitForSeconds(0.3f);
        SetOutput(out_FallbackUsed, false);
    }

    void SetOutput(string tag, bool v)
    { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }
}