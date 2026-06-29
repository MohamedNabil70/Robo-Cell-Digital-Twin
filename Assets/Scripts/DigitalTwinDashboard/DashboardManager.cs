using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum DashboardPresentationMode
{
    WorldSpace,
    ScreenSpace
}

public enum DashboardOfflineArmFaultMode
{
    Normal,
    Warning,
    Fault
}

/// <summary>
/// Owns the single active read-only dashboard instance.
/// </summary>
public sealed class DashboardManager : MonoBehaviour
{
    public static DashboardManager Instance { get; private set; }

    [Tooltip("World-space Canvas prefab with an ObjectDashboard component.")]
    public GameObject dashboardPrefab;

    [Tooltip("World-space offset from the clicked object's transform position.")]
    public Vector3 dashboardOffset = new Vector3(0f, 2f, 0f);

    [Tooltip("In world-space/First Person mode, place the dashboard at the active camera Y height.")]
    public bool alignWorldDashboardHeightToCamera = true;

    [Header("Click Detection")]
    [Tooltip("Uses a camera raycast for reliable clicks with Unity's Input System.")]
    public bool enableRaycastClickDetection = true;

    [Tooltip("Camera used for machine selection. If empty, the Main Camera is used.")]
    public Camera clickCamera;

    [Tooltip("Physics layers that may contain clickable twin objects.")]
    public LayerMask clickableLayers = ~0;

    [Tooltip("In First-Person/world dashboard mode, clicks use the fixed center-screen reticle instead of the mouse position.")]
    public bool useCenterScreenClickInWorldSpace = true;

    [Tooltip("Rotates the world-space dashboard to match the selection camera when it opens.")]
    public bool faceCameraOnOpen = true;

    [Tooltip("Comma-separated object IDs that should never open a dashboard.")]
    public string disabledDashboardObjectIds = "shelvingA,shelvingB,car1,car2";

    [Header("Appearance")]
    [Tooltip("Applies the optional industrial HUD style at runtime. Disable to restore the original prefab look.")]
    public bool useEnhancedStyle = true;

    [Header("Presentation")]
    [SerializeField] private DashboardPresentationMode presentationMode = DashboardPresentationMode.WorldSpace;
    [Tooltip("Screen-space overlay position used in Monitoring mode.")]
    public Vector2 screenOverlayAnchoredPosition = new Vector2(0f, 0f);

    [Tooltip("Screen-space overlay scale used in Monitoring mode.")]
    public Vector3 screenOverlayScale = Vector3.one;

    [Tooltip("Sorting order for the Monitoring mode overlay canvas.")]
    public int screenOverlaySortingOrder = 100;

    [Header("Offline Test Telemetry")]
    [Tooltip("Generates bounded read-only status snapshots for dashboard testing. This never publishes MQTT commands.")]
    public bool enableOfflineTestTelemetry;

    [Tooltip("Comma-separated conveyor object IDs for offline dashboard testing.")]
    public string offlineConveyorObjectIds = "conveyor1,conveyor2";

    [Tooltip("Comma-separated arm object IDs for offline dashboard testing.")]
    public string offlineArmObjectIds = "arm1,arm2,arm3";

    [Range(0.2f, 10f)]
    public float offlineTelemetryInterval = 1f;

    [Tooltip("Runtime arm fault injection. Change during Play mode to update arm dashboard values live.")]
    public DashboardOfflineArmFaultMode offlineArmFaultMode = DashboardOfflineArmFaultMode.Normal;

    private GameObject currentDashboard;
    private string currentObjectId;
    private int lastShowFrame = -1;
    private int lastInputFrame = -1;
    private float nextOfflineTelemetryTime;
    private DashboardOfflineArmFaultMode lastOfflineArmFaultMode;
#if ENABLE_INPUT_SYSTEM
    private InputAction clickAction;
    private bool hasQueuedInputClick;
    private Vector2 queuedInputPosition;
#endif

    public DashboardPresentationMode PresentationMode => presentationMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("A second DashboardManager was found and will be removed.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        Debug.Log("DashboardManager initialized.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        clickAction = new InputAction("Digital Twin Select", InputActionType.Button, "<Mouse>/leftButton");
        clickAction.performed += OnInputSystemClick;
        clickAction.Enable();
#endif
    }

    private void OnDisable()
    {
#if ENABLE_INPUT_SYSTEM
        if (clickAction != null)
        {
            clickAction.performed -= OnInputSystemClick;
            clickAction.Disable();
            clickAction.Dispose();
            clickAction = null;
        }
#endif
    }

