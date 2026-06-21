using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// RobotArmController — three roles in one script.
/// (Original functionality preserved. Telemetry section added at bottom of Inspector.)
/// </summary>
[ExecuteAlways]
public class RobotArmController : MonoBehaviour
{
    public enum ArmRole
    {
        Arm1_Car1ToConveyor1,
        Arm2_Conv1CNCConv2,
        Arm3_Conv2ToCar2,
    }

    [Header("══ Arm Role ══════════════════════════════════════════════════")]
    public ArmRole role = ArmRole.Arm1_Car1ToConveyor1;

    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    public bool offlineMode = true;

    [Header("── Joints — drag each bone Transform here ───────────────────")]
    public Transform joint1, joint2, joint3, joint4, joint5, joint6;

    [Header("── Gripper tip (empty child at fingertip) ────────────────────")]
    public Transform gripPoint;

    [Header("── Object — set via SetCubeReference() before trigger ────────")]
    [Tooltip("The object this arm will handle in its next sequence.")]
    public Transform cube;

    [Header("── Conveyors ────────────────────────────────────────────────")]
    public ConveyorMotor sourceConveyorMotor;
    public ConveyorMotor destConveyorMotor;

    [Header("── CNC  (Arm2 only) ──────────────────────────────────────────")]
    [Range(0f, 120f)]
    public float cncProcessTime = 4f;
    public Transform cncDropPoint;

    [Tooltip("(Arm2 only) Drag the desired CNC output shape prefab/GameObject here.\n" +
             "Injected into the product CubeProcessor right before Process() is called.\n" +
             "Keep this on Arm2 (a fixed scene object) so it is never lost when the\n" +
             "product is instantiated or recycled.\n" +
             "Leave empty to use whatever is already set on the product itself.")]
    public GameObject cncOutputShapePrefab = null;

    [Tooltip("(Arm2 only) Quality mode injected into the product CubeProcessor before processing.")]
    public CubeProcessor.QualityMode cncQualityMode = CubeProcessor.QualityMode.Perfect;

    [Header("── Arm1 Staging ─────────────────────────────────────────────")]
    public Transform car1GrabPoint;
    public Transform conv1DropPoint;
    public ConveyorMotor conveyor1Motor;

    [Header("── Arm2 Chaining ─────────────────────────────────────────────")]
    [Tooltip("Arm3 — receives cube reference after Arm2 places on Conv2")]
    public RobotArmController arm3;
    [Tooltip("SensorTrigger2 — receives expected object after Arm2 places on Conv2")]
    public SensorTrigger sensor2;

    [Header("── Arm3 / Car2 ──────────────────────────────────────────────")]
    public RobotCar2   robotCar2;
    public Transform   car2DropPoint;

    public enum RotAxis { X, Y, Z }
    [Header("── Joint Rotation Axes ────────────────────────────────────────")]
    public RotAxis j1Axis=RotAxis.Y, j2Axis=RotAxis.Z, j3Axis=RotAxis.Z;
    public RotAxis j4Axis=RotAxis.X, j5Axis=RotAxis.Z, j6Axis=RotAxis.X;

    [Header("── Speed & Timing ────────────────────────────────────────────")]
    [Range(5f,  360f)] public float jointSpeed        = 60f;
    [Range(0f,  1f)]   public float smoothing         = 0.85f;
    [Range(0f,  2f)]   public float pauseBetweenMoves = 0.2f;
    [Range(0f,  3f)]   public float gripDelay         = 0.35f;

    [Header("── Safety Watchdog ──────────────────────────────────────────")]
    [Range(0f, 300f)] public float sequenceTimeoutSeconds = 60f;

    [System.Serializable]
    public class RobotPose
    {
        [HideInInspector] public string name = "";
        [Range(-180f,180f)] public float j1,j2,j3,j4,j5,j6;
        public bool savePoseOnCapture = false;
        public float[] ToArr()          => new[]{j1,j2,j3,j4,j5,j6};
        public void  FromArr(float[] a) { if(a.Length<6)return; j1=a[0];j2=a[1];j3=a[2];j4=a[3];j5=a[4];j6=a[5]; }
    }

    [System.Serializable]
    public class PoseFileData
    {
        public RobotPose poseHome      = new RobotPose{name="Home"};
        public RobotPose poseWaypointA = new RobotPose{name="WaypointA"};
        public RobotPose poseGrab      = new RobotPose{name="Grab"};
        public RobotPose poseWaypointB = new RobotPose{name="WaypointB"};
        public RobotPose poseConv1Drop = new RobotPose{name="Conv1Drop"};
        public RobotPose poseCNCPlace  = new RobotPose{name="CNCPlace"};
        public RobotPose poseWaypointC = new RobotPose{name="WaypointC"};
        public RobotPose poseConv2Drop = new RobotPose{name="Conv2Drop"};
        public RobotPose poseCar2Drop  = new RobotPose{name="Car2Drop"};
    }

    [Header("── Poses — capture via Inspector buttons during Play ─────────")]
    public RobotPose poseHome      = new RobotPose{name="Home"};
    public RobotPose poseWaypointA = new RobotPose{name="WaypointA"};
    public RobotPose poseGrab      = new RobotPose{name="Grab"};
    public RobotPose poseWaypointB = new RobotPose{name="WaypointB"};
    public RobotPose poseConv1Drop = new RobotPose{name="Conv1Drop"};
    public RobotPose poseCNCPlace  = new RobotPose{name="CNCPlace"};
    public RobotPose poseWaypointC = new RobotPose{name="WaypointC"};
    public RobotPose poseConv2Drop = new RobotPose{name="Conv2Drop"};
    public RobotPose poseCar2Drop  = new RobotPose{name="Car2Drop"};

    [HideInInspector] public bool _capHome=false,_capWpA=false,_capGrab=false,_capWpB=false;
    [HideInInspector] public bool _capConv1Drop=false,_capCNCPlace=false;
    [HideInInspector] public bool _capWpC=false,_capConv2Drop=false,_capCar2Drop=false;

    [Header("── Pose File Persistence (per-robot, conflict-free) ─────────")]
    public string poseFileName = "";
    public bool autoLoadPosesOnStart = false;

    [Header("══ PLC INPUT Tags (PLC → Unity) ════════════════════════════")]
    [Tooltip("Boolean TRUE: start one arm cycle.\n⚠ Case-sensitive.")]
    public string plcTriggerTag = "";
    [Tooltip("Alternate trigger (also responds to TRUE).\n⚠ Case-sensitive.")]
    public string plcRestartTag = "";
    [Tooltip("(Arm2 only) Boolean TRUE: CNC machine finished.\n⚠ Case-sensitive.")]
    public string in_CNCDoneTag = "";
    [Tooltip("Boolean TRUE: emergency stop active / FALSE: stop cleared.\n⚠ Case-sensitive.")]
    public string in_EStopTag   = "";

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════")]
    public string out_ArmBusy    = "";
    public string out_ArmAtGrab  = "";
    public string out_ArmGripped = "";
    public string out_ArmAtCNC   = "";
    public string out_CNCStart   = "";
    public string out_ArmAtDrop  = "";
    public string out_ArmDropped = "";
    public string out_ArmIdle    = "";
    public string out_ArmError   = "";
    public string out_ArmEStop   = "";

