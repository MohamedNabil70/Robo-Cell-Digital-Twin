// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz    


using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN ) && !UNITY_WEBGL
using SpaceNavigatorDriver;
#endif
#if CINEMACHINE
using Cinemachine;
#endif


namespace realvirtual
{
    //! Controls the Mouse and Touch navigation in Game mode
    public class SceneMouseNavigation : realvirtualBehavior
    {
        //! delegate for starting and stopping camera interpolation
        public delegate void StartStopCameraInterpolation(bool start);

        //! delegate for starting and stopping panning
        public delegate void StartStopPanning(bool start);

        /// Delegates for Events
        //! delegate for starting and stopping rotation
        public delegate void StartStopRotation(bool start);

        [Tooltip("Toggle the orbit camera mode")]
        public bool UseOrbitCameraMode; //!< Toggle the orbit camera mode 

        public bool FirstPersonControllerActive = true; //!< Toggle the first person controller 

        [Tooltip("Rotate the camera with the left mouse button")]
        public bool RotateWithLeftMouseButton; //!< Rotate the camera with the left mouse button 

        [Tooltip("Rotates the camera to focused objects instead of panning the camera ")]
        public bool RotateToFocusObject; //!< Rotates the camera to focused objects instead of panning the camera 

        [Tooltip("Block rotation on selected objects")] [HideInInspector]
        public bool BlockRotationOnSelected;

        [Tooltip("Reference to the first person controller script")]
        public FirstPersonController FirstPersonController; //!< Reference to the first person controller script 

        [Tooltip("The last camera position before switching modes")] 
        public CameraPos LastCameraPosition; //!< The last camera position before switching modes

        [Tooltip("Set the camera position on start play")]
        public bool SetCameraPosOnStartPlay = true; //!< Set the camera position on start play

        [Tooltip("Save the camera position on quitting the application")]
        public bool SaveCameraPosOnQuit = true; //!< Save the camera position on quitting the application 

        [Tooltip("Set the editor camera position")]
        public bool SetEditorCameraPos = true; //!< Set the editor camera position

        [Tooltip("Interpolate to new saved Camerapositions")]
        public bool InterpolateToNewCamerapoitions = true; //!< Interpolate to new saved Camerapositions

        [Tooltip("Interpolate to new saved Camerapositions with this speed")]
        public float CameraInterpolationSpeed = 0.1f; //!< Interpolation speed to new saved Camerapositions

        [HideInInspector] [Tooltip("The target of the camera")]
        public Transform target; //!< The target of the camera

        [Tooltip("Offset of the camera's target")] [HideInInspector]
        public Vector3 targetOffset; //!< Offset of the camera's target 

        [Tooltip("The distance of the camera from its target")] [HideInInspector]
        public float distance = 5.0f; //!< The distance of the camera from its target

        [Tooltip(
            "The DPI scale of the screen, is automatically calculated and is used to scale all screen pixel related distances measurements")]
        [ReadOnly]
        public float
            DPIScale = 1; //!< The DPI scale of the screen, is automatically calculated and is used to scale all screen pixel related distances measurements

        [Tooltip("The minimum distance of the camera from its target")] [HideInInspector]
        public float minDistance = 1f; //!< The minimum distance of the camera from its target

        [Tooltip("The speed of mouse rotation, 1 is standard value")]
        public float MouseRotationSpeed = 1f; //!< The speed of rotation around the y-axis

        [Header("Master Controls")]
        [Tooltip("Master sensitivity multiplier for all mouse/touch navigation (0.1 to 3.0)")]
        [Range(0.1f, 3f)]
        [RuntimePersistenceRange(0.1f, 3.0f)]
        [RuntimePersistenceLabel("Mouse Sensitivity")]
        [RuntimePersistenceHint("(0.1-3.0)")]
        [RuntimePersistenceFormat("F2")]
        public float MasterSensitivity = 1f; //!< Master sensitivity multiplier affecting all navigation speeds

        [Tooltip("The minimum angle limit for the camera rotation around the horizontal axis ")]
        public float MinHorizontalRotation; //!< The minimum angle limit for the camera rotation around the x-axis 

        [Tooltip("The maximum angle limit for the camera rotation around the horizontal axis")]
        public float MaxHorizontalRotation = 100; //!< The maximum angle limit for the camera rotation around the x-axis

        [Tooltip("The speed of zooming in and out, 1 is standard")]
        public float ZoomSpeed = 1; //!< The speed of zooming in and out, 1 is standard 

        [Tooltip("The speed at which the zooming slows down, 1 is standard")]
        public float RotDamping = 1f; //!< The speed at which the zooming slows down, 1 is standard

        [Tooltip("The speed at which the panning slows down, 1 is standard")]
        public float PanDamping = 1; //!< The speed at which the panning slows down, 1 is standard
        
        [Tooltip("The speed at which movement with the cursor should be done, 1 is standard")]
        public float CursorSpeed = 1; //!< The speed at which the panning slows down, 1 is standard

        [Tooltip("The speed at which the zooming slows down, 1 is standard")]
        public float ZoomDamping = 1f; //!< The speed at which the zooming slows down, 1 is standard

        [Tooltip("The speed of panning the camera in orthographic mode")]
        public float orthoPanSpeed = 1; //!< The speed of panning the camera in orthographic mode

        [Tooltip("The time to wait before starting the demo due to inactivity")]
        public float StartDemoOnInactivity = 5.0f; //!< The time to wait before starting the demo due to inactivity 

        [Tooltip("The time without any mouse activity before considering the camera inactive")]
        public float
            DurationNoMouseActivity; //!< The time without any mouse activity before considering the camera inactive

        [Tooltip("A game object used for debugging purposes")]
        public GameObject DebugObj;

        [Header("Touch Controls")] [Tooltip("The touch interaction script")]
        public TouchInteraction Touch; //!< The touch interaction script 

        [Tooltip("The speed of pan speed with touch")]
        public float TouchPanSpeed = 1f; //!< The speed of rotating with touch

        [Tooltip("The speed of rotating with touch")]
        public float TouchRotationSpeed = 1f; //!< The speed of rotating with touch

        [Tooltip("The speed of zooming with touch")]
        public float TouchZoomSpeed = 1f; //!< The speed of zooming with touch

        [Tooltip("Invert vertical touch axis")]
        public bool TouchInvertVertical; //! Touch invert vertical

        [Tooltip("Invert horizohntal touch axis")]
        public bool TouchInvertHorizontal; //! Touch invert horizontal

        [Header("SpaceNavigator")] public bool EnableSpaceNavigator = true; //! Enable space navigator
        public float SpaceNavTransSpeed = 1; //! Space navigator translation speed


        [Header("Status")] [NaughtyAttributes.ReadOnly]
        public float currentDistance; //! Current distance

        [ReadOnly] public float desiredDistance; //! Desired distance
        [ReadOnly] public Quaternion currentRotation; //! Current rotation
        [ReadOnly] public Quaternion desiredRotation; //! Desired rotation
        [ReadOnly] public bool isRotating;
        [ReadOnly] public bool isPanning;
        [ReadOnly] public bool interpolatingToNewCameraPos;
        [ReadOnly] public bool CinemachineIsActive;
        [ReadOnly] public bool blockrotation;
        [ReadOnly] public bool blockleftmouserotation;
        [HideInInspector] public bool orthograhicview;
        [HideInInspector] public OrthoViewController orthoviewcontroller;
        private bool _demostarted;
        private float _lastmovement;
        private Vector3 _pos;

        private bool blockrotationbefore;
        private Vector3 calculatedboundscenter;
        public StartStopCameraInterpolation EventStartStopCameraInterpolation;
        public StartStopPanning EventStartStopPanning;
        public StartStopRotation EventStartStopRotation;
#if REALVIRTUAL_PROFESSIONAL
        private HMI_Controller hmiController;
#endif
        private Camera incamera;
        private float interpolatedistance;
        private Vector3 interpolatenewcampos;
        private Quaternion interpolatenewcamrot;
        private Vector3 interpolaterotation;

        private Vector3 interpolatetargetpos;
        private float lastperspectivedistance;
        
        private float maxdistance;
        private Camera mycamera;
        private Vector3 position;
        private Vector3 raycastto0plane;
        private Vector3 raycastto0planebefore;
        private Vector3 rotationPivotPoint;
        private bool bypassTargetSystemDuringRotation;

        private Quaternion rotation;
        private GameObject selectedbefore;
        private SelectionRaycast selectionmanager;
        private bool selectionmanagernotnull;
        private bool startcameraposset;
        private float startpanhitdistance;
        private Vector3 targetposition;
        private bool touch;

        private bool touchnotnull;

       
        private bool verticalplanedetection;
        private float xDeg;
        private float yDeg;
        private float zoomhitdistance;
        private Vector3 zoomposviewport = Vector3.zero;
        private Vector3 zoomposworld = Vector3.zero;

        // Follow mode state variables
        private bool isFollowingObject = false;
        private GameObject followedObject = null;
        private bool isFollowingMultipleObjects = false;
        private List<Renderer> followedRenderers = null;
        private float followBoundsPaddingFactor = 1.2f;
        [Tooltip("Smoothing time for bounds-based follow center/distance (seconds). 0 = no extra smoothing.")]
        public float FollowBoundsSmoothTime = 0.1f;
        private float followDistance = 5.0f;
        private bool allowRotationWhileFollowing = false;
        private bool allowZoomWhileFollowing = false;
        private float followLerpSpeed = 1.0f; // Lerp speed multiplier for follow mode (0.1 = smooth, 1.0 = tight, 10.0 = instant)
        private bool isInitialFollowLerp = false; // True when initially lerping to follow position
        private Vector3 followInitialTargetPos; // Initial target position when starting follow
        private Quaternion followInitialRotation; // Initial target rotation when starting follow
        private Vector3 smoothedFollowPoint;
        private Vector3 followPointVelocity;
        private float followDistanceVelocity;
        private bool hasSmoothedFollowPoint = false;
        private bool bypassUICheckThisFrame = false; // Allow StartFollowing() to bypass UI blocking for proper initialization
        private bool stopFollowOnMouseClick = false; // Stop following when user clicks any mouse button
        private bool isRidingTransform = false; // True when camera is "riding" the object (matching its transform), false for orbital follow

        // Navigation calculation constants
        private const float ROTATION_SPEED_BASE = 0.05f; // Base multiplier for rotation speed calculations
        private const float ROTATION_SPEED_MULTIPLIER = 100f; // Final multiplier for mouse rotation speed
        private const float TOUCH_ROTATION_MULTIPLIER = 400f; // Multiplier for touch rotation speed
        private const float TOUCH_ROTATION_SCALE = 0.001f; // Scale factor for touch rotation
        private const float ROTATION_LERP_SPEED = 3f; // Speed multiplier for rotation lerping in follow mode
        private const float ROTATION_DAMPING_MULTIPLIER = 6f; // Multiplier for rotation damping in general lerping
        private const float PAN_DAMPING_MULTIPLIER = 10f; // Multiplier for pan damping
        private const float ZOOM_SCROLL_FACTOR = 0.7f; // Factor for scroll wheel zoom sensitivity
        private const float ZOOM_DISTANCE_MULTIPLIER = 6f; // Multiplier for distance-based zoom scaling
        private const float ZOOM_DAMPING_MULTIPLIER = 10f; // Multiplier for zoom damping
        private const float ZOOM_SCROLL_BASE_SPEED = 0.05f; // Base speed for mouse scroll zoom
        private const float ZOOM_SCROLL_SENSITIVITY_MULTIPLIER = 65f; // Additional sensitivity multiplier for scroll zoom
        private const float TOUCH_ZOOM_SCALE = 0.0042f; // Scale factor for touch pinch zoom
        private const float ZOOM_DISTANCE_FACTOR_NEAR = 0.25f; // Distance factor when zooming on nearby objects (< 5 units)
        private const float ZOOM_DISTANCE_FACTOR_FAR = 1f; // Distance factor when zooming on distant objects (>= 5 units)
        private const float ZOOM_DISTANCE_THRESHOLD = 5f; // Distance threshold for switching between near and far zoom factors
        private const float ORTHO_PAN_SPEED_DIVISOR = 10f; // Divisor for orthographic view panning speed

        //checking focus on UI input field
        private VisualElement root;
        private FocusController focusController;

