// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using NaughtyAttributes;
using UnityEngine;

namespace realvirtual
{
    //! Flies the camera smoothly between two points with configurable speed and easing.
    //! Can be triggered by buttons, PLC signals, or automatically at start.
    //! Supports both one-shot and looping modes with optional ping-pong motion.
    //! Camera can be offset from the path and can look at a target or along the path direction.
    [AddComponentMenu("realvirtual/Utility/Camera Fly Between Points")]
    public class CameraFlyBetweenPoints : MonoBehaviour
    {
        public enum EasingMode
        {
            Linear,
            EaseInOut,
            EaseIn,
            EaseOut
        }

        public enum PlayMode
        {
            Once,           //!< Play once from start to end
            Loop,           //!< Loop from start to end, then jump back to start
            PingPong        //!< Fly back and forth between points
        }

        public enum LookMode
        {
            InterpolateRotation,  //!< Interpolate between start and end point rotations
            LookAtPath,           //!< Always look at the path position (useful when camera is offset)
            LookAtTarget,         //!< Always look at a specified target object
            LookAlongPath,        //!< Look in the direction of travel along the path
            LookAtEndPoint        //!< Always look at the end point destination (keeps destination in view)
        }

        [Header("Points")]
        [Tooltip("Starting position and rotation for the camera fly path")]
        public Transform StartPoint; //!< Starting position and rotation for the camera fly path

        [Tooltip("Ending position and rotation for the camera fly path")]
        public Transform EndPoint; //!< Ending position and rotation for the camera fly path

        [Header("Camera Position Offset")]
        [Tooltip("Position offset from path (X=right, Y=up, Z=forward relative to path direction)")]
        public Vector3 PositionOffset = Vector3.zero; //!< Position offset relative to path direction

        [Header("Camera Rotation")]
        [Tooltip("How the camera rotation is controlled during flight")]
        public LookMode RotationMode = LookMode.InterpolateRotation; //!< Controls how camera rotation behaves during flight

        [ShowIf("RotationMode", LookMode.LookAtTarget)]
        [Tooltip("Target object for the camera to look at during flight")]
        public Transform LookAtTarget; //!< Target transform to look at when using LookAtTarget mode

        [Tooltip("Horizontal rotation offset in degrees (yaw, left/right)")]
        [Range(-180f, 180f)]
        public float HorizontalAngle = 0f; //!< Horizontal rotation offset applied to camera orientation

        [Tooltip("Vertical rotation offset in degrees (pitch, up/down)")]
        [Range(-90f, 90f)]
        public float VerticalAngle = 0f; //!< Vertical rotation offset applied to camera orientation

        [Header("Timing")]
        [Tooltip("Duration in seconds for the camera to fly from start to end")]
        public float Duration = 3f; //!< Duration in seconds for the camera to fly between points

        [Tooltip("Easing mode for the camera motion")]
        public EasingMode Easing = EasingMode.EaseInOut; //!< Easing mode for smooth acceleration/deceleration

        [Header("Play Settings")]
        [Tooltip("How the camera fly animation plays")]
        public PlayMode Mode = PlayMode.Once; //!< Determines if animation plays once, loops, or ping-pongs

        [Tooltip("If true, automatically starts flying when the component is enabled")]
        public bool AutoStartOnEnable = false; //!< Automatically start flying when enabled

        [Tooltip("Delay in seconds before starting the fly animation")]
        public float StartDelay = 0f; //!< Delay before starting the fly animation

        [Header("PLC Signal Control")]
        [Tooltip("PLC signal to start flying. Rising edge triggers the animation")]
        public PLCInputBool SignalStart; //!< PLC signal to trigger start (rising edge)

        [Tooltip("PLC signal to stop flying")]
        public PLCInputBool SignalStop; //!< PLC signal to stop the animation

        [Tooltip("Output signal that is true while flying")]
        public PLCOutputBool SignalIsFlying; //!< Output signal indicating flight is active

        [Tooltip("Output signal that pulses true when the destination is reached")]
        public PLCOutputBool SignalReachedEnd; //!< Output signal when end point is reached

        [Header("Public Control")]
        [Tooltip("Set to true to start flying, automatically resets to false")]
        public bool StartFlying = false; //!< Public bool to trigger start