    [Header("══ QC Output Tags — Arm2 only (Unity → PLC) ══════════════════")]
    public string out_CNCPerfect = "";
    public string out_CNCDefect  = "";

    [Header("── Runtime State (Read Only) ──────────────────────────────")]
    [SerializeField] string dbStep      = "Idle";
    [SerializeField] bool   dbExecuting = false;
    [SerializeField] bool   dbHeld      = false;
    [SerializeField] string dbCubeName  = "—";
    [SerializeField] bool   dbEStop     = false;
    [SerializeField] string dbMode      = "Offline";
    [SerializeField] string dbFeedbackPhase = "Idle";
    [SerializeField] string dbLastQCVerdict = "—";
    [SerializeField] string dbLastQCShape   = "—";

    // ══════════════════════════════════════════════════════════════════════════
    // ══ TELEMETRY — Joint Angles (Live, Read Only) ═══════════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    [Header("══ TELEMETRY — Joint Angles (°) ════════════════════════════")]
    [Tooltip("Live angle of Joint 1 (degrees). Updated every frame during execution.")]
    [SerializeField] float tel_J1_Angle = 0f;
    [Tooltip("Live angle of Joint 2 (degrees).")]
    [SerializeField] float tel_J2_Angle = 0f;
    [Tooltip("Live angle of Joint 3 (degrees).")]
    [SerializeField] float tel_J3_Angle = 0f;
    [Tooltip("Live angle of Joint 4 (degrees).")]
    [SerializeField] float tel_J4_Angle = 0f;
    [Tooltip("Live angle of Joint 5 (degrees).")]
    [SerializeField] float tel_J5_Angle = 0f;
    [Tooltip("Live angle of Joint 6 (degrees).")]
    [SerializeField] float tel_J6_Angle = 0f;

    [Header("══ TELEMETRY — Joint Velocity (°/s) ═════════════════════════")]
    [Tooltip("Estimated angular velocity of Joint 1 (°/s). Derived each frame.")]
    [SerializeField] float tel_J1_Velocity = 0f;
    [SerializeField] float tel_J2_Velocity = 0f;
    [SerializeField] float tel_J3_Velocity = 0f;
    [SerializeField] float tel_J4_Velocity = 0f;
    [SerializeField] float tel_J5_Velocity = 0f;
    [SerializeField] float tel_J6_Velocity = 0f;

    [Header("══ TELEMETRY — Joint Temperature (°C) ═══════════════════════")]
    [Tooltip("Simulated motor temperature for Joint 1. Rises with angular velocity, cools at idle.")]
    [SerializeField] float tel_J1_Temp = 30f;
    [SerializeField] float tel_J2_Temp = 30f;
    [SerializeField] float tel_J3_Temp = 30f;
    [SerializeField] float tel_J4_Temp = 30f;
    [SerializeField] float tel_J5_Temp = 30f;
    [SerializeField] float tel_J6_Temp = 30f;

    [Header("══ TELEMETRY — Joint Torque / Load (Nm) ══════════════════════")]
    [Tooltip("Simulated servo torque. Spikes at grip/drop events.")]
    [SerializeField] float tel_J1_Torque = 0f;
    [SerializeField] float tel_J2_Torque = 0f;
    [SerializeField] float tel_J3_Torque = 0f;
    [SerializeField] float tel_J4_Torque = 0f;
    [SerializeField] float tel_J5_Torque = 0f;
    [SerializeField] float tel_J6_Torque = 0f;

    [Header("══ TELEMETRY — Arm Summary ══════════════════════════════════")]
    [Tooltip("Time elapsed since the current sequence started (seconds).")]
    [SerializeField] float   tel_SequenceElapsed  = 0f;
    [Tooltip("Number of completed pick-and-place cycles since scene start.")]
    [SerializeField] int     tel_CycleCount       = 0;
    [Tooltip("Average cycle time over last 5 completed cycles (seconds).")]
    [SerializeField] float   tel_AvgCycleTime     = 0f;
    [Tooltip("Gripper currently holding an object.")]
    [SerializeField] bool    tel_GripperClosed     = false;
    [Tooltip("Simulated gripper finger force (N). Non-zero only while holding.")]
    [SerializeField] float   tel_GripperForce      = 0f;
    [Tooltip("E-Stop events since scene start.")]
    [SerializeField] int     tel_EStopCount        = 0;
    [Tooltip("Timeout/abort events since scene start.")]
    [SerializeField] int     tel_ErrorCount        = 0;

    // ── Telemetry private state ───────────────────────────────────────────────
    float[] prevAngles      = new float[6];
    float   seqStartTime    = 0f;
    float   gripEventTimer  = 0f;
    bool    gripEventActive = false;
    readonly Queue<float> cycleTimes = new Queue<float>();

    // ─────────────────────────────────────────────────────────────────────────
    ProcessingDefectReport lastQCReport = null;

    bool isExecuting      = false;
    bool hasGripped       = false;
    bool coroutineRunning = false;
    bool eStopActive      = false;

    public bool IsExecuting => isExecuting;
    public bool HasGripped  => hasGripped;

    List<Transform> joints;
    List<RotAxis>   axes;
    ConveyorMotor   frozenBelt;
    Rigidbody       cubeRb;

    WaitForSeconds waitGripDelay;
    WaitForSeconds waitPause;
    WaitForSeconds waitPulse;
    static readonly WaitForSeconds waitHalfSec   = new WaitForSeconds(0.5f);
    static readonly WaitForSeconds waitShortPulse = new WaitForSeconds(0.15f);

    System.Action<bool> cbTrigger, cbRestart, cbCNCDone, cbEStop;
    bool cncDoneFlag = false;

    // ─────────────────────────────────────────────────────────────────────────
    void OnEnable()
    {
        BuildLists();
        CubeProcessor.OnProcessingComplete += HandleQCReport;
    }
    void OnValidate() => ApplyCaptures();

    void OnDisable()
    {
        CubeProcessor.OnProcessingComplete -= HandleQCReport;
    }

