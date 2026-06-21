using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// QualityControlStation — downstream consumer of ProcessingDefectReport.
/// (Original functionality preserved. Telemetry section added at bottom of Inspector.)
/// </summary>
public class QualityControlStation : MonoBehaviour
{
    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    public bool offlineMode = true;

    [Header("══ Filter — which objects this station monitors ══════════════════")]
    [Tooltip("If set, this station only reacts to reports where ObjectName contains this string.")]
    public string filterObjectName = "";

    [Header("══ Rejection / Acceptance Config ══════════════════════════════")]
    [Range(0.1f, 5f)]
    public float conveyorPulseDuration = 0.5f;
    public bool logAllReports  = true;
    public bool warnOnDefects  = true;

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════════")]
    public string out_StationDefect  = "";
    public string out_StationPerfect = "";
    public string out_RejectConveyor = "";
    public string out_AcceptConveyor = "";
    public string out_AlarmLight     = "";
    public string out_LogReady       = "";

    [Header("══ Debug (Read Only) ════════════════════════════════════════════")]
    [SerializeField] int    dbTotalProcessed = 0;
    [SerializeField] int    dbDefectCount    = 0;
    [SerializeField] int    dbPerfectCount   = 0;
    [SerializeField] string dbLastVerdict    = "—";
    [SerializeField] string dbLastShape      = "—";
    [SerializeField] string dbLastDefects    = "—";
    [SerializeField] string dbLastObject     = "—";
    [SerializeField] float  dbDefectRate     = 0f;

    // ══════════════════════════════════════════════════════════════════════════
    // ══ TELEMETRY — QC Station ════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    [Header("══ TELEMETRY — Live Station State ════════════════════════════")]
    [Tooltip("TRUE while the alarm light is active (defective part in system).")]
    [SerializeField] bool   tel_AlarmActive       = false;
    [Tooltip("TRUE while reject conveyor pulse is active.")]
    [SerializeField] bool   tel_RejectActive      = false;
    [Tooltip("TRUE while accept conveyor pulse is active.")]
    [SerializeField] bool   tel_AcceptActive      = false;
    [Tooltip("Time since the last QC report was received (s). Useful to detect pipeline stall.")]
    [SerializeField] float  tel_TimeSinceLastPart = 0f;
    [Tooltip("Parts per minute (rolling 60s window).")]
    [SerializeField] float  tel_PartsPerMinute    = 0f;

    [Header("══ TELEMETRY — Batch / Session Stats ════════════════════════")]
    [Tooltip("Defect rate across all parts inspected since scene start (%).")]
    [SerializeField] float  tel_SessionDefectRate = 0f;
    [Tooltip("Defect category breakdown — VertexRemoval count.")]
    [SerializeField] int    tel_DefectVertex      = 0;
    [Tooltip("Defect category breakdown — SurfaceNoise count.")]
    [SerializeField] int    tel_DefectSurface     = 0;
    [Tooltip("Defect category breakdown — ScaleDeform count.")]
    [SerializeField] int    tel_DefectScale       = 0;
    [Tooltip("Combined (multi-flag) defect count.")]
    [SerializeField] int    tel_DefectCombined    = 0;
    [Tooltip("Longest consecutive perfect-part streak since scene start.")]
    [SerializeField] int    tel_BestPerfectStreak = 0;
    [Tooltip("Current consecutive perfect-part streak.")]
    [SerializeField] int    tel_CurrentStreak     = 0;

    // ── Telemetry private ──────────────────────────────────────────────────────
    float lastPartTime  = 0f;
    bool  firstPart     = true;
    readonly Queue<float> partTimestamps = new Queue<float>(); // for parts/min window

    // ─────────────────────────────────────────────────────────────────────────
    public ProcessingDefectReport LastReport { get; private set; }
    public readonly List<ProcessingDefectReport> ReportHistory = new List<ProcessingDefectReport>();

    Coroutine rejectPulse;
    Coroutine acceptPulse;