    private void Update()
    {
        if (!enableRaycastClickDetection)
        {
            UpdateOfflineTestTelemetry();
            return;
        }

        UpdateOfflineTestTelemetry();

#if ENABLE_INPUT_SYSTEM
        if (hasQueuedInputClick)
        {
            hasQueuedInputClick = false;
            HandleScreenClick(queuedInputPosition, "Input Action");
            return;
        }
#endif

        if (TryGetLeftClick(out Vector2 screenPosition))
        {
            HandleScreenClick(screenPosition, "frame polling");
        }
    }

    private void OnGUI()
    {
        Event currentEvent = Event.current;
        if (!enableRaycastClickDetection ||
            currentEvent == null ||
            currentEvent.type != EventType.MouseDown ||
            currentEvent.button != 0)
        {
            return;
        }

        Vector2 screenPosition = new Vector2(
            currentEvent.mousePosition.x,
            Screen.height - currentEvent.mousePosition.y);
        HandleScreenClick(screenPosition, "Game view event");
    }

    private void HandleScreenClick(Vector2 screenPosition, string source)
    {
        if (lastInputFrame == Time.frameCount)
        {
            return;
        }

        lastInputFrame = Time.frameCount;

        // Let buttons on an open dashboard receive the click without reopening a machine behind it.
        if (currentDashboard != null &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Debug.Log($"Dashboard click detected via {source} at {screenPosition}.", this);
        if (presentationMode == DashboardPresentationMode.WorldSpace && useCenterScreenClickInWorldSpace)
        {
            screenPosition = GetScreenCenter();
        }

        if (!TryShowDashboardAtScreenPosition(screenPosition))
        {
            Debug.Log("Dashboard click did not hit a ClickableTwinObject.", this);
        }
    }

    public bool TryShowDashboardAtScreenCenter()
    {
        return TryShowDashboardAtScreenPosition(GetScreenCenter());
    }

    /// <summary>
    /// Raycasts from the selection camera and opens the nearest clickable twin object.
    /// </summary>
    public bool TryShowDashboardAtScreenPosition(Vector2 screenPosition)
    {
        Camera cameraToUse = clickCamera != null ? clickCamera : Camera.main;
        if (cameraToUse == null)
        {
            Debug.LogWarning("Cannot select a twin object because no click camera or Main Camera exists.", this);
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            cameraToUse.farClipPlane,
            clickableLayers,
            QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            ClickableTwinObject twinObject = hit.collider.GetComponentInParent<ClickableTwinObject>();
            if (twinObject == null || string.IsNullOrWhiteSpace(twinObject.objectId))
            {
                continue;
            }

            string hitObjectId = twinObject.objectId.Trim();
            if (IsDashboardDisabledForObject(hitObjectId))
            {
                Debug.Log($"Dashboard disabled for '{hitObjectId}'.", twinObject);
                return false;
            }

            Debug.Log(
                $"Raycast selected twin object '{twinObject.name}' ({hitObjectId}).",
                twinObject);
            ShowDashboard(hitObjectId, twinObject.GetDashboardAnchorPosition());
            return true;
        }

        return false;
    }

    Vector2 GetScreenCenter()
    {
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    public void ShowDashboard(string objectId, Vector3 objectPosition)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            Debug.LogWarning("Dashboard request ignored because objectId was empty.", this);
            return;
        }

        if (dashboardPrefab == null)
        {
            Debug.LogError("DashboardManager has no dashboardPrefab assigned.", this);
            return;
        }

        string trimmedObjectId = objectId.Trim();
        if (IsDashboardDisabledForObject(trimmedObjectId))
        {
            Debug.Log($"Dashboard request ignored because '{trimmedObjectId}' is disabled.", this);
            CloseDashboard();
            return;
        }

        if (lastShowFrame == Time.frameCount &&
            string.Equals(currentObjectId, trimmedObjectId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CloseDashboard();

        if (presentationMode == DashboardPresentationMode.ScreenSpace)
        {
            currentDashboard = Instantiate(dashboardPrefab);
            ConfigureScreenSpaceDashboard(currentDashboard);
        }
        else
        {
            Vector3 dashboardPosition = objectPosition + dashboardOffset;
            if (alignWorldDashboardHeightToCamera)
            {
                Camera cameraToUse = clickCamera != null ? clickCamera : Camera.main;
                if (cameraToUse != null)
                {
                    dashboardPosition.y = cameraToUse.transform.position.y;
                }
            }

            currentDashboard = Instantiate(dashboardPrefab, dashboardPosition, dashboardPrefab.transform.rotation);
            ConfigureWorldSpaceDashboard(currentDashboard, objectPosition);
        }

        if (!currentDashboard.TryGetComponent(out ObjectDashboard dashboard))
        {
            Debug.LogError("The dashboard prefab is missing an ObjectDashboard component.", currentDashboard);
            Destroy(currentDashboard);
            currentDashboard = null;
            return;
        }

        currentObjectId = trimmedObjectId;
        lastShowFrame = Time.frameCount;
        dashboard.SetTargetObject(trimmedObjectId);

        if (presentationMode == DashboardPresentationMode.ScreenSpace && useEnhancedStyle)
        {
            MonitoringDashboardVisualEnhancer enhancer = currentDashboard.GetComponent<MonitoringDashboardVisualEnhancer>();
            if (enhancer == null)
            {
                enhancer = currentDashboard.AddComponent<MonitoringDashboardVisualEnhancer>();
            }

            enhancer.Initialize(dashboard);
        }
        else if (presentationMode == DashboardPresentationMode.WorldSpace && useEnhancedStyle)
        {
            Camera cameraToUse = clickCamera != null ? clickCamera : Camera.main;
            DashboardVisualEnhancer enhancer = currentDashboard.GetComponent<DashboardVisualEnhancer>();
            if (enhancer == null)
            {
                enhancer = currentDashboard.AddComponent<DashboardVisualEnhancer>();
            }

            enhancer.Initialize(dashboard, objectPosition, cameraToUse);
        }

        Debug.Log($"Opened {presentationMode} dashboard for '{trimmedObjectId}'.", currentDashboard);
    }

    bool IsDashboardDisabledForObject(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(disabledDashboardObjectIds))
        {
            return false;
        }

        foreach (string disabledId in disabledDashboardObjectIds.Split(','))
        {
            if (string.Equals(disabledId.Trim(), objectId.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    void ConfigureWorldSpaceDashboard(GameObject dashboardInstance, Vector3 objectPosition)
    {
        Camera cameraToUse = clickCamera != null ? clickCamera : Camera.main;
        Canvas dashboardCanvas = dashboardInstance.GetComponent<Canvas>();
        if (dashboardCanvas != null && cameraToUse != null)
        {
            dashboardCanvas.renderMode = RenderMode.WorldSpace;
            dashboardCanvas.worldCamera = cameraToUse;
        }

        if (faceCameraOnOpen && cameraToUse != null)
        {
            dashboardInstance.transform.rotation = cameraToUse.transform.rotation;
        }
    }

    void ConfigureScreenSpaceDashboard(GameObject dashboardInstance)
    {
        Canvas dashboardCanvas = dashboardInstance.GetComponent<Canvas>();
        if (dashboardCanvas == null)
        {
            return;
        }

        dashboardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dashboardCanvas.worldCamera = null;
        dashboardCanvas.sortingOrder = screenOverlaySortingOrder;

        CanvasScaler scaler = dashboardInstance.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = dashboardInstance.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rectTransform = dashboardInstance.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = screenOverlayScale;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = screenOverlayAnchoredPosition;
        }
    }

    /// <summary>
    /// Selects how subsequently opened dashboards will be presented.
    /// Screen-space layout is configured by the view-mode integration step.
    /// </summary>
    public void SetPresentationMode(DashboardPresentationMode mode)
    {
        if (presentationMode == mode)
        {
            return;
        }

        CloseDashboard();
        presentationMode = mode;
    }

    public void SetFirstPersonCameraMode()
    {
        SetPresentationMode(DashboardPresentationMode.WorldSpace);
    }

    public void SetMonitoringCameraMode()
    {
        SetPresentationMode(DashboardPresentationMode.ScreenSpace);
    }

    public void CloseDashboard()
    {
        ObjectDashboard[] dashboards = FindObjectsByType<ObjectDashboard>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (ObjectDashboard dashboard in dashboards)
        {
            if (dashboard == null)
            {
                continue;
            }

            DestroyDashboardObject(dashboard.gameObject);
        }

        currentDashboard = null;
        currentObjectId = null;
    }

    static void DestroyDashboardObject(GameObject dashboardObject)
    {
        if (dashboardObject == null)
        {
            return;
        }

        dashboardObject.SetActive(false);

        if (Application.isPlaying)
        {
            Destroy(dashboardObject);
        }
        else
        {
            DestroyImmediate(dashboardObject);
        }
    }

    private static bool TryGetLeftClick(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    void UpdateOfflineTestTelemetry()
    {
        if (!enableOfflineTestTelemetry)
        {
            return;
        }

        bool faultModeChanged = lastOfflineArmFaultMode != offlineArmFaultMode;
        if (!faultModeChanged && Time.unscaledTime < nextOfflineTelemetryTime)
        {
            return;
        }

        lastOfflineArmFaultMode = offlineArmFaultMode;
        nextOfflineTelemetryTime = Time.unscaledTime + Mathf.Max(0.2f, offlineTelemetryInterval);

        TwinObjectStatusStore statusStore = EnsureStatusStore();
        if (statusStore == null)
        {
            return;
        }

        long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (string objectId in SplitObjectIds(offlineConveyorObjectIds))
        {
            float phase = Mathf.Repeat(Time.unscaledTime * 0.21f + objectId.GetHashCode() * 0.001f, 1f);
            float speed = Mathf.Clamp(1.25f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.35f, 0.4f, 2.2f);
            float temperature = Mathf.Clamp(36f + speed * 7f, 25f, 58f);
            float vibration = Mathf.Clamp(0.8f + speed * 0.35f, 0.2f, 2.5f);

            string payload = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{{\"objectId\":\"{0}\",\"displayName\":\"{0}\",\"objectType\":\"conveyor\",\"state\":\"Running\",\"running\":true,\"speed\":{1:0.##},\"temperature\":{2:0.##},\"vibration\":{3:0.##},\"aiStatus\":\"Normal\",\"aiConfidence\":0.96,\"timestamp\":{4}}}",
                objectId,
                speed,
                temperature,
                vibration,
                now);
            statusStore.UpdateStatus("offline", objectId, payload);
        }

        foreach (string objectId in SplitObjectIds(offlineArmObjectIds))
        {
            OfflineArmSnapshot arm = BuildOfflineArmSnapshot(objectId);
            string payload = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{{\"objectId\":\"{0}\",\"displayName\":\"{0}\",\"objectType\":\"arm\",\"state\":\"{1}\",\"health\":\"{2}\",\"rul_cycles\":{3:0},\"health_score\":{4:0.##},\"health_state\":\"{2}\",\"maintenance_required\":{5},\"fault_type\":\"{6}\",\"timestamp\":{7}}}",
                objectId,
                arm.Status,
                arm.HealthState,
                arm.RulCycles,
                arm.HealthScore,
                arm.MaintenanceRequired ? "true" : "false",
                arm.FaultType,
                now);
            statusStore.UpdateStatus("offline", objectId, payload);
        }
    }

    TwinObjectStatusStore EnsureStatusStore()
    {
        if (TwinObjectStatusStore.Instance != null)
        {
            return TwinObjectStatusStore.Instance;
        }

        TwinObjectStatusStore existingStore = FindAnyObjectByType<TwinObjectStatusStore>();
        if (existingStore != null)
        {
            return existingStore;
        }

        GameObject storeObject = new GameObject("TwinObjectStatusStore");
        return storeObject.AddComponent<TwinObjectStatusStore>();
    }

    OfflineArmSnapshot BuildOfflineArmSnapshot(string objectId)
    {
        switch (offlineArmFaultMode)
        {
            case DashboardOfflineArmFaultMode.Fault:
                return new OfflineArmSnapshot("Fault", "Critical", 1250f, 41f, true, "joint_overheat");
            case DashboardOfflineArmFaultMode.Warning:
                return new OfflineArmSnapshot("Warning", "Degraded", 4800f, 73f, true, "bearing_wear");
            default:
                float phase = Mathf.Repeat(Time.unscaledTime * 0.13f + objectId.GetHashCode() * 0.001f, 1f);
                float healthScore = Mathf.Clamp(96f + Mathf.Sin(phase * Mathf.PI * 2f) * 2f, 92f, 99f);
                float rulCycles = Mathf.Clamp(18500f + Mathf.Cos(phase * Mathf.PI * 2f) * 500f, 17500f, 19500f);
                return new OfflineArmSnapshot("Normal", "Healthy", rulCycles, healthScore, false, "None");
        }
    }

    static System.Collections.Generic.IEnumerable<string> SplitObjectIds(string objectIds)
    {
        if (string.IsNullOrWhiteSpace(objectIds))
        {
            yield break;
        }

        foreach (string objectId in objectIds.Split(','))
        {
            string trimmed = objectId.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    readonly struct OfflineArmSnapshot
    {
        public readonly string Status;
        public readonly string HealthState;
        public readonly float RulCycles;
        public readonly float HealthScore;
        public readonly bool MaintenanceRequired;
        public readonly string FaultType;

        public OfflineArmSnapshot(
            string status,
            string healthState,
            float rulCycles,
            float healthScore,
            bool maintenanceRequired,
            string faultType)
        {
            Status = status;
            HealthState = healthState;
            RulCycles = rulCycles;
            HealthScore = healthScore;
            MaintenanceRequired = maintenanceRequired;
            FaultType = faultType;
        }
    }

#if ENABLE_INPUT_SYSTEM
    private void OnInputSystemClick(InputAction.CallbackContext context)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        queuedInputPosition = mouse.position.ReadValue();
        hasQueuedInputClick = true;
    }
#endif
}
