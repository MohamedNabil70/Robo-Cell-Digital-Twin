using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum DashboardPresentationMode
{
    WorldSpace,
    ScreenSpace
}

/// <summary>
/// Owns the single active world-space dashboard instance.
/// </summary>
public sealed class DashboardManager : MonoBehaviour
{
    public static DashboardManager Instance { get; private set; }

    [Tooltip("World-space Canvas prefab with an ObjectDashboard component.")]
    public GameObject dashboardPrefab;

    [Tooltip("World-space offset from the clicked object's transform position.")]
    public Vector3 dashboardOffset = new Vector3(0f, 2f, 0f);

    [Header("Click Detection")]
    [Tooltip("Uses a camera raycast for reliable clicks with Unity's Input System.")]
    public bool enableRaycastClickDetection = true;

    [Tooltip("Camera used for machine selection. If empty, the Main Camera is used.")]
    public Camera clickCamera;

    [Tooltip("Physics layers that may contain clickable twin objects.")]
    public LayerMask clickableLayers = ~0;

    [Tooltip("Rotates the world-space dashboard to match the selection camera when it opens.")]
    public bool faceCameraOnOpen = true;

    [Header("Appearance")]
    [Tooltip("Applies the optional industrial HUD style at runtime. Disable to restore the original prefab look.")]
    public bool useEnhancedStyle = true;

    [Header("Presentation")]
    [SerializeField] private DashboardPresentationMode presentationMode = DashboardPresentationMode.WorldSpace;

    private GameObject currentDashboard;
    private string currentObjectId;
    private int lastShowFrame = -1;
    private int lastInputFrame = -1;
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
            return;
        }

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
        if (!TryShowDashboardAtScreenPosition(screenPosition))
        {
            Debug.Log("Dashboard click did not hit a ClickableTwinObject.", this);
        }
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

            Debug.Log(
                $"Raycast selected twin object '{twinObject.name}' ({twinObject.objectId.Trim()}).",
                twinObject);
            ShowDashboard(twinObject.objectId.Trim(), twinObject.GetDashboardAnchorPosition());
            return true;
        }

        return false;
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
        if (lastShowFrame == Time.frameCount &&
            string.Equals(currentObjectId, trimmedObjectId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CloseDashboard();

        Vector3 dashboardPosition = objectPosition + dashboardOffset;
        currentDashboard = Instantiate(dashboardPrefab, dashboardPosition, dashboardPrefab.transform.rotation);

        Camera cameraToUse = clickCamera != null ? clickCamera : Camera.main;
        Canvas dashboardCanvas = currentDashboard.GetComponent<Canvas>();
        if (dashboardCanvas != null && cameraToUse != null)
        {
            dashboardCanvas.worldCamera = cameraToUse;
        }

        if (faceCameraOnOpen && cameraToUse != null)
        {
            currentDashboard.transform.rotation = cameraToUse.transform.rotation;
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

        if (useEnhancedStyle)
        {
            DashboardVisualEnhancer enhancer = currentDashboard.GetComponent<DashboardVisualEnhancer>();
            if (enhancer == null)
            {
                enhancer = currentDashboard.AddComponent<DashboardVisualEnhancer>();
            }

            enhancer.Initialize(dashboard, objectPosition, cameraToUse);
        }

        Debug.Log($"Opened dashboard for '{trimmedObjectId}'.", currentDashboard);
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

    public void CloseDashboard()
    {
        if (currentDashboard == null)
        {
            return;
        }

        Debug.Log("Closed the active object dashboard.", currentDashboard);
        Destroy(currentDashboard);
        currentDashboard = null;
        currentObjectId = null;
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