        // UIDocument caching for checking all UI Toolkit panels
        private UIDocument[] cachedUIDocuments;
        private float lastUIDocumentCacheTime;
        private const float UI_DOCUMENT_CACHE_INTERVAL = 2.0f;

        private UIDocument[] GetCachedUIDocuments()
        {
            if (cachedUIDocuments == null || Time.realtimeSinceStartup - lastUIDocumentCacheTime > UI_DOCUMENT_CACHE_INTERVAL)
            {
                cachedUIDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
                lastUIDocumentCacheTime = Time.realtimeSinceStartup;
            }
            return cachedUIDocuments;
        }

        // IsRotating Getter Setter
        private bool IsRotating
        {
            get => isRotating;
            set
            {
                if (isRotating != value)
                {
                    var oldValue = isRotating;
                    isRotating = value;
                    // Step 4: Call the delegate when the value changes.
                    EventStartStopRotation?.Invoke(isRotating);
                }
            }
        }

        // IsPanning Getter Setter
        private bool IsPanning
        {
            get => isPanning;
            set
            {
                if (isPanning != value)
                {
                    var oldValue = isPanning;
                    isPanning = value;
                    // Step 4: Call the delegate when the value changes.
                    EventStartStopPanning?.Invoke(isPanning);
                }
            }
        }

        private void Start()
        {
            selectionmanagernotnull = GetComponent<SelectionRaycast>() != null;
            if (selectionmanagernotnull)
            {
                selectionmanager = GetComponent<SelectionRaycast>();
                selectionmanager.EventSelected.AddListener(OnSelected);
                selectionmanager.EventBlockRotation.AddListener(BlockRotation);
                selectionmanager.EventMultiSelect.AddListener(OnMultiSelect);
            }

            if (LastCameraPosition != null)
                if (SetCameraPosOnStartPlay)
                    LastCameraPosition.SetCameraPositionPlaymode(this);

            // force to rotate with left mouse button if in webgl
#if UNITY_WEBGL
            RotateWithLeftMouseButton = true;
#endif
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument)
            {
                root = uiDocument.rootVisualElement;
                focusController = root.focusController;
            }
        }

        //! Check if any UI input field has focus — checks both classic Canvas TMP_InputField and UI Toolkit TextFields across all UIDocuments.
        private bool CheckForFocusedFields()
        {
            // Check 1: TMP_InputField focus via EventSystem (classic Canvas UI)
            var selectedObject = EventSystem.current?.currentSelectedGameObject;
            if (selectedObject != null)
            {
                var inputField = selectedObject.GetComponent<TMP_InputField>();
                if (inputField != null && inputField.isFocused)
                    return true;
            }

            // Check 2: UI Toolkit text field focus across ALL UIDocuments
            foreach (var doc in GetCachedUIDocuments())
            {
                if (doc == null || doc.rootVisualElement == null)
                    continue;

                var fc = doc.rootVisualElement.focusController;
                if (fc?.focusedElement == null)
                    continue;

                var focused = fc.focusedElement;

                // TextField or any text input element
                if (focused is TextField)
                    return true;

                // App UI SearchBar wraps a TextField — check class names as fallback
                if (focused is VisualElement focusedVe &&
                    (focusedVe.ClassListContains("unity-text-field") ||
                     focusedVe.ClassListContains("unity-text-input") ||
                     focusedVe.ClassListContains("appui-searchbar")))
                    return true;
            }

            return false;
        }

        //! Checks if pointer is over a visible UI element — supports both classic Canvas UI and UI Toolkit / App UI.
        //! Canvas UI is detected via EventSystem, UI Toolkit uses Pick + parent chain walk for robust detection.
        private bool IsPointerOverVisibleUIToolkitElement()
        {
            // Check 1: Classic Canvas/uGUI via EventSystem (preserves existing behavior)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // Verify it's a real Canvas hit, not a PanelRaycaster (UI Toolkit bridged into EventSystem)
                PointerEventData data = new PointerEventData(EventSystem.current);
                data.position = Input.mousePosition;
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(data, results);

                foreach (RaycastResult result in results)
                {
                    if (result.gameObject.GetComponentInChildren<PanelRaycaster>() == null)
                        return true; // Pure Canvas UI hit
                }
            }

            // Check 2: UI Toolkit — use Pick (topmost element) and walk parent chain
            Vector2 screenPos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

            foreach (var doc in GetCachedUIDocuments())
            {
                if (doc == null || doc.rootVisualElement == null)
                    continue;

                var panel = doc.rootVisualElement.panel;
                if (panel == null)
                    continue;

                Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
                VisualElement picked = panel.Pick(panelPos);

                // Nothing picked or picked the root itself → not over meaningful UI
                if (picked == null || picked == doc.rootVisualElement)
                    continue;

                // Walk from picked element up to root — if any ancestor is interactive or visible, block
                if (IsUIToolkitElementBlocking(picked, doc.rootVisualElement))
                    return true;
            }