    void Start()
    {
        if (role == ArmRole.Arm3_Conv2ToCar2 && robotCar2 == null)
        {
            robotCar2 = FindObjectOfType<RobotCar2>();
            if (robotCar2 != null) Debug.Log($"[ARM:{role}] Auto-assigned robotCar2: '{robotCar2.name}'");
        }

        BuildLists();
        dbMode = offlineMode ? "Offline" : "PLC";

        waitGripDelay = new WaitForSeconds(gripDelay);
        waitPause     = new WaitForSeconds(pauseBetweenMoves);
        waitPulse     = new WaitForSeconds(0.5f);

        if (gripPoint == null)
            Debug.LogError($"[ARM:{role}] gripPoint not assigned!");

        if (autoLoadPosesOnStart) LoadPosesFromFile();
        if (cube != null) dbCubeName = cube.name;

        prevAngles = GetAngles();

        StartCoroutine(StartupSelfCheck());

        cbTrigger = v => { if (v && !offlineMode) NotifyRobotTrigger(); };
        cbRestart = v => { if (v && !offlineMode) NotifyRobotTrigger(); };
        cbCNCDone = v => { if (v) cncDoneFlag = true; };

        cbEStop = v =>
        {
            if (v && !eStopActive)
            {
                eStopActive = true;
                dbEStop     = true;
                tel_EStopCount++;
                SetOutput(out_ArmEStop, true);
                if (isExecuting)
                {
                    StopAllCoroutines();
                    coroutineRunning = false;
                    EmergencyRelease();
                    tel_ErrorCount++;
                    SetOutput(out_ArmError, true);
                    StartCoroutine(PulseError());
                }
                Debug.LogWarning($"[ARM:{role}] E-STOP received!");
            }
            else if (!v && eStopActive)
            {
                eStopActive = false;
                dbEStop     = false;
                SetOutput(out_ArmEStop, false);
                Debug.Log($"[ARM:{role}] E-STOP cleared.");
            }
        };

        if (!string.IsNullOrEmpty(plcTriggerTag))
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(plcTriggerTag, cbTrigger, this));

