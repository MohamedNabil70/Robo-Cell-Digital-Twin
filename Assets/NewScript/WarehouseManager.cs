using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// WarehouseManager — master batch controller with integrated QC routing.
/// (Original functionality preserved. Telemetry section added at bottom of Inspector.)
/// </summary>
public enum WarehouseControlMode
{
    DirectSnapshot,
    EventDrivenPayloadFlow
}

public class WarehouseManager : MonoBehaviour
{
    public static WarehouseManager Instance { get; private set; }

    [Header("══ Warehouse A — Object Spawn Slots ══════════════════════════")]
    public List<Transform> warehouseASlots = new List<Transform>();
    public GameObject objectPrefab;
    public List<GameObject> sceneObjects = new List<GameObject>();
    public Vector3 spawnOffset = new Vector3(0f, 0.05f, 0f);

    [Header("══ Warehouse B — Delivery Slots ══════════════════════════════")]
    public List<Transform> warehouseBSlots = new List<Transform>();
    [Tooltip("Finished output shown on Warehouse B for MQTT deliveredToB snapshots. Assign Assets/Turbine-Model/Turbine.fbx here.")]
    public GameObject deliveredOutputPrefab;
    [Tooltip("Reference scene object to copy turbine display scale/rotation from, e.g. TurbineExample.")]
    public GameObject deliveredOutputExampleObject;
    [Tooltip("Used to auto-find the scene reference when deliveredOutputExampleObject is not assigned.")]
    public string deliveredOutputExampleName = "TurbineExample";
    [Tooltip("Optional pre-placed finished output objects to reuse before instantiating deliveredOutputPrefab.")]
    public List<GameObject> deliveredOutputSceneObjects = new List<GameObject>();
    public Vector3 deliveryOffset = new Vector3(0f, 0.05f, 0f);

    [Header("══ Warehouse B — Reject Slots (defective parts) ════════════════")]
    [Tooltip("Slots where DEFECTIVE parts are placed. Leave empty to use normal WH-B slots.")]
    public List<Transform> warehouseRejectSlots = new List<Transform>();
    public Vector3 rejectOffset = new Vector3(0f, 0.05f, 0f);

    [Header("══ System References ══════════════════════════════════════════")]
    public RobotCar1          robotCar1;
    public SensorTrigger      sensor1;
    public SensorTrigger      sensor2;
    public RobotArmController arm1;
    public ConveyorMotor      conveyor1;
    public ConveyorMotor      conveyor2;

    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    public bool  offlineMode         = true;
    public bool  offlineAutoStart    = true;
    public float postDispatchDelay   = 0.5f;
    public float offlineAutoRestartDelay = 0f;

    [Header("══ PLC INPUT Tags (PLC → Unity) ════════════════════════════════")]
    public string in_StartBatch = "";
    public string in_StopEStop  = "";
    public string in_PauseTag   = "";
    public string in_ResumeTag  = "";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ════════════════════════════════")]
    public string out_Started     = "";
    public string out_InProgress  = "";
    public string out_AllDone     = "";
    public string out_EStopActive = "";

    [HideInInspector] public string out_ObjectReady     = "";
    [HideInInspector] public string out_BatchInProgress = "";
    [HideInInspector] public string out_DispatchPaused  = "";

    [Header("══ QC Output Tags (Unity → PLC) ═══════════════════════════════")]
    public string out_BatchHasDefects   = "";
    public string out_LastPartDefective = "";
    public string out_LastPartPerfect   = "";
    public string out_BatchQCSummary    = "";

    [Header("══ Debug — Pipeline Tracker (Read Only) ══════════════════════")]
    [SerializeField] int    dbTotalObjects       = 0;
    [SerializeField] int    dbRemainingInA       = 0;
    [SerializeField] int    dbDeliveredToB       = 0;
    [SerializeField] bool   dbPaused             = false;
    [SerializeField] bool   dbEStop              = false;
    [SerializeField] bool   dbWaitingForDelivery = false;
    [SerializeField] bool   dbWaitingForCar1     = false;
    [SerializeField] string dbStatus             = "Idle";
    [SerializeField] string dbCurrentObject      = "—";
    [SerializeField] string dbPipelineStage      = "—";
    [SerializeField] string dbMode               = "Offline";
    [SerializeField] string dbFeedbackPhase      = "Idle";

    [Header("══ Debug — QC Batch Summary (Read Only) ══════════════════════")]
    [SerializeField] int    dbBatchDefectCount     = 0;
    [SerializeField] int    dbBatchPerfectCount    = 0;
    [SerializeField] string dbLastDeliveredVerdict = "—";
    [SerializeField] string dbLastDeliveredShape   = "—";

    // ══════════════════════════════════════════════════════════════════════════
    // ══ TELEMETRY ════════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    [Header("══ TELEMETRY — Batch Progress ════════════════════════════════")]
    [Tooltip("Batch completion percentage (0–100%).")]
    [SerializeField] float tel_BatchProgressPct   = 0f;
    [Tooltip("Elapsed time since current batch started (s).")]
    [SerializeField] float tel_BatchElapsedTime   = 0f;
    [Tooltip("Estimated time to batch completion based on average cycle time (s).")]
    [SerializeField] float tel_EstTimeRemaining   = 0f;
    [Tooltip("Average time per full object cycle (WH-A → WH-B) in seconds.")]
    [SerializeField] float tel_AvgCycleTime       = 0f;
    [Tooltip("Total batches completed since scene start.")]
    [SerializeField] int   tel_BatchesCompleted   = 0;

    [Header("══ TELEMETRY — Slot Occupancy ════════════════════════════════")]
    [Tooltip("Number of occupied WH-A slots (objects still waiting to be dispatched).")]
    [SerializeField] int tel_WHAOccupied     = 0;
    [Tooltip("Number of occupied WH-B slots (delivered objects).")]
    [SerializeField] int tel_WHBOccupied     = 0;
    [Tooltip("Number of occupied WH-B reject slots (defective objects delivered).")]
    [SerializeField] int tel_RejectOccupied  = 0;

    [Header("══ TELEMETRY — QC Session Stats ════════════════════════════")]
    [Tooltip("Running defect rate across all batches (%).")]
    [SerializeField] float tel_AllBatchDefectRate = 0f;
    [Tooltip("Total perfect parts delivered across all batches.")]
    [SerializeField] int   tel_TotalPerfect       = 0;
    [Tooltip("Total defective parts delivered across all batches.")]
    [SerializeField] int   tel_TotalDefective     = 0;
    [Tooltip("Pipeline throughput: objects delivered per hour (rolling estimate).")]
    [SerializeField] float tel_ThroughputPerHour  = 0f;