            return false;
        }

        //! Walks up the parent chain from a picked element checking if it or any ancestor is a blocking UI element.
        private bool IsUIToolkitElementBlocking(VisualElement element, VisualElement root)
        {
            var current = element;
            while (current != null && current != root)
            {
                // Any element with a visible background blocks input
                if (current.resolvedStyle.backgroundColor.a > 0)
                    return true;

                // Interactive element types block input even if transparent
                if (current is ScrollView || current is Scroller || current is Button
                    || current is Slider || current is Toggle || current is DropdownField
                    || current is TextField || current is ListView || current is TreeView)
                    return true;

                current = current.parent;
            }
            return false;
        }

        private void LateUpdate()
        {
#if CINEMACHINE && REALVIRTUAL_PROFESSIONAL
            if (hmiController != null && hmiController.BlockUserMouseNavigation)
                return;
#endif

            // Handle continuous object following
            if (isFollowingObject)
            {
                bool hasValidFollowTarget = isFollowingMultipleObjects
                    ? (followedRenderers != null && followedRenderers.Count > 0)
                    : (followedObject != null);

                if (hasValidFollowTarget)
                {
                    // Check if user wants to stop following by clicking
                    if (stopFollowOnMouseClick &&
                        (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)))
                    {
                        Logger.Message("Stopped following due to mouse click", this);
                        StopFollowing();
                        return;
                    }

                    // Update follow mode - sets targetposition to object center and locks rotation/zoom if needed
                    UpdateFollowMode();

                    // Continue to normal flow - we need the lerping and final CalculateCamPos() to work properly
                    // User input is blocked by the checks in UpdateFollowMode() locking rotation/distance
                }
                else
                {
                    // Object(s) were destroyed or invalid, stop following
                    Logger.Warning("Followed object(s) were destroyed or invalid, stopping follow mode", this);
                    StopFollowing();
                    return;
                }
            }

            // if currently interpolating to new camera position, do not allow any other camera movement
            if (interpolatingToNewCameraPos)
            {
                InterpolateToNewCameraPos();
                return;
            }

            // if it is over a UI object nothing else exit
            // EXCEPTION 1: Allow rotation in follow mode even over UI elements for better control
            // EXCEPTION 2: Bypass UI check when StartFollowing() was just called to prevent double-click issue
            // EXCEPTION 3: Allow all camera operations in follow mode when rotation/zoom are allowed
            bool allowOverUI = bypassUICheckThisFrame ||
                               (isFollowingObject && (allowRotationWhileFollowing || allowZoomWhileFollowing));
            if (!allowOverUI && (IsPointerOverVisibleUIToolkitElement() || CheckForFocusedFields()))
                return;

            // Reset bypass flag after using it once
            bypassUICheckThisFrame = false;
            
          
            // Check Touch Status
            var istouching = false;
            var touchpanning = false;
            var starttouch = false;
            var touchrotate = false;
            var endtouch = false;
            var iszooming = false;
            var istwofinger = false;

            if (touchnotnull)
            {
                istouching = Touch.IsTouching;
                touchpanning = Touch.IsTwoFinger;
                starttouch = Touch.IsBeginPhase;
                istwofinger = Touch.IsTwoFinger;
                endtouch = Touch.IsEndPhase;
                iszooming = Touch.TwoFingerDeltaDistance != 0;
                // if touch is over UI do nothing
                if (Touch.IsOverUI)
                    return;

                if (istouching && !touchpanning)
                    touchrotate = true;
            }

            // check for shift and set status for later use
            var shift = false;
            float shiftfactor = 1;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                shift = true;
                shiftfactor = 0.5f;
            }


            // sets the button for rotation (in web it must be left mouse button)
            var buttonrotate = 1;
            if (RotateWithLeftMouseButton)
                buttonrotate = 0;

            // check if camera is in overlay orthograhic view - for navigation in view and not in main camera
            var MouseInOrthoCamera = CheckMouseOverOrthoView();

            // leaeve if first person controller is active
            if (FirstPersonControllerActive)
                return;


            if (UseOrbitCameraMode)
                return;

            // if cinemachiune is active and there is any mouse activity, deactivate cinemachine
            if (CinemachineIsActive)
            {
                var scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Input.GetMouseButton(2) || Input.GetMouseButton(3) || Input.GetMouseButton(1)
                    || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.LeftArrow) ||
                    Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.UpArrow) || Math.Abs(scroll) > 0.001f ||
                    Input.GetKey(KeyCode.Escape))
                    ActivateCinemachine(false);
            }

            // is panning starting? Set init position when mouse bottom is going down
            // Block panning completely when in follow mode (object must stay centered)
            if (!isFollowingObject && (Input.GetMouseButtonDown(2) || starttouch))
            {
                startpanhitdistance = GetClosestHitDistance();
                if (startpanhitdistance == 0)
                    startpanhitdistance = 3.0f;
                raycastto0planebefore = RaycastToPanPlane(istouching, startpanhitdistance);
                targetposition = target.position;
            }

            // is rotation starting?
            // Allow rotation only if: not in follow mode OR in follow mode with rotation allowed
            if ((!isFollowingObject || allowRotationWhileFollowing) && (Input.GetMouseButtonDown(buttonrotate) || starttouch))
                if (!blockrotation)
                {
                    StartRotation();
                }

            //  ending of rotation on buttonrotate up
            if (Input.GetMouseButtonUp(buttonrotate) || endtouch || istwofinger)
            {
                EndRotation();
            }

            // If pannig is active raycasst to the pan plane
            // Block panning when in follow mode
            if (!isFollowingObject && (Input.GetMouseButton(2) || touchpanning))
            {
                raycastto0plane = RaycastToPanPlane(istouching, startpanhitdistance);
                IsPanning = true;
            }

            if (!Input.GetMouseButton(2) && !touchpanning) IsPanning = false;

            // If Control and Middle button? ZOOM!
            if (Input.GetMouseButton(2) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                desiredDistance -= Input.GetAxis("Mouse Y") * Time.deltaTime * ZoomSpeed * MasterSensitivity * shiftfactor * ZOOM_SCROLL_FACTOR *
                                   DPIScale * ZOOM_DISTANCE_MULTIPLIER * Mathf.Abs(currentDistance);
            }

            // If right mouse (or left if rotation is with left) is selected ORBIT
            else if (IsRotating && !blockrotation && !(blockleftmouserotation && RotateWithLeftMouseButton))
            {
                _lastmovement = Time.realtimeSinceStartup;

                // Handle rotation differently for follow mode vs normal mode
                if (isFollowingObject && allowRotationWhileFollowing)
                {
                    // In follow mode, just update the rotation angles
                    // The target system will handle the actual camera positioning
                    GetRotationInput(touchrotate, shiftfactor, out float mouseX, out float mouseY);
                    xDeg += mouseX;
                    yDeg -= mouseY;

                    // Clamp the vertical axis for the orbit
                    yDeg = ClampAngle(yDeg, MinHorizontalRotation, MaxHorizontalRotation);

                    // Set camera rotation for follow mode
                    desiredRotation = Quaternion.Euler(yDeg, xDeg, 0);
                    currentRotation = transform.rotation;
                    rotation = Quaternion.Lerp(currentRotation, desiredRotation, Time.deltaTime * RotDamping * ROTATION_LERP_SPEED);
                    transform.rotation = rotation;
                }
                else if (bypassTargetSystemDuringRotation)
                {
                    // Normal rotation mode with bypass - original code
                    // Implement pure orbital rotation around fixed pivot point
                    GetRotationInput(touchrotate, shiftfactor, out float mouseX, out float mouseY);

                    // Use Unity's RotateAround for stable orbital rotation
                    if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
                    {
                        // Store original distance to maintain perfect orbital motion
                        Vector3 originalOffset = transform.position - rotationPivotPoint;
                        float originalDistance = originalOffset.magnitude;

                        // Horizontal rotation around world up axis
                        if (Mathf.Abs(mouseX) > 0.001f)
                        {
                            transform.RotateAround(rotationPivotPoint, Vector3.up, mouseX);
                        }

                        // Vertical rotation around camera's local right axis (inverted for natural movement)
                        if (Mathf.Abs(mouseY) > 0.001f)
                        {
                            Vector3 rightAxis = transform.right;

                            // Check if this rotation would cause flipping
                            Vector3 testDirection = transform.position - rotationPivotPoint;
                            Vector3 testRotated = Quaternion.AngleAxis(-mouseY, rightAxis) * testDirection;
                            float testElevation = Mathf.Asin(testRotated.y / testRotated.magnitude) * Mathf.Rad2Deg;

                            // Only apply rotation if it doesn't exceed limits
                            if (testElevation > -89f && testElevation < 89f)
                            {
                                transform.RotateAround(rotationPivotPoint, rightAxis, -mouseY);
                            }
                        }

                        // Ensure perfect distance preservation (correct any floating point drift)
                        Vector3 currentOffset = transform.position - rotationPivotPoint;
                        float currentDistance = currentOffset.magnitude;
                        if (Mathf.Abs(currentDistance - originalDistance) > 0.001f)
                        {
                            Vector3 correctedOffset = currentOffset.normalized * originalDistance;
                            transform.position = rotationPivotPoint + correctedOffset;
                        }

                        // Update internal rotation tracking for smooth transitions
                        desiredRotation = transform.rotation;
                        xDeg = transform.rotation.eulerAngles.y;
                        yDeg = transform.rotation.eulerAngles.x;
                    }
                }
                else
                {
                    // Original rotation logic for normal target-based rotation
                    GetRotationInput(touchrotate, shiftfactor, out float mouseX, out float mouseY);
                    xDeg += mouseX;
                    yDeg -= mouseY;

                    //Clamp the vertical axis for the orbit
                    yDeg = ClampAngle(yDeg, MinHorizontalRotation, MaxHorizontalRotation);
                    // set camera rotation
                    desiredRotation = Quaternion.Euler(yDeg, xDeg, 0);
                    currentRotation = transform.rotation;
                    rotation = Quaternion.Lerp(currentRotation, desiredRotation, Time.deltaTime * RotDamping * ROTATION_LERP_SPEED);
                    transform.rotation = rotation;
                }
            }
            // otherwise if middle mouse is selected, we pan by way of transforming the target in screenspace
            // Block panning when in follow mode
            else if (!isFollowingObject && (Input.GetMouseButton(2) || touchpanning))
            {
                if (!MouseInOrthoCamera)
                {
                    var delta = raycastto0planebefore - raycastto0plane;
                    _lastmovement = Time.realtimeSinceStartup;
                    var vec = delta;
                    var norm = mycamera.transform.forward;
                    vec -= norm * Vector3.Dot(vec, norm); //the result is parallel to the plane defined by norm
                    targetposition = targetposition + vec;
                }
                else
                {
                    if (orthoviewcontroller != null)
                    {
                        if (incamera.name == "Side")
                        {
                            orthoviewcontroller.transform.Translate(Vector3.right * -Input.GetAxis("Mouse X") *
                                orthoPanSpeed * MasterSensitivity * shiftfactor * orthoviewcontroller.Distance / ORTHO_PAN_SPEED_DIVISOR);
                            orthoviewcontroller.transform.Translate(Vector3.up * -Input.GetAxis("Mouse Y") *
                                orthoPanSpeed * MasterSensitivity * shiftfactor * orthoviewcontroller.Distance / ORTHO_PAN_SPEED_DIVISOR);
                        }

                        if (incamera.name == "Top")
                        {
                            orthoviewcontroller.transform.Translate(new Vector3(0, 0, -1) * -Input.GetAxis("Mouse X") *
                                orthoPanSpeed * MasterSensitivity * shiftfactor * orthoviewcontroller.Distance / ORTHO_PAN_SPEED_DIVISOR);
                            orthoviewcontroller.transform.Translate(new Vector3(1, 0, 0) * -Input.GetAxis("Mouse Y") *
                                orthoPanSpeed * MasterSensitivity * shiftfactor * orthoviewcontroller.Distance / ORTHO_PAN_SPEED_DIVISOR);
                        }

                        if (incamera.name == "Front")
                        {
                            orthoviewcontroller.transform.Translate(new Vector3(0, 0, -1) * -Input.GetAxis("Mouse X") *
                                orthoPanSpeed * MasterSensitivity * orthoviewcontroller.Distance / ORTHO_PAN_SPEED_DIVISOR);
                            orthoviewcontroller.transform.Translate(new Vector3(0, 1, 0) * -Input.GetAxis("Mouse Y") *
                                orthoPanSpeed * MasterSensitivity * orthoviewcontroller.Distance / ORTHO_PAN_SPEED_DIVISOR);
                        }
                    }
                }
            }

            /// Zoom in and out
            // affect the desired Zoom distance if we roll the scrollwheel
            // Allow zoom only if: not in follow mode OR in follow mode with zoom allowed
            var mousescroll = Input.GetAxis("Mouse ScrollWheel");
            if ((!isFollowingObject || allowZoomWhileFollowing) && (mousescroll != 0 || istwofinger))
            {
                var distancefactor = ZOOM_DISTANCE_FACTOR_NEAR;
                zoomhitdistance = GetClosestHitDistance();
                if (zoomhitdistance > ZOOM_DISTANCE_THRESHOLD)
                    distancefactor = ZOOM_DISTANCE_FACTOR_FAR;
                _lastmovement = Time.realtimeSinceStartup;
                if (!MouseInOrthoCamera)
                {
                    if (!iszooming)
                    {
                        desiredDistance -= mousescroll * distancefactor * ZOOM_SCROLL_BASE_SPEED * shiftfactor * ZoomSpeed * MasterSensitivity * ZOOM_SCROLL_FACTOR * ZOOM_SCROLL_SENSITIVITY_MULTIPLIER *
                                           Mathf.Abs(currentDistance);
                    }
                    else
                    {
#if UNITY_WEBGL && !UNITY_EDITOR
                        desiredDistance -=
 Touch.TwoFingerDeltaDistance * shiftfactor * TouchZoomSpeed * MasterSensitivity * TOUCH_ZOOM_SCALE * DPIScale*
                                           Mathf.Abs(currentDistance);
#else
                        desiredDistance -= Touch.TwoFingerDeltaDistance * shiftfactor * TouchZoomSpeed * MasterSensitivity * TOUCH_ZOOM_SCALE *
                                           DPIScale *
                                           Mathf.Abs(currentDistance);
#endif
                    }
                }
                else
                {
                    if (orthoviewcontroller != null)
                    {
                        orthoviewcontroller.Distance += mousescroll * orthoviewcontroller.Distance;
                        orthoviewcontroller.UpdateViews();
                    }
                }

                if (desiredDistance > maxdistance)
                    desiredDistance = maxdistance;
                if (desiredDistance <= minDistance)
                {
                    var delta = minDistance - desiredDistance;
                    desiredDistance = minDistance;
                    // move targetposition in delta in camera view direction
                    targetposition = targetposition + mycamera.transform.forward * delta;
                }
            }

            var focuspressed = false;
            if (Input.GetKey(realvirtualController.HotKeyFocus)) focuspressed = true;

            // Block focus/reset keys when in follow mode
            if (isFollowingObject)
                focuspressed = false;

            // if hotkey focus is pressed and selectionamanger has a selected object, focus it
            if (selectionmanagernotnull)
            {
                if ((focuspressed || (selectionmanager.DoubleSelect &&
                                      (selectionmanager.FocusDoubleClickedObject ||
                                       selectionmanager.ZoomDoubleClickedObject))) &&
                    selectionmanager.SelectedObject != null)
                {
                    focuspressed = false;
                    bool shouldZoom = selectionmanager.ZoomDoubleClickedObject || Input.GetKey(KeyCode.F);
                    ApplyFocusToSelectedObject(shouldZoom);
                }

                if (selectionmanager.SelectedObject != null &&
                    (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt) ||
                     selectionmanager.AutoCenterSelectedObject))
                {
                    ApplyFocusToSelectedObject(false);
                }
            }

            // if F is pressed without anything selected or relection manager focus the whole scene
            // Block when in follow mode
            if (!isFollowingObject && (focuspressed || Input.GetKey(realvirtualController.HotKeyResetView)))
            {
                var dist = CalculateFocusViewDistance(FindObjectsByType<Renderer>(FindObjectsSortMode.None));
                targetposition = calculatedboundscenter;
                desiredDistance = distance;
            }


            if (!MouseInOrthoCamera)
            {
                // Block key navigation when in follow mode
                if (!isFollowingObject)
                {
                    // Key Navigation
                    var control = false;
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                        control = true;
                    // Key 3D Navigation
                    if (Input.GetKey(KeyCode.UpArrow) && shift && !control)
                        CameraTransform(mycamera.transform.forward);

                if (Input.GetKey(KeyCode.DownArrow) && shift && !control)
                    CameraTransform(-mycamera.transform.forward);

                if (Input.GetKey(KeyCode.UpArrow) && !shift && !control)
                    CameraTransform(mycamera.transform.up);

                if (Input.GetKey(KeyCode.DownArrow) && !shift && !control)
                    CameraTransform(-mycamera.transform.up);

                if (Input.GetKey(KeyCode.RightArrow) && !control)
                    CameraTransform(mycamera.transform.right);

                    if (Input.GetKey(KeyCode.LeftArrow) && !control)
                        CameraTransform(-mycamera.transform.right);

                    if (realvirtualController.EnableHotkeys)
                    {
                        if (Input.GetKey(realvirtualController.HotKeyTopView)) SetViewDirection(new Vector3(90, 90, 0));

                    if (Input.GetKey(realvirtualController.HotKeyFrontView))
                        if (selectionmanagernotnull && realvirtualController.HotKeyFrontView ==
                            realvirtualController.HotKeyFocus)
                            if (selectionmanager.SelectedObject == null)
                                SetViewDirection(new Vector3(0, 90, 0));
                            else
                                SetViewDirection(new Vector3(0, 90, 0));

                    if (Input.GetKey(realvirtualController.HotKeyBackView)) SetViewDirection(new Vector3(0, 180, 0));

                    if (Input.GetKey(realvirtualController.HotKeyLeftView)) SetViewDirection(new Vector3(0, 180, 0));

                        if (Input.GetKey(realvirtualController.HotKeyRightView)) SetViewDirection(new Vector3(0, 0, 0));
                    }
                }
            }
            else
            {
                if (realvirtualController.EnableHotkeys)
                {
                    if (Input.GetKeyDown(realvirtualController.HotKeyOrhtoBigger))
                        orthoviewcontroller.Size += 0.05f;
                    if (Input.GetKeyDown(realvirtualController.HotKeyOrhtoSmaller))
                        orthoviewcontroller.Size -= 0.05f;
                    if (orthoviewcontroller.Size > 0.45f)
                        orthoviewcontroller.Size = 0.45f;
                    if (orthoviewcontroller.Size < 0.1f)
                        orthoviewcontroller.Size = 0.1f;
                    if (Input.GetKeyDown(realvirtualController.HoteKeyOrthoDirection))
                        orthoviewcontroller.Angle += 90;
                    if (orthoviewcontroller.Angle >= 360)
                        orthoviewcontroller.Angle = 0;
                    orthoviewcontroller.UpdateViews();
                }
            }

            if (realvirtualController.EnableHotkeys)
                if (Input.GetKeyDown(realvirtualController.HotKeyOrthoViews))
                {
                    orthoviewcontroller.OrthoEnabled = !orthoviewcontroller.OrthoEnabled;
                    var button =
                        Global.GetComponentByName<GenericButton>(realvirtualController.gameObject, "OrthoViews");
                    if (button != null)
                        button.SetStatus(orthoviewcontroller.OrthoEnabled);
                    orthoviewcontroller.UpdateViews();
                }

            if (mycamera.orthographic)
            {
                mycamera.orthographicSize += mousescroll * mycamera.orthographicSize;
                desiredDistance = 0;
            }

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && !UNITY_WEBGL
            // Space Navigator
            if (EnableSpaceNavigator)
            {
                if (SpaceNavigator.Translation != Vector3.zero)
                {
                    target.rotation = transform.rotation;
                    var spacetrans = SpaceNavigator.Translation;
                    var newtrans = new Vector3(-spacetrans.x, spacetrans.y, -spacetrans.z) * SpaceNavTransSpeed;
                    target.Translate(newtrans, Space.Self);
                }

                if (SpaceNavigator.Rotation.eulerAngles != Vector3.zero)
                {
                    transform.Rotate(-SpaceNavigator.Rotation.eulerAngles);
                    rotation = transform.rotation;
                }
            }