        [Tooltip("Set to true to stop flying")]
        public bool StopFlying = false; //!< Public bool to stop flying

        [Header("Status (Read Only)")]
        [ReadOnly]
        public bool IsFlying = false; //!< Current flying state (read only)

        [ReadOnly]
        public float Progress = 0f; //!< Current progress 0-1 (read only)

        [Header("User Control During Flight")]
        [Tooltip("If true, clicking stops the flight (useful for allowing user to take control)")]
        public bool StopOnMouseClick = false; //!< Stop flight when user clicks

        [Header("Camera Following")]
        [Tooltip("Smoothness of camera following the path (lower = smoother cinematic, higher = tighter tracking)")]
        [Range(0.1f, 10f)]
        public float FollowLerpSpeed = 5f; //!< Lerp speed for camera following

        [Tooltip("If true, camera smoothly transitions to start position. If false, jumps immediately.")]
        public bool SmoothStart = true; //!< Enable smooth transition when starting to fly

        // Private state
        private Camera _mainCamera;
        private SceneMouseNavigation _nav;
        private float _currentTime = 0f;
        private float _delayTimer = 0f;
        private bool _isDelaying = false;
        private bool _forwardDirection = true;
        private bool _lastSignalStartValue = false;
        private Vector3 _originalCameraPosition;
        private Quaternion _originalCameraRotation;
        private Quaternion _currentRotation;
        private Vector3 _pathDirection;
        private GameObject _pathFollower; // Hidden object that moves along the path for camera to follow

        void Awake()
        {
            // Find main camera and SceneMouseNavigation
            GameObject mainCamObj = GameObject.Find("/realvirtual/Main Camera");
            if (mainCamObj != null)
            {
                _mainCamera = mainCamObj.GetComponent<Camera>();
                _nav = mainCamObj.GetComponent<SceneMouseNavigation>();
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_mainCamera == null)
            {
                Logger.Warning("CameraFlyBetweenPoints: Could not find main camera", this);
            }
        }

        void OnEnable()
        {
            if (AutoStartOnEnable)
            {
                StartFly();
            }
        }

        void OnDisable()
        {
            if (IsFlying)
            {
                StopFly();
            }

            // Ensure path follower is cleaned up
            DestroyPathFollower();
        }

        void Update()
        {
            // === Trigger Detection (must run BEFORE flight logic) ===

            // Handle PLC signal start (rising edge detection)
            if (SignalStart != null)
            {
                bool currentValue = SignalStart.Value;
                if (currentValue && !_lastSignalStartValue && !IsFlying)
                {
                    StartFly();
                }
                _lastSignalStartValue = currentValue;
            }

            // Handle PLC signal stop
            if (SignalStop != null && SignalStop.Value && IsFlying)
            {
                StopFly();
            }

            // Handle public bool triggers
            if (StartFlying && !IsFlying)
            {
                StartFlying = false;
                StartFly();
            }

            if (StopFlying && IsFlying)
            {
                StopFlying = false;
                StopFly();
            }

            // Update output signal
            if (SignalIsFlying != null)
            {
                SignalIsFlying.Value = IsFlying;
            }

            // === Flight Logic ===
            if (!IsFlying || _mainCamera == null)
                return;

            // Note: StopOnMouseClick is handled by SceneMouseNavigation.StartRidingTransform
            // Check if navigation stopped following (e.g., due to mouse click)
            if (_nav != null && !_nav.IsFollowing && !_isDelaying)
            {
                StopFly();
                return;
            }

            // Handle start delay
            if (_isDelaying)
            {
                _delayTimer += Time.deltaTime;
                if (_delayTimer >= StartDelay)
                {
                    _isDelaying = false;
                }
                return;
            }

            // Update animation time
            _currentTime += Time.deltaTime * (_forwardDirection ? 1f : -1f);

            // Calculate progress
            float t = Mathf.Clamp01(_currentTime / Duration);
            Progress = _forwardDirection ? t : 1f - t;

            // Apply easing
            float easedT = ApplyEasing(t);

            // Move the path follower along the OFFSET path with proper rotation
            // SceneMouseNavigation.StartRidingTransform handles camera following the path follower
            if (StartPoint != null && EndPoint != null && _pathFollower != null)
            {
                // Calculate base path position
                Vector3 pathPosition = Vector3.Lerp(StartPoint.position, EndPoint.position, easedT);

                // Update path direction
                _pathDirection = (EndPoint.position - StartPoint.position).normalized;
                if (!_forwardDirection)
                    _pathDirection = -_pathDirection;

                // Apply position offset to get the actual camera position
                Vector3 offsetPosition = CalculateOffsetPosition(pathPosition, _pathDirection);

                // Calculate target rotation based on rotation mode
                Quaternion targetRotation = CalculatePathFollowerRotation(pathPosition, offsetPosition, easedT);

                // Apply smooth rotation interpolation to path follower
                _currentRotation = Quaternion.Slerp(_currentRotation, targetRotation, Time.deltaTime * FollowLerpSpeed * 2f);

                // Update path follower transform - SceneMouseNavigation will follow this
                _pathFollower.transform.SetPositionAndRotation(offsetPosition, _currentRotation);
            }

            // Check for completion
            if (t >= 1f)
            {
                OnReachedEnd();
            }
            else if (t <= 0f && !_forwardDirection)
            {
                OnReachedStart();
            }
        }