    // BUG FIX: Subscribe in Awake / Unsubscribe in OnDestroy instead of OnEnable/OnDisable.
    //
    // The original code used OnEnable/OnDisable which fires whenever the Canvas (or any
    // parent GameObject) is enabled or disabled — for example when a UI panel is hidden,
    // toggled, or when the scene loads and the Canvas wakes up in a disabled state for
    // even one frame. This silently killed the event subscription mid-run, so CNC reports
    // were never received by this station after the first Canvas toggle.
    //
    // Using Awake/OnDestroy ties the subscription lifetime to the OBJECT lifetime, not
    // the enabled state. The station keeps receiving reports even when the Canvas is hidden.
    // If you deliberately want to pause reporting when the panel is hidden, gate inside
    // HandleReport() with `if (!isActiveAndEnabled) return;` instead.

    void Awake()
    {
        CubeProcessor.OnProcessingComplete += HandleReport;
        Debug.Log($"[QC STATION:{name}] Subscribed to CubeProcessor.OnProcessingComplete (Awake).");
    }

    void OnDestroy()
    {
        CubeProcessor.OnProcessingComplete -= HandleReport;
        Debug.Log($"[QC STATION:{name}] Unsubscribed from CubeProcessor.OnProcessingComplete (OnDestroy).");
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Time since last part
        if (!firstPart)
            tel_TimeSinceLastPart = Time.time - lastPartTime;

        // Parts per minute: count timestamps within last 60s
        float now = Time.time;
        while (partTimestamps.Count > 0 && now - partTimestamps.Peek() > 60f)
            partTimestamps.Dequeue();
        tel_PartsPerMinute = partTimestamps.Count; // count in last 60s = PPM

        // Alarm mirror
        tel_AlarmActive = IO_Router.Instance != null && !string.IsNullOrEmpty(out_AlarmLight)
            ? IO_Router.Instance.GetValue(out_AlarmLight)
            : dbDefectCount > 0 && dbLastVerdict.Contains("DEFECTIVE");
    }

    void HandleReport(ProcessingDefectReport report)
    {
        if (!string.IsNullOrEmpty(filterObjectName) &&
            !report.ObjectName.Contains(filterObjectName)) return;

        LastReport = report;
        ReportHistory.Add(report);
        dbTotalProcessed++;

        if (logAllReports)
        {
            if (warnOnDefects && report.IsDefective) Debug.LogWarning($"[QC STATION:{name}]\n{report}");
            else                                      Debug.Log($"[QC STATION:{name}]\n{report}");
        }

        dbLastObject = report.ObjectName;
        dbLastShape  = report.SelectedShapeName;

        // Telemetry: timing
        float now = Time.time;
        lastPartTime = now;
        firstPart    = false;
        tel_TimeSinceLastPart = 0f;
        partTimestamps.Enqueue(now);

        if (report.IsDefective)
        {
            dbDefectCount++;
            dbLastVerdict = $"DEFECTIVE [{report.Defects}]";
            dbLastDefects = report.DefectDetail;

            // Defect category breakdown
            if (report.Defects.HasFlag(ProcessingDefectReport.DefectCategory.VertexRemoval)) tel_DefectVertex++;
            if (report.Defects.HasFlag(ProcessingDefectReport.DefectCategory.SurfaceNoise))  tel_DefectSurface++;
            if (report.Defects.HasFlag(ProcessingDefectReport.DefectCategory.ScaleDeform))   tel_DefectScale++;
            int flagCount = 0;
            foreach (ProcessingDefectReport.DefectCategory c in System.Enum.GetValues(typeof(ProcessingDefectReport.DefectCategory)))
                if (c != ProcessingDefectReport.DefectCategory.None && report.Defects.HasFlag(c)) flagCount++;
            if (flagCount > 1) tel_DefectCombined++;

            // Streak
            tel_CurrentStreak = 0;

            tel_RejectActive = true;
            tel_AcceptActive = false;
            OnDefectivePartReceived(report);
        }
        else
        {
            dbPerfectCount++;
            dbLastVerdict = "PERFECT";
            dbLastDefects = "—";

            // Streak
            tel_CurrentStreak++;
            if (tel_CurrentStreak > tel_BestPerfectStreak)
                tel_BestPerfectStreak = tel_CurrentStreak;

            tel_AcceptActive = true;
            tel_RejectActive = false;
            OnPerfectPartReceived(report);
        }

        dbDefectRate          = dbTotalProcessed > 0 ? dbDefectCount * 100f / dbTotalProcessed : 0f;
        tel_SessionDefectRate = dbDefectRate;

        SetOutput(out_LogReady, true);
        StartCoroutine(PulseTag(out_LogReady, 0.2f));
    }