    [Header("══ TELEMETRY — Pipeline Gates ════════════════════════════════")]
    [Tooltip("Delivery gate: TRUE = unlocked (next object can be dispatched).")]
    [SerializeField] bool  tel_GateDelivery   = true;
    [Tooltip("Car1 gate: TRUE = Car1 is back at WH-A and ready.")]
    [SerializeField] bool  tel_GateCar1       = true;
    [Tooltip("Dispatch gate: TRUE = not paused or E-Stopped.")]
    [SerializeField] bool  tel_GateDispatch   = true;

    [Header("MQTT / External Warehouse Sync")]
    [Tooltip("DirectSnapshot preserves warehouse/control remainingInA/B behavior. EventDrivenPayloadFlow makes WarehouseManager the owner of payload visuals.")]
    public WarehouseControlMode warehouseControlMode = WarehouseControlMode.DirectSnapshot;
    [Tooltip("When true, WarehouseManager does not spawn/reorder shelf visuals or run the local batch flow until warehouse/control snapshots arrive.")]
    public bool externalWarehouseControlOnly = true;
    [Tooltip("Log whenever an inbound warehouse/control snapshot corrects shelf occupancy.")]
    [SerializeField] bool logExternalWarehouseSync = true;
    [Tooltip("Use separate shelf display objects for MQTT warehouse snapshots so live/offline simulation is not interrupted.")]
    [SerializeField] bool useSeparateMqttShelfVisuals = true;
    [Tooltip("Parent MQTT shelf visuals to their slot transforms. This makes offsets local to each shelf slot.")]
    [SerializeField] bool parentMqttShelfVisualsToSlots = true;
    [Tooltip("Fallback size for passive raw-box shelf visuals when no clean object prefab is assigned.")]
    [SerializeField] Vector3 mqttRawBoxVisualScale = new Vector3(0.45f, 0.45f, 0.45f);
    [SerializeField] Vector3 mqttRawBoxVisualOffset = new Vector3(0f, 0.05f, 0f);
    [SerializeField] Vector3 mqttRawBoxVisualEuler = Vector3.zero;
    [Tooltip("Scale for turbine visuals spawned on Warehouse B by MQTT deliveredToB.")]
    [SerializeField] Vector3 mqttDeliveredOutputVisualScale = new Vector3(0.051053386f, 0.051053386f, 0.051053386f);
    [SerializeField] Vector3 mqttDeliveredOutputVisualOffset = new Vector3(0f, 0.05f, 0f);
    [SerializeField] Vector3 mqttDeliveredOutputVisualEuler = new Vector3(-98.206f, 90f, -90f);
    [Tooltip("Copy scale from deliveredOutputExampleObject / TurbineExample when available.")]
    [SerializeField] bool matchDeliveredOutputExampleScale = true;
    [Tooltip("Copy rotation from deliveredOutputExampleObject / TurbineExample when available.")]
    [SerializeField] bool matchDeliveredOutputExampleRotation = true;
    [Tooltip("Wrap spawned turbines in a parent object and shift the mesh so the parent axis is at the turbine bounds center.")]
    [SerializeField] bool centerDeliveredOutputPivot = true;

    // ── Telemetry private ──────────────────────────────────────────────────────
    float batchStartTime   = 0f;
    float cycleStartTime   = 0f;
    float cycleTotalTime   = 0f;
    int   cycleSamples     = 0;
    int   totalAllBatchDelivered = 0;
    int   totalAllBatchDefective = 0;
    readonly Queue<float> deliveryTimestamps = new Queue<float>(); // for throughput
    bool hasReceivedExternalWarehouseControl = false;
    bool warnedEventDrivenSnapshotIgnored = false;

    // ─────────────────────────────────────────────────────────────────────────
    List<GameObject> spawnedObjects = new List<GameObject>();
    List<GameObject> mqttWarehouseAObjects = new List<GameObject>();
    List<GameObject> deliveredOutputObjects = new List<GameObject>();
    Queue<int>       pendingQueue   = new Queue<int>();
    int              nextBSlot      = 0;
    bool             batchStarted   = false;
    bool             carBusy        = false;
    bool             dispatchPaused = false;
    bool             eStopActive    = false;
    bool             batchComplete  = false;

    readonly Dictionary<int, ProcessingDefectReport> pendingQCReports =
        new Dictionary<int, ProcessingDefectReport>();

    int  nextRejectSlot  = 0;
    bool batchHasDefects = false;
    bool previousDelivered = true;

    Coroutine dispatchCoroutine = null;