        if (!string.IsNullOrEmpty(plcRestartTag) && plcRestartTag != plcTriggerTag)
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(plcRestartTag, cbRestart, this));

        if (!string.IsNullOrEmpty(in_CNCDoneTag))
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_CNCDoneTag, cbCNCDone, this));

        if (!string.IsNullOrEmpty(in_EStopTag))
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(in_EStopTag, cbEStop, this));

        SetOutput(out_ArmIdle, true);
        dbFeedbackPhase = "Idle";

        Debug.Log($"[ARM:{role}] Started in {dbMode} mode.");
    }

    void OnDestroy()
    {
        IO_Router.Instance?.Unregister(plcTriggerTag, cbTrigger);
        IO_Router.Instance?.Unregister(plcRestartTag, cbRestart);
        IO_Router.Instance?.Unregister(in_CNCDoneTag, cbCNCDone);
        IO_Router.Instance?.Unregister(in_EStopTag,   cbEStop);
        TagSubscriptionHelper.Remove(plcTriggerTag);
        TagSubscriptionHelper.Remove(plcRestartTag);
        TagSubscriptionHelper.Remove(in_CNCDoneTag);
        TagSubscriptionHelper.Remove(in_EStopTag);
    }

    // ── Telemetry update ──────────────────────────────────────────────────────
    void Update()
    {
        if (!Application.isPlaying) return;

        float dt = Time.deltaTime;
        float[] angles = GetAngles();

        // Live joint angles
        if (angles.Length >= 6)
        {
            tel_J1_Angle = angles[0]; tel_J2_Angle = angles[1];
            tel_J3_Angle = angles[2]; tel_J4_Angle = angles[3];
            tel_J5_Angle = angles[4]; tel_J6_Angle = angles[5];

            // Angular velocity (°/s)
            tel_J1_Velocity = dt > 0 ? Mathf.Abs(Mathf.DeltaAngle(prevAngles[0], angles[0])) / dt : 0f;
            tel_J2_Velocity = dt > 0 ? Mathf.Abs(Mathf.DeltaAngle(prevAngles[1], angles[1])) / dt : 0f;
            tel_J3_Velocity = dt > 0 ? Mathf.Abs(Mathf.DeltaAngle(prevAngles[2], angles[2])) / dt : 0f;
            tel_J4_Velocity = dt > 0 ? Mathf.Abs(Mathf.DeltaAngle(prevAngles[3], angles[3])) / dt : 0f;
            tel_J5_Velocity = dt > 0 ? Mathf.Abs(Mathf.DeltaAngle(prevAngles[4], angles[4])) / dt : 0f;
            tel_J6_Velocity = dt > 0 ? Mathf.Abs(Mathf.DeltaAngle(prevAngles[5], angles[5])) / dt : 0f;

            System.Array.Copy(angles, prevAngles, 6);
        }

        // Temperature: rises proportional to velocity, cools toward 30°C at idle
        tel_J1_Temp = SimTemp(tel_J1_Temp, tel_J1_Velocity, dt);
        tel_J2_Temp = SimTemp(tel_J2_Temp, tel_J2_Velocity, dt);
        tel_J3_Temp = SimTemp(tel_J3_Temp, tel_J3_Velocity, dt);
        tel_J4_Temp = SimTemp(tel_J4_Temp, tel_J4_Velocity, dt);
        tel_J5_Temp = SimTemp(tel_J5_Temp, tel_J5_Velocity, dt);
        tel_J6_Temp = SimTemp(tel_J6_Temp, tel_J6_Velocity, dt);

        // Torque: proportional to velocity + spike while carrying
        float carryLoad = tel_GripperClosed ? 1.0f : 0f;
        tel_J1_Torque = SimTorque(tel_J1_Velocity, carryLoad);
        tel_J2_Torque = SimTorque(tel_J2_Velocity, carryLoad);
        tel_J3_Torque = SimTorque(tel_J3_Velocity, carryLoad * 1.2f);
        tel_J4_Torque = SimTorque(tel_J4_Velocity, carryLoad * 0.8f);
        tel_J5_Torque = SimTorque(tel_J5_Velocity, carryLoad * 0.6f);
        tel_J6_Torque = SimTorque(tel_J6_Velocity, carryLoad * 0.4f);

        // Gripper
        tel_GripperClosed = dbHeld;
        tel_GripperForce  = tel_GripperClosed
            ? Mathf.Lerp(tel_GripperForce, 35f + Mathf.Sin(Time.time * 3f) * 2f, dt * 5f)
            : Mathf.Lerp(tel_GripperForce, 0f, dt * 10f);

        // Grip-event torque spike
        if (gripEventActive)
        {
            gripEventTimer -= dt;
            float spike = Mathf.Clamp01(gripEventTimer / 0.3f) * 15f;
            tel_J4_Torque += spike;
            tel_J5_Torque += spike * 1.3f;
            tel_J6_Torque += spike * 1.5f;
            if (gripEventTimer <= 0f) gripEventActive = false;
        }

        // Sequence elapsed
        if (isExecuting)
            tel_SequenceElapsed = Time.time - seqStartTime;
    }

    // ── Simulated temperature model ───────────────────────────────────────────
    float SimTemp(float current, float velocity, float dt)
    {
        float target = 30f + (velocity / jointSpeed) * 55f;    // max ~85°C at full speed
        float rate   = velocity > 1f ? 8f : 3f;                // heats fast, cools slow
        return Mathf.MoveTowards(current, target, rate * dt);
    }

    // ── Simulated torque model ────────────────────────────────────────────────
    float SimTorque(float velocity, float loadBias)
    {
        return Mathf.Clamp((velocity / jointSpeed) * 30f + loadBias * 10f
               + Mathf.PerlinNoise(Time.time * 2f, velocity) * 2f, 0f, 50f);
    }

    IEnumerator RegisterTag(string tag, System.Action<bool> cb)
    {
        yield return StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(tag, cb, this));
    }

    IEnumerator PulseError()
    {
        yield return waitHalfSec;
        SetOutput(out_ArmError, false);
    }

    [ContextMenu("Diagnose Tag Subscriptions")]
    public void DiagnoseTagSubscriptions() => TagSubscriptionHelper.DiagnoseAll();

    IEnumerator StartupSelfCheck()
    {
        yield return null;

        int issues = 0;
        void Fail(string m) { Debug.LogError($"[ARM:{role}] ✖ {m}"); issues++; }
        void Warn(string m) { Debug.LogWarning($"[ARM:{role}] ⚠ {m}"); }
        void Ok  (string m) { Debug.Log($"[ARM:{role}] ✔ {m}"); }

        Debug.Log($"╔══ ARM SELF-CHECK [{role} / {gameObject.name}] ══╗");

        bool hasAnyJoint = joints != null && joints.Exists(j => j != null);
        if (!hasAnyJoint) Fail("No joints assigned.");
        else
        {
            int nullJ = joints.FindAll(j => j == null).Count;
            if (nullJ > 0) Warn($"{nullJ} joint slot(s) are null.");
            else Ok("All 6 joints assigned.");
        }

        if (gripPoint == null) Fail("gripPoint not assigned.");
        else Ok("gripPoint assigned.");

        bool IsZero(RobotPose p) => p.j1==0&&p.j2==0&&p.j3==0&&p.j4==0&&p.j5==0&&p.j6==0;
        if (IsZero(poseHome))      Fail("poseHome is all zeros — not captured.");
        if (IsZero(poseWaypointA)) Fail("poseWaypointA is all zeros — not captured.");
        if (IsZero(poseGrab))      Fail("poseGrab is all zeros — not captured.");
        if (issues == 0)           Ok("Core poses (Home, WaypointA, Grab) are non-zero.");

        if (!offlineMode)
        {
            if (string.IsNullOrEmpty(plcTriggerTag))
                Warn("offlineMode=false but plcTriggerTag is empty.");
            else Ok($"plcTriggerTag = '{plcTriggerTag}'");
        }

        switch (role)
        {
            case ArmRole.Arm1_Car1ToConveyor1:
                if (conveyor1Motor == null) Warn("conveyor1Motor not assigned."); break;
            case ArmRole.Arm2_Conv1CNCConv2:
                if (sourceConveyorMotor == null) Warn("sourceConveyorMotor (Conv1) not assigned.");
                if (destConveyorMotor   == null) Warn("destConveyorMotor (Conv2) not assigned."); break;
            case ArmRole.Arm3_Conv2ToCar2:
                if (sourceConveyorMotor == null) Warn("sourceConveyorMotor (Conv2) not assigned.");
                if (robotCar2           == null) Warn("robotCar2 not assigned."); break;
        }

        Debug.Log($"╚══ ARM SELF-CHECK DONE: {issues} failure(s). ══╝");
    }

    // ── Public API ────────────────────────────────────────────────────────────
    public void SetCubeReference(Transform newCube)
    {
        if (newCube == null) return;
        cube       = newCube;
        dbCubeName = newCube.name;
        Debug.Log($"[ARM:{role}] SetCubeReference → '{newCube.name}'");
    }

    public void NotifyRobotTrigger()
    {
        if (isExecuting)
        {
            Debug.LogWarning($"[ARM:{role}] Trigger ignored — arm already executing.");
            return;
        }
        if (eStopActive) return;

        bool hasJoints = joints != null && joints.Exists(j => j != null);
        if (!hasJoints) { Debug.LogError($"[ARM:{role}] Cannot run — no joints assigned!"); return; }
        if (AllPosesAreZero()) { Debug.LogError($"[ARM:{role}] Cannot run — all poses are zero."); return; }
        if (cube == null) { Debug.LogError($"[ARM:{role}] Cannot run — no cube assigned."); return; }

        StartCoroutine(RunSequence());
    }

    bool AllPosesAreZero()
    {
        bool IsZero(RobotPose p) => p.j1==0 && p.j2==0 && p.j3==0 && p.j4==0 && p.j5==0 && p.j6==0;
        return IsZero(poseHome) && IsZero(poseWaypointA) && IsZero(poseGrab);
    }

    IEnumerator RunSequence()
    {
        isExecuting      = true;
        dbExecuting      = true;
        hasGripped       = false;
        coroutineRunning = true;
        dbCubeName       = cube != null ? cube.name : "—";
        seqStartTime     = Time.time;
        tel_SequenceElapsed = 0f;

        Debug.Log($"[ARM:{role}] Sequence START — object: '{dbCubeName}'");

        IO_Router.Instance?.SetValueWithHandoff(out_ArmIdle, out_ArmBusy);
        dbFeedbackPhase = "Busy";

        switch (role)
        {
            case ArmRole.Arm1_Car1ToConveyor1: StartCoroutine(Arm1_Sequence()); break;
            case ArmRole.Arm2_Conv1CNCConv2:   StartCoroutine(Arm2_Sequence()); break;
            case ArmRole.Arm3_Conv2ToCar2:     StartCoroutine(Arm3_Sequence()); break;
        }

        float elapsed = 0f;
        while (coroutineRunning)
        {
            elapsed += Time.deltaTime;
            if (sequenceTimeoutSeconds > 0f && elapsed >= sequenceTimeoutSeconds)
            {
                Debug.LogError($"[ARM:{role}] ⚠ TIMEOUT after {elapsed:F1}s — aborting.");
                StopAllCoroutines();
                coroutineRunning = false;
                EmergencyRelease();
                tel_ErrorCount++;
                SetOutput(out_ArmError, true);
                yield return waitHalfSec;
                SetOutput(out_ArmError, false);
                break;
            }
            yield return null;
        }

        // Record cycle
        float cycleTime = Time.time - seqStartTime;
        cycleTimes.Enqueue(cycleTime);
        if (cycleTimes.Count > 5) cycleTimes.Dequeue();
        float sum = 0f;
        foreach (float t in cycleTimes) sum += t;
        tel_AvgCycleTime    = cycleTimes.Count > 0 ? sum / cycleTimes.Count : 0f;
        tel_CycleCount++;
        tel_SequenceElapsed = cycleTime;

        isExecuting     = false;
        dbExecuting     = false;
        dbFeedbackPhase = "Idle";
        SetStep("Idle");

        Debug.Log($"[ARM:{role}] Sequence COMPLETE in {cycleTime:F2}s.");
    }

    // ══ ARM 1 ════════════════════════════════════════════════════════════════
    IEnumerator Arm1_Sequence()
    {
        SetStep("Arm1 ▸ Home → WaypointA");
        yield return GoTo(poseWaypointA);

        SetStep("Arm1 ▸ WaypointA → Grab");
        SetOutput(out_ArmAtGrab, true);
        dbFeedbackPhase = "AtGrab";
        yield return GoTo(poseGrab);

        SetStep("Arm1 ▸ Gripping from Car1");
        if (cube == null) { Debug.LogError($"[ARM:{role}] Grip aborted — cube null!"); coroutineRunning = false; yield break; }

        cube.SetParent(null, worldPositionStays: true);
        if (car1GrabPoint != null) cube.position = car1GrabPoint.position;
        Grip(sourceBelt: null);
        sourceConveyorMotor?.SetSensorOverride(false);
        TriggerGripSpike();

        SetOutput(out_ArmAtGrab,  false);
        SetOutput(out_ArmGripped, true);
        dbFeedbackPhase = "Gripped";
        hasGripped = true;
        yield return waitGripDelay;
        SetOutput(out_ArmGripped, false);

        SetStep("Arm1 ▸ Grab → WaypointA → WaypointB");
        yield return GoTo(poseWaypointA);
        yield return GoTo(poseWaypointB);

        SetStep("Arm1 ▸ WaypointB → Conv1Drop");
        SetOutput(out_ArmAtDrop, true);
        dbFeedbackPhase = "AtDrop";
        yield return GoTo(poseConv1Drop);

        SetStep("Arm1 ▸ Releasing on Conveyor 1");
        if (conveyor1Motor == null) Debug.LogWarning($"[ARM:{role}] conveyor1Motor null.");
        if (conv1DropPoint != null && cube != null) cube.position = conv1DropPoint.position;
        conveyor1Motor?.SetObjectToMove(cube);
        ReleaseOntoBelt(conveyor1Motor);
        TriggerGripSpike();

        SetOutput(out_ArmAtDrop,  false);
        SetOutput(out_ArmDropped, true);
        dbFeedbackPhase = "Dropped";
        yield return waitGripDelay;
        SetOutput(out_ArmDropped, false);

        SetStep("Arm1 ▸ Conv1Drop → Home");
        yield return GoTo(poseHome);

        IO_Router.Instance?.SetValueWithHandoff(out_ArmBusy, out_ArmIdle);
        dbFeedbackPhase = "Idle";
        coroutineRunning = false;
    }

    // ══ ARM 2 ════════════════════════════════════════════════════════════════
    IEnumerator Arm2_Sequence()
    {
        SetStep("Arm2 ▸ Home → WaypointA");
        yield return GoTo(poseWaypointA);

        SetStep("Arm2 ▸ WaypointA → Grab (Conv1)");
        SetOutput(out_ArmAtGrab, true);
        dbFeedbackPhase = "AtGrab";
        yield return GoTo(poseGrab);

        SetStep("Arm2 ▸ Gripping from Conveyor 1");
        if (cube == null) { Debug.LogError($"[ARM:{role}] Grip aborted — cube null!"); coroutineRunning = false; yield break; }

        Grip(sourceBelt: sourceConveyorMotor);
        TriggerGripSpike();

        SetOutput(out_ArmAtGrab,  false);
        SetOutput(out_ArmGripped, true);
        dbFeedbackPhase = "Gripped";
        hasGripped = true;
        yield return waitGripDelay;
        SetOutput(out_ArmGripped, false);

        SetStep("Arm2 ▸ Grab → WaypointA → WaypointB");
        yield return GoTo(poseWaypointA);
        yield return GoTo(poseWaypointB);

        SetStep("Arm2 ▸ WaypointB → CNCPlace");
        SetOutput(out_ArmAtCNC, true);
        dbFeedbackPhase = "AtCNC";
        yield return GoTo(poseCNCPlace);

        SetStep("Arm2 ▸ Placing on CNC");
        if (cncDropPoint != null && cube != null) cube.position = cncDropPoint.position;

        CubeProcessor cp = cube != null ? cube.GetComponent<CubeProcessor>() : null;
        if (cp == null && cube != null) Debug.LogWarning($"[ARM:{role}] '{cube.name}' has no CubeProcessor.");

        Transform cncCube = cube;
        ReleaseToSurface();
        TriggerGripSpike();

        if (role == ArmRole.Arm2_Conv1CNCConv2)
        {
            SetOutput(out_CNCPerfect, false);
            SetOutput(out_CNCDefect,  false);
            lastQCReport    = null;
            dbLastQCVerdict = "Machining…";
            dbLastQCShape   = cncCube != null ? cncCube.name : "—";
        }

        if (cp != null)
        {
            // CORE FIX: inject shape and quality mode from Arm2's Inspector fields
            // into the product's CubeProcessor right before Process() is called.
            //
            // WHY: The product object (C1, C2…) is either Instantiated from a prefab
            // or recycled from the scene. In both cases its own Inspector fields
            // (processedShapePrefab, qualityMode) are either blank (on a fresh clone)
            // or whatever they were set to last run. The product is a moving object —
            // it is the wrong place to configure the CNC output shape.
            //
            // Arm2 is a fixed scene object. Its Inspector fields are always intact.
            // We copy them to the product here, immediately before Process(), so the
            // product always gets the shape you configured on Arm2 in the Inspector.
            // Use SetShapeOverride() instead of directly setting processedShapePrefab.
        // This preserves the Inspector setting on the product if Arm2's field is null.
        if (cncOutputShapePrefab != null)
        {
            cp.SetShapeOverride(cncOutputShapePrefab);
            Debug.Log($"[ARM:{role}] SetShapeOverride('{cncOutputShapePrefab.name}') on '{cncCube?.name}'.");
        }
        else
        {
            cp.ClearShapeOverride();
            Debug.Log($"[ARM:{role}] No Arm2 shape override — product will use its own Inspector setting or fallback.");
        }
        cp.qualityMode = cncQualityMode;

            cp.Process();
            Debug.Log($"[ARM:{role}] CubeProcessor.Process() called on '{cncCube?.name}' — shape='{(cp.processedShapePrefab != null ? cp.processedShapePrefab.name : "fallback:"+cp.fallbackShape)}' mode={cncQualityMode}");
        }
        yield return waitGripDelay;

        SetOutput(out_CNCStart, true);
        dbFeedbackPhase = "CNCRunning";

        SetStep("Arm2 ▸ CNCPlace → WaypointB (retract)");
        yield return GoTo(poseWaypointB);

        SetStep($"Arm2 ▸ Waiting CNC ({cncProcessTime:F1}s)");
        cncDoneFlag = false;

        if (!offlineMode && !string.IsNullOrEmpty(in_CNCDoneTag))
        {
            float t2 = 0f, limit = cncProcessTime + 60f;
            float warnAt = cncProcessTime + 10f; bool warned = false;
            yield return new WaitUntil(() =>
            {
                t2 += Time.deltaTime;
                if (!warned && t2 > warnAt) { warned = true; Debug.LogWarning($"[ARM:{role}] Waiting >{warnAt:F0}s for CNC done."); }
                return cncDoneFlag || t2 >= limit;
            });
        }
        else { yield return new WaitForSeconds(cncProcessTime); }

        SetOutput(out_CNCStart, false);
        cncDoneFlag = false;

        if (cube == null) cube = cncCube;

        SetStep("Arm2 ▸ WaypointB → CNCPlace (pick)");
        yield return GoTo(poseCNCPlace);

        SetStep("Arm2 ▸ Picking processed part");
        if (cube == null) { Debug.LogError($"[ARM:{role}] Cannot pick — cube lost!"); coroutineRunning = false; yield break; }

        Grip(sourceBelt: null);
        TriggerGripSpike();
        SetOutput(out_ArmGripped, true);
        yield return waitGripDelay;
        SetOutput(out_ArmGripped, false);

        SetStep("Arm2 ▸ CNCPlace → WaypointB");
        yield return GoTo(poseWaypointB);

        SetOutput(out_ArmAtCNC, false);
        dbFeedbackPhase = "TransitToConv2";

        SetStep("Arm2 ▸ WaypointB → WaypointC → Conv2Drop");
        yield return GoTo(poseWaypointC);

        SetOutput(out_ArmAtDrop, true);
        dbFeedbackPhase = "AtDrop";
        yield return GoTo(poseConv2Drop);

        SetStep("Arm2 ▸ Releasing on Conveyor 2");
        if (destConveyorMotor == null) Debug.LogWarning($"[ARM:{role}] destConveyorMotor null.");

        Transform cubeRef = cube;
        arm3?.SetCubeReference(cubeRef);
        sensor2?.SetExpectedObject(cubeRef?.gameObject);
        destConveyorMotor?.SetObjectToMove(cube);
        ReleaseOntoBelt(destConveyorMotor);
        TriggerGripSpike();

        SetOutput(out_ArmAtDrop,  false);
        SetOutput(out_ArmDropped, true);
        dbFeedbackPhase = "Dropped";
        yield return waitGripDelay;
        SetOutput(out_ArmDropped, false);

        SetStep("Arm2 ▸ Conv2Drop → Home");
        yield return GoTo(poseHome);

        IO_Router.Instance?.SetValueWithHandoff(out_ArmBusy, out_ArmIdle);
        dbFeedbackPhase = "Idle";
        coroutineRunning = false;
    }

    // ══ ARM 3 ════════════════════════════════════════════════════════════════
    IEnumerator Arm3_Sequence()
    {
        SetStep("Arm3 ▸ Home → WaypointA");
        yield return GoTo(poseWaypointA);

        SetStep("Arm3 ▸ WaypointA → Grab (Conv2)");
        SetOutput(out_ArmAtGrab, true);
        dbFeedbackPhase = "AtGrab";
        yield return GoTo(poseGrab);

        SetStep("Arm3 ▸ Gripping from Conveyor 2");
        if (cube == null) { Debug.LogError($"[ARM:{role}] Grip aborted — cube null!"); coroutineRunning = false; yield break; }

        Grip(sourceBelt: sourceConveyorMotor);
        TriggerGripSpike();

        SetOutput(out_ArmAtGrab,  false);
        SetOutput(out_ArmGripped, true);
        dbFeedbackPhase = "Gripped";
        hasGripped = true;
        yield return waitGripDelay;
        SetOutput(out_ArmGripped, false);

        SetStep("Arm3 ▸ Grab → WaypointA → WaypointB");
        yield return GoTo(poseWaypointA);
        yield return GoTo(poseWaypointB);

        SetStep("Arm3 ▸ WaypointB → Car2Drop");
        SetOutput(out_ArmAtDrop, true);
        dbFeedbackPhase = "AtDrop";
        yield return GoTo(poseCar2Drop);

        SetStep("Arm3 ▸ Placing into RobotCar2");
        if (robotCar2 == null) Debug.LogWarning($"[ARM:{role}] robotCar2 null.");
        if (car2DropPoint != null && cube != null) cube.position = car2DropPoint.position;

        Transform cubeRef = cube;
        ReleaseToSurface();
        TriggerGripSpike();

        if (robotCar2 != null) { robotCar2.NotifyBoxLoaded(cubeRef); Debug.Log($"[ARM:{role}] Car2.NotifyBoxLoaded('{cubeRef?.name}') called."); }

        SetOutput(out_ArmAtDrop,  false);
        SetOutput(out_ArmDropped, true);
        dbFeedbackPhase = "Dropped";
        yield return waitGripDelay;
        SetOutput(out_ArmDropped, false);

        SetStep("Arm3 ▸ Car2Drop → Home");
        yield return GoTo(poseHome);

        IO_Router.Instance?.SetValueWithHandoff(out_ArmBusy, out_ArmIdle);
        dbFeedbackPhase = "Idle";
        coroutineRunning = false;
    }

    void TriggerGripSpike() { gripEventActive = true; gripEventTimer = 0.3f; }

    // ── Grip / Release ────────────────────────────────────────────────────────
    void Grip(ConveyorMotor sourceBelt)
    {
        if (cube == null || gripPoint == null) return;
        if (sourceBelt != null) { sourceBelt.SetHeld(true); frozenBelt = sourceBelt; }
        cubeRb = cube.GetComponent<Rigidbody>();
        if (cubeRb != null) { cubeRb.linearVelocity=Vector3.zero; cubeRb.angularVelocity=Vector3.zero; cubeRb.isKinematic=true; cubeRb.detectCollisions=false; }
        cube.SetParent(gripPoint, worldPositionStays: true);
        dbHeld = true;
        Debug.Log($"[ARM:{role}] Gripped '{cube.name}' at {gripPoint.position}.");
    }

    void ReleaseOntoBelt(ConveyorMotor destBelt)
    {
        if (cube == null) return;
        cube.SetParent(null, worldPositionStays: true);
        if (cubeRb != null) { cubeRb.isKinematic=false; cubeRb.detectCollisions=true; cubeRb.linearVelocity=Vector3.zero; cubeRb.angularVelocity=Vector3.zero; }
        cubeRb = null;
        if (frozenBelt != null) { frozenBelt.SetHeld(false); frozenBelt.SetSensorOverride(false); frozenBelt=null; }
        if (destBelt   != null) { destBelt.SetHeld(false); }
        dbHeld = false;
        Debug.Log($"[ARM:{role}] Released onto belt '{destBelt?.name ?? "null"}'.");
    }

    void ReleaseToSurface()
    {
        if (cube == null) return;
        cube.SetParent(null, worldPositionStays: true);
        if (cubeRb != null) { cubeRb.isKinematic=true; cubeRb.detectCollisions=false; cubeRb.linearVelocity=Vector3.zero; cubeRb.angularVelocity=Vector3.zero; }
        cubeRb = null;
        if (frozenBelt != null) { frozenBelt.SetHeld(false); frozenBelt.SetSensorOverride(false); frozenBelt=null; }
        dbHeld = false;
        Debug.Log($"[ARM:{role}] Released to surface ('{cube?.name}').");
    }

    public void EmergencyRelease()
    {
        if (cube != null)
        {
            cube.SetParent(null, worldPositionStays: true);
            var rb = cubeRb != null ? cubeRb : cube.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic=false; rb.detectCollisions=true; rb.linearVelocity=Vector3.zero; rb.angularVelocity=Vector3.zero; }
        }
        cubeRb = null;
        if (frozenBelt != null) { frozenBelt.SetHeld(false); frozenBelt.SetSensorOverride(false); frozenBelt=null; }
        sourceConveyorMotor?.SetHeld(false);
        destConveyorMotor?.SetHeld(false);
        dbHeld=false; isExecuting=false; dbExecuting=false; coroutineRunning=false;

        IO_Router.Instance?.ClearLatchedFeedback(
            out_ArmAtGrab, out_ArmGripped, out_ArmAtCNC,
            out_CNCStart,  out_ArmAtDrop,  out_ArmDropped, out_ArmBusy);
        SetOutput(out_ArmIdle, true);
        dbFeedbackPhase = "Idle (emergency released)";

        if (role == ArmRole.Arm2_Conv1CNCConv2)
        {
            SetOutput(out_CNCPerfect, false);
            SetOutput(out_CNCDefect,  false);
            dbLastQCVerdict = "— (emergency)";
        }

        Debug.LogWarning($"[ARM:{role}] Emergency release — all feedback tags cleared, Idle set.");
    }

    void HandleQCReport(ProcessingDefectReport report)
    {
        if (role != ArmRole.Arm2_Conv1CNCConv2) return;
        lastQCReport    = report;
        dbLastQCVerdict = report.IsDefective ? $"DEFECTIVE [{report.Defects}]" : "PERFECT";
        dbLastQCShape   = report.SelectedShapeName;

        if (report.IsDefective)
            IO_Router.Instance?.SetValueWithHandoff(out_CNCPerfect, out_CNCDefect);
        else
            IO_Router.Instance?.SetValueWithHandoff(out_CNCDefect, out_CNCPerfect);
    }

    public ProcessingDefectReport GetLastQCReport() => lastQCReport;

    // ── Motion ────────────────────────────────────────────────────────────────
    IEnumerator GoTo(RobotPose pose)   { yield return MoveToAngles(pose); yield return waitPause; }

    IEnumerator MoveToAngles(RobotPose pose)
    {
        float[] tgt=pose.ToArr(), cur=GetAngles();
        float maxD=0f;
        for(int i=0;i<joints.Count;i++) { if(joints[i]==null) continue; float d=Mathf.Abs(Mathf.DeltaAngle(cur[i],tgt[i])); if(d>maxD) maxD=d; }
        float dur=maxD/Mathf.Max(jointSpeed,0.1f);
        if(dur<0.001f) yield break;

        float el=0f;
        while(el<dur)
        {
            el+=Time.deltaTime;
            float t=Mathf.Clamp01(el/dur);
            float s=Mathf.Lerp(t,Mathf.SmoothStep(0f,1f,t),smoothing);
            for(int i=0;i<joints.Count;i++) { if(joints[i]==null) continue; SetAngle(joints[i],axes[i],Mathf.LerpAngle(cur[i],tgt[i],s)); }
            yield return null;
        }
        for(int i=0;i<joints.Count;i++) { if(joints[i]==null) continue; SetAngle(joints[i],axes[i],tgt[i]); }
    }

    void  SetAngle(Transform j,RotAxis ax,float a) { Vector3 e=j.localEulerAngles; switch(ax){case RotAxis.X:e.x=a;break;case RotAxis.Y:e.y=a;break;default:e.z=a;break;} j.localEulerAngles=e; }
    float GetAngle(Transform j,RotAxis ax) { if(j==null)return 0f; Vector3 e=j.localEulerAngles; float r=ax==RotAxis.X?e.x:ax==RotAxis.Y?e.y:e.z; return r>180f?r-360f:r; }
    float[] GetAngles() { var a=new float[joints.Count]; for(int i=0;i<joints.Count;i++) a[i]=GetAngle(joints[i],axes[i]); return a; }

    void BuildLists()
    {
        joints=new List<Transform>{joint1,joint2,joint3,joint4,joint5,joint6};
        axes  =new List<RotAxis>  {j1Axis,j2Axis,j3Axis,j4Axis,j5Axis,j6Axis};
    }

    void ApplyCaptures()
    {
        if(_capHome)      {CaptureInto(poseHome);      _capHome=false;}
        if(_capWpA)       {CaptureInto(poseWaypointA); _capWpA=false;}
        if(_capGrab)      {CaptureInto(poseGrab);      _capGrab=false;}
        if(_capWpB)       {CaptureInto(poseWaypointB); _capWpB=false;}
        if(_capConv1Drop) {CaptureInto(poseConv1Drop); _capConv1Drop=false;}
        if(_capCNCPlace)  {CaptureInto(poseCNCPlace);  _capCNCPlace=false;}
        if(_capWpC)       {CaptureInto(poseWaypointC); _capWpC=false;}
        if(_capConv2Drop) {CaptureInto(poseConv2Drop); _capConv2Drop=false;}
        if(_capCar2Drop)  {CaptureInto(poseCar2Drop);  _capCar2Drop=false;}
    }

    public void CaptureInto(RobotPose p)
    {
        if(joints==null||joints.Count==0) BuildLists();
#if UNITY_EDITOR
        Undo.RecordObject(this,$"Capture [{p.name}]");
#endif
        p.FromArr(GetAngles());
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        if(p.savePoseOnCapture) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    public string DefaultPoseFileName()
    {
        string safe = gameObject.name;
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        safe = safe.Replace(' ', '_');
        return $"{role}_{safe}";
    }

    string PoseFolder()
    {
        string dir =
#if UNITY_EDITOR
            System.IO.Path.Combine(Application.dataPath, "RobotPoses");
#else
            System.IO.Path.Combine(Application.persistentDataPath, "RobotPoses");
#endif
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    public string PoseFilePath()
    {
        string fname = string.IsNullOrEmpty(poseFileName) ? DefaultPoseFileName() : poseFileName;
        return System.IO.Path.Combine(PoseFolder(), fname + ".json");
    }

    public void SavePosesToFile()
    {
        var data = new PoseFileData { poseHome=poseHome, poseWaypointA=poseWaypointA, poseGrab=poseGrab, poseWaypointB=poseWaypointB, poseConv1Drop=poseConv1Drop, poseCNCPlace=poseCNCPlace, poseWaypointC=poseWaypointC, poseConv2Drop=poseConv2Drop, poseCar2Drop=poseCar2Drop };
        string path = PoseFilePath();
        try { System.IO.File.WriteAllText(path, JsonUtility.ToJson(data, true)); Debug.Log($"[ARM:{role}] ✔ Poses saved → {path}");
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
        catch (System.Exception e) { Debug.LogError($"[ARM:{role}] Save poses failed: {e.Message}"); }
    }

    public void LoadPosesFromFile()
    {
        string path = PoseFilePath();
        if (!System.IO.File.Exists(path)) { Debug.LogWarning($"[ARM:{role}] No pose file at '{path}'."); return; }
        try
        {
            var data = JsonUtility.FromJson<PoseFileData>(System.IO.File.ReadAllText(path));
#if UNITY_EDITOR
            Undo.RecordObject(this, $"Load Poses [{role}]");
#endif
            poseHome=data.poseHome; poseWaypointA=data.poseWaypointA; poseGrab=data.poseGrab; poseWaypointB=data.poseWaypointB; poseConv1Drop=data.poseConv1Drop; poseCNCPlace=data.poseCNCPlace; poseWaypointC=data.poseWaypointC; poseConv2Drop=data.poseConv2Drop; poseCar2Drop=data.poseCar2Drop;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[ARM:{role}] ✔ Poses loaded ← {path}");
        }
        catch (System.Exception e) { Debug.LogError($"[ARM:{role}] Load poses failed: {e.Message}"); }
    }

    void SetStep(string s) => dbStep = s;
    void SetOutput(string tag, bool v) { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }

    public void PreviewHome()      => Preview(poseHome);
    public void PreviewWpA()       => Preview(poseWaypointA);
    public void PreviewGrab()      => Preview(poseGrab);
    public void PreviewWpB()       => Preview(poseWaypointB);
    public void PreviewConv1Drop() => Preview(poseConv1Drop);
    public void PreviewCNCPlace()  => Preview(poseCNCPlace);
    public void PreviewWpC()       => Preview(poseWaypointC);
    public void PreviewConv2Drop() => Preview(poseConv2Drop);
    public void PreviewCar2Drop()  => Preview(poseCar2Drop);

    void Preview(RobotPose p) { if(!isExecuting) StartCoroutine(PreviewCoroutine(p)); }
    IEnumerator PreviewCoroutine(RobotPose p) { isExecuting=true; SetStep($"Preview→{p.name}"); yield return MoveToAngles(p); isExecuting=false; SetStep("Idle"); }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RobotArmController))]