    void OnDefectivePartReceived(ProcessingDefectReport report)
    {
        IO_Router.Instance?.SetValueWithHandoff(out_StationPerfect, out_StationDefect);
        SetOutput(out_AlarmLight, true);

        if (rejectPulse != null) StopCoroutine(rejectPulse);
        rejectPulse = StartCoroutine(PulseTagAndClear(out_RejectConveyor, conveyorPulseDuration, () => tel_RejectActive = false));
        SetOutput(out_AcceptConveyor, false);

        Debug.LogWarning(
            $"[QC STATION:{name}] ⚠ REJECTION TRIGGERED\n" +
            $"  Object  : {report.ObjectName}\n  Shape   : {report.SelectedShapeName}\n" +
            $"  Defects : {report.Defects}\n  Detail  : {report.DefectDetail}\n" +
            $"  Defect rate: {dbDefectRate:F1}% ({dbDefectCount}/{dbTotalProcessed})");
    }

    void OnPerfectPartReceived(ProcessingDefectReport report)
    {
        IO_Router.Instance?.SetValueWithHandoff(out_StationDefect, out_StationPerfect);
        SetOutput(out_AlarmLight, false);

        if (acceptPulse != null) StopCoroutine(acceptPulse);
        acceptPulse = StartCoroutine(PulseTagAndClear(out_AcceptConveyor, conveyorPulseDuration, () => tel_AcceptActive = false));
        SetOutput(out_RejectConveyor, false);

        Debug.Log($"[QC STATION:{name}] ✔ ACCEPTED — '{report.ObjectName}' passed QC.");
    }

    public string GetSessionSummary()
    {
        return $"[QC SESSION SUMMARY — {name}]\n" +
               $"  Total      : {dbTotalProcessed}\n  Perfect    : {dbPerfectCount}\n" +
               $"  Defective  : {dbDefectCount}\n  Defect rate: {dbDefectRate:F1}%\n" +
               $"  Last object: {dbLastObject}\n  Last verdict:{dbLastVerdict}\n" +
               $"  Best streak: {tel_BestPerfectStreak} perfect in a row\n" +
               $"  Defect breakdown — Vertex:{tel_DefectVertex} Surface:{tel_DefectSurface} Scale:{tel_DefectScale} Combined:{tel_DefectCombined}";
    }

    [ContextMenu("Print Session Summary")]  void EditorPrint()   => Debug.Log(GetSessionSummary());
    [ContextMenu("Print Full Report History")]
    void EditorHistory()
    {
        if (ReportHistory.Count==0){Debug.Log("[QC STATION] No reports.");return;}
        var sb=new System.Text.StringBuilder();
        sb.AppendLine($"[QC STATION:{name}] History ({ReportHistory.Count}):");
        foreach (var r in ReportHistory) sb.AppendLine(r.ToString());
        Debug.Log(sb.ToString());
    }
    [ContextMenu("Clear History")]
    void EditorClear()
    {
        ReportHistory.Clear(); LastReport=null;
        dbTotalProcessed=dbDefectCount=dbPerfectCount=0; dbDefectRate=0f;
        dbLastVerdict=dbLastShape=dbLastDefects=dbLastObject="—";
        tel_DefectVertex=tel_DefectSurface=tel_DefectScale=tel_DefectCombined=0;
        tel_CurrentStreak=tel_BestPerfectStreak=0; tel_SessionDefectRate=0f;
        Debug.Log($"[QC STATION:{name}] History cleared.");
    }

    IEnumerator PulseTag(string tag, float dur)
    { SetOutput(tag,true); yield return new WaitForSeconds(dur); SetOutput(tag,false); }

    IEnumerator PulseTagAndClear(string tag, float dur, System.Action onDone)
    { SetOutput(tag,true); yield return new WaitForSeconds(dur); SetOutput(tag,false); onDone?.Invoke(); }

    void SetOutput(string tag, bool v)
    { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }
}