    System.Action<bool> cbStartBatch, cbStop, cbPause, cbResume;
    WarehouseControlMode appliedWarehouseControlMode;
    bool hasAppliedWarehouseControlMode;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ApplyPayloadVisualOwnershipMode();
    }

    void OnEnable()  => CubeProcessor.OnProcessingComplete += HandleQCReport;
    void OnDisable() => CubeProcessor.OnProcessingComplete -= HandleQCReport;

    void Update()
    {
        if (!Application.isPlaying) return;

        if (!hasAppliedWarehouseControlMode || appliedWarehouseControlMode != warehouseControlMode)
        {
            ApplyPayloadVisualOwnershipMode();
        }

        // Batch elapsed
        if (batchStarted && !batchComplete)
            tel_BatchElapsedTime = Time.time - batchStartTime;

        // Progress percentage
        tel_BatchProgressPct = dbTotalObjects > 0
            ? Mathf.Clamp01((float)dbDeliveredToB / dbTotalObjects) * 100f
            : 0f;

        // Estimated time remaining
        if (tel_AvgCycleTime > 0f && dbTotalObjects > 0)
        {
            int remaining = dbTotalObjects - dbDeliveredToB;
            tel_EstTimeRemaining = remaining * tel_AvgCycleTime;
        }
        else tel_EstTimeRemaining = 0f;

        // Slot occupancy
        tel_WHAOccupied    = pendingQueue.Count;
        tel_WHBOccupied    = nextBSlot;
        tel_RejectOccupied = nextRejectSlot;
        if (externalWarehouseControlOnly && hasReceivedExternalWarehouseControl)
        {
            tel_WHAOccupied = dbRemainingInA;
            tel_WHBOccupied = dbDeliveredToB;
            tel_RejectOccupied = 0;
        }

        // Pipeline gate mirrors
        tel_GateDelivery = previousDelivered;
        tel_GateCar1     = !carBusy;
        tel_GateDispatch = !dispatchPaused && !eStopActive;

        // Throughput per hour: count deliveries in last 3600s (approximate)
        float now = Time.time;
        while (deliveryTimestamps.Count > 0 && now - deliveryTimestamps.Peek() > 3600f)
            deliveryTimestamps.Dequeue();
        tel_ThroughputPerHour = deliveryTimestamps.Count; // delivered in last hour

        // All-batch defect rate
        int allTotal = tel_TotalPerfect + tel_TotalDefective;
        tel_AllBatchDefectRate = allTotal > 0 ? tel_TotalDefective * 100f / allTotal : 0f;
    }

    void Start()
    {
        ApplyPayloadVisualOwnershipMode();

        if (robotCar1 == null)
        {
            robotCar1 = FindObjectOfType<RobotCar1>();
            if (robotCar1 != null) Debug.Log("[WH] Auto-assigned robotCar1 reference.");
        }

        dbMode = (offlineMode || offlineAutoStart) ? "Offline" : "PLC";
        if (externalWarehouseControlOnly)
        {
            dbMode = "MQTT";
            dbStatus = "Waiting for warehouse/control";
            dbPipelineStage = "Waiting for external warehouse snapshot";
            dbFeedbackPhase = "ExternalControlOnly";
        }
        else
        {
            RunSystemDiagnostics();
            SpawnWarehouseAObjects();
        }

        cbStartBatch = v =>
        {
            if (!v) return;
            if      (!batchStarted)  StartBatch();
            else if (batchComplete)  { Debug.Log("[WH] Restarting batch."); RestartBatch(); }
            else                     Debug.LogWarning("[WH] Batch already running — ignored.");
        };

        cbStop = v =>
        {
            if (!v) return;
            eStopActive=true; dispatchPaused=true; dbEStop=true; dbPaused=true;
            SetOutput(out_EStopActive,true); SetOutput(out_DispatchPaused,true);
            Debug.LogWarning("[WH] STOP/E-STOP — dispatch halted.");
        };

        cbPause = v =>
        {
            if (offlineMode||!v) return;
            dispatchPaused=true; dbPaused=true; SetOutput(out_DispatchPaused,true);
            Debug.Log("[WH] Dispatch PAUSED.");
        };

        cbResume = v =>
        {
            if (offlineMode||!v) return;
            if (eStopActive){eStopActive=false;dbEStop=false;SetOutput(out_EStopActive,false);}
            dispatchPaused=false; dbPaused=false; SetOutput(out_DispatchPaused,false);
            Debug.Log("[WH] Dispatch RESUMED.");
        };

        RegisterAllInputTags();

        if (!externalWarehouseControlOnly && (offlineMode || offlineAutoStart)) StartCoroutine(AutoStart());
    }

    void RegisterAllInputTags()
    {
        if (!string.IsNullOrEmpty(in_StartBatch)) { Debug.Log($"[WH] Registering '{in_StartBatch}'."); StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_StartBatch,cbStartBatch,this)); }
        if (!string.IsNullOrEmpty(in_StopEStop))  { Debug.Log($"[WH] Registering '{in_StopEStop}'."); StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_StopEStop, cbStop,       this)); }
        if (!string.IsNullOrEmpty(in_PauseTag))   { Debug.Log($"[WH] Registering '{in_PauseTag}'."); StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_PauseTag,  cbPause,      this)); }
        if (!string.IsNullOrEmpty(in_ResumeTag))  { Debug.Log($"[WH] Registering '{in_ResumeTag}'."); StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_ResumeTag, cbResume,     this)); }
    }

    IEnumerator AutoStart()
    {
        yield return null;
        while (IO_Router.Instance == null) yield return null;
        yield return new WaitForSeconds(0.5f);

        int fails=0;
        if (robotCar1==null) fails++;
        if (arm1==null) fails++;
        int validScene=sceneObjects.FindAll(o=>o!=null).Count;
        if (objectPrefab==null&&validScene==0) fails++;
        if (warehouseASlots.FindAll(s=>s!=null).Count==0) fails++;
        if (fails>0){Debug.LogError($"[WH] AutoStart aborted — {fails} critical refs missing.");yield break;}
        StartBatch();
    }

    void OnDestroy()
    {
        IO_Router.Instance?.Unregister(in_StartBatch,cbStartBatch);
        IO_Router.Instance?.Unregister(in_StopEStop, cbStop);
        IO_Router.Instance?.Unregister(in_PauseTag,  cbPause);
        IO_Router.Instance?.Unregister(in_ResumeTag, cbResume);
        TagSubscriptionHelper.Remove(in_StartBatch);
        TagSubscriptionHelper.Remove(in_StopEStop);
        TagSubscriptionHelper.Remove(in_PauseTag);
        TagSubscriptionHelper.Remove(in_ResumeTag);
        CubeProcessor.OnProcessingComplete -= HandleQCReport;
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    [ContextMenu("Apply Payload Visual Ownership Mode")]
    public void ApplyPayloadVisualOwnershipMode()
    {
        bool allowLocalPayloadVisuals = warehouseControlMode != WarehouseControlMode.EventDrivenPayloadFlow;

        CarControlReceiver[] carReceivers = FindObjectsByType<CarControlReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < carReceivers.Length; i++)
        {
            if (carReceivers[i] != null)
                carReceivers[i].SetLocalPayloadVisualsAllowed(allowLocalPayloadVisuals);
        }

        ConveyorControlReceiver[] conveyorReceivers = FindObjectsByType<ConveyorControlReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < conveyorReceivers.Length; i++)
        {
            if (conveyorReceivers[i] != null)
                conveyorReceivers[i].SetLocalPayloadVisualsAllowed(allowLocalPayloadVisuals);
        }

        appliedWarehouseControlMode = warehouseControlMode;
        hasAppliedWarehouseControlMode = true;

        if (Application.isPlaying)
        {
            Debug.Log($"[WH] Payload visual ownership mode applied: {warehouseControlMode}. Local car/conveyor payload visuals allowed={allowLocalPayloadVisuals}.");
        }
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────
    void SpawnWarehouseAObjects()
    {
        spawnedObjects.Clear();
        int count=0;
        for (int i=0;i<warehouseASlots.Count;i++)
        {
            Transform slot=warehouseASlots[i];
            if (slot==null){spawnedObjects.Add(null);Debug.LogWarning($"[WH] Slot {i} is null — skipped.");continue;}
            GameObject obj=null;
            if (objectPrefab!=null) { obj=Instantiate(objectPrefab,slot.position+spawnOffset,slot.rotation); obj.name=$"ProductObject_{i:D2}"; }
            else if (i<sceneObjects.Count&&sceneObjects[i]!=null) { obj=sceneObjects[i]; obj.transform.position=slot.position+spawnOffset; obj.transform.rotation=slot.rotation; obj.SetActive(true); }
            else { Debug.LogWarning($"[WH] Slot {i}: no prefab and no scene object."); spawnedObjects.Add(null); continue; }
            try { if(obj.CompareTag("Untagged")) obj.tag="ProductObject"; } catch {}
            conveyor1?.RegisterObject(obj.transform);
            conveyor2?.RegisterObject(obj.transform);
            spawnedObjects.Add(obj); count++;
        }
        dbTotalObjects=count; dbRemainingInA=count;
        tel_WHAOccupied=count;
        Debug.Log($"[WH] Spawned {count} objects.");
    }

    // ── Batch control ─────────────────────────────────────────────────────────
    public void StartBatch()
    {
        if (externalWarehouseControlOnly)
        {
            Debug.LogWarning("[WH] Ignored StartBatch because externalWarehouseControlOnly=true. Publish factory/cell1/twin/warehouse/control to drive shelf visuals.");
            return;
        }

        if (batchStarted) return;
        batchStarted=true; batchComplete=false; previousDelivered=true;
        dbWaitingForDelivery=false; dbWaitingForCar1=false;
        dbStatus="Batch running"; dbDeliveredToB=0; nextBSlot=0;

        pendingQCReports.Clear(); nextRejectSlot=0; batchHasDefects=false;
        dbBatchDefectCount=0; dbBatchPerfectCount=0;
        dbLastDeliveredVerdict="—"; dbLastDeliveredShape="—";

        SetOutput(out_BatchHasDefects,false); SetOutput(out_LastPartDefective,false);
        SetOutput(out_LastPartPerfect,false); SetOutput(out_BatchQCSummary,false);
        SetOutput(out_Started,true); SetOutput(out_InProgress,true); SetOutput(out_AllDone,false);
        dbFeedbackPhase="BatchRunning";

        // Telemetry
        batchStartTime       = Time.time;
        tel_BatchElapsedTime = 0f;
        tel_BatchProgressPct = 0f;
        cycleStartTime       = Time.time;

        pendingQueue.Clear();
        for (int i=0;i<spawnedObjects.Count;i++)
            if (spawnedObjects[i]!=null) pendingQueue.Enqueue(i);
        dbRemainingInA=pendingQueue.Count;

        Debug.Log($"[WH] Batch started — {pendingQueue.Count} objects in {dbMode} mode.");

        if (dispatchCoroutine!=null){StopCoroutine(dispatchCoroutine);dispatchCoroutine=null;}
        dispatchCoroutine=StartCoroutine(DispatchLoop());
        StartCoroutine(StuckWatchdog());
    }

    public void RestartBatch()
    {
        if (externalWarehouseControlOnly)
        {
            Debug.LogWarning("[WH] Ignored RestartBatch because externalWarehouseControlOnly=true. Warehouse visuals are controlled by warehouse/control snapshots.");
            return;
        }

        if (batchStarted&&!batchComplete){Debug.LogWarning("[WH] RestartBatch while running — ignored.");return;}
        Debug.Log("[WH] ▶▶ Restarting batch — recycling objects to WH-A...");
        if (dispatchCoroutine!=null){StopCoroutine(dispatchCoroutine);dispatchCoroutine=null;}
        batchStarted=false; batchComplete=false; carBusy=false;
        pendingQCReports.Clear(); nextRejectSlot=0; batchHasDefects=false;
        dbBatchDefectCount=0; dbBatchPerfectCount=0;

        int recycled=0;
        for (int i=0;i<spawnedObjects.Count&&i<warehouseASlots.Count;i++)
        {
            GameObject obj=spawnedObjects[i]; Transform slot=warehouseASlots[i];
            if(obj==null||slot==null) continue;
            obj.transform.SetParent(null,false);
            var rb=obj.GetComponent<Rigidbody>();
            if(rb!=null){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.isKinematic=true;}
            obj.transform.position=slot.position+spawnOffset; obj.transform.rotation=slot.rotation; obj.SetActive(true);
            if(rb!=null){rb.isKinematic=false;rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;}
            var cp=obj.GetComponent<CubeProcessor>();
            if(cp!=null){cp.Restore();Debug.Log($"[WH] Restore() on '{obj.name}'.");}
            conveyor1?.RegisterObject(obj.transform); conveyor2?.RegisterObject(obj.transform);
            recycled++;
        }
        sensor1?.ResetTrigger(); sensor2?.ResetTrigger();
        conveyor1?.SetSensorOverride(false); conveyor2?.SetSensorOverride(false);
        conveyor1?.SetHeld(false); conveyor2?.SetHeld(false);
        dbDeliveredToB=0; nextBSlot=0;
        dbTotalObjects=spawnedObjects.FindAll(o=>o!=null).Count; dbRemainingInA=dbTotalObjects;
        // Telemetry reset
        tel_WHBOccupied=0; tel_RejectOccupied=0; tel_BatchProgressPct=0f;
        Debug.Log($"[WH] ✔ Recycled {recycled} objects. Starting new batch.");
        StartBatch();
    }

    IEnumerator DispatchLoop()
    {
        while (pendingQueue.Count>0)
        {
            dbWaitingForDelivery=!previousDelivered;
            dbWaitingForCar1=carBusy||(robotCar1!=null&&!robotCar1.IsReadyForNextObject);
            if (!previousDelivered) dbPipelineStage="Waiting — delivery not confirmed";
            else if (carBusy)       dbPipelineStage="Waiting — Car1 not returned";
            else                    dbPipelineStage="Waiting — Car1 readiness";

            yield return new WaitUntil(()=>
                previousDelivered&&!carBusy&&!dispatchPaused&&
                robotCar1!=null&&robotCar1.IsReadyForNextObject);

            dbWaitingForDelivery=false; dbWaitingForCar1=false;
            if (postDispatchDelay>0f) yield return new WaitForSeconds(postDispatchDelay);
            if (pendingQueue.Count==0) break;

            int idx=pendingQueue.Dequeue(); dbRemainingInA=pendingQueue.Count;
            GameObject obj=spawnedObjects[idx];
            if (obj==null) continue;

            previousDelivered=false; carBusy=true;
            dbCurrentObject=obj.name; dbPipelineStage="Car1 → Arm1 staging";
            SetOutput(out_ObjectReady,true);

            // BUG FIX: Restore() removed from dispatch time.
            // It was wiping the processed mesh/material on the previous batch's object
            // the moment the next dispatch started. Restore() only belongs in
            // RestartBatch() when objects are physically recycled back to WH-A.
            arm1?.SetCubeReference(obj.transform);
            cycleStartTime=Time.time;

            Debug.Log($"[WH] ▶ Dispatching '{obj.name}' (slot {idx}). Gate LOCKED.");
            if (robotCar1==null){Debug.LogError("[WH] robotCar1 null!");carBusy=false;previousDelivered=true;SetOutput(out_ObjectReady,false);continue;}
            try { robotCar1.LoadObject(obj.transform); }
            catch (System.Exception ex){Debug.LogError($"[WH] LoadObject threw: {ex.Message}");carBusy=false;previousDelivered=true;SetOutput(out_ObjectReady,false);}
        }
        dbStatus="All dispatched — waiting final WH-B delivery"; dbCurrentObject="—";
        Debug.Log("[WH] All objects dispatched."); dispatchCoroutine=null;
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────
    public void NotifyCarFree()
    {
        carBusy=false; dbWaitingForCar1=false;
        dbPipelineStage="Car1 returned — waiting WH-B delivery";
        SetOutput(out_ObjectReady,false);
        Debug.Log("[WH] Car1 returned. Gate 2 (Car1) open.");
    }

    public void NotifyDelivered(Transform deliveredObject)
    {
        if (deliveredObject==null)
        {
            Debug.LogWarning("[WH] NotifyDelivered: null!");
            previousDelivered=true; dbWaitingForDelivery=false; return;
        }

        dbDeliveredToB++; dbRemainingInA=Mathf.Max(0,dbTotalObjects-dbDeliveredToB);
        dbPipelineStage=$"Delivered {dbDeliveredToB}/{dbTotalObjects} — gate open";

        // Cycle time telemetry
        float cycleTime=Time.time-cycleStartTime;
        cycleTotalTime+=cycleTime; cycleSamples++;
        tel_AvgCycleTime=cycleSamples>0?cycleTotalTime/cycleSamples:0f;
        deliveryTimestamps.Enqueue(Time.time);
        totalAllBatchDelivered++;

        int instanceID=deliveredObject.GetInstanceID(); // deliveredObject is a Transform — .GetInstanceID() matches CubeProcessor's transform.GetInstanceID()
        bool isDefective=false; ProcessingDefectReport report=null;
        if (pendingQCReports.TryGetValue(instanceID,out report))
        { isDefective=report.IsDefective; pendingQCReports.Remove(instanceID); }
        else Debug.LogWarning($"[WH] No QC report for '{deliveredObject.name}' — treated as perfect.");

        if (isDefective)
        {
            dbBatchDefectCount++; totalAllBatchDefective++;
            tel_TotalDefective++;
            dbLastDeliveredVerdict=$"DEFECTIVE [{report?.Defects}]";
            dbLastDeliveredShape=report?.SelectedShapeName??"—";
            if(!batchHasDefects){batchHasDefects=true;SetOutput(out_BatchHasDefects,true);}
            IO_Router.Instance?.SetValueWithHandoff(out_LastPartPerfect,out_LastPartDefective);
            Debug.LogWarning($"[WH] ⚠ DEFECTIVE '{deliveredObject.name}' delivered. Batch defects: {dbBatchDefectCount}/{dbDeliveredToB}.");
        }
        else
        {
            dbBatchPerfectCount++; tel_TotalPerfect++;
            dbLastDeliveredVerdict="PERFECT"; dbLastDeliveredShape=report?.SelectedShapeName??"—";
            IO_Router.Instance?.SetValueWithHandoff(out_LastPartDefective,out_LastPartPerfect);
            Debug.Log($"[WH] ✔ Perfect '{deliveredObject.name}'. Batch perfect: {dbBatchPerfectCount}/{dbDeliveredToB}.");
        }

        previousDelivered=true; dbWaitingForDelivery=false;

        if (isDefective&&warehouseRejectSlots.Count>0)
        {
            if (nextRejectSlot<warehouseRejectSlots.Count&&warehouseRejectSlots[nextRejectSlot]!=null)
            {
                Transform rs=warehouseRejectSlots[nextRejectSlot];
                var rb=deliveredObject.GetComponent<Rigidbody>();
                if(rb!=null){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.isKinematic=true;}
                deliveredObject.SetParent(null); deliveredObject.position=rs.position+rejectOffset; deliveredObject.rotation=rs.rotation; deliveredObject.gameObject.SetActive(true);
                conveyor1?.UnregisterObject(deliveredObject); conveyor2?.UnregisterObject(deliveredObject);
                Debug.LogWarning($"[WH] '{deliveredObject.name}' → REJECT slot {nextRejectSlot}.");
                nextRejectSlot++;
            }
            else { Debug.LogWarning("[WH] No reject slot available — sending to normal WH-B."); PlaceInNormalBSlot(deliveredObject); }
        }
        else PlaceInNormalBSlot(deliveredObject);

        if (dbDeliveredToB>=dbTotalObjects) OnBatchComplete();
    }

    void PlaceInNormalBSlot(Transform obj)
    {
        if (nextBSlot<warehouseBSlots.Count&&warehouseBSlots[nextBSlot]!=null)
        {
            Transform slot=warehouseBSlots[nextBSlot];
            var rb=obj.GetComponent<Rigidbody>();
            if(rb!=null){rb.linearVelocity=Vector3.zero;rb.angularVelocity=Vector3.zero;rb.isKinematic=true;}
            obj.SetParent(null); obj.position=slot.position+deliveryOffset; obj.rotation=slot.rotation; obj.gameObject.SetActive(true);
            conveyor1?.UnregisterObject(obj); conveyor2?.UnregisterObject(obj);
            Debug.Log($"[WH] '{obj.name}' → WH-B slot {nextBSlot}. ({dbDeliveredToB}/{dbTotalObjects})");
            nextBSlot++;
        }
        else Debug.LogWarning($"[WH] No WH-B slot (slot={nextBSlot}/{warehouseBSlots.Count}).");
    }

    void OnBatchComplete()
    {
        dbStatus="BATCH COMPLETE"; dbCurrentObject="—"; dbPipelineStage="Done — send in_StartBatch to restart"; dbFeedbackPhase="BatchComplete";
        tel_BatchProgressPct=100f; tel_EstTimeRemaining=0f;
        tel_BatchesCompleted++;

        string qcSummary=batchHasDefects
            ?$"⚠ BATCH HAD DEFECTS: {dbBatchDefectCount} defective / {dbBatchPerfectCount} perfect"
            :$"✔ BATCH CLEAN: all {dbBatchPerfectCount} part(s) perfect";

        Debug.Log("╔══════════════════════════════════════════════════════╗");
        Debug.Log($"║  BATCH COMPLETE — QC: {qcSummary,-35}║");
        Debug.Log("║  Send in_StartBatch rising edge to restart.         ║");
        Debug.Log("╚══════════════════════════════════════════════════════╝");

        IO_Router.Instance?.SetValueWithHandoff(out_InProgress,out_AllDone);
        SetOutput(out_BatchQCSummary,true);
        StartCoroutine(PulseAllDone());
        StartCoroutine(PulseBatchQCSummary());

        dispatchCoroutine=null; batchStarted=false; batchComplete=true;

        if ((offlineMode||offlineAutoStart)&&offlineAutoRestartDelay>0f)
            StartCoroutine(OfflineAutoRestart());
    }

    IEnumerator OfflineAutoRestart() { Debug.Log($"[WH] Auto-restart in {offlineAutoRestartDelay:F1}s..."); yield return new WaitForSeconds(offlineAutoRestartDelay); RestartBatch(); }
    IEnumerator PulseAllDone()       { yield return new WaitForSeconds(0.5f); SetOutput(out_AllDone,false); }
    IEnumerator PulseBatchQCSummary(){ yield return new WaitForSeconds(0.5f); SetOutput(out_BatchQCSummary,false); }

    void HandleQCReport(ProcessingDefectReport report)
    {
        pendingQCReports[report.ObjectInstanceID]=report;
        string verdict=report.IsDefective?$"⚠ DEFECTIVE [{report.Defects}]":"✔ PERFECT";
        Debug.Log($"[WH] QC report stored for '{report.ObjectName}' (ID:{report.ObjectInstanceID}) — {verdict}");
    }

    public string GetBatchQCSummary()
    {
        int total=dbBatchDefectCount+dbBatchPerfectCount;
        float rate=total>0?dbBatchDefectCount*100f/total:0f;
        return $"[WH QC BATCH SUMMARY]\n  Perfect  : {dbBatchPerfectCount}\n  Defective: {dbBatchDefectCount}\n  Total    : {total}\n  Defect % : {rate:F1}%\n  Last part: {dbLastDeliveredVerdict} ({dbLastDeliveredShape})";
    }

    public void ApplyExternalWarehouseSnapshot(int remainingInA, int deliveredToB, string batchStatus)
    {
        if (warehouseControlMode == WarehouseControlMode.EventDrivenPayloadFlow)
        {
            if (!warnedEventDrivenSnapshotIgnored)
            {
                Debug.LogWarning("[WH] Ignored direct warehouse remainingInA/remainingInB snapshot because WarehouseControlMode is EventDrivenPayloadFlow.");
                warnedEventDrivenSnapshotIgnored = true;
            }

            return;
        }

        if (remainingInA > 25)
        {
            Debug.LogWarning($"[WH] Ignored MQTT warehouse snapshot because remainingInA={remainingInA} exceeds 25.");
            return;
        }

        if (deliveredToB > 25)
        {
            Debug.LogWarning($"[WH] Ignored MQTT warehouse snapshot because remainingInB/deliveredToB={deliveredToB} exceeds 25.");
            return;
        }

        hasReceivedExternalWarehouseControl = true;
        int requestedA = Mathf.Max(0, remainingInA);
        int requestedB = Mathf.Max(0, deliveredToB);
        int availableASlots = CountValidSlots(warehouseASlots);
        int availableBSlots = CountValidSlots(warehouseBSlots);
        int placedA = Mathf.Min(requestedA, availableASlots);
        int placedB = Mathf.Min(requestedB, availableBSlots);
        int requiredObjects = placedA;

        if (requestedA > availableASlots)
            Debug.LogWarning($"[WH] MQTT snapshot requested {requestedA} WH-A objects, but only {availableASlots} valid WH-A slot(s) exist.");
        if (requestedB > availableBSlots)
            Debug.LogWarning($"[WH] MQTT snapshot requested {requestedB} WH-B objects, but only {availableBSlots} valid WH-B slot(s) exist.");

        if (useSeparateMqttShelfVisuals)
            EnsureMqttWarehouseAObjectPool(requiredObjects);
        else
            EnsureWarehouseObjectPool(requiredObjects);
        EnsureDeliveredOutputPool(placedB);

        int availableObjects = useSeparateMqttShelfVisuals
            ? mqttWarehouseAObjects.FindAll(o => o != null).Count
            : spawnedObjects.FindAll(o => o != null).Count;
        if (availableObjects < requiredObjects)
            Debug.LogWarning($"[WH] MQTT snapshot needs {requiredObjects} visible object(s), but only {availableObjects} object(s) are available.");
        int availableOutputs = deliveredOutputObjects.FindAll(o => o != null).Count;
        if (availableOutputs < placedB)
            Debug.LogWarning($"[WH] MQTT snapshot needs {placedB} delivered output object(s), but only {availableOutputs} turbine object(s) are available.");

        if (!useSeparateMqttShelfVisuals && dispatchCoroutine != null)
        {
            StopCoroutine(dispatchCoroutine);
            dispatchCoroutine = null;
        }

        if (!useSeparateMqttShelfVisuals)
            pendingQueue.Clear();
        int objectIndex = 0;
        int actualPlacedA = 0;
        int actualPlacedB = 0;

        for (int slotIndex = 0; slotIndex < warehouseASlots.Count && actualPlacedA < placedA; slotIndex++)
        {
            Transform slot = warehouseASlots[slotIndex];
            if (slot == null) continue;

            GameObject obj = useSeparateMqttShelfVisuals
                ? GetMqttWarehouseAObject(objectIndex)
                : GetWarehouseObject(objectIndex);
            if (obj == null) break;

            if (useSeparateMqttShelfVisuals)
            {
                PlaceMqttShelfVisualOnSlot(obj, slot, mqttRawBoxVisualOffset, mqttRawBoxVisualEuler, mqttRawBoxVisualScale);
            }
            else
            {
                PlaceObjectOnSlot(obj, slot, spawnOffset);
            }

            if (!useSeparateMqttShelfVisuals)
                pendingQueue.Enqueue(objectIndex);
            objectIndex++;
            actualPlacedA++;
        }

        int outputIndex = 0;
        for (int slotIndex = 0; slotIndex < warehouseBSlots.Count && actualPlacedB < placedB; slotIndex++)
        {
            Transform slot = warehouseBSlots[slotIndex];
            if (slot == null) continue;

            GameObject obj = GetDeliveredOutputObject(outputIndex);
            if (obj == null) break;

            PlaceMqttShelfVisualOnSlot(obj, slot, mqttDeliveredOutputVisualOffset, GetDeliveredOutputVisualEuler(), GetDeliveredOutputVisualScale());
            outputIndex++;
            actualPlacedB++;
        }

        List<GameObject> rawShelfPool = useSeparateMqttShelfVisuals ? mqttWarehouseAObjects : spawnedObjects;
        for (int i = objectIndex; i < rawShelfPool.Count; i++)
        {
            if (rawShelfPool[i] != null)
                HideWarehouseObject(rawShelfPool[i]);
        }

        for (int i = outputIndex; i < deliveredOutputObjects.Count; i++)
        {
            if (deliveredOutputObjects[i] != null)
                HideWarehouseObject(deliveredOutputObjects[i]);
        }

        dbTotalObjects = Mathf.Max(dbTotalObjects, requestedA + requestedB);
        dbRemainingInA = actualPlacedA;
        dbDeliveredToB = actualPlacedB;
        dbStatus = string.IsNullOrEmpty(batchStatus) ? "External warehouse sync" : batchStatus;
        dbPipelineStage = $"External sync: WH-A={actualPlacedA}, WH-B={actualPlacedB}";
        dbCurrentObject = "—";
        dbFeedbackPhase = "ExternalSync";
        if (!useSeparateMqttShelfVisuals)
        {
            dbWaitingForDelivery = false;
            dbWaitingForCar1 = false;
            carBusy = false;
            previousDelivered = true;
            nextBSlot = actualPlacedB;
            nextRejectSlot = 0;
            batchComplete = actualPlacedA == 0 && actualPlacedB > 0;
            batchStarted = false;
        }

        tel_WHAOccupied = actualPlacedA;
        tel_WHBOccupied = actualPlacedB;
        tel_RejectOccupied = 0;
        tel_BatchProgressPct = requestedA + requestedB > 0
            ? Mathf.Clamp01((float)actualPlacedB / (requestedA + requestedB)) * 100f
            : 0f;
        tel_EstTimeRemaining = 0f;
        tel_GateDelivery = true;
        tel_GateCar1 = true;
        tel_GateDispatch = !dispatchPaused && !eStopActive;

        if (logExternalWarehouseSync)
            Debug.Log($"[WH] MQTT warehouse sync applied. WH-A={actualPlacedA}/{requestedA}, WH-B={actualPlacedB}/{requestedB}, status='{dbStatus}'.");
    }

    int CountValidSlots(List<Transform> slots)
    {
        int count = 0;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null) count++;
        return count;
    }

    void EnsureWarehouseObjectPool(int requiredObjects)
    {
        for (int i = 0; i < requiredObjects; i++)
        {
            if (i < spawnedObjects.Count && spawnedObjects[i] != null)
                continue;

            GameObject obj = null;
            if (i < sceneObjects.Count && sceneObjects[i] != null)
            {
                obj = sceneObjects[i];
            }
            else if (objectPrefab != null)
            {
                obj = Instantiate(objectPrefab);
                obj.name = $"ProductObject_{i:D2}";
            }

            if (obj == null)
                continue;

            try { if (obj.CompareTag("Untagged")) obj.tag = "ProductObject"; } catch {}
            conveyor1?.RegisterObject(obj.transform);
            conveyor2?.RegisterObject(obj.transform);

            while (spawnedObjects.Count <= i)
                spawnedObjects.Add(null);

            spawnedObjects[i] = obj;
        }
    }

    void EnsureMqttWarehouseAObjectPool(int requiredObjects)
    {
        for (int i = 0; i < requiredObjects; i++)
        {
            if (i < mqttWarehouseAObjects.Count && mqttWarehouseAObjects[i] != null)
                continue;

            GameObject obj = CreatePassiveRawBoxVisual(i);
            if (obj == null)
                continue;

            while (mqttWarehouseAObjects.Count <= i)
                mqttWarehouseAObjects.Add(null);

            mqttWarehouseAObjects[i] = obj;
        }

        if (requiredObjects > 0 && mqttWarehouseAObjects.FindAll(o => o != null).Count < requiredObjects)
            Debug.LogWarning("[WH] MQTT warehouse A snapshot needs raw object visuals, but passive visual creation failed.");
    }

    GameObject CreatePassiveRawBoxVisual(int index)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = $"MqttWarehouseABox_{index:D2}";
        obj.transform.localScale = mqttRawBoxVisualScale;
        StripShelfVisualRuntimeComponents(obj);
        return obj;
    }

    void EnsureDeliveredOutputPool(int requiredOutputs)
    {
        for (int i = 0; i < requiredOutputs; i++)
        {
            if (i < deliveredOutputObjects.Count && deliveredOutputObjects[i] != null)
                continue;

            GameObject obj = null;
            if (i < deliveredOutputSceneObjects.Count && deliveredOutputSceneObjects[i] != null)
            {
                obj = deliveredOutputSceneObjects[i];
            }
            GameObject outputPrefab = GetDeliveredOutputPrefab();
            if (obj == null && outputPrefab != null)
            {
                obj = CreateDeliveredOutputVisual(outputPrefab, i);
            }

            if (obj == null)
                continue;

            while (deliveredOutputObjects.Count <= i)
                deliveredOutputObjects.Add(null);

            deliveredOutputObjects[i] = obj;
        }

        if (requiredOutputs > 0 &&
            deliveredOutputObjects.FindAll(o => o != null).Count < requiredOutputs &&
            deliveredOutputPrefab == null)
        {
            Debug.LogWarning("[WH] deliveredOutputPrefab is not assigned. Assign Assets/Turbine-Model/Turbine.fbx to show delivered turbines on Warehouse B.");
        }
    }

    GameObject GetDeliveredOutputPrefab()
    {
        if (deliveredOutputPrefab != null)
            return deliveredOutputPrefab;

#if UNITY_EDITOR
        deliveredOutputPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Turbine-Model/Turbine.fbx");
        return deliveredOutputPrefab;
#else
        return null;
#endif
    }

    GameObject CreateDeliveredOutputVisual(GameObject sourcePrefab, int index)
    {
        GameObject root = new GameObject($"DeliveredOutput_Turbine_{index:D2}");
        GameObject model = Instantiate(sourcePrefab, root.transform);
        model.name = "TurbineVisual";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        StripShelfVisualRuntimeComponents(model);

        if (centerDeliveredOutputPivot)
            CenterChildRenderersOnParent(root.transform, model.transform);

        return root;
    }

    Vector3 GetDeliveredOutputVisualScale()
    {
        GameObject example = GetDeliveredOutputExampleObject();
        if (matchDeliveredOutputExampleScale && example != null)
            return example.transform.localScale;

        return mqttDeliveredOutputVisualScale;
    }

    Vector3 GetDeliveredOutputVisualEuler()
    {
        GameObject example = GetDeliveredOutputExampleObject();
        if (matchDeliveredOutputExampleRotation && example != null)
            return example.transform.localEulerAngles;

        return mqttDeliveredOutputVisualEuler;
    }

    GameObject GetDeliveredOutputExampleObject()
    {
        if (deliveredOutputExampleObject != null)
            return deliveredOutputExampleObject;

        if (string.IsNullOrEmpty(deliveredOutputExampleName))
            return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == deliveredOutputExampleName)
            {
                deliveredOutputExampleObject = transforms[i].gameObject;
                return deliveredOutputExampleObject;
            }
        }

        return null;
    }

    void CenterChildRenderersOnParent(Transform parent, Transform child)
    {
        if (parent == null || child == null)
            return;

        Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 localCenter = parent.InverseTransformPoint(bounds.center);
        child.localPosition -= localCenter;
    }

    GameObject GetWarehouseObject(int index)
    {
        if (index < 0 || index >= spawnedObjects.Count)
            return null;

        return spawnedObjects[index];
    }

    GameObject GetMqttWarehouseAObject(int index)
    {
        if (index < 0 || index >= mqttWarehouseAObjects.Count)
            return null;

        return mqttWarehouseAObjects[index];
    }

    GameObject GetDeliveredOutputObject(int index)
    {
        if (index < 0 || index >= deliveredOutputObjects.Count)
            return null;

        return deliveredOutputObjects[index];
    }

    void PlaceObjectOnSlot(GameObject obj, Transform slot, Vector3 offset)
    {
        if (obj == null || slot == null)
            return;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        obj.transform.SetParent(null);
        obj.transform.position = slot.position + offset;
        obj.transform.rotation = slot.rotation;
        obj.SetActive(true);
        conveyor1?.UnregisterObject(obj.transform);
        conveyor2?.UnregisterObject(obj.transform);
    }

    void PlaceMqttShelfVisualOnSlot(GameObject obj, Transform slot, Vector3 localOffset, Vector3 localEuler, Vector3 localScale)
    {
        if (obj == null || slot == null)
            return;

        StripShelfVisualRuntimeComponents(obj);

        if (parentMqttShelfVisualsToSlots)
        {
            obj.transform.SetParent(slot, false);
            obj.transform.localPosition = localOffset;
            obj.transform.localRotation = Quaternion.Euler(localEuler);
            obj.transform.localScale = localScale;
        }
        else
        {
            obj.transform.SetParent(null);
            obj.transform.position = slot.position + slot.TransformVector(localOffset);
            obj.transform.rotation = slot.rotation * Quaternion.Euler(localEuler);
            obj.transform.localScale = localScale;
        }

        obj.SetActive(true);
        conveyor1?.UnregisterObject(obj.transform);
        conveyor2?.UnregisterObject(obj.transform);
    }

    void StripShelfVisualRuntimeComponents(GameObject visual)
    {
        if (visual == null)
            return;

        Rigidbody[] rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            Destroy(rigidbodies[i]);

        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);

        MonoBehaviour[] behaviours = visual.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            Destroy(behaviours[i]);
    }

    void HideWarehouseObject(GameObject obj)
    {
        if (obj == null)
            return;

        conveyor1?.UnregisterObject(obj.transform);
        conveyor2?.UnregisterObject(obj.transform);
        obj.transform.SetParent(null);
        obj.SetActive(false);
    }

    public void SetPipelineStage(string stage) => dbPipelineStage=stage;

    [ContextMenu("Run System Diagnostics")]
    public void RunSystemDiagnostics()
    {
        int warn=0,fail=0;
        void Ok  (string m)=>Debug.Log    ($"[WH-DIAG]  OK    {m}");
        void Warn(string m){Debug.LogWarning($"[WH-DIAG]  WARN  {m}");warn++;}
        void Fail(string m){Debug.LogError  ($"[WH-DIAG]  FAIL  {m}");fail++;}

        Debug.Log("╔══════════════ WAREHOUSE SYSTEM DIAGNOSTICS ══════════════╗");
        if (IO_Router.Instance==null) Fail("IO_Router.Instance is null."); else Ok($"IO_Router found — mode={(IO_Router.Instance.offlineMode?"Offline":"PLC")}");
        if (!offlineMode&&!offlineAutoStart&&IO_Router.Instance!=null&&IO_Router.Instance.offlineMode) Warn("WarehouseManager is in PLC mode but IO_Router.offlineMode=true.");
        if (robotCar1==null) Fail("robotCar1 not assigned."); else Ok("robotCar1 assigned.");
        if (sensor1==null)   Warn("sensor1 not assigned.");   else Ok("sensor1 assigned.");
        if (sensor2==null)   Warn("sensor2 not assigned.");   else Ok("sensor2 assigned.");
        if (arm1==null)      Fail("arm1 not assigned.");      else Ok("arm1 assigned.");
        if (conveyor1==null) Warn("conveyor1 not assigned."); else Ok("conveyor1 assigned.");
        if (conveyor2==null) Warn("conveyor2 not assigned."); else Ok("conveyor2 assigned.");
        int validA=warehouseASlots.FindAll(s=>s!=null).Count;
        if (validA==0)Fail("No valid Warehouse A slots."); else Ok($"{validA} WH-A slots.");
        if (objectPrefab==null){int vs=sceneObjects.FindAll(o=>o!=null).Count;if(vs==0)Fail("No objectPrefab AND no sceneObjects.");else Ok($"Using {vs} sceneObject(s).");}else Ok("objectPrefab assigned.");
        int validB=warehouseBSlots.FindAll(s=>s!=null).Count;
        if (validB<validA)Warn($"Only {validB} WH-B slot(s) for {validA} object(s)."); else Ok($"{validB} WH-B slots.");
        if (deliveredOutputPrefab==null&&deliveredOutputSceneObjects.FindAll(o=>o!=null).Count==0) Warn("No deliveredOutputPrefab or deliveredOutputSceneObjects assigned. MQTT deliveredToB cannot show turbines.");
        else Ok("Delivered output turbine source assigned.");
        int validR=warehouseRejectSlots.FindAll(s=>s!=null).Count;
        if (validR==0)Warn("No reject slots — defective parts go to normal WH-B slots."); else Ok($"{validR} reject slot(s).");
        Debug.Log($"╚═══ {warn} warning(s), {fail} failure(s). {(fail==0?(warn==0?"All clear.":"Check warnings."):"FIX FAILURES.")} ═══╝");
    }

    [Tooltip("Seconds of no pipeline progress before stuck-watchdog warns. 0 = disabled.")]
    public float stuckWatchdogSeconds = 45f;

    IEnumerator StuckWatchdog()
    {
        if (stuckWatchdogSeconds<=0f) yield break;
        string lastStage=dbPipelineStage, lastObj=dbCurrentObject; float idle=0f;
        while (batchStarted)
        {
            yield return new WaitForSeconds(1f);
            if (dbPipelineStage==lastStage&&dbCurrentObject==lastObj){
                idle+=1f;
                if(idle>=stuckWatchdogSeconds){
                    Debug.LogWarning($"[WH-WATCHDOG] Pipeline unchanged for {idle:F0}s — possibly stuck.\n  Stage:{dbPipelineStage}\n  Object:{dbCurrentObject}\n  carBusy={carBusy} prevDelivered={previousDelivered} paused={dispatchPaused}");
                    TagSubscriptionHelper.DiagnoseAll(); idle=0f;
                }
            } else { idle=0f; lastStage=dbPipelineStage; lastObj=dbCurrentObject; }
        }
    }

    void SetOutput(string tag, bool v)
    { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }

#if UNITY_EDITOR
    [ContextMenu("Add Warehouse A Slot")]   void AddSlotA()      { warehouseASlots.Add(null); }
    [ContextMenu("Add Warehouse B Slot")]   void AddSlotB()      { warehouseBSlots.Add(null); }
    [ContextMenu("Add Reject Slot")]        void AddRejectSlot() { warehouseRejectSlots.Add(null); }
    [ContextMenu("Start Batch Manually")]   void ManualStart()   { if(Application.isPlaying)StartBatch(); }
    [ContextMenu("Restart Batch Manually")] void ManualRestart() { if(Application.isPlaying)RestartBatch(); }
    [ContextMenu("Log All IO_Router Tags")] void DumpTags()      { IO_Router.Instance?.LogAllTags(); }
    [ContextMenu("Print Batch QC Summary")] void DumpQC()        { Debug.Log(GetBatchQCSummary()); }
#endif
}