public class RobotArmControllerEditor : Editor
{
    static readonly Color GREEN  = new Color(0.25f,0.75f,0.35f);
    static readonly Color BLUE   = new Color(0.25f,0.55f,0.90f);
    static readonly Color ORANGE = new Color(0.90f,0.50f,0.15f);
    static readonly Color RED    = new Color(0.85f,0.20f,0.20f);
    static readonly Color CYAN   = new Color(0.20f,0.75f,0.85f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var arm=(RobotArmController)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("── POSE CAPTURE & PREVIEW ───────────────────────",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Play mode: pose the arm joints in Scene, then press CAPTURE.\nPREVIEW moves arm to saved pose (arm must be idle).",MessageType.Info);
        EditorGUILayout.Space(4);

        Row(arm,"Home",      ref arm._capHome, arm.poseHome,      arm.PreviewHome);
        Row(arm,"WaypointA", ref arm._capWpA,  arm.poseWaypointA, arm.PreviewWpA);
        Row(arm,"Grab",      ref arm._capGrab, arm.poseGrab,      arm.PreviewGrab);
        Row(arm,"WaypointB", ref arm._capWpB,  arm.poseWaypointB, arm.PreviewWpB);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"── Role poses ({arm.role}) ──────────────────────",EditorStyles.boldLabel);
        switch(arm.role)
        {
            case RobotArmController.ArmRole.Arm1_Car1ToConveyor1:
                Row(arm,"Conv1 Drop",ref arm._capConv1Drop,arm.poseConv1Drop,arm.PreviewConv1Drop); break;
            case RobotArmController.ArmRole.Arm2_Conv1CNCConv2:
                Row(arm,"CNC Place", ref arm._capCNCPlace, arm.poseCNCPlace, arm.PreviewCNCPlace);
                Row(arm,"WaypointC", ref arm._capWpC,      arm.poseWaypointC,arm.PreviewWpC);
                Row(arm,"Conv2 Drop",ref arm._capConv2Drop,arm.poseConv2Drop,arm.PreviewConv2Drop); break;
            case RobotArmController.ArmRole.Arm3_Conv2ToCar2:
                Row(arm,"Car2 Drop", ref arm._capCar2Drop, arm.poseCar2Drop, arm.PreviewCar2Drop); break;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("── POSE FILE ────────────────────────────────────────",EditorStyles.boldLabel);
        string fname = string.IsNullOrEmpty(arm.poseFileName) ? arm.DefaultPoseFileName() : arm.poseFileName;
        EditorGUILayout.LabelField($"File: RobotPoses/{fname}.json", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor=GREEN;
        if(GUILayout.Button("💾  Save Poses To File",GUILayout.Height(28))) arm.SavePosesToFile();
        GUI.backgroundColor=BLUE;
        if(GUILayout.Button("📂  Load Poses From File",GUILayout.Height(28))) arm.LoadPosesFromFile();
        GUI.backgroundColor=Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        GUI.backgroundColor=ORANGE;
        if(GUILayout.Button("▶  Trigger Sequence Manually",GUILayout.Height(34)))
            if(Application.isPlaying) arm.NotifyRobotTrigger();
        GUI.backgroundColor=RED;
        if(GUILayout.Button("⚠  Emergency Release (Debug)",GUILayout.Height(28)))
            if(Application.isPlaying) arm.EmergencyRelease();

        EditorGUILayout.Space(4);
        GUI.backgroundColor=CYAN;
        if(GUILayout.Button("🔍  Diagnose Tag Subscriptions",GUILayout.Height(28)))
            if(Application.isPlaying) arm.DiagnoseTagSubscriptions();
        GUI.backgroundColor=Color.white;
    }

    void Row(RobotArmController arm,string label,ref bool flag,
             RobotArmController.RobotPose pose,System.Action preview)
    {
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor=GREEN;
        if(GUILayout.Button($"⬇ Capture  {label}",GUILayout.Width(165)))
        { Undo.RecordObject(arm,$"Capture {label}"); arm.CaptureInto(pose); EditorUtility.SetDirty(arm);
          if(pose.savePoseOnCapture) EditorSceneManager.MarkSceneDirty(arm.gameObject.scene); }
        GUI.backgroundColor=BLUE;
        if(GUILayout.Button($"▶ Preview  {label}",GUILayout.Width(165)))
            if(Application.isPlaying) preview?.Invoke();
        GUI.backgroundColor=Color.white;
        EditorGUILayout.LabelField(
            $"J1={pose.j1:F0}° J2={pose.j2:F0}° J3={pose.j3:F0}° J4={pose.j4:F0}° J5={pose.j5:F0}° J6={pose.j6:F0}°",
            EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
}
#endif