        private float ApplyEasing(float t)
        {
            switch (Easing)
            {
                case EasingMode.EaseIn:
                    return t * t;
                case EasingMode.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case EasingMode.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case EasingMode.Linear:
                default:
                    return t;
            }
        }

        //! Calculates the offset position from the path based on PositionOffset
        private Vector3 CalculateOffsetPosition(Vector3 pathPosition, Vector3 pathDirection)
        {
            if (PositionOffset == Vector3.zero)
                return pathPosition;

            // Build a coordinate system based on path direction
            Vector3 forward = pathDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.magnitude < 0.001f)
                right = Vector3.Cross(Vector3.forward, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;

            // Apply position offset (X=right, Y=up, Z=forward relative to path)
            Vector3 offset = right * PositionOffset.x + up * PositionOffset.y + forward * PositionOffset.z;

            return pathPosition + offset;
        }

        //! Calculates the rotation for the path follower based on RotationMode
        private Quaternion CalculatePathFollowerRotation(Vector3 pathPosition, Vector3 cameraPosition, float easedT)
        {
            Quaternion baseRotation;

            switch (RotationMode)
            {
                case LookMode.LookAtTarget:
                    if (LookAtTarget != null)
                    {
                        Vector3 lookDir = (LookAtTarget.position - cameraPosition).normalized;
                        if (lookDir != Vector3.zero)
                        {
                            baseRotation = Quaternion.LookRotation(lookDir, Vector3.up);
                            break;
                        }
                    }
                    // Fallback to looking at path position
                    Vector3 toPath = (pathPosition - cameraPosition).normalized;
                    if (toPath != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(toPath, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, easedT);
                    break;

                case LookMode.LookAlongPath:
                    if (_pathDirection != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(_pathDirection, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, easedT);
                    break;

                case LookMode.LookAtEndPoint:
                    // Always look at the end point destination (keeps destination in view)
                    Vector3 targetPos = _forwardDirection ? EndPoint.position : StartPoint.position;
                    Vector3 toEnd = (targetPos - cameraPosition).normalized;
                    if (toEnd != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(toEnd, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, easedT);
                    break;

                case LookMode.LookAtPath:
                    // Look at the path position (useful when camera is offset)
                    Vector3 toPathLook = (pathPosition - cameraPosition).normalized;
                    if (toPathLook != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(toPathLook, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, easedT);
                    break;

                case LookMode.InterpolateRotation:
                default:
                    baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, easedT);
                    break;
            }

            // Apply horizontal (yaw) and vertical (pitch) angle offsets in world space
            if (HorizontalAngle != 0f || VerticalAngle != 0f)
            {
                // World-space rotation: apply offset around world Y (horizontal) and world X (vertical)
                Quaternion worldOffset = Quaternion.Euler(-VerticalAngle, HorizontalAngle, 0f);
                // Multiply worldOffset * baseRotation to apply offset in world space
                return worldOffset * baseRotation;
            }

            return baseRotation;
        }

        private void OnReachedEnd()
        {
            // Pulse the reached end signal
            if (SignalReachedEnd != null)
            {
                SignalReachedEnd.Value = true;
                // Reset in next frame
                Invoke(nameof(ResetReachedEndSignal), 0.1f);
            }

            switch (Mode)
            {
                case PlayMode.Once:
                    StopFly();
                    break;
                case PlayMode.Loop:
                    _currentTime = 0f;
                    break;
                case PlayMode.PingPong:
                    _forwardDirection = false;
                    _currentTime = Duration;
                    break;
            }
        }

        private void OnReachedStart()
        {
            // Only relevant for PingPong mode
            if (Mode == PlayMode.PingPong)
            {
                _forwardDirection = true;
                _currentTime = 0f;
            }
        }

        private void ResetReachedEndSignal()
        {
            if (SignalReachedEnd != null)
            {
                SignalReachedEnd.Value = false;
            }
        }

        //! Starts the camera fly animation from start point to end point.
        //! Can be called from button OnClick events or via code.
        [Button("Start Flying")]
        public void StartFly()
        {
            if (StartPoint == null || EndPoint == null)
            {
                Logger.Warning("CameraFlyBetweenPoints: Start and End points must be assigned", this);
                return;
            }

            if (_mainCamera == null)
            {
                Logger.Warning("CameraFlyBetweenPoints: No camera found", this);
                return;
            }

            // Store original camera transform for potential restoration
            _originalCameraPosition = _mainCamera.transform.position;
            _originalCameraRotation = _mainCamera.transform.rotation;

            // Initialize animation state
            _currentTime = 0f;
            _forwardDirection = true;
            Progress = 0f;
            IsFlying = true;

            // Calculate initial path direction
            _pathDirection = (EndPoint.position - StartPoint.position).normalized;

            // Handle start delay
            if (StartDelay > 0f)
            {
                _isDelaying = true;
                _delayTimer = 0f;
            }
            else
            {
                _isDelaying = false;
            }

            // Create the path follower object first
            CreatePathFollower();

            // Position the path follower at the initial offset position with correct rotation
            Vector3 initialOffsetPosition = CalculateOffsetPosition(StartPoint.position, _pathDirection);
            Quaternion initialRotation = CalculatePathFollowerRotation(StartPoint.position, initialOffsetPosition, 0f);
            _pathFollower.transform.position = initialOffsetPosition;
            _pathFollower.transform.rotation = initialRotation;
            _currentRotation = initialRotation;

            // Use SceneMouseNavigation's StartRidingTransform to follow the path follower
            // This properly integrates with the navigation system while matching our rotation
            if (_nav != null)
            {
                _nav.StartRidingTransform(_pathFollower, FollowLerpSpeed, SmoothStart, StopOnMouseClick);
            }
            else
            {
                Logger.Warning("CameraFlyBetweenPoints: SceneMouseNavigation required but not found", this);
                IsFlying = false;
                DestroyPathFollower();
            }
        }

        //! Creates the hidden path follower GameObject
        private void CreatePathFollower()
        {
            // Destroy existing path follower if any
            if (_pathFollower != null)
            {
                Destroy(_pathFollower);
            }

            // Create a new hidden game object to follow the path
            _pathFollower = new GameObject("_CameraPathFollower");
            _pathFollower.hideFlags = HideFlags.HideAndDontSave;

            // Position at start point
            _pathFollower.transform.position = StartPoint.position;
            _pathFollower.transform.rotation = StartPoint.rotation;
        }

        //! Stops the camera fly animation.
        //! Camera remains at its current position.
        [Button("Stop Flying")]
        public void StopFly()
        {
            IsFlying = false;
            _isDelaying = false;

            // Stop riding the path follower - this returns camera control to user
            if (_nav != null && _nav.IsFollowing)
            {
                _nav.StopFollowing();
            }

            // Destroy path follower after stopping
            DestroyPathFollower();

            // Update output signal
            if (SignalIsFlying != null)
            {
                SignalIsFlying.Value = false;
            }
        }

        //! Destroys the path follower GameObject
        private void DestroyPathFollower()
        {
            if (_pathFollower != null)
            {
                Destroy(_pathFollower);
                _pathFollower = null;
            }
        }

        //! Stops flying and restores the camera to its original position before flying started.
        [Button("Stop and Restore")]
        public void StopAndRestore()
        {
            IsFlying = false;
            _isDelaying = false;

            // Stop following first
            if (_nav != null && _nav.IsFollowing)
            {
                _nav.StopFollowing();
            }

            DestroyPathFollower();

            // Restore camera to original position via SceneMouseNavigation
            if (_nav != null)
            {
                float syncDistance = 10f;
                Vector3 targetPos = _originalCameraPosition + _originalCameraRotation * Vector3.forward * syncDistance;
                Vector3 cameraRotation = _originalCameraRotation.eulerAngles;
                _nav.SetNewCameraPosition(targetPos, syncDistance, cameraRotation, true);
            }
            else
            {
                Logger.Warning("CameraFlyBetweenPoints: SceneMouseNavigation required but not found", this);
            }

            // Update output signal
            if (SignalIsFlying != null)
            {
                SignalIsFlying.Value = false;
            }
        }

        //! Flies in reverse from the current position back to the start point.
        [Button("Fly Reverse")]
        public void FlyReverse()
        {
            if (!IsFlying)
            {
                StartFly();
            }
            _forwardDirection = false;
            _currentTime = Duration * Progress;
        }

        //! Sets the camera immediately to the start point without animating.
        [Button("Go To Start")]
        public void GoToStart()
        {
            if (StartPoint == null)
            {
                Logger.Warning("CameraFlyBetweenPoints: StartPoint not assigned", this);
                return;
            }

            if (_nav != null)
            {
                // Calculate offset position if applicable
                Vector3 pathDir = EndPoint != null ? (EndPoint.position - StartPoint.position).normalized : StartPoint.forward;
                Vector3 cameraPos = CalculateOffsetPosition(StartPoint.position, pathDir);
                Quaternion cameraRot = CalculatePathFollowerRotation(StartPoint.position, cameraPos, 0f);

                float syncDistance = 10f;
                Vector3 targetPos = cameraPos + cameraRot * Vector3.forward * syncDistance;
                _nav.SetNewCameraPosition(targetPos, syncDistance, cameraRot.eulerAngles, true);
            }
            else
            {
                Logger.Warning("CameraFlyBetweenPoints: SceneMouseNavigation required but not found", this);
            }
        }

        //! Sets the camera immediately to the end point without animating.
        [Button("Go To End")]
        public void GoToEnd()
        {
            if (EndPoint == null)
            {
                Logger.Warning("CameraFlyBetweenPoints: EndPoint not assigned", this);
                return;
            }

            if (_nav != null)
            {
                // Calculate offset position if applicable
                Vector3 pathDir = StartPoint != null ? (EndPoint.position - StartPoint.position).normalized : EndPoint.forward;
                Vector3 cameraPos = CalculateOffsetPosition(EndPoint.position, pathDir);
                Quaternion cameraRot = CalculatePathFollowerRotation(EndPoint.position, cameraPos, 1f);

                float syncDistance = 10f;
                Vector3 targetPos = cameraPos + cameraRot * Vector3.forward * syncDistance;
                _nav.SetNewCameraPosition(targetPos, syncDistance, cameraRot.eulerAngles, true);
            }
            else
            {
                Logger.Warning("CameraFlyBetweenPoints: SceneMouseNavigation required but not found", this);
            }
        }

        //! Sets the start point to the current camera position and rotation.
        //! Useful for setting up fly paths in the editor.
        [Button("Set Start From Camera")]
        public void SetStartFromCamera()
        {
            if (_mainCamera != null && StartPoint != null)
            {
                StartPoint.position = _mainCamera.transform.position;
                StartPoint.rotation = _mainCamera.transform.rotation;
            }
        }

        //! Sets the end point to the current camera position and rotation.
        //! Useful for setting up fly paths in the editor.
        [Button("Set End From Camera")]
        public void SetEndFromCamera()
        {
            if (_mainCamera != null && EndPoint != null)
            {
                EndPoint.position = _mainCamera.transform.position;
                EndPoint.rotation = _mainCamera.transform.rotation;
            }
        }

#if UNITY_EDITOR
        //! Creates empty GameObjects for Start and End points as children.
        [Button("Create Point Objects")]
        public void CreatePointObjects()
        {
            if (StartPoint == null)
            {
                GameObject startObj = new GameObject("FlyStartPoint");
                startObj.transform.SetParent(transform);
                startObj.transform.localPosition = Vector3.zero;
                StartPoint = startObj.transform;

                if (_mainCamera != null)
                {
                    StartPoint.position = _mainCamera.transform.position;
                    StartPoint.rotation = _mainCamera.transform.rotation;
                }
            }

            if (EndPoint == null)
            {
                GameObject endObj = new GameObject("FlyEndPoint");
                endObj.transform.SetParent(transform);
                endObj.transform.localPosition = Vector3.forward * 5f;
                EndPoint = endObj.transform;

                if (_mainCamera != null)
                {
                    EndPoint.position = _mainCamera.transform.position + _mainCamera.transform.forward * 5f;
                    EndPoint.rotation = _mainCamera.transform.rotation;
                }
            }
        }
#endif

        [Header("Gizmo Settings")]
        [Tooltip("Number of intermediate camera positions to show in gizmos")]
        [Range(0, 20)]
        public int GizmoIntermediateSteps = 5; //!< Number of intermediate camera positions shown in editor gizmos

        [Tooltip("Size of the camera frustum gizmos")]
        [Range(0.5f, 5f)]
        public float GizmoFrustumSize = 2f; //!< Size of the camera frustum visualization

        void OnDrawGizmosSelected()
        {
            // Draw the fly path in the editor
            if (StartPoint != null && EndPoint != null)
            {
                Vector3 pathDir = (EndPoint.position - StartPoint.position).normalized;

                // Draw the base path (thin cyan line)
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
                Gizmos.DrawLine(StartPoint.position, EndPoint.position);

                // Calculate offset positions for start and end
                Vector3 offsetStartPos = CalculateOffsetPositionGizmo(StartPoint.position, pathDir);
                Vector3 offsetEndPos = CalculateOffsetPositionGizmo(EndPoint.position, pathDir);

                // Draw the actual camera path with offset (yellow line)
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(offsetStartPos, offsetEndPos);

                // Draw connection lines from path to offset positions (if offset is applied)
                if (PositionOffset != Vector3.zero)
                {
                    Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                    Gizmos.DrawLine(StartPoint.position, offsetStartPos);
                    Gizmos.DrawLine(EndPoint.position, offsetEndPos);
                }

                // Calculate rotations for start and end
                Quaternion startRot = CalculateTargetRotationGizmo(offsetStartPos, pathDir, 0f);
                Quaternion endRot = CalculateTargetRotationGizmo(offsetEndPos, pathDir, 1f);

                // Draw camera frustum at start (green)
                Gizmos.color = Color.green;
                Gizmos.matrix = Matrix4x4.TRS(offsetStartPos, startRot, Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, 60f, GizmoFrustumSize, 0.1f, 1.78f);

                // Draw camera frustum at end (red)
                Gizmos.color = Color.red;
                Gizmos.matrix = Matrix4x4.TRS(offsetEndPos, endRot, Vector3.one);
                Gizmos.DrawFrustum(Vector3.zero, 60f, GizmoFrustumSize, 0.1f, 1.78f);

                Gizmos.matrix = Matrix4x4.identity;

                // Draw intermediate camera positions along the path
                if (GizmoIntermediateSteps > 0)
                {
                    for (int i = 1; i <= GizmoIntermediateSteps; i++)
                    {
                        float t = (float)i / (GizmoIntermediateSteps + 1);
                        float easedT = ApplyEasing(t);

                        // Calculate intermediate path position
                        Vector3 pathPos = Vector3.Lerp(StartPoint.position, EndPoint.position, easedT);
                        Vector3 offsetPos = CalculateOffsetPositionGizmo(pathPos, pathDir);
                        Quaternion rot = CalculateTargetRotationGizmo(offsetPos, pathDir, easedT);

                        // Draw intermediate camera position (gradient from green to red)
                        Gizmos.color = Color.Lerp(Color.green, Color.red, t);
                        Gizmos.matrix = Matrix4x4.TRS(offsetPos, rot, Vector3.one * 0.6f);
                        Gizmos.DrawFrustum(Vector3.zero, 60f, GizmoFrustumSize * 0.5f, 0.05f, 1.78f);

                        Gizmos.matrix = Matrix4x4.identity;

                        // Draw small sphere at camera position
                        Gizmos.DrawWireSphere(offsetPos, 0.1f);

                        // Draw direction line showing where camera is looking
                        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
                        Gizmos.DrawLine(offsetPos, offsetPos + rot * Vector3.forward * GizmoFrustumSize * 0.3f);
                    }
                }

                // Draw spheres at base path points
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawWireSphere(StartPoint.position, 0.15f);
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawWireSphere(EndPoint.position, 0.15f);

                // Draw larger spheres at offset positions
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(offsetStartPos, 0.25f);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(offsetEndPos, 0.25f);

                // Draw look at target connection if in LookAtTarget mode
                if (RotationMode == LookMode.LookAtTarget && LookAtTarget != null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(offsetStartPos, LookAtTarget.position);
                    Gizmos.DrawLine(offsetEndPos, LookAtTarget.position);
                    Gizmos.DrawWireSphere(LookAtTarget.position, 0.3f);

                    // Draw intermediate look-at lines
                    if (GizmoIntermediateSteps > 0)
                    {
                        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                        for (int i = 1; i <= GizmoIntermediateSteps; i++)
                        {
                            float t = (float)i / (GizmoIntermediateSteps + 1);
                            Vector3 pathPos = Vector3.Lerp(StartPoint.position, EndPoint.position, ApplyEasing(t));
                            Vector3 offsetPos = CalculateOffsetPositionGizmo(pathPos, pathDir);
                            Gizmos.DrawLine(offsetPos, LookAtTarget.position);
                        }
                    }
                }
            }
        }

        // Gizmo-safe version of CalculateOffsetPosition (doesn't rely on runtime state)
        private Vector3 CalculateOffsetPositionGizmo(Vector3 pathPosition, Vector3 pathDirection)
        {
            if (PositionOffset == Vector3.zero)
                return pathPosition;

            Vector3 forward = pathDirection;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.magnitude < 0.001f)
                right = Vector3.Cross(Vector3.forward, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;

            // Apply position offset (X=right, Y=up, Z=forward relative to path)
            Vector3 offset = right * PositionOffset.x + up * PositionOffset.y + forward * PositionOffset.z;

            return pathPosition + offset;
        }

        // Gizmo-safe version of CalculateTargetRotation
        private Quaternion CalculateTargetRotationGizmo(Vector3 cameraPosition, Vector3 pathDirection, float t)
        {
            Quaternion baseRotation;

            switch (RotationMode)
            {
                case LookMode.LookAtTarget:
                    if (LookAtTarget != null)
                    {
                        Vector3 lookDir = (LookAtTarget.position - cameraPosition).normalized;
                        if (lookDir != Vector3.zero)
                        {
                            baseRotation = Quaternion.LookRotation(lookDir, Vector3.up);
                            break;
                        }
                    }
                    // Fallback to looking at path
                    Vector3 pathPos = Vector3.Lerp(StartPoint.position, EndPoint.position, t);
                    Vector3 toPath = (pathPos - cameraPosition).normalized;
                    if (toPath != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(toPath, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, t);
                    break;

                case LookMode.LookAlongPath:
                    if (pathDirection != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(pathDirection, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, t);
                    break;

                case LookMode.LookAtEndPoint:
                    // Always look at the end point destination
                    Vector3 toEndGizmo = (EndPoint.position - cameraPosition).normalized;
                    if (toEndGizmo != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(toEndGizmo, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, t);
                    break;

                case LookMode.LookAtPath:
                    Vector3 pathPosLook = Vector3.Lerp(StartPoint.position, EndPoint.position, t);
                    Vector3 toPathLook = (pathPosLook - cameraPosition).normalized;
                    if (toPathLook != Vector3.zero)
                        baseRotation = Quaternion.LookRotation(toPathLook, Vector3.up);
                    else
                        baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, t);
                    break;

                case LookMode.InterpolateRotation:
                default:
                    baseRotation = Quaternion.Slerp(StartPoint.rotation, EndPoint.rotation, t);
                    break;
            }

            // Apply horizontal (yaw) and vertical (pitch) angle offsets in world space
            if (HorizontalAngle != 0f || VerticalAngle != 0f)
            {
                // World-space rotation: apply offset around world Y (horizontal) and world X (vertical)
                Quaternion worldOffset = Quaternion.Euler(-VerticalAngle, HorizontalAngle, 0f);
                // Multiply worldOffset * baseRotation to apply offset in world space
                return worldOffset * baseRotation;
            }

            return baseRotation;
        }
    }
}