#endif
            currentRotation = transform.rotation;

            // Don't lerp rotation during follow mode with locked rotation - it's already set in UpdateFollowMode
            // Also skip during initial follow lerp as rotation is handled in UpdateFollowMode
            // CRITICAL: Skip rotation lerp when in bypass mode - rotation is controlled directly by RotateAround
            if ((!isFollowingObject || allowRotationWhileFollowing) && !isInitialFollowLerp && !bypassTargetSystemDuringRotation)
            {
                if (desiredRotation != currentRotation)
                {
                    rotation = Quaternion.Lerp(currentRotation, desiredRotation, Time.deltaTime * RotDamping * ROTATION_DAMPING_MULTIPLIER);
                    transform.rotation = rotation;
                }
            }

            // Lerp the target movement
            // During follow mode, the lerping is already handled in UpdateFollowMode with custom speed
            // So skip additional lerping here to avoid double-smoothing
            if (!isFollowingObject)
            {
                target.position = Vector3.Lerp(target.position, targetposition, Time.deltaTime * PanDamping * PAN_DAMPING_MULTIPLIER);
            }
            //target.position = targetposition;

            // For smoothing of the zoom, lerp distance
            // During initial follow lerp, distance is handled in UpdateFollowMode with custom lerp speed
            if (!isInitialFollowLerp)
            {
                currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * ZoomDamping * ZOOM_DAMPING_MULTIPLIER);
            }

            // calculate position based on the new currentDistance 
            CalculateCamPos();

            zoomposworld = Vector3.zero;
            DurationNoMouseActivity = Time.realtimeSinceStartup - _lastmovement;
            raycastto0planebefore = RaycastToPanPlane(istouching, startpanhitdistance);
            blockrotationbefore = blockrotation;

#if CINEMACHINE
            if (Time.realtimeSinceStartup - _lastmovement > StartDemoOnInactivity)
                if (!CinemachineIsActive)
                    ActivateCinemachine(true);
#endif
        }

        //! is called when the component is enabled
        private void OnEnable()
        {
            if (realvirtualController == null)
            {
                Awake();
            }
            
            
            if (Touch != null) touchnotnull = true;
            Init();
        }

        //! is called when the application is left - e.g. for saving last camera position
        private void OnApplicationQuit()
        {
            if (LastCameraPosition != null)
                if (SaveCameraPosOnQuit)
                    LastCameraPosition.SaveCameraPosition(this);
        }

        //! is called when orthographic overlay views button is pressed in the main menu bar
        public void OnButtonOrthoOverlay(GenericButton button)
        {
            orthoviewcontroller.OrthoEnabled = button.IsOn;
            orthoviewcontroller.UpdateViews();
        }

        public void OrthoOverlayToggleOn()
        {
            orthoviewcontroller.OrthoEnabled = true;
            orthoviewcontroller.UpdateViews();
        }

        public void OrthoOverlayToggleOff()
        {
            orthoviewcontroller.OrthoEnabled = false;
            orthoviewcontroller.UpdateViews();
        }

        //! is called when orthographic view is enabled or disabled
        public void OnButtonOrthographicView(GenericButton button)
        {
            SetOrthographicView(button.IsOn);
        }

        //! is called when orthographic view is enabled or disabled
        public void OrthographicViewToggleOn()
        {
            SetOrthographicView(true);
        }

        public void OrthographicViewToggleOff()
        {
            SetOrthographicView(false);
        }

        public void SetOrthographicView(bool active)
        {
            if (active == orthograhicview && Application.isPlaying)
                return; /// no changes
            orthograhicview = active;
            if (mycamera == null)
                mycamera = GetComponent<Camera>();
            mycamera.orthographic = active;
            if (!active)
            {
                desiredDistance = lastperspectivedistance;
                mycamera.farClipPlane = 5000f;
                mycamera.nearClipPlane = 0.1f;
            }
            else
            {
                lastperspectivedistance = desiredDistance;
                mycamera.farClipPlane = 5000f;
                mycamera.nearClipPlane = -5000f;
            }

            // change button in UI
            var button = Global.GetComponentByName<GenericButton>(Global.realvirtualcontroller.gameObject, "Perspective");
            if (button != null)
                if (button.IsOn != active)
                    button.SetStatus(active);
        }

        private void OnMultiSelect(bool multisel)
        {
        }

        //! Blocks the rotation of the camera e.g. when an element is selected with left mouse and left mouse rotation is on
        public void BlockRotation(bool block, bool onlyleftmouse)
        {
            if (block)
                IsRotating = false;


            blockrotation = block;

            if (onlyleftmouse)
            {
                blockrotation = false;
                blockleftmouserotation = block;
            }
        }

        //! is called by object selection manager when an object is selected
        private void OnSelected(GameObject go, bool selected, bool multiselect, bool changedSelection)
        {
            var touch = Input.touchCount > 0;
            if ((RotateWithLeftMouseButton || touch) && selected) BlockRotation(true, true);
            if ((RotateWithLeftMouseButton || touch) && !selected) BlockRotation(false, true);
        }

        //! called by main menu view button to switch between first person controller and normal mouse navigation
        public void OnViewButton(GenericButton button)
        {
            if (button.IsOn && FirstPersonController != null)
            {
                SetOrthographicView(false);
                if (CinemachineIsActive)
                    ActivateCinemachine(false);
                Global.SetActiveIncludingSubObjects(FirstPersonController.gameObject, true);
                FirstPersonControllerActive = true;
                FirstPersonController.SetActive(true);
            }
            else
            {
                FirstPersonControllerActive = false;
                Global.SetActiveIncludingSubObjects(FirstPersonController.gameObject, false);
            }
        }

        public void ViewButtonToggleOn()
        {
            if (FirstPersonController != null)
            {
                SetOrthographicView(false);
                if (CinemachineIsActive)
                    ActivateCinemachine(false);
                Global.SetActiveIncludingSubObjects(FirstPersonController.gameObject, true);
                FirstPersonControllerActive = true;
                FirstPersonController.SetActive(true);
            }
        }

        public void ViewButtonToggleOff()
        {
            if (FirstPersonController != null)
            {
                FirstPersonControllerActive = false;
                Global.SetActiveIncludingSubObjects(FirstPersonController.gameObject, false);
            }
        }

        private void CalculateCamPos()
        {
            // Skip camera positioning when bypassing target system during rotation
            if (bypassTargetSystemDuringRotation)
            {
                // Camera position is controlled directly by RotateAround - don't interfere
                return;
            }

            // Normal camera position calculation based on target
            // This formula works for both normal mode AND follow mode
            // In follow mode, target.position is the object center, which gets centered in view
            position = target.position - (rotation * Vector3.forward * currentDistance + targetOffset);

            if (position != transform.position)
                transform.position = position;
        }

        private bool TryGetFollowBounds(out Bounds combinedBounds)
        {
            combinedBounds = new Bounds();

            if (followedRenderers == null || followedRenderers.Count == 0)
                return false;

            bool boundsInitialized = false;
            foreach (var renderer in followedRenderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    if (!boundsInitialized)
                    {
                        combinedBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return boundsInitialized;
        }

        private bool TryGetFollowPoint(out Vector3 followPoint, out Bounds combinedBounds)
        {
            combinedBounds = new Bounds();

            if (isFollowingMultipleObjects)
            {
                if (!TryGetFollowBounds(out combinedBounds))
                {
                    followPoint = Vector3.zero;
                    return false;
                }

                followPoint = combinedBounds.center;
                return true;
            }

            if (followedObject == null)
            {
                followPoint = Vector3.zero;
                return false;
            }

            Renderer[] activeRenderers = GetActiveRenderers(followedObject);
            if (activeRenderers.Length > 0)
            {
                followPoint = GetBoundsCenterFromRenderers(activeRenderers);
            }
            else
            {
                followPoint = followedObject.transform.position;
            }
            return true;
        }

        private float CalculateDistanceFromBounds(Bounds bounds, float paddingFactor)
        {
            Vector3 objectSizes = bounds.max - bounds.min;
            float objectSize = Mathf.Max(objectSizes.x, objectSizes.y, objectSizes.z);

            objectSize *= paddingFactor;

            float cameraView = 2.0f * Mathf.Tan(0.5f * Mathf.Deg2Rad * mycamera.fieldOfView);
            float targetDistance = objectSize / (0.33f * cameraView);

            targetDistance += 0.3f * objectSize;
            targetDistance = Mathf.Max(targetDistance, minDistance);
            if (maxdistance > 0)
                targetDistance = Mathf.Min(targetDistance, maxdistance);

            return targetDistance;
        }

        private Renderer[] GetActiveRenderers(GameObject obj)
        {
            if (obj == null)
                return new Renderer[0];

            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            List<Renderer> activeRenderers = new List<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    activeRenderers.Add(renderer);
            }

            return activeRenderers.ToArray();
        }

        private Vector3 GetBoundsCenterFromRenderers(Renderer[] renderers)
        {
            var combinedBounds = new Bounds();
            bool boundsInitialized = false;

            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    if (!boundsInitialized)
                    {
                        combinedBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            return boundsInitialized ? combinedBounds.center : Vector3.zero;
        }

        //! Updates camera position and rotation when in follow mode to continuously track the followed object
        private void UpdateFollowMode()
        {
            // ===== RIDING MODE: Camera directly matches object's transform =====
            if (isRidingTransform)
            {
                if (followedObject == null)
                {
                    Logger.Warning("Riding target lost, stopping follow mode", this);
                    StopFollowing();
                    return;
                }

                Vector3 targetPos = followedObject.transform.position;
                Quaternion targetRot = followedObject.transform.rotation;

                float rideLerp = Time.deltaTime * followLerpSpeed * 10f;
                rideLerp = Mathf.Clamp01(rideLerp);

                if (isInitialFollowLerp)
                {
                    transform.position = Vector3.Lerp(transform.position, targetPos, rideLerp);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rideLerp);

                    rotation = transform.rotation;
                    currentRotation = rotation;
                    desiredRotation = rotation;

                    float distToTarget = Vector3.Distance(transform.position, targetPos);
                    float angleDiff = Quaternion.Angle(transform.rotation, targetRot);

                    if (distToTarget < 0.05f && angleDiff < 0.5f)
                        isInitialFollowLerp = false;
                }
                else
                {
                    transform.position = Vector3.Lerp(transform.position, targetPos, rideLerp);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rideLerp);

                    rotation = transform.rotation;
                    currentRotation = rotation;
                    desiredRotation = rotation;
                }

                target.position = targetPos;
                targetposition = targetPos;
                xDeg = rotation.eulerAngles.y;
                yDeg = rotation.eulerAngles.x;

                return; // Riding mode handles everything directly
            }

            // ===== ORBITAL FOLLOW MODE =====
            if (!TryGetFollowPoint(out Vector3 followPoint, out Bounds combinedBounds))
            {
                Logger.Warning("Follow target lost, stopping follow mode", this);
                StopFollowing();
                return;
            }

            if (FollowBoundsSmoothTime > 0f)
            {
                if (!hasSmoothedFollowPoint)
                {
                    smoothedFollowPoint = followPoint;
                    followPointVelocity = Vector3.zero;
                    hasSmoothedFollowPoint = true;
                }
                else
                {
                    smoothedFollowPoint = Vector3.SmoothDamp(
                        smoothedFollowPoint,
                        followPoint,
                        ref followPointVelocity,
                        FollowBoundsSmoothTime);
                }

                followPoint = smoothedFollowPoint;
            }

            if (isFollowingMultipleObjects && !allowZoomWhileFollowing)
            {
                float targetDistance = CalculateDistanceFromBounds(combinedBounds, followBoundsPaddingFactor);
                if (FollowBoundsSmoothTime > 0f)
                {
                    followDistance = Mathf.SmoothDamp(
                        followDistance,
                        targetDistance,
                        ref followDistanceVelocity,
                        FollowBoundsSmoothTime);
                }
                else
                {
                    followDistance = targetDistance;
                }
            }
            else if (!isFollowingMultipleObjects && !allowZoomWhileFollowing && followedObject != null)
            {
                Renderer[] activeRenderers = GetActiveRenderers(followedObject);
                if (activeRenderers.Length > 0)
                {
                    Bounds bounds = new Bounds();
                    bool boundsInitialized = false;
                    foreach (var renderer in activeRenderers)
                    {
                        if (!boundsInitialized)
                        {
                            bounds = renderer.bounds;
                            boundsInitialized = true;
                        }
                        else
                        {
                            bounds.Encapsulate(renderer.bounds);
                        }
                    }

                    if (boundsInitialized)
                    {
                        float targetDistance = CalculateDistanceFromBounds(bounds, 1.0f);
                        if (FollowBoundsSmoothTime > 0f)
                        {
                            followDistance = Mathf.SmoothDamp(
                                followDistance,
                                targetDistance,
                                ref followDistanceVelocity,
                                FollowBoundsSmoothTime);
                        }
                        else
                        {
                            followDistance = targetDistance;
                        }
                    }
                }
            }

            // CRITICAL: Reset targetOffset to zero for pure orbital centering
            targetOffset = Vector3.zero;

            // Apply custom lerp speed for smooth or tight tracking
            // Higher lerp speed = tighter tracking, lower = smoother motion
            float lerpFactor = Time.deltaTime * followLerpSpeed * 10f;
            lerpFactor = Mathf.Clamp01(lerpFactor); // Ensure it stays between 0 and 1

            // Handle initial lerp phase when following first starts
            if (isInitialFollowLerp)
            {
                // Lerp to the initial follow position smoothly
                target.position = Vector3.Lerp(target.position, followInitialTargetPos, lerpFactor);
                targetposition = Vector3.Lerp(targetposition, followInitialTargetPos, lerpFactor);

                // Also lerp the rotation to the initial look-at angle
                Quaternion targetRotation = followInitialRotation;
                rotation = Quaternion.Slerp(rotation, targetRotation, lerpFactor);
                currentRotation = rotation;
                desiredRotation = rotation;

                // Apply the rotation to the transform immediately for smooth visual update
                transform.rotation = rotation;

                // Sync angle tracking
                xDeg = rotation.eulerAngles.y;
                yDeg = rotation.eulerAngles.x;

                // CRITICAL: Lerp the distance smoothly during initial phase
                // This prevents the camera from jumping due to aggressive zoom lerping
                currentDistance = Mathf.Lerp(currentDistance, followDistance, lerpFactor);
                desiredDistance = followDistance;

                // Check if we're close enough to the initial target to exit initial lerp
                float distToTarget = Vector3.Distance(target.position, followInitialTargetPos);
                float angleDiff = Quaternion.Angle(desiredRotation, followInitialRotation);
                float distDiff = Mathf.Abs(currentDistance - followDistance);

                if (distToTarget < 0.1f && angleDiff < 1.0f && distDiff < 0.1f)
                {
                    // Close enough, exit initial lerp phase
                    isInitialFollowLerp = false;
                    target.position = followInitialTargetPos;
                    targetposition = followInitialTargetPos;
                    desiredRotation = followInitialRotation;
                    currentDistance = followDistance;
                    desiredDistance = followDistance;
                }
            }
            else
            {
                // Normal follow mode - smoothly track the moving object
                target.position = Vector3.Lerp(target.position, followPoint, lerpFactor);
                targetposition = Vector3.Lerp(targetposition, followPoint, lerpFactor);
            }

            // Handle distance locking if zoom is not allowed
            if (!allowZoomWhileFollowing)
            {
                desiredDistance = followDistance;
                currentDistance = followDistance;
            }

            // CRITICAL FIX: Continuously recalculate rotation to keep object centered
            if (!allowRotationWhileFollowing)
            {
                if (!isInitialFollowLerp)
                {
                    Vector3 directionToObject = (target.position - transform.position).normalized;

                    if (directionToObject.sqrMagnitude > 0.001f)
                    {
                        Quaternion lookRotation = Quaternion.LookRotation(directionToObject);

                        float rotLerpFactor = Time.deltaTime * followLerpSpeed * 10f;
                        rotLerpFactor = Mathf.Clamp01(rotLerpFactor);

                        rotation = Quaternion.Slerp(rotation, lookRotation, rotLerpFactor);
                        currentRotation = rotation;
                        desiredRotation = rotation;

                        xDeg = rotation.eulerAngles.y;
                        yDeg = rotation.eulerAngles.x;

                        transform.rotation = rotation;
                    }
                }
            }
        }

        private Vector3 GetCurrentFollowCenter()
        {
            if (isFollowingMultipleObjects)
            {
                if (TryGetFollowBounds(out Bounds bounds))
                    return bounds.center;

                return target.position;
            }

            if (followedObject != null)
            {
                Renderer[] activeRenderers = GetActiveRenderers(followedObject);
                if (activeRenderers.Length > 0)
                    return GetBoundsCenterFromRenderers(activeRenderers);

                return followedObject.transform.position;
            }

            return target.position;
        }

        //! sets a new camera position based ob targetos, distance to targetois and rotation
        public void SetNewCameraPosition(Vector3 targetpos, float camdistance, Vector3 camrotation,
            bool nointerpolate = false)
        {
            // End first person controller if it is on
            if (FirstPersonControllerActive)
            {
                FirstPersonController.SetActive(false);
                FirstPersonControllerActive = false;
            }

            if (target == null)
                return;
            if (InterpolateToNewCamerapoitions && !nointerpolate)
            {
                interpolatingToNewCameraPos = true;
                interpolaterotation = camrotation;
                interpolatedistance = camdistance;
                interpolatetargetpos = targetpos;
                interpolatenewcamrot = Quaternion.Euler(camrotation);
                interpolatenewcampos = interpolatetargetpos -
                                       (interpolatenewcamrot * Vector3.forward * interpolatedistance + targetOffset);
                EventStartStopCameraInterpolation?.Invoke(true);
                InterpolateToNewCameraPos();
                return;
            }

            desiredDistance = camdistance;
            currentDistance = camdistance;
            target.position = targetpos;
            targetposition = targetpos;
            desiredRotation = Quaternion.Euler(camrotation);
            currentRotation = Quaternion.Euler(camrotation);
            rotation = Quaternion.Euler(camrotation);
            transform.rotation = Quaternion.Euler(camrotation);

            CalculateCamPos();
        }

        private void InterpolateToNewCameraPos()
        {
            var atpos = false;
            var atrot = false;
            // lerping to new camera position
            if (Vector3.Distance(transform.position, interpolatenewcampos) > 0.01f)
                transform.position = Vector3.Lerp(transform.position, interpolatenewcampos,
                    Time.deltaTime * CameraInterpolationSpeed * 5);
            else
                atpos = true;

            // lerp to new rotation
            if (Quaternion.Angle(transform.rotation, interpolatenewcamrot) > 0.01f)
                transform.rotation = Quaternion.Lerp(transform.rotation, interpolatenewcamrot,
                    Time.deltaTime * CameraInterpolationSpeed * 5);
            else
                atrot = true;

            if (atpos && atrot)
            {
                interpolatingToNewCameraPos = false;
                SetNewCameraPosition(interpolatetargetpos, interpolatedistance, interpolaterotation, true);
                EventStartStopCameraInterpolation?.Invoke(false);
            }
        }

        //! sets the camera view direction based on a vector
        public void SetViewDirection(Vector3 camrotation)
        {
            desiredRotation = Quaternion.Euler(camrotation);
            currentRotation = Quaternion.Euler(camrotation);
            rotation = Quaternion.Euler(camrotation);
            transform.rotation = Quaternion.Euler(camrotation);
        }



        //! Performs a one-time camera movement to focus on multiple GameObjects.
        //! The camera will smoothly move to view all objects in the list but will not continuously track them.
        //! Calculates combined bounds of all objects and positions camera to fit all objects in view.
        //!
        //! Parameters:
        //! - objectsToFocus: List of GameObjects to focus on (required, must not be empty)
        //! - paddingFactor: Multiplier for extra padding around combined bounds (default 1.2 = 20% padding)
        //!
        //! Example usage:
        //! - FocusOnObjects(selectedParts) - Focus on list of parts with default padding
        //! - FocusOnObjects(conveyors, 1.5f) - Focus with 50% extra padding for more breathing room
        public void FocusOnObjects(List<GameObject> objectsToFocus, float paddingFactor = 1.2f)
        {
            if (objectsToFocus == null || objectsToFocus.Count == 0)
            {
                Logger.Warning("FocusOnObjects called with null or empty list", this);
                return;
            }

            // Stop any active following mode
            if (isFollowingObject)
                StopFollowing();

            // Collect all renderers from all objects
            List<Renderer> allRenderers = new List<Renderer>();
            foreach (var obj in objectsToFocus)
            {
                if (obj != null)
                {
                    Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                    foreach (var renderer in renderers)
                    {
                        if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                            allRenderers.Add(renderer);
                    }
                }
            }

            if (allRenderers.Count == 0)
            {
                Logger.Warning("FocusOnObjects: No renderers found in the provided objects", this);
                return;
            }

            // Calculate combined bounds of all objects
            Bounds combinedBounds = new Bounds();
            bool boundsInitialized = false;

            foreach (var renderer in allRenderers)
            {
                if (renderer != null)
                {
                    if (!boundsInitialized)
                    {
                        combinedBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (!boundsInitialized)
            {
                Logger.Warning("FocusOnObjects: Could not calculate bounds from renderers", this);
                return;
            }

            // Calculate the center of all objects (focus point)
            Vector3 focusPoint = combinedBounds.center;

            // Calculate optimal distance to fit all objects in view
            // Use the maximum extent of the bounds to ensure everything fits
            Vector3 objectSizes = combinedBounds.max - combinedBounds.min;
            float objectSize = Mathf.Max(objectSizes.x, objectSizes.y, objectSizes.z);

            // Apply padding factor
            objectSize *= paddingFactor;

            // Calculate distance where object occupies about 1/3 of screen (same as FocusOnObject)
            float cameraView = 2.0f * Mathf.Tan(0.5f * Mathf.Deg2Rad * mycamera.fieldOfView);
            float targetDistance = objectSize / (0.33f * cameraView);

            // Add safety margin
            targetDistance += 0.3f * objectSize;

            // Clamp to reasonable values
            targetDistance = Mathf.Max(targetDistance, minDistance);
            if (maxdistance > 0)
                targetDistance = Mathf.Min(targetDistance, maxdistance);

            // Calculate view angle to look at the bounds center from current direction
            Vector3 desiredCameraPos = focusPoint - (transform.forward * targetDistance);
            Vector3 targetAngle = CalculateLookAtAngle(focusPoint, desiredCameraPos);

            // Perform smooth camera movement to the new position
            SetNewCameraPosition(focusPoint, targetDistance, targetAngle);
        }

        //! Performs a one-time camera movement to focus on the specified GameObject.
        //! The camera will smoothly move to view the object but will not continuously track it if it moves.
        //! Uses the object's pivot point for positioning, bounds only for distance calculation.
        //!
        //! Example usage:
        //! - FocusOnObject(robot) - Auto-calculates distance to fit object at 1/3 screen space
        //! - FocusOnObject(conveyor, distance: 5.0f) - Focus with specific distance
        //! - FocusOnObject(sensor, distance: 2.5f, viewAngle: new Vector3(45, 90, 0)) - With specific angle
        public void FocusOnObject(GameObject objectToFocus, float? distance = null, Vector3? viewAngle = null)
        {
            if (objectToFocus == null)
            {
                Logger.Warning("FocusOnObject called with null object", this);
                return;
            }

            // Stop any active following mode
            if (isFollowingObject)
                StopFollowing();

            // Calculate optimal distance if not provided
            // Use bounds ONLY for distance calculation to ensure object fits in view
            float targetDistance;
            if (distance.HasValue)
            {
                targetDistance = distance.Value;
            }
            else
            {
                // Get active renderers for bounds-based distance calculation
                Renderer[] renderers = GetActiveRenderers(objectToFocus);
                if (renderers.Length > 0)
                {
                    // Calculate distance based on bounds to ensure whole object is visible
                    targetDistance = CalculateOptimalDistance(renderers, 0.33f);
                }
                else
                {
                    targetDistance = 5.0f; // Default distance if no renderers
                }
            }

            // Use bounds center for camera focus (active children only)
            Renderer[] activeRenderers = GetActiveRenderers(objectToFocus);
            Vector3 focusPoint = activeRenderers.Length > 0
                ? GetBoundsCenterFromRenderers(activeRenderers)
                : objectToFocus.transform.position;

            // Calculate view angle if not provided
            Vector3 targetAngle;
            if (viewAngle.HasValue)
            {
                targetAngle = viewAngle.Value;
            }
            else
            {
                // Calculate angle to look at object's pivot from optimal position
                Vector3 desiredCameraPos = focusPoint - (transform.forward * targetDistance);
                targetAngle = CalculateLookAtAngle(focusPoint, desiredCameraPos);
            }

            // Set new camera position focusing on pivot point
            SetNewCameraPosition(focusPoint, targetDistance, targetAngle);
        }

        //! Starts continuous camera following of the specified GameObject.
        //! The object will remain centered in the camera view and the camera will track its movement.
        //! Following can only be stopped by calling StopFollowing() - user input cannot interrupt it.
        //!
        //! Parameters:
        //! - objectToFollow: The GameObject to continuously track (required)
        //! - distance: Camera distance from object in meters. If null, auto-calculates to fit object at 1/3 screen space
        //! - viewAngle: Initial camera rotation in degrees. If null, maintains current camera angle
        //! - allowRotation: If true, user can orbit camera around the object while keeping it centered
        //! - allowZoom: If true, user can zoom in/out from the object while keeping it centered
        //! - lerpSpeed: Controls camera tracking smoothness (0.1 = very smooth, 1.0 = tight tracking, 10.0 = near instant)
        //! - smoothStart: If true, camera smoothly lerps to target when starting. If false, jumps immediately
        //! - stopOnMouseClick: If true, following stops when any mouse button is clicked
        //!
        //! Example usage:
        //! - StartFollowing(agvRobot) - Fully locked cinematic follow with default lerp
        //! - StartFollowing(robot, allowRotation: true) - Follow with user rotation control
        //! - StartFollowing(conveyor, lerpSpeed: 0.3f) - Smooth cinematic following
        //! - StartFollowing(fastMover, lerpSpeed: 5.0f) - Very tight tracking for fast objects
        //! - StartFollowing(sensor, distance: 2.5f, viewAngle: new Vector3(45, 90, 0), lerpSpeed: 2.0f) - Custom everything
        //! - StartFollowing(target, smoothStart: false) - Jump immediately to target without initial lerp
        //! - StartFollowing(target, stopOnMouseClick: true) - Stop following when user clicks
        public void StartFollowing(GameObject objectToFollow, float? distance = null, Vector3? viewAngle = null,
            bool allowRotation = false, bool allowZoom = false, float lerpSpeed = 1.0f, bool smoothStart = true, bool stopOnMouseClick = false)
        {
            if (objectToFollow == null)
            {
                Logger.Warning("StartFollowing called with null object", this);
                return;
            }

            // Stop any existing follow mode
            if (isFollowingObject)
                StopFollowing();

            // Store object reference and parameters
            isFollowingMultipleObjects = false;
            followedRenderers = null;
            followBoundsPaddingFactor = 1.2f;
            followedObject = objectToFollow;
            allowRotationWhileFollowing = allowRotation;
            allowZoomWhileFollowing = allowZoom;
            followLerpSpeed = Mathf.Clamp(lerpSpeed, 0.01f, 100.0f); // Clamp to reasonable range
            stopFollowOnMouseClick = stopOnMouseClick;

            // Calculate distance if not provided
            // Use bounds center ONLY for distance calculation to ensure object fits in view
            if (distance.HasValue)
            {
                followDistance = distance.Value;
            }
            else
            {
                // Get active renderers for bounds-based distance calculation
                Renderer[] renderers = GetActiveRenderers(objectToFollow);
                if (renderers.Length > 0)
                {
                    // Calculate optimal distance based on object bounds
                    followDistance = CalculateOptimalDistance(renderers, 0.33f);
                }
                else
                {
                    followDistance = 5.0f; // Default distance
                }
            }

            // Use bounds center for camera positioning (active children only)
            Renderer[] activeRenderers = GetActiveRenderers(objectToFollow);
            Vector3 followPoint = activeRenderers.Length > 0
                ? GetBoundsCenterFromRenderers(activeRenderers)
                : objectToFollow.transform.position;

            // Calculate view angle if not provided
            Vector3 targetAngle;
            if (viewAngle.HasValue)
            {
                targetAngle = viewAngle.Value;
            }
            else
            {
                // Calculate rotation that looks at the object's pivot point
                Vector3 directionToObject = (followPoint - transform.position).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(directionToObject);
                targetAngle = lookRotation.eulerAngles;
            }

            // Deactivate Cinemachine if active
            if (CinemachineIsActive)
                ActivateCinemachine(false);

            // Deactivate FirstPersonController if active
            if (FirstPersonControllerActive && FirstPersonController != null)
            {
                FirstPersonController.SetActive(false);
                FirstPersonControllerActive = false;
            }

            // Store the target position and rotation for initial lerp
            followInitialTargetPos = followPoint;
            followInitialRotation = Quaternion.Euler(targetAngle);
            smoothedFollowPoint = followPoint;
            followPointVelocity = Vector3.zero;
            followDistanceVelocity = 0f;
            hasSmoothedFollowPoint = true;

            // Start initial lerp phase or jump immediately based on smoothStart parameter
            if (smoothStart)
            {
                // Start smooth lerp to target
                isInitialFollowLerp = true;

                // Set the desired values without immediately jumping
                desiredDistance = followDistance;
                // Don't force current distance immediately - let it lerp
            }
            else
            {
                // Jump immediately to target position
                isInitialFollowLerp = false;
                SetNewCameraPosition(followPoint, followDistance, targetAngle, true);
            }

            // CRITICAL: Reset targetOffset for pure orbital centering in follow mode
            // This ensures the object appears exactly centered
            targetOffset = Vector3.zero;

            // Activate follow mode
            isFollowingObject = true;

            // CRITICAL FIX: Set bypass flag to prevent double-click issue when called from UI button
            // This ensures LateUpdate processes the follow mode properly even if called from UI
            bypassUICheckThisFrame = true;
        }

        //! Starts "riding" an object's transform - camera position AND rotation match the followed object.
        //! Unlike StartFollowing which orbits around an object, this mode makes the camera move and rotate
        //! exactly as the followed object does. Useful for camera fly-throughs and animated sequences.
        //!
        //! Parameters:
        //! - objectToRide: The GameObject whose transform the camera will match
        //! - lerpSpeed: How quickly camera follows (0.1=smooth cinematic, 5.0=tight tracking, 10.0=near instant)
        //! - smoothStart: If true, smoothly transitions to object; if false, jumps immediately
        //! - stopOnMouseClick: If true, stops riding when any mouse button is clicked
        public void StartRidingTransform(GameObject objectToRide, float lerpSpeed = 5.0f, bool smoothStart = true, bool stopOnMouseClick = false)
        {
            if (objectToRide == null)
            {
                Logger.Warning("StartRidingTransform called with null object", this);
                return;
            }

            if (isFollowingObject)
                StopFollowing();

            followedObject = objectToRide;
            isRidingTransform = true;
            followLerpSpeed = Mathf.Clamp(lerpSpeed, 0.01f, 100.0f);
            stopFollowOnMouseClick = stopOnMouseClick;
            followDistance = 0f;
            allowRotationWhileFollowing = false;
            allowZoomWhileFollowing = false;

            if (CinemachineIsActive)
                ActivateCinemachine(false);

            if (FirstPersonControllerActive && FirstPersonController != null)
            {
                FirstPersonController.SetActive(false);
                FirstPersonControllerActive = false;
            }

            followInitialTargetPos = objectToRide.transform.position;
            followInitialRotation = objectToRide.transform.rotation;

            if (smoothStart)
            {
                isInitialFollowLerp = true;
            }
            else
            {
                isInitialFollowLerp = false;
                transform.position = objectToRide.transform.position;
                transform.rotation = objectToRide.transform.rotation;
                rotation = objectToRide.transform.rotation;
                currentRotation = rotation;
                desiredRotation = rotation;
            }

            targetOffset = Vector3.zero;
            isFollowingObject = true;
            bypassUICheckThisFrame = true;
        }

        //! Starts continuous camera following of multiple GameObjects.
        //! The camera tracks the combined bounds center and keeps all objects in view.
        //!
        //! Parameters:
        //! - objectsToFollow: List of GameObjects to track (required, must not be empty)
        //! - paddingFactor: Extra padding around combined bounds (default 1.2 = 20% padding)
        //! - allowRotation: If true, user can orbit camera around the group while keeping it centered
        //! - allowZoom: If true, user can zoom in/out while keeping the group centered
        //! - lerpSpeed: Controls camera tracking smoothness (0.1 = very smooth, 1.0 = tight tracking, 10.0 = near instant)
        //! - smoothStart: If true, camera smoothly lerps to target when starting. If false, jumps immediately
        //! - stopOnMouseClick: If true, following stops when any mouse button is clicked
        public void StartFollowingObjects(List<GameObject> objectsToFollow, float paddingFactor = 1.2f,
            bool allowRotation = false, bool allowZoom = false, float lerpSpeed = 1.0f, bool smoothStart = true, bool stopOnMouseClick = false)
        {
            if (objectsToFollow == null || objectsToFollow.Count == 0)
            {
                Logger.Warning("StartFollowingObjects called with null or empty list", this);
                return;
            }

            // Stop any existing follow mode
            if (isFollowingObject)
                StopFollowing();

            // Collect renderers from all objects
            List<Renderer> renderers = new List<Renderer>();
            HashSet<Renderer> rendererSet = new HashSet<Renderer>();
            foreach (var obj in objectsToFollow)
            {
                if (obj == null)
                    continue;

                Renderer[] objRenderers = obj.GetComponentsInChildren<Renderer>();
                foreach (var renderer in objRenderers)
                {
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && rendererSet.Add(renderer))
                        renderers.Add(renderer);
                }
            }

            if (renderers.Count == 0)
            {
                Logger.Warning("StartFollowingObjects: No renderers found in the provided objects", this);
                return;
            }

            // Calculate combined bounds from renderers
            Bounds combinedBounds = new Bounds();
            bool boundsInitialized = false;
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    if (!boundsInitialized)
                    {
                        combinedBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (!boundsInitialized)
            {
                Logger.Warning("StartFollowingObjects: Could not calculate bounds from renderers", this);
                return;
            }

            // Store follow parameters
            isFollowingMultipleObjects = true;
            followedRenderers = renderers;
            followBoundsPaddingFactor = Mathf.Max(0.01f, paddingFactor);
            followedObject = null;

            allowRotationWhileFollowing = allowRotation;
            allowZoomWhileFollowing = allowZoom;
            followLerpSpeed = Mathf.Clamp(lerpSpeed, 0.01f, 100.0f);
            stopFollowOnMouseClick = stopOnMouseClick;

            // Calculate target position and distance
            Vector3 followPoint = GetBoundsCenterFromRenderers(renderers.ToArray());
            followDistance = CalculateDistanceFromBounds(combinedBounds, followBoundsPaddingFactor);

            // Calculate view angle based on current camera position
            Vector3 directionToObject = (followPoint - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(directionToObject);
            Vector3 targetAngle = lookRotation.eulerAngles;

            // Deactivate Cinemachine if active
            if (CinemachineIsActive)
                ActivateCinemachine(false);

            // Deactivate FirstPersonController if active
            if (FirstPersonControllerActive && FirstPersonController != null)
            {
                FirstPersonController.SetActive(false);
                FirstPersonControllerActive = false;
            }

            // Store the target position and rotation for initial lerp
            followInitialTargetPos = followPoint;
            followInitialRotation = Quaternion.Euler(targetAngle);
            smoothedFollowPoint = followPoint;
            followPointVelocity = Vector3.zero;
            followDistanceVelocity = 0f;
            hasSmoothedFollowPoint = true;

            // Start initial lerp phase or jump immediately based on smoothStart parameter
            if (smoothStart)
            {
                isInitialFollowLerp = true;
                desiredDistance = followDistance;
            }
            else
            {
                isInitialFollowLerp = false;
                SetNewCameraPosition(followPoint, followDistance, targetAngle, true);
            }

            // Reset targetOffset for pure orbital centering in follow mode
            targetOffset = Vector3.zero;

            // Activate follow mode
            isFollowingObject = true;

            // Allow follow start even if called from UI
            bypassUICheckThisFrame = true;
        }

        //! Returns true if the camera is currently "riding" an object's transform (matching position and rotation).
        public bool IsRiding => isFollowingObject && isRidingTransform;

        //! Stops continuous camera following of the currently tracked object.
        //! After calling this, the camera will return to normal user-controlled navigation.
        public void StopFollowing()
        {
            if (isFollowingObject)
            {
                // CRITICAL: Reset rotation state to current camera orientation to prevent jumps
                // This ensures smooth transition back to normal navigation
                rotation = transform.rotation;
                currentRotation = transform.rotation;
                desiredRotation = transform.rotation;

                // CRITICAL FIX: Don't re-extract xDeg/yDeg from eulerAngles!
                // During follow mode, rotation was set via Quaternion.LookRotation (direction-based).
                // If we extract Euler angles from this quaternion, they might not match the orbital
                // rotation system used in bypass mode, causing a rotation jump when rotation starts.
                // Keep the existing xDeg/yDeg values which represent the correct orbital angles.
                // They will be naturally updated during actual rotation via RotateAround.

                // CRITICAL FIX: Use a REAL scene point instead of artificial target position
                // This prevents rotation jump issues when transitioning back to normal navigation
                Vector3 realTargetPoint;

                // Option 1: If following multiple objects and bounds are valid, use bounds center
                if (isFollowingMultipleObjects)
                {
                    if (TryGetFollowBounds(out Bounds bounds))
                    {
                        realTargetPoint = bounds.center;
                    }
                    else
                    {
                        realTargetPoint = target.position;
                    }
                }
                // Option 2: If the followed object still exists, use its current position
                else if (followedObject != null)
                {
                    realTargetPoint = GetCurrentFollowCenter();
                }
                else
                {
                    // Option 3: Raycast from camera center to find a real scene point
                    Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
                    Ray centerRay = mycamera.ScreenPointToRay(screenCenter);

                    if (Physics.Raycast(centerRay, out RaycastHit hit))
                    {
                        // Found a real object - use hit point
                        realTargetPoint = hit.point;
                        // Update distance to match the real point
                        currentDistance = Vector3.Distance(transform.position, hit.point);
                        desiredDistance = currentDistance;
                    }
                    else
                    {
                        // No raycast hit - raycast to ground plane or use fallback
                        var groundPlane = new Plane(Vector3.up, Vector3.zero);
                        if (groundPlane.Raycast(centerRay, out float distance))
                        {
                            realTargetPoint = centerRay.GetPoint(distance);
                            currentDistance = Vector3.Distance(transform.position, realTargetPoint);
                            desiredDistance = currentDistance;
                        }
                        else
                        {
                            // Final fallback: use point at current distance in forward direction
                            realTargetPoint = transform.position + transform.forward * currentDistance;
                        }
                    }
                }

                // Set target to the REAL point, not artificial
                target.position = realTargetPoint;
                targetposition = realTargetPoint;

                // Calculate and set targetOffset to maintain camera position
                // Formula: position = target.position - (rotation * Vector3.forward * currentDistance + targetOffset)
                // Rearranging: targetOffset = target.position - position - rotation * Vector3.forward * currentDistance
                targetOffset = target.position - transform.position - (rotation * Vector3.forward * currentDistance);

            }

            isFollowingObject = false;
            followedObject = null;
            isFollowingMultipleObjects = false;
            followedRenderers = null;
            followBoundsPaddingFactor = 1.2f;
            hasSmoothedFollowPoint = false;
            followPointVelocity = Vector3.zero;
            followDistanceVelocity = 0f;
            allowRotationWhileFollowing = false;
            allowZoomWhileFollowing = false;
            stopFollowOnMouseClick = false; // Reset stop on click setting
            isInitialFollowLerp = false; // Reset initial lerp state
            isRidingTransform = false; // Reset riding mode state
            IsRotating = false; // Ensure rotation state is cleared
        }

        //! Returns true if the camera is currently in follow mode, tracking an object.
        public bool IsFollowing => isFollowingObject;

        //! Returns the GameObject currently being followed, or null if not in follow mode.
        public GameObject FollowedObject => followedObject;

        public void ActivateCinemachine(bool activate)
        {
#if CINEMACHINE
            CinemachineBrain brain;
            brain = GetComponent<CinemachineBrain>();
            if (brain == null)
                return;

            if (!activate)
                if (brain.ActiveVirtualCamera != null)
                {
                    var camrot = brain.ActiveVirtualCamera.VirtualCameraGameObject.transform.rotation;
                    var rot = camrot.eulerAngles;
                    distance = Vector3.Distance(transform.position, target.position);
                    var tarpos = brain.ActiveVirtualCamera.VirtualCameraGameObject.transform.position +
                                 (camrot * Vector3.forward * distance + targetOffset);
                    SetNewCameraPosition(tarpos, distance, rot);
                }

            if (brain != null)
            {
                if (activate)
                    brain.enabled = true;
                else
                    brain.enabled = false;
            }

            CinemachineIsActive = activate;
#endif
        }

#if CINEMACHINE
        //! Activates the cinemachine camera and sets it to the highest priority
        public void ActivateCinemachineCam(CinemachineVirtualCamera vcam)
        {
            vcam.enabled = true;
            vcam.Priority = 100;
            if (CinemachineIsActive == false)
                ActivateCinemachine(true);

            // Set low priority to all other vcams
            var vcams = FindObjectsByType(typeof(CinemachineVirtualCamera),FindObjectsSortMode.None);
            foreach (CinemachineVirtualCamera vc in vcams)
                if (vc != vcam)
                    vc.Priority = 10;
        }
#endif

        public void Init()
        {
#if CINEMACHINE
            ActivateCinemachine(false);
            
#endif
#if REALVIRTUAL_PROFESSIONAL
            hmiController = FindAnyObjectByType<HMI_Controller>();
#endif
            var rnds = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            //If there is no target, create a temporary target at 'distance' from the cameras current viewpoint
            if (!target)
            {
                var go = new GameObject("Cam Target");
                go.transform.position = transform.position + transform.forward * distance;
                target = go.transform;
            }

            mycamera = GetComponent<Camera>();

            distance = Vector3.Distance(transform.position, target.position);
            currentDistance = distance;
            desiredDistance = distance;

            //be sure to grab the current rotations as starting points.
            position = transform.position;
            rotation = transform.rotation;
            currentRotation = transform.rotation;
            desiredRotation = transform.rotation;

            // Initialize rotation angles from current camera rotation to prevent jump on first rotation
            xDeg = transform.rotation.eulerAngles.y;
            yDeg = transform.rotation.eulerAngles.x;

            if (LastCameraPosition != null && !FirstPersonControllerActive && !startcameraposset)
            {
                if (SetCameraPosOnStartPlay)
                    SetNewCameraPosition(LastCameraPosition.TargetPos, LastCameraPosition.CameraDistance,
                        LastCameraPosition.CameraRot);
                startcameraposset = true;
            }

            if (FirstPersonController != null)
            {
                if (FirstPersonControllerActive)
                    FirstPersonController.SetActive(true);
                else
                    FirstPersonController.SetActive(false);
            }
#if (UNITY_EDTOR_LINUX || UNITY_STANDALONE_LINUX)
            		            DPIScale = 1;
#else
            DPIScale = 144 / Screen.dpi;
#endif
            orthoviewcontroller = transform.parent.GetComponentInChildren<OrthoViewController>();
            maxdistance = CalculateFocusViewDistance(rnds) * 2;
        }

        //! Calculates rotation input deltas from mouse or touch, applying all sensitivity modifiers.
        //! This centralizes the duplicated input handling logic used across different rotation modes.
        private void GetRotationInput(bool isTouchRotate, float shiftFactor, out float mouseX, out float mouseY)
        {
            if (!isTouchRotate)
            {
                // Mouse rotation input with sensitivity scaling
                var scale = ROTATION_SPEED_BASE * DPIScale * shiftFactor * MouseRotationSpeed * MasterSensitivity * ROTATION_SPEED_MULTIPLIER;
                mouseX = Input.GetAxis("Mouse X") * scale;
                mouseY = Input.GetAxis("Mouse Y") * scale;
            }
            else
            {
                // Touch rotation input with different scaling
                mouseX = Touch.TouchDeltaPos.x * DPIScale * TouchRotationSpeed * MasterSensitivity * TOUCH_ROTATION_MULTIPLIER * TOUCH_ROTATION_SCALE;
                mouseY = Touch.TouchDeltaPos.y * DPIScale * TouchRotationSpeed * MasterSensitivity * TOUCH_ROTATION_MULTIPLIER * TOUCH_ROTATION_SCALE;
            }
        }

        //! Applies focus/center logic for a selected object - positions camera and optionally rotates to face it.
        //! This centralizes the duplicated focus logic used for hotkey focus and auto-center modes.
        private void ApplyFocusToSelectedObject(bool shouldZoom)
        {
            _lastmovement = Time.realtimeSinceStartup;
            var pos = selectionmanager.GetHitpoint();
            selectionmanager.ShowCenterIcon(true);

            if (!RotateToFocusObject)
            {
                // If not rotate to target object on focus then just move the targetposition (=panning the camera)
                targetposition = pos;
            }
            else
            {
                // if rotation is wished - calculate desired rotation and new distance (camera should not move)
                targetposition = pos;
                var tonewtarget = pos - position;
                desiredDistance = Vector3.Magnitude(tonewtarget);
                desiredRotation = Quaternion.LookRotation(tonewtarget, transform.up);
                var euler = desiredRotation.eulerAngles;
                desiredRotation = Quaternion.Euler(euler.x, euler.y, 0);
            }

            // Only calculate and apply zoom distance if requested
            if (shouldZoom)
            {
                // get bounding box of all children of selected object
                distance = CalculateFocusViewDistance(selectionmanager.SelectedObject.GetComponentsInChildren<Renderer>());
                desiredDistance = distance;
            }
        }

        //! Initializes rotation state when rotation starts (mouse button down).
        //! Handles both follow mode rotation and normal bypass rotation with proper pivot point selection.
        private void StartRotation()
        {
            // Get the point to rotate around
            // CRITICAL: When following an object with rotation allowed, always orbit around the followed object
            if (isFollowingObject && allowRotationWhileFollowing)
            {
                rotationPivotPoint = GetCurrentFollowCenter();
            }
            else
            {
                // CRITICAL FIX: Use the existing target.position as pivot point
                // This prevents jumps when starting rotation, as the camera is already positioned
                // correctly relative to this target from StopFollowing() or previous operations
                // Using GetRotationPivotPoint() would raycast for a NEW pivot from mouse position,
                // which could be different and cause RotateAround to reposition the camera
                rotationPivotPoint = target.position;
            }

            // In follow mode with rotation allowed, don't bypass the target system
            // We want to keep following while allowing user to change view angle
            if (isFollowingObject && allowRotationWhileFollowing)
            {
                bypassTargetSystemDuringRotation = false;

                // Initialize rotation from current camera state to prevent jumps
                rotation = transform.rotation;
                currentRotation = transform.rotation;
                desiredRotation = transform.rotation;
                xDeg = transform.rotation.eulerAngles.y;
                yDeg = transform.rotation.eulerAngles.x;
            }
            else
            {
                // Normal rotation mode - bypass target system
                bypassTargetSystemDuringRotation = true;

                // Just sync rotation state - don't re-orient the camera
                // The camera's current rotation is correct from StopFollowing()
                // xDeg and yDeg are already correct and should not be re-extracted
                rotation = transform.rotation;
                currentRotation = transform.rotation;
                desiredRotation = transform.rotation;
            }

            IsRotating = true;
        }

        //! Synchronizes camera state when rotation ends (mouse button up).
        //! Handles syncing back to target system after bypass rotation or cleanly ending follow mode rotation.
        private void EndRotation()
        {
            // Handle end of rotation for follow mode differently
            if (IsRotating && isFollowingObject && allowRotationWhileFollowing)
            {
                // Just stop rotating, no need to sync anything as we're using target system
                IsRotating = false;
            }
            else if (IsRotating && bypassTargetSystemDuringRotation)
            {
                // Store current state for debugging
                Vector3 currentCamPos = transform.position;
                Vector3 oldTargetOffset = targetOffset;

                // Sync all rotation variables to prevent jumping
                rotation = transform.rotation;
                desiredRotation = transform.rotation;
                currentRotation = transform.rotation;
                xDeg = transform.rotation.eulerAngles.y;
                yDeg = transform.rotation.eulerAngles.x;

                // CRITICAL: Maintain position continuity by solving for target.position while preserving targetOffset
                //
                // The Problem:
                // During bypass rotation, RotateAround() directly manipulates transform.position and transform.rotation.
                // If we recalculate targetOffset based on the new rotation, it will differ from the original offset
                // (calculated in StopFollowing()), causing CalculateCamPos() to reposition the camera in the next frame.
                //
                // The Solution:
                // Keep targetOffset UNCHANGED and instead solve for target.position:
                // Camera position formula: position = target.position - (rotation * Vector3.forward * currentDistance + targetOffset)
                // Solving for target.position: target.position = position + rotation * Vector3.forward * currentDistance + targetOffset
                //
                // This ensures perfect position continuity - the camera stays exactly where RotateAround left it.

                // Calculate distance from camera to rotation pivot
                Vector3 pivotForDistance = (isFollowingObject && allowRotationWhileFollowing)
                    ? GetCurrentFollowCenter()
                    : rotationPivotPoint;
                desiredDistance = Vector3.Distance(transform.position, pivotForDistance);
                currentDistance = desiredDistance;

                // For follow mode, reset targetOffset to zero for proper centering
                if (isFollowingObject && allowRotationWhileFollowing)
                {
                    targetOffset = Vector3.zero;
                    Vector3 followCenter = GetCurrentFollowCenter();
                    target.position = followCenter;
                    targetposition = followCenter;
                }
                else
                {
                    // For normal mode: preserve targetOffset and solve for target.position
                    Vector3 newTargetPosition = transform.position + (rotation * Vector3.forward * currentDistance) + targetOffset;
                    target.position = newTargetPosition;
                    targetposition = newTargetPosition;
                }

                // Debug logging to verify the fix
                if (Debug.isDebugBuild || Application.isEditor)
                {
                    // Debug logging removed - was causing console spam
                    // string modeInfo = (isFollowingObject && allowRotationWhileFollowing)
                    //     ? $" (Following: {followedObject?.name})"
                    //     : "";
                    //
                    // Debug.Log($"[CameraSync] Exiting orbital rotation mode{modeInfo}:" +
                    //     $"\n  Camera Pos: {currentCamPos}" +
                    //     $"\n  New Target Pos: {target.position}" +
                    //     $"\n  Old Pivot Point: {pivotForDistance}" +
                    //     $"\n  targetOffset (preserved): {targetOffset} (was: {oldTargetOffset})" +
                    //     $"\n  Distance: {currentDistance}");

                    // Verify perfect position continuity
                    Vector3 expectedPos = target.position - (rotation * Vector3.forward * currentDistance + targetOffset);
                    float positionError = Vector3.Distance(expectedPos, currentCamPos);

                    // Debug.Log($"[CameraSync] Position continuity check: Error = {positionError:F6} units");

                    if (positionError > 0.001f)
                    {
                        Debug.LogWarning($"[CameraSync] Position continuity error detected!" +
                            $"\n  Error: {positionError:F6} units" +
                            $"\n  Expected: {expectedPos}" +
                            $"\n  Current: {currentCamPos}" +
                            $"\n  Difference: {expectedPos - currentCamPos}");
                    }
                }

                // Reset bypass mode
                bypassTargetSystemDuringRotation = false;
            }
            IsRotating = false;
        }

        private void CameraTransform(Vector3 direction)
        {
            target.rotation = transform.rotation;
            targetposition = targetposition+direction * CursorSpeed * MasterSensitivity * 0.02f;
            
            _lastmovement = Time.realtimeSinceStartup;
        }

        private void CamereSetDirection(Vector3 direction)
        {
            desiredDistance = 10f;
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        private bool MouseOverViewport(Camera main_cam, Camera local_cam)
        {
            if (!Input.mousePresent) return true; //always true if no mouse??

            var main_mou = main_cam.ScreenToViewportPoint(Input.mousePosition);
            return local_cam.rect.Contains(main_mou);
        }

        //! gets the closest distance of an object which is hit by a raycast at m ouse position
        private float GetClosestHitDistance()
        {
            RaycastHit[] hits;
            var hitdistance = 0f;
            var hitpoint = Vector3.zero;
            var pointerpos = Input.mousePosition;
            if (touch)
                pointerpos = Touch.TouchPos;
            var ray = mycamera.ScreenPointToRay(pointerpos);
            hits = Physics.RaycastAll(ray);
            var min = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.distance < min)
                {
                    min = hit.distance;
                    hitpoint = hit.point;
                }

                hitdistance = Vector3.Distance(hitpoint, transform.position);
            }

            return hitdistance;
        }

        //! gets the rotation pivot point by raycasting from mouse position - if hit, use hit point, otherwise raycast to ground plane
        private Vector3 GetRotationPivotPoint()
        {
            var pointerpos = Input.mousePosition;
            if (touch)
                pointerpos = Touch.TouchPos;

            var ray = mycamera.ScreenPointToRay(pointerpos);

            // First try to hit any object
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return hit.point;
            }

            // If no hit, raycast to ground plane (y=0)
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            // Fallback: use a point at current distance from camera
            return transform.position + ray.direction * currentDistance;
        }

        //! raycasts to a plane for panning in parallel to the camera. distance of the plane is selected at the closest hitpoint (if there is a hit) or at 3m
        private Vector3 RaycastToPanPlane(bool touch, float distance)
        {
            try
            {
                var pointerpos = Input.mousePosition;
                if (touch)
                    pointerpos = Touch.TouchPos;
                if (float.IsNaN(pointerpos.x) || float.IsNaN(pointerpos.y))
                    return Vector3.zero;
                Ray ray;
                try
                {
                     ray = mycamera.ScreenPointToRay(pointerpos);
                } catch
                {
                    return Vector3.zero;
                }
               

                // find distance to the bottom plane
                var bottom = new Plane(transform.forward,
                    transform.position + transform.forward * distance);
                if (bottom.Raycast(ray, out distance)) return ray.GetPoint(distance);

                return Vector3.zero;
            }
            catch (Exception e)
            {
                Debug.Log("Error in RaycastToPanPlane: " + e.Message);
                return Vector3.zero;
            }
        }


        private Vector3 RayCastToBottomZoom(bool touch = false)
        {
            var pointerpos = !touch ? Input.mousePosition : (Vector3)Touch.TwoFingerMiddlePos;
            if (float.IsNaN(pointerpos.x) || float.IsNaN(pointerpos.y))
                return Vector3.zero;
            Ray ray = mycamera.ScreenPointToRay(pointerpos);

            var plane = new Plane(Vector3.up, Vector3.zero);
            // raycast from mouseposition to this plane
            float distance;
            if (plane.Raycast(ray, out distance)) return ray.GetPoint(distance);

            return Vector3.zero;
        }

        private bool CheckMouseOverOrthoView()
        {
         
                incamera = mycamera;
                if (Camera.allCameras.Length > 1)
                    foreach (var cam in Camera.allCameras)
                    {
                        // get a parent of cam
                        var parent = cam.transform.parent;
                        if (parent != null)
                            if (parent.name == "OrthoViews")
                            {
                                if (cam != Camera.main)
                                    if (MouseOverViewport(mycamera, cam))
                                    {
                                        incamera = cam;
                                        return true;
                                    }
                            }
                    }
                return false;
        }

        private float CalculateFocusViewDistance(Renderer[] renderers)
        {
            var combinedBounds = new Bounds();


            // fist deleta everything which is under realvirtual from the bounds
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i].transform.IsChildOf(transform))
                    renderers[i] = null;

            // Get the renderer for each child object and combine their bounds
            foreach (var renderer in renderers)
                if (renderer != null)
                {
                    if (combinedBounds.size == Vector3.zero)
                        combinedBounds = renderer.bounds;
                    else
                        combinedBounds.Encapsulate(renderer.bounds);
                }

            var cameraDistance = 2.0f;
            var objectSizes = combinedBounds.max - combinedBounds.min;
            var objectSize = Mathf.Max(objectSizes.x, objectSizes.y, objectSizes.z);
            var cameraView =
                2.0f * Mathf.Tan(0.5f * Mathf.Deg2Rad *
                                 mycamera.fieldOfView); // Visible height 1 meter in front
            var distance =
                cameraDistance * objectSize / cameraView; // Combined wanted distance from the object
            distance += 0.5f * objectSize; // Estimated offset from the center to the outside of the object
            calculatedboundscenter = combinedBounds.center;
            return distance;
        }

        //! Calculates optimal camera distance to fit object at specified screen fraction (default 1/3)
        private float CalculateOptimalDistance(Renderer[] renderers, float screenFraction = 0.33f)
        {
            if (renderers.Length == 0)
                return 5.0f; // Default distance

            var combinedBounds = new Bounds();

            // Get the renderer for each child object and combine their bounds
            bool boundsInitialized = false;
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    if (!boundsInitialized)
                    {
                        combinedBounds = renderer.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (!boundsInitialized)
                return 5.0f; // No valid renderers

            // Calculate the maximum dimension of the object
            var objectSizes = combinedBounds.max - combinedBounds.min;
            var objectSize = Mathf.Max(objectSizes.x, objectSizes.y, objectSizes.z);

            // Calculate visible height at 1 meter based on camera FOV
            var cameraView = 2.0f * Mathf.Tan(0.5f * Mathf.Deg2Rad * mycamera.fieldOfView);

            // Calculate distance where object occupies the specified fraction of screen
            // screenFraction = objectSize / visibleHeightAtDistance
            // visibleHeightAtDistance = distance * cameraView
            // screenFraction = objectSize / (distance * cameraView)
            // distance = objectSize / (screenFraction * cameraView)
            var distance = objectSize / (screenFraction * cameraView);

            // Add safety margin
            distance += 0.3f * objectSize;

            // Clamp to reasonable values
            distance = Mathf.Max(distance, minDistance);
            if (maxdistance > 0)
                distance = Mathf.Min(distance, maxdistance);

            return distance;
        }

        //! Gets the center point of an object's combined bounding box from all its active renderers
        private Vector3 GetObjectBoundsCenter(GameObject obj)
        {
            if (obj == null)
                return Vector3.zero;

            Renderer[] renderers = GetActiveRenderers(obj);

            if (renderers.Length == 0)
                return obj.transform.position;

            return GetBoundsCenterFromRenderers(renderers);
        }

        //! Calculates the Euler angles needed to look at a target position from a given camera position
        private Vector3 CalculateLookAtAngle(Vector3 targetPosition, Vector3 fromPosition)
        {
            Vector3 direction = targetPosition - fromPosition;
            if (direction.sqrMagnitude < 0.001f)
            {
                // Target is too close to camera position, return current rotation
                return transform.rotation.eulerAngles;
            }

            Quaternion lookRotation = Quaternion.LookRotation(direction);
            return lookRotation.eulerAngles;
        }

        private static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360)
                angle += 360;
            if (angle > 360)
                angle -= 360;
            return Mathf.Clamp(angle, min, max);
        }
    }
}