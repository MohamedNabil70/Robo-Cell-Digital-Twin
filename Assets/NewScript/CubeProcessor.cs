using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// CubeProcessor — applies CNC machining to any selected shape.
///
/// ══ CRITICAL FIX: Shape Selection ══════════════════════════════════════════
///   The shape is resolved in this priority order at Process() time:
///     1. injectedShapePrefab (set via SetShapeOverride() or by RobotArmController)
///     2. processedShapePrefab (set in Inspector on this component)
///     3. fallbackShape enum (Sphere/Cylinder/Capsule/Cube)
///   
///   RobotArmController.Arm2 calls SetShapeOverride() instead of directly
///   setting processedShapePrefab, which prevents Inspector values from being
///   silently overwritten by null values.
///
///   IMPORTANT: If you're setting the shape in the Inspector on a PREFAB,
///   make sure you're editing the PREFAB asset (double-click it in Project),
///   not just a scene instance. Scene instance changes don't apply to
///   newly instantiated objects.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class CubeProcessor : MonoBehaviour
{
    public enum QualityMode  { Perfect, Defective }
    public enum FallbackShape { Sphere, Cylinder, Capsule, Cube }

    [Header("══ Dynamic Shape Selection ════════════════════════════════════")]
    [Tooltip("Drag a DIFFERENT prefab/scene-object here to use its mesh as the CNC output shape.\n" +
             "⚠ Do NOT drag this object itself — that produces the same mesh (no visible change).\n" +
             "Leave empty to use the FallbackShape enum below.\n" +
             "⚠ If using a PREFAB, edit the PREFAB asset, not just a scene instance!")]
    public GameObject processedShapePrefab = null;

    [Tooltip("Used ONLY when processedShapePrefab is empty AND no shape override is set.\n" +
             "• Sphere/Cylinder/Capsule: visible shape change after CNC\n" +
             "• Cube: NO visible change (same as raw shape)")]
    public FallbackShape fallbackShape = FallbackShape.Sphere;

    [Tooltip("Colour applied to the processed shape.")]
    public Color processedColor = new Color(0.2f, 0.85f, 0.3f);

    [Header("══ Quality Mode ════════════════════════════════════════════════")]
    public QualityMode qualityMode = QualityMode.Perfect;

    [Header("══ Defect Engine — active when qualityMode = Defective ════════")]
    public bool defect_VertexRemoval = true;
    public bool defect_SurfaceNoise  = false;
    public bool defect_ScaleDeform   = false;
    [Range(0.05f, 0.60f)] public float vertexRemovalFraction = 0.20f;
    [Range(0.01f, 0.50f)] public float surfaceNoiseIntensity = 0.08f;
    [Range(-1, 2)]         public int   scaleDeformAxis      = -1;
    [Range(0.1f, 0.9f)]   public float scaleDeformAmount    = 0.35f;
    public int defectSeed = -1;

    [Header("══ PLC INPUT Tags (PLC → Unity) ════════════════════════════════")]
    public string in_ProcessCmd = "";
    public string in_RestoreCmd = "";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════════")]
    public string out_IsProcessed = "";
    public string out_IsRaw       = "";
    public string out_ProcessDone = "";
    public string out_RestoreDone = "";

    [Header("══ QC Feedback Output Tags (Unity → PLC) ══════════════════════")]
    public string out_IsDefective = "";
    public string out_IsPerfect   = "";
    public string out_QCResult    = "";

    [Header("══ Debug (Read Only) ════════════════════════════════════════════")]
    [SerializeField] bool   dbIsProcessed       = false;
    [SerializeField] bool   dbIsDefective       = false;
    [SerializeField] string dbShape             = "Raw";
    [SerializeField] string dbQualityVerdict    = "—";
    [SerializeField] string dbDefectDetail      = "—";
    [SerializeField] string dbFeedbackPhase     = "Raw";
    [SerializeField] string dbResolvedMeshName  = "—";
    [SerializeField] string dbShapeSource       = "—";
    [SerializeField] string dbInjectedShapeName = "—";

    // ══════════════════════════════════════════════════════════════════════════
    // ══ TELEMETRY ════════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    [Header("══ TELEMETRY — CNC Process State ════════════════════════════")]
    [SerializeField] bool   tel_CycleActive      = false;
    [SerializeField] float  tel_CycleElapsed     = 0f;
    [SerializeField] float  tel_SpindleRPM       = 0f;
    [SerializeField] float  tel_SpindleTemp      = 30f;
    [SerializeField] float  tel_ToolVibration    = 0f;
    [SerializeField] float  tel_CoolantFlow      = 0f;
    [SerializeField] float  tel_CoolantTemp      = 20f;

    [Header("══ TELEMETRY — CNC Axis Positions (mm) ════════════════════════")]
    [SerializeField] float  tel_AxisX = 0f;
    [SerializeField] float  tel_AxisY = 0f;
    [SerializeField] float  tel_AxisZ = 0f;

    [Header("══ TELEMETRY — QC & Counters ════════════════════════════════")]
    [SerializeField] int    tel_TotalProcessed   = 0;
    [SerializeField] int    tel_DefectiveCount   = 0;
    [SerializeField] int    tel_PerfectCount     = 0;
    [SerializeField] float  tel_DefectRatePct    = 0f;
    [SerializeField] string tel_LastVerdict      = "—";
    [SerializeField] int    tel_LastDefectFlags  = 0;
    [SerializeField] float  tel_AvgCycleTime     = 0f;

    // ── Telemetry private ──────────────────────────────────────────────────────
    float cycleStartTime   = 0f;
    float cycleTotalTime   = 0f;
    int   cycleSamples     = 0;
    float axisPhase        = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    Mesh     originalMesh;
    Material originalMaterial;
    Material processedMaterial;

    MeshFilter   mf;
    MeshRenderer mr;

    Collider    originalCollider;
    bool        addedMeshCollider = false;

    // ══════════════════════════════════════════════════════════════════════════
    // ══ SHAPE OVERRIDE SYSTEM ════════════════════════════════════════════════
    // This allows RobotArmController (or any other system) to inject a shape
    // at runtime WITHOUT overwriting the Inspector-set processedShapePrefab.
    // The override takes HIGHEST priority during ResolveMesh().
    // ══════════════════════════════════════════════════════════════════════════
    private GameObject injectedShapePrefab = null;

    /// <summary>
    /// Inject a shape override that takes priority over the Inspector setting.
    /// Call this BEFORE Process() to use a different shape for this cycle.
    /// The override is consumed (set to null) after Process() uses it.
    /// 
    /// Use this instead of directly setting processedShapePrefab to avoid
    /// overwriting the user's Inspector configuration.
    /// </summary>
    public void SetShapeOverride(GameObject shapePrefab)
    {
        injectedShapePrefab = shapePrefab;
        dbInjectedShapeName = shapePrefab != null ? shapePrefab.name : "—";
        Debug.Log($"[PROCESSOR:{name}] Shape override SET to '{dbInjectedShapeName}'. " +
                  "This will be used on next Process() call.");
    }

    /// <summary>
    /// Clear any shape override, reverting to Inspector settings.
    /// </summary>
    public void ClearShapeOverride()
    {
        injectedShapePrefab = null;
        dbInjectedShapeName = "—";
        Debug.Log($"[PROCESSOR:{name}] Shape override CLEARED. Will use Inspector settings.");
    }

    // ══════════════════════════════════════════════════════════════════════════

    public bool IsProcessed => dbIsProcessed;
    public bool IsDefective => dbIsDefective;

    Action<bool> cbProcess, cbRestore;

    public static event Action<ProcessingDefectReport> OnProcessingComplete;

    static readonly Dictionary<FallbackShape, Mesh> fallbackMeshCache = new Dictionary<FallbackShape, Mesh>();
    Mesh processedMesh;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        mf = GetComponent<MeshFilter>();
        mr = GetComponent<MeshRenderer>();
        if (mf == null) { Debug.LogError($"[PROCESSOR:{name}] No MeshFilter!"); return; }
        if (mr == null) { Debug.LogError($"[PROCESSOR:{name}] No MeshRenderer!"); return; }

        originalMesh = mf.sharedMesh;
        originalMaterial = mr.sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? (originalMaterial != null ? originalMaterial.shader : Shader.Find("Diffuse"));
        processedMaterial       = new Material(shader);
        processedMaterial.color = processedColor;
        processedMaterial.name  = $"{name}_ProcessedMaterial";

        originalCollider = GetComponent<Collider>();

        Debug.Log($"[PROCESSOR:{name}] Awake complete. Original mesh: '{(originalMesh?.name ?? "NULL")}'");
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(in_ProcessCmd))
        {
            cbProcess = v => { if (v) Process(); };
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_ProcessCmd, cbProcess, this));
        }
        if (!string.IsNullOrEmpty(in_RestoreCmd))
        {
            cbRestore = v => { if (v) Restore(); };
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_RestoreCmd, cbRestore, this));
        }

        SetOutput(out_IsRaw,       true);
        SetOutput(out_IsProcessed, false);
        SetOutput(out_IsDefective, false);
        SetOutput(out_IsPerfect,   false);
        dbFeedbackPhase  = "Raw";
        dbQualityVerdict = "—";

        // Startup validation
        LogShapeConfiguration();
    }

    /// <summary>
    /// Logs the current shape configuration for debugging.
    /// Call this from Inspector context menu to check settings at any time.
    /// </summary>
    void LogShapeConfiguration()
    {
        Debug.Log($"╔═════════════ SHAPE CONFIG: {name} ═════════════╗");
        Debug.Log($"  Original mesh      : '{(originalMesh?.name ?? "NULL")}'");
        Debug.Log($"  Inspector prefab   : '{(processedShapePrefab?.name ?? "NULL (will use fallback)")}']");
        Debug.Log($"  Injected override  : '{(injectedShapePrefab?.name ?? "NULL (not set)")}']");
        Debug.Log($"  Fallback shape     : {fallbackShape}");
        Debug.Log($"  ════════════════════════════════════════════");
        
        // Predict what mesh will be used
        Mesh predictedMesh = PredictResolvedMesh(out string source, out string meshName);
        Debug.Log($"  PREDICTED at Process():");
        Debug.Log($"    Source  : {source}");
        Debug.Log($"    Mesh    : '{meshName}'");
        Debug.Log($"    Same as original? : {(predictedMesh == originalMesh ? "YES (no visible shape change)" : "NO")}");
        Debug.Log($"╚═════════════════════════════════════════════════╝");
    }

    /// <summary>
    /// Predicts what mesh will be resolved without actually processing.
    /// Used for debugging/validation.
    /// </summary>
    Mesh PredictResolvedMesh(out string source, out string meshName)
    {
        source = "Unknown";
        meshName = "Unknown";

        if (injectedShapePrefab != null)
        {
            var prefabMF = injectedShapePrefab.GetComponent<MeshFilter>();
            Mesh m = prefabMF?.sharedMesh ?? prefabMF?.mesh;
            source = $"INJECTED ('{injectedShapePrefab.name}')";
            meshName = m?.name ?? "NULL";
            return m;
        }

        if (processedShapePrefab != null)
        {
            var prefabMF = processedShapePrefab.GetComponent<MeshFilter>();
            Mesh m = prefabMF?.sharedMesh ?? prefabMF?.mesh;
            source = $"INSPECTOR ('{processedShapePrefab.name}')";
            meshName = m?.name ?? "NULL";
            return m;
        }

        Mesh fallback = GetFallbackMesh(out string fallbackName);
        source = $"FALLBACK ({fallbackShape})";
        meshName = fallback?.name ?? "NULL";
        return fallback;
    }

    void OnDestroy()
    {
        IO_Router.Instance?.Unregister(in_ProcessCmd, cbProcess);
        IO_Router.Instance?.Unregister(in_RestoreCmd, cbRestore);
        TagSubscriptionHelper.Remove(in_ProcessCmd);
        TagSubscriptionHelper.Remove(in_RestoreCmd);
        DestroyProcessedMesh();
        if (processedMaterial != null) { Destroy(processedMaterial); processedMaterial = null; }
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    [ContextMenu("Log Shape Configuration")]
    public void EditorLogShapeConfig() => LogShapeConfiguration();

    // ── Telemetry update ──────────────────────────────────────────────────────
    void Update()
    {
        if (!Application.isPlaying) return;
        float dt = Time.deltaTime;

        if (tel_CycleActive)
        {
            tel_CycleElapsed = Time.time - cycleStartTime;
            float fraction = Mathf.Clamp01(tel_CycleElapsed / Mathf.Max(0.5f, tel_AvgCycleTime));
            float ramp = Mathf.SmoothStep(0f, 1f, Mathf.Min(fraction * 6f, 1f))
                       * Mathf.SmoothStep(0f, 1f, Mathf.Min((1f - fraction) * 6f, 1f));
            tel_SpindleRPM    = ramp * (8000f + Mathf.PerlinNoise(Time.time * 0.5f, 0f) * 800f);
            tel_ToolVibration = ramp * (3f    + Mathf.PerlinNoise(Time.time * 3f,   1f) * 2f);
            tel_CoolantFlow   = ramp * (7f    + Mathf.PerlinNoise(Time.time * 0.8f, 2f) * 1f);
            axisPhase += dt * 0.8f;
            tel_AxisX  = Mathf.Sin(axisPhase * 1.3f) * 60f * ramp;
            tel_AxisY  = ramp * (30f + Mathf.Sin(axisPhase * 0.7f) * 10f);
            tel_AxisZ  = Mathf.Cos(axisPhase * 1.1f) * 50f * ramp;
        }
        else
        {
            tel_SpindleRPM    = Mathf.MoveTowards(tel_SpindleRPM,    0f, 2000f * dt);
            tel_ToolVibration = Mathf.MoveTowards(tel_ToolVibration, 0f,   5f  * dt);
            tel_CoolantFlow   = Mathf.MoveTowards(tel_CoolantFlow,   0f,   3f  * dt);
            tel_AxisX = Mathf.MoveTowards(tel_AxisX, 0f, 40f * dt);
            tel_AxisY = Mathf.MoveTowards(tel_AxisY, 0f, 20f * dt);
            tel_AxisZ = Mathf.MoveTowards(tel_AxisZ, 0f, 40f * dt);
        }

        float targetSpindleTemp = 30f + (tel_SpindleRPM / 8000f) * 150f;
        tel_SpindleTemp = Mathf.MoveTowards(tel_SpindleTemp, targetSpindleTemp,
                                            (tel_SpindleRPM > 100f ? 20f : 5f) * dt);
        float targetCoolantTemp = 20f + (tel_CoolantFlow / 8f) * 30f;
        tel_CoolantTemp = Mathf.MoveTowards(tel_CoolantTemp, targetCoolantTemp, 3f * dt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void Process()
    {
        if (dbIsProcessed)
        {
            Debug.LogWarning($"[PROCESSOR:{name}] Process() called but already processed — ignored.");
            return;
        }
        if (mf == null || mr == null)
        {
            Debug.LogError($"[PROCESSOR:{name}] MeshFilter or MeshRenderer is null!");
            return;
        }

        // ═══ LOG FULL SHAPE RESOLUTION ═══
        Debug.Log($"╔═════════════ PROCESS START: {name} ═════════════╗");
        Debug.Log($"  Injected override  : '{(injectedShapePrefab?.name ?? "NONE")}'");
        Debug.Log($"  Inspector prefab   : '{(processedShapePrefab?.name ?? "NONE")}'");
        Debug.Log($"  Fallback shape     : {fallbackShape}");
        Debug.Log($"  Original mesh      : '{(originalMesh?.name ?? "NULL")}'");
        Debug.Log($"  Quality mode       : {qualityMode}");

        tel_CycleActive  = true;
        cycleStartTime   = Time.time;
        tel_CycleElapsed = 0f;
        axisPhase        = 0f;

        Mesh baseMesh = ResolveMesh(out string shapeName, out string shapeSource);
        dbResolvedMeshName = baseMesh?.name ?? "NULL";
        dbShapeSource = shapeSource;
        
        Debug.Log($"  ──────────────────────────────────────────────");
        Debug.Log($"  RESOLVED: source='{shapeSource}' mesh='{dbResolvedMeshName}'");
        
        if (baseMesh == null)
        {
            Debug.LogError($"[PROCESSOR:{name}] ✖ Could not resolve a mesh — Process() aborted.");
            Debug.Log($"╚═════════════════════════════════════════════════╝");
            tel_CycleActive = false;
            return;
        }

        // Verify mesh will actually change
        if (originalMesh != null && baseMesh == originalMesh)
        {
            Debug.LogWarning($"[PROCESSOR:{name}] ⚠ RESOLVED MESH IS SAME AS ORIGINAL — no visible shape change!");
            Debug.LogWarning($"  To fix: Set a DIFFERENT shape in Inspector or change fallbackShape.");
        }
        else
        {
            Debug.Log($"[PROCESSOR:{name}] ✔ Mesh WILL change: '{originalMesh?.name}' → '{baseMesh.name}'");
        }

        processedMaterial.color = processedColor;

        bool   isDefective  = false;
        string defectDetail = "";
        Vector3 defectLoc   = Vector3.zero;
        ProcessingDefectReport.DefectCategory defectFlags = ProcessingDefectReport.DefectCategory.None;

        Mesh meshToApply;
        if (qualityMode == QualityMode.Defective)
        {
            DestroyProcessedMesh();
            processedMesh = CloneMesh(baseMesh, $"{baseMesh.name}_Defective");
            meshToApply   = processedMesh;
            ApplyDefects(processedMesh, out defectFlags, out defectDetail, out defectLoc);
            isDefective = (defectFlags != ProcessingDefectReport.DefectCategory.None);
        }
        else
        {
            DestroyProcessedMesh();
            processedMesh = CloneMesh(baseMesh, $"{baseMesh.name}_Processed");
            meshToApply   = processedMesh;
        }

        // Apply mesh
        Mesh previousMesh = mf.sharedMesh;
        mf.sharedMesh     = meshToApply;
        mr.sharedMaterial = processedMaterial;

        // Verify mesh was applied
        if (mf.sharedMesh != meshToApply)
        {
            Debug.LogError($"[PROCESSOR:{name}] ✖ MESH ASSIGNMENT FAILED! " +
                          $"Tried to set '{meshToApply.name}' but got '{mf.sharedMesh?.name}'");
        }
        else
        {
            Debug.Log($"[PROCESSOR:{name}] ✔ Mesh APPLIED: '{previousMesh?.name}' → '{meshToApply.name}'");
        }

        UpdateCollider(meshToApply);

        // Consume the shape override (one-time use)
        if (injectedShapePrefab != null)
        {
            Debug.Log($"[PROCESSOR:{name}] Shape override consumed (injectedShapePrefab cleared).");
            injectedShapePrefab = null;
            dbInjectedShapeName = "—";
        }

        dbIsProcessed    = true;
        dbIsDefective    = isDefective;
        dbShape          = shapeName;
        dbQualityVerdict = isDefective ? $"DEFECTIVE [{defectFlags}]" : "PERFECT";
        dbDefectDetail   = string.IsNullOrEmpty(defectDetail) ? "—" : defectDetail;
        dbFeedbackPhase  = "Processed";

        tel_CycleActive    = false;
        tel_TotalProcessed++;
        if (isDefective) tel_DefectiveCount++;
        else             tel_PerfectCount++;
        tel_DefectRatePct  = tel_TotalProcessed > 0 ? tel_DefectiveCount * 100f / tel_TotalProcessed : 0f;
        tel_LastVerdict    = isDefective ? $"DEFECTIVE [{defectFlags}]" : "PERFECT";
        tel_LastDefectFlags= (int)defectFlags;
        float elapsed      = Time.time - cycleStartTime;
        cycleTotalTime    += elapsed;
        cycleSamples++;
        tel_AvgCycleTime   = cycleSamples > 0 ? cycleTotalTime / cycleSamples : 0f;
        tel_CycleElapsed   = elapsed;

        var report = new ProcessingDefectReport(
            objectName:        name,
            objectInstanceID:  transform.GetInstanceID(),
            selectedShapeName: shapeName,
            isDefective:       isDefective,
            defects:           defectFlags,
            defectLocation:    defectLoc,
            defectDetail:      defectDetail);

        Debug.Log($"  Final shape: '{shapeName}' | Verdict: {dbQualityVerdict}");
        Debug.Log($"╚═════════════════════════════════════════════════╝");

        try { OnProcessingComplete?.Invoke(report); }
        catch (Exception e) { Debug.LogError($"[PROCESSOR:{name}] Subscriber threw: {e.Message}"); }

        IO_Router.Instance?.SetValueWithHandoff(out_IsRaw, out_IsProcessed);

        if (isDefective) { SetOutput(out_IsDefective, true);  SetOutput(out_IsPerfect,   false); }
        else             { SetOutput(out_IsPerfect,   true);  SetOutput(out_IsDefective, false); }

        SetOutput(out_ProcessDone, true);
        SetOutput(out_QCResult,    true);
        StartCoroutine(PulseDone(out_ProcessDone));
        StartCoroutine(PulseDone(out_QCResult));
    }

    public void Restore()
    {
        if (!dbIsProcessed)
        {
            Debug.LogWarning($"[PROCESSOR:{name}] Restore() called but already raw — ignored.");
            return;
        }

        if (mf != null && originalMesh     != null) mf.sharedMesh    = originalMesh;
        if (mr != null && originalMaterial != null) mr.sharedMaterial = originalMaterial;
        RestoreCollider();
        DestroyProcessedMesh();

        dbIsProcessed    = false;
        dbIsDefective    = false;
        dbShape          = "Raw";
        dbQualityVerdict = "—";
        dbDefectDetail   = "—";
        dbFeedbackPhase  = "Raw";
        dbResolvedMeshName = "—";
        dbShapeSource     = "—";
        injectedShapePrefab = null;
        dbInjectedShapeName = "—";

        tel_CycleActive = false;
        tel_LastVerdict = "—";

        IO_Router.Instance?.SetValueWithHandoff(out_IsProcessed, out_IsRaw);
        SetOutput(out_IsDefective, false);
        SetOutput(out_IsPerfect,   false);
        SetOutput(out_RestoreDone, true);
        StartCoroutine(PulseDone(out_RestoreDone));

        Debug.Log($"[PROCESSOR:{name}] Restored to raw state.");
    }

    // ── Mesh resolution ══════════════════════════════════════════════════════

    /// <summary>
    /// Returns the SOURCE mesh for CNC processing.
    /// Priority:
    ///   1. injectedShapePrefab (set via SetShapeOverride) — HIGHEST
    ///   2. processedShapePrefab (set in Inspector)
    ///   3. fallbackShape enum
    /// </summary>
    Mesh ResolveMesh(out string shapeName, out string shapeSource)
    {
        shapeName = "Unknown";
        shapeSource = "Unknown";

        // Priority 1: Injected override
        if (injectedShapePrefab != null)
        {
            shapeName = injectedShapePrefab.name;
            shapeSource = $"INJECTED";
            
            var prefabMF = injectedShapePrefab.GetComponent<MeshFilter>();
            if (prefabMF == null)
            {
                Debug.LogError($"[PROCESSOR:{name}] Injected shape '{injectedShapePrefab.name}' has no MeshFilter! Falling back.");
            }
            else
            {
                Mesh m = prefabMF.sharedMesh ?? prefabMF.mesh;
                if (m != null)
                {
                    Debug.Log($"[PROCESSOR:{name}] Using INJECTED shape: '{injectedShapePrefab.name}' → mesh '{m.name}'");
                    return m;
                }
                Debug.LogError($"[PROCESSOR:{name}] Injected shape '{injectedShapePrefab.name}' MeshFilter has no mesh! Falling back.");
            }
        }

        // Priority 2: Inspector prefab
        if (processedShapePrefab != null)
        {
            shapeName = processedShapePrefab.name;
            shapeSource = $"INSPECTOR";
            
            var prefabMF = processedShapePrefab.GetComponent<MeshFilter>();
            if (prefabMF == null)
            {
                Debug.LogError($"[PROCESSOR:{name}] Inspector shape '{processedShapePrefab.name}' has no MeshFilter! Falling back to fallbackShape.");
            }
            else
            {
                Mesh m = prefabMF.sharedMesh ?? prefabMF.mesh;
                if (m != null)
                {
                    Debug.Log($"[PROCESSOR:{name}] Using INSPECTOR shape: '{processedShapePrefab.name}' → mesh '{m.name}'");
                    return m;
                }
                Debug.LogError($"[PROCESSOR:{name}] Inspector shape '{processedShapePrefab.name}' MeshFilter has no mesh! Falling back to fallbackShape.");
            }
        }

        // Priority 3: Fallback
        Mesh fallback = GetFallbackMesh(out string fallbackName);
        shapeName = fallbackName;
        shapeSource = $"FALLBACK({fallbackShape})";
        Debug.Log($"[PROCESSOR:{name}] Using FALLBACK shape: {fallbackShape} → mesh '{fallback?.name}'");
        return fallback;
    }

    Mesh GetFallbackMesh(out string shapeName)
    {
        shapeName = fallbackShape.ToString();
        if (fallbackMeshCache.TryGetValue(fallbackShape, out Mesh cached) && cached != null) 
            return cached;
        
        PrimitiveType pt = fallbackShape switch
        {
            FallbackShape.Sphere   => PrimitiveType.Sphere,
            FallbackShape.Cylinder => PrimitiveType.Cylinder,
            FallbackShape.Capsule  => PrimitiveType.Capsule,
            FallbackShape.Cube     => PrimitiveType.Cube,
            _                      => PrimitiveType.Cube
        };
        var tmp = GameObject.CreatePrimitive(pt);
        Mesh m  = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        fallbackMeshCache[fallbackShape] = m;
        return m;
    }

    // ── Collider management ══════════════════════════════════════════════════
    void UpdateCollider(Mesh m)
    {
        if (m == null) return;

        var mc = GetComponent<MeshCollider>();
        if (mc != null)
        {
            mc.sharedMesh = null;
            mc.sharedMesh = m;
            mc.convex     = true;
            return;
        }

        bool meshChanged = m != originalMesh;
        if (!meshChanged) return;

        if (originalCollider != null)
            originalCollider.enabled = false;

        var newMC     = gameObject.AddComponent<MeshCollider>();
        newMC.sharedMesh = m;
        newMC.convex     = true;
        addedMeshCollider = true;
    }

    void RestoreCollider()
    {
        if (addedMeshCollider)
        {
            var mc = GetComponent<MeshCollider>();
            if (mc != null) Destroy(mc);
            addedMeshCollider = false;
        }
        if (originalCollider != null)
            originalCollider.enabled = true;
    }

    // ── Defect engine ════════════════════════════════════════════════════════
    void ApplyDefects(Mesh mesh, out ProcessingDefectReport.DefectCategory defectFlags,
                      out string defectDetail, out Vector3 defectLocation)
    {
        defectFlags    = ProcessingDefectReport.DefectCategory.None;
        defectDetail   = "";
        defectLocation = Vector3.zero;

        var rng = new System.Random(defectSeed >= 0 ? defectSeed : Environment.TickCount);
        var details = new System.Text.StringBuilder();

        if (defect_VertexRemoval)
        {
            int removed = ApplyVertexRemoval(mesh, rng, out Vector3 loc, out string desc);
            if (removed > 0) { defectFlags |= ProcessingDefectReport.DefectCategory.VertexRemoval; defectLocation = loc; details.AppendLine($"VertexRemoval: {desc}"); }
        }
        if (defect_SurfaceNoise)
        {
            ApplySurfaceNoise(mesh, rng, out string desc);
            defectFlags |= ProcessingDefectReport.DefectCategory.SurfaceNoise;
            details.AppendLine($"SurfaceNoise: {desc}");
            if (defectLocation == Vector3.zero) defectLocation = mesh.bounds.center;
        }
        if (defect_ScaleDeform)
        {
            ApplyScaleDeform(mesh, rng, out Vector3 axis, out string desc);
            defectFlags |= ProcessingDefectReport.DefectCategory.ScaleDeform;
            details.AppendLine($"ScaleDeform: {desc}");
            if (defectLocation == Vector3.zero) defectLocation = axis;
        }

        defectDetail = details.ToString().TrimEnd();
    }

    int ApplyVertexRemoval(Mesh mesh, System.Random rng, out Vector3 loc, out string desc)
    {
        Vector3 axis = new Vector3((float)(rng.NextDouble()*2-1),(float)(rng.NextDouble()*2-1),(float)(rng.NextDouble()*2-1)).normalized;
        Vector3[] verts = mesh.vertices; int[] tris = mesh.triangles; int triCount = tris.Length/3;
        var kept = new List<int>(tris.Length); int removed = 0;
        for (int i=0;i<triCount;i++)
        {
            int i0=tris[i*3],i1=tris[i*3+1],i2=tris[i*3+2];
            Vector3 c=(verts[i0]+verts[i1]+verts[i2])/3f;
            if (Vector3.Dot(c.normalized,axis)>0f && rng.NextDouble()<vertexRemovalFraction) removed++;
            else { kept.Add(i0);kept.Add(i1);kept.Add(i2); }
        }
        mesh.triangles = kept.ToArray(); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        loc  = axis;
        float pct = triCount>0?removed*100f/triCount:0f;
        desc = $"{removed}/{triCount} triangles ({pct:F0}%) stripped";
        return removed;
    }

    void ApplySurfaceNoise(Mesh mesh, System.Random rng, out string desc)
    {
        Vector3[] verts=mesh.vertices; Vector3[] normals=mesh.normals;
        if (normals.Length!=verts.Length) { mesh.RecalculateNormals(); normals=mesh.normals; }
        for (int i=0;i<verts.Length;i++) verts[i]+=normals[i]*((float)(rng.NextDouble()*2-1)*surfaceNoiseIntensity);
        mesh.vertices=verts; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        desc=$"All {verts.Length} vertices displaced ±{surfaceNoiseIntensity:F3}u";
    }

    void ApplyScaleDeform(Mesh mesh, System.Random rng, out Vector3 crushAxis, out string desc)
    {
        int ax = scaleDeformAxis<0?rng.Next(0,3):Mathf.Clamp(scaleDeformAxis,0,2);
        float crush=1f-scaleDeformAmount; Vector3[] v=mesh.vertices;
        for (int i=0;i<v.Length;i++) { if(ax==0)v[i].x*=crush; else if(ax==1)v[i].y*=crush; else v[i].z*=crush; }
        mesh.vertices=v; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string axName=ax==0?"X":ax==1?"Y":"Z";
        crushAxis=ax==0?Vector3.right:ax==1?Vector3.up:Vector3.forward;
        desc=$"Axis {axName} crushed to {crush*100f:F0}%";
    }

    static Mesh CloneMesh(Mesh src, string n)
    {
        if (src == null)
        {
            Debug.LogError($"[CubeProcessor] CloneMesh: source is NULL!");
            return new Mesh { name = n + "_Error" };
        }
        var c = new Mesh { name = n };
        c.SetVertices(new List<Vector3>(src.vertices));
        c.SetNormals(new List<Vector3>(src.normals));
        c.SetUVs(0, new List<Vector2>(src.uv));
        c.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++) c.SetTriangles(src.GetTriangles(s), s);
        c.RecalculateBounds();
        return c;
    }

    void DestroyProcessedMesh() 
    { 
        if (processedMesh != null) { Destroy(processedMesh); processedMesh = null; } 
    }

    IEnumerator PulseDone(string tag) 
    { 
        yield return new WaitForSeconds(0.2f); 
        SetOutput(tag, false); 
    }

    void SetOutput(string tag, bool v)
    { 
        if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); 
    }

#if UNITY_EDITOR
    [ContextMenu("Test: Process (Perfect)")]
    void EditorTestProcessPerfect()
    { 
        if (!Application.isPlaying) { Debug.LogWarning("[PROCESSOR] Must be in Play mode."); return; } 
        qualityMode = QualityMode.Perfect; 
        if (dbIsProcessed) Restore(); 
        Process(); 
    }

    [ContextMenu("Test: Process (Defective)")]
    void EditorTestProcessDefective()
    { 
        if (!Application.isPlaying) { Debug.LogWarning("[PROCESSOR] Must be in Play mode."); return; } 
        qualityMode = QualityMode.Defective; 
        if (dbIsProcessed) Restore(); 
        Process(); 
    }

    [ContextMenu("Test: Restore")]
    void EditorTestRestore()
    { 
        if (!Application.isPlaying) { Debug.LogWarning("[PROCESSOR] Must be in Play mode."); return; } 
        Restore(); 
    }
#endif
}