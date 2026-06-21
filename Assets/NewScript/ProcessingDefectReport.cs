using System;
using UnityEngine;

/// <summary>
/// ProcessingDefectReport — immutable record of one CNC processing event.
///
/// ══ PURPOSE ═════════════════════════════════════════════════════════════════
///   Produced by CubeProcessor.Process() every time an object exits the CNC
///   stage. Downstream systems — quality-control arms, rejection conveyors,
///   PLC tags, UI dashboards — consume this record to decide what to do next.
///
///   Subscribers register via:
///       CubeProcessor.OnProcessingComplete += MyHandler;
///
///   The report is passed by value-like reference (sealed class) so no
///   subscriber can mutate another subscriber's view of the same event.
///
/// ══ DEFECT CATALOGUE ════════════════════════════════════════════════════════
///   None          — shape matches specification exactly.
///   VertexRemoval — triangles stripped from mesh (pitting / missing material).
///   SurfaceNoise  — vertex positions displaced randomly (rough/warped surface).
///   ScaleDeform   — non-uniform scale applied to one axis (dimensional error).
///   Combined      — two or more of the above applied together.
///
/// ══ INTEGRATION PATTERN ══════════════════════════════════════════════════════
///   // In any MonoBehaviour that needs quality-control information:
///   void OnEnable()  => CubeProcessor.OnProcessingComplete += HandleReport;
///   void OnDisable() => CubeProcessor.OnProcessingComplete -= HandleReport;
///
///   void HandleReport(ProcessingDefectReport r)
///   {
///       if (!r.IsDefective) return;
///       rejectionConveyor.Activate();
///       IO_Router.Instance?.SetValue("QC_Reject", true);
///   }
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
[Serializable]
public sealed class ProcessingDefectReport
{
    // ── Identity ──────────────────────────────────────────────────────────────
    /// <summary>Name of the processed GameObject.</summary>
    public readonly string ObjectName;

    /// <summary>Instance ID of the processed GameObject (unique per session).</summary>
    public readonly int ObjectInstanceID;

    /// <summary>Name of the shape prefab/asset that was selected in the Inspector.</summary>
    public readonly string SelectedShapeName;

    // ── Quality verdict ───────────────────────────────────────────────────────
    /// <summary>TRUE = shape has one or more defects; FALSE = perfect output.</summary>
    public readonly bool IsDefective;

    /// <summary>Composite flags describing which defect categories were applied.</summary>
    [Flags]
    public enum DefectCategory
    {
        None          = 0,
        VertexRemoval = 1 << 0,   // Missing geometry (pits, holes, missing sections)
        SurfaceNoise  = 1 << 1,   // Displaced vertices (roughness, warping)
        ScaleDeform   = 1 << 2,   // Axis-specific scale error (dimensional inaccuracy)
    }

    /// <summary>
    /// Bitmask of all defect categories present.
    /// DefectCategory.None on a perfect part.
    /// </summary>
    public readonly DefectCategory Defects;

    // ── Spatial detail ────────────────────────────────────────────────────────
    /// <summary>
    /// Local-space bounding-box centre of the primary defect region.
    /// Vector3.zero when IsDefective = false.
    /// For ScaleDeform this is the axis vector (e.g. Vector3.up for Y-axis crush).
    /// </summary>
    public readonly Vector3 DefectLocation;

    /// <summary>
    /// Human-readable description of each defect applied, e.g.
    /// "VertexRemoval: 18% of triangles stripped from upper hemisphere".
    /// Empty when IsDefective = false.
    /// </summary>
    public readonly string DefectDetail;

    // ── Timing ────────────────────────────────────────────────────────────────
    /// <summary>Time.time when Process() was called (seconds since startup).</summary>
    public readonly float ProcessedAtTime;

    /// <summary>Wall-clock timestamp string (yyyy-MM-dd HH:mm:ss).</summary>
    public readonly string ProcessedAtTimestamp;

    // ── Constructor (called only by CubeProcessor) ────────────────────────────
    internal ProcessingDefectReport(
        string          objectName,
        int             objectInstanceID,
        string          selectedShapeName,
        bool            isDefective,
        DefectCategory  defects,
        Vector3         defectLocation,
        string          defectDetail)
    {
        ObjectName           = objectName;
        ObjectInstanceID     = objectInstanceID;
        SelectedShapeName    = selectedShapeName;
        IsDefective          = isDefective;
        Defects              = defects;
        DefectLocation       = defectLocation;
        DefectDetail         = defectDetail;
        ProcessedAtTime      = Time.time;
        ProcessedAtTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Returns a multi-line human-readable summary of this report.
    /// Suitable for Debug.Log() or a UI label.
    /// </summary>
    public override string ToString()
    {
        string verdict = IsDefective
            ? $"⚠ DEFECTIVE  [{Defects}]\n  Detail   : {DefectDetail}\n  Location : {DefectLocation}"
            : "✔ PERFECT";

        return $"[QC REPORT]\n" +
               $"  Object   : {ObjectName} (ID:{ObjectInstanceID})\n" +
               $"  Shape    : {SelectedShapeName}\n" +
               $"  Verdict  : {verdict}\n" +
               $"  Time     : {ProcessedAtTimestamp}  (t={ProcessedAtTime:F2}s)";
    }
}