using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene/UI bridge for dashboard presentation mode. It only changes how a
/// selected-object dashboard is shown; it never sends commands or MQTT messages.
/// </summary>
public sealed class DashboardCameraModeBridge : MonoBehaviour
{
    [Tooltip("Dashboard manager to drive. If empty, the scene instance is found at runtime.")]
    public DashboardManager dashboardManager;

    [Tooltip("Mode applied on Start so the scene begins in a predictable dashboard presentation.")]
    public DashboardPresentationMode initialMode = DashboardPresentationMode.WorldSpace;

    [Tooltip("Apply initialMode on Start.")]
    public bool applyInitialModeOnStart = true;

    [Header("Camera Switching")]
    [Tooltip("Camera activated by the First Person button.")]
    public Camera firstPersonCamera;

    [Tooltip("Camera restored by Monitoring / Factory Analysis buttons. If empty, Main Camera is used.")]
    public Camera monitoringCamera;

    [Tooltip("Capsule/body used for first-person movement.")]
    public Transform firstPersonBody;

    [Tooltip("Movement and mouse-look controller on the first-person body.")]
    public FirstPersonCapsuleController firstPersonController;

    [Tooltip("Parent FirstPersonCam to the capsule/body while First Person mode is active.")]
    public bool attachCameraToFirstPersonBody = true;

    [Tooltip("Camera local position while attached to the first-person body.")]
    public Vector3 firstPersonCameraLocalPosition = new Vector3(0f, 0.55f, 0f);

    [Tooltip("Keep the first-person camera at this Y position. When attached, this is local Y.")]
    public float firstPersonCameraY = 0.55f;

    [Tooltip("Locks only the camera Y coordinate, so existing X/Z movement and rotation can still work.")]
    public bool lockFirstPersonCameraY = true;

    [Tooltip("When enabled, first-person clicks select from the screen-center reticle.")]
    public bool useCenterDotSelection = true;

    [Tooltip("Show a fixed center-screen dot while First Person mode is active.")]
    public bool showCenterDotInFirstPerson = true;

    [Range(4f, 32f)]
    public float centerDotSize = 9f;

    [Header("Factory Analysis")]
    [Tooltip("Full-screen Factory Analysis overlay opened by the Factory Analysis button.")]
    public FactoryAnalysisPanel factoryAnalysisPanel;

    [Tooltip("When available, follow realvirtual's SceneMouseNavigation.FirstPersonControllerActive flag.")]
    public bool syncWithRealvirtualFirstPersonState = true;

    [Tooltip("How often to poll external camera state when sync is enabled.")]
    [Range(0.05f, 2f)]
    public float cameraStatePollInterval = 0.25f;

    [Header("Optional Test Shortcuts")]
    [Tooltip("Disabled by default. Enable only for local Play mode testing.")]
    public bool enableKeyboardShortcuts;

    public KeyCode firstPersonModeKey = KeyCode.F1;
    public KeyCode monitoringModeKey = KeyCode.F2;

    Component sceneMouseNavigation;
    Canvas centerDotCanvas;
    Image centerDotImage;
    Camera activeCameraBeforeFirstPerson;
    bool lastKnownFirstPersonActive;
    bool hasKnownFirstPersonState;
    float nextCameraStatePollTime;

    void Start()
    {
        EnsureManager();
        FindSceneCameras();
        FindSceneMouseNavigation();

        if (applyInitialModeOnStart)
        {
            SetPresentationMode(initialMode);
        }
    }

    void Update()
    {
        ApplyFirstPersonCameraLock();
        SyncWithExternalCameraState();

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!enableKeyboardShortcuts)
        {
            return;
        }

        if (Input.GetKeyDown(firstPersonModeKey))
        {
            EnterFirstPersonMode();
        }
        else if (Input.GetKeyDown(monitoringModeKey))
        {
            EnterMonitoringMode();
        }
#endif
    }

    public void EnterFirstPersonMode()
    {
        HideFactoryAnalysisPanel();
        SetPresentationMode(DashboardPresentationMode.WorldSpace);
    }

    public void EnterMonitoringMode()
    {
        HideFactoryAnalysisPanel();
        SetPresentationMode(DashboardPresentationMode.ScreenSpace);
    }

    public void EnterFactoryAnalysisMode()
    {
        SetPresentationMode(DashboardPresentationMode.ScreenSpace);
        EnsureFactoryAnalysisPanel();

        if (factoryAnalysisPanel != null)
        {
            factoryAnalysisPanel.ShowPanel();
        }
    }

    public void SetMonitoringMode(bool isMonitoring)
    {
        SetPresentationMode(isMonitoring
            ? DashboardPresentationMode.ScreenSpace
            : DashboardPresentationMode.WorldSpace);
    }

    public void SetPresentationModeByIndex(int modeIndex)
    {
        SetPresentationMode(modeIndex == (int)DashboardPresentationMode.ScreenSpace
            ? DashboardPresentationMode.ScreenSpace
            : DashboardPresentationMode.WorldSpace);
    }

    public void SetPresentationMode(DashboardPresentationMode mode)
    {
        EnsureManager();
        FindSceneCameras();

        if (dashboardManager == null)
        {
            Debug.LogWarning("DashboardCameraModeBridge could not find a DashboardManager.", this);
            return;
        }

        dashboardManager.SetPresentationMode(mode);
        ApplyCameraMode(mode);
    }

    void ApplyCameraMode(DashboardPresentationMode mode)
    {
        bool firstPerson = mode == DashboardPresentationMode.WorldSpace;
        if (firstPerson)
        {
            if (firstPersonCamera == null)
            {
                Debug.LogWarning("First Person mode requested, but FirstPersonCam was not found.", this);
                SetCenterDotVisible(false);
                return;
            }

            if (monitoringCamera == null)
            {
                activeCameraBeforeFirstPerson = Camera.main;
            }

            if (monitoringCamera != null)
            {
                monitoringCamera.gameObject.SetActive(false);
            }

            firstPersonCamera.gameObject.SetActive(true);
            EnsureFirstPersonController();
            if (attachCameraToFirstPersonBody && firstPersonController != null)
            {
                firstPersonController.cameraLocalPosition = firstPersonCameraLocalPosition;
                firstPersonController.Activate(firstPersonCamera);
            }

            Camera.SetupCurrent(firstPersonCamera);
            FixFirstPersonCameraY();
            if (dashboardManager != null)
            {
                dashboardManager.clickCamera = firstPersonCamera;
                dashboardManager.useCenterScreenClickInWorldSpace = useCenterDotSelection;
            }

            SetCenterDotVisible(showCenterDotInFirstPerson);
            return;
        }

        SetCenterDotVisible(false);
        if (firstPersonController != null)
        {
            firstPersonController.Deactivate();
        }

        if (firstPersonCamera != null)
        {
            firstPersonCamera.gameObject.SetActive(false);
        }

        Camera cameraToRestore = monitoringCamera != null ? monitoringCamera : activeCameraBeforeFirstPerson;
        if (cameraToRestore != null)
        {
            cameraToRestore.gameObject.SetActive(true);
            Camera.SetupCurrent(cameraToRestore);
        }

        if (dashboardManager != null)
        {
            dashboardManager.clickCamera = cameraToRestore;
            dashboardManager.useCenterScreenClickInWorldSpace = false;
        }
    }

    void ApplyFirstPersonCameraLock()
    {
        if (dashboardManager == null ||
            dashboardManager.PresentationMode != DashboardPresentationMode.WorldSpace ||
            firstPersonCamera == null)
        {
            return;
        }

        FixFirstPersonCameraY();
    }

    void FixFirstPersonCameraY()
    {
        if (!lockFirstPersonCameraY || firstPersonCamera == null)
        {
            return;
        }

        if (attachCameraToFirstPersonBody && firstPersonCamera.transform.parent != null)
        {
            Vector3 localPosition = firstPersonCamera.transform.localPosition;
            if (!Mathf.Approximately(localPosition.y, firstPersonCameraY))
            {
                localPosition.y = firstPersonCameraY;
                firstPersonCamera.transform.localPosition = localPosition;
            }
        }
        else
        {
            Vector3 position = firstPersonCamera.transform.position;
            if (!Mathf.Approximately(position.y, firstPersonCameraY))
            {
                position.y = firstPersonCameraY;
                firstPersonCamera.transform.position = position;
            }
        }
    }

    void SetCenterDotVisible(bool visible)
    {
        EnsureCenterDot();

        if (centerDotCanvas != null)
        {
            centerDotCanvas.gameObject.SetActive(visible);
        }
    }

    void EnsureCenterDot()
    {
        if (!showCenterDotInFirstPerson || centerDotCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "First Person Center Dot",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        centerDotCanvas = canvasObject.GetComponent<Canvas>();
        centerDotCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        centerDotCanvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dotObject = new GameObject(
            "Center Dot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform dotRect = dotObject.GetComponent<RectTransform>();
        dotRect.SetParent(canvasObject.transform, false);
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = Vector2.zero;
        dotRect.sizeDelta = new Vector2(centerDotSize, centerDotSize);

        centerDotImage = dotObject.GetComponent<Image>();
        centerDotImage.color = new Color(1f, 0.78f, 0.26f, 0.96f);
        centerDotImage.raycastTarget = false;

        Outline outline = dotObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.78f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        canvasObject.SetActive(false);
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(canvasObject);
        }
    }

    void EnsureManager()
    {
        if (dashboardManager != null)
        {
            return;
        }

        dashboardManager = DashboardManager.Instance != null
            ? DashboardManager.Instance
            : FindAnyObjectByType<DashboardManager>();
    }

    void EnsureFactoryAnalysisPanel()
    {
        if (factoryAnalysisPanel != null)
        {
            return;
        }

        factoryAnalysisPanel = FindAnyObjectByType<FactoryAnalysisPanel>(FindObjectsInactive.Include);
        if (factoryAnalysisPanel != null)
        {
            return;
        }

        GameObject panelObject = new GameObject(
            "Factory Analysis Panel",
            typeof(RectTransform),
            typeof(FactoryAnalysisPanel));
        factoryAnalysisPanel = panelObject.GetComponent<FactoryAnalysisPanel>();
        factoryAnalysisPanel.hideOnStart = true;
        factoryAnalysisPanel.HidePanel();
    }

    void HideFactoryAnalysisPanel()
    {
        EnsureFactoryAnalysisPanel();
        if (factoryAnalysisPanel != null)
        {
            factoryAnalysisPanel.HidePanel();
        }
    }

    void FindSceneCameras()
    {
        if (firstPersonCamera == null)
        {
            GameObject cameraObject = GameObject.Find("FirstPersonCam");
            firstPersonCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        }

        if (monitoringCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera != firstPersonCamera)
            {
                monitoringCamera = mainCamera;
            }
            else
            {
                GameObject cameraObject = GameObject.Find("Main Camera");
                monitoringCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            }
        }

        if (firstPersonBody == null)
        {
            GameObject bodyObject = GameObject.Find("Capsule");
            firstPersonBody = bodyObject != null ? bodyObject.transform : null;
        }

        if (firstPersonController == null && firstPersonBody != null)
        {
            firstPersonController = firstPersonBody.GetComponent<FirstPersonCapsuleController>();
        }
    }

    void EnsureFirstPersonController()
    {
        FindSceneCameras();
        if (firstPersonBody == null)
        {
            return;
        }

        if (firstPersonController == null)
        {
            firstPersonController = firstPersonBody.GetComponent<FirstPersonCapsuleController>();
        }

        if (firstPersonController == null)
        {
            firstPersonController = firstPersonBody.gameObject.AddComponent<FirstPersonCapsuleController>();
        }

        firstPersonController.viewCamera = firstPersonCamera;
        firstPersonController.cameraLocalPosition = firstPersonCameraLocalPosition;
    }

    void SyncWithExternalCameraState()
    {
        if (!syncWithRealvirtualFirstPersonState || Time.unscaledTime < nextCameraStatePollTime)
        {
            return;
        }

        nextCameraStatePollTime = Time.unscaledTime + Mathf.Max(0.05f, cameraStatePollInterval);

        if (sceneMouseNavigation == null)
        {
            FindSceneMouseNavigation();
        }

        if (sceneMouseNavigation == null)
        {
            return;
        }

        System.Reflection.FieldInfo field = sceneMouseNavigation.GetType().GetField("FirstPersonControllerActive");
        if (field == null || field.FieldType != typeof(bool))
        {
            return;
        }

        bool firstPersonActive = (bool)field.GetValue(sceneMouseNavigation);
        if (hasKnownFirstPersonState && firstPersonActive == lastKnownFirstPersonActive)
        {
            return;
        }

        hasKnownFirstPersonState = true;
        lastKnownFirstPersonActive = firstPersonActive;
        SetPresentationMode(firstPersonActive
            ? DashboardPresentationMode.WorldSpace
            : DashboardPresentationMode.ScreenSpace);
    }

    void FindSceneMouseNavigation()
    {
        sceneMouseNavigation = null;
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
            {
                continue;
            }

            if (behaviour.GetType().Name == "SceneMouseNavigation")
            {
                sceneMouseNavigation = behaviour;
                return;
            }
        }
    }
}
