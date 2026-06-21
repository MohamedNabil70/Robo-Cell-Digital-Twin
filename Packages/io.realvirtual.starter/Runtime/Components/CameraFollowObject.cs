// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using NaughtyAttributes;
using UnityEngine;

namespace realvirtual
{
    [AddComponentMenu("realvirtual/Utility/Camera Follow Object")]
    //! Controls camera following of a specific GameObject, can be triggered by buttons or other UI elements.
    //! Provides both one-time focus and continuous following modes with optional user control over rotation and zoom.
    //! The camera follows the object's pivot point (transform.position) for predictable behavior.
    //! When distance is auto-calculated, it uses the object's bounds to ensure the entire object fits in view.
    //!
    //! Usage:
    //! - Attach to a UI button or any GameObject
    //! - Set the ObjectToFollow property to the target GameObject
    //! - Configure follow options (distance, rotation, zoom)
    //! - Call StartFollowing(), StopFollowing(), or FocusOnObject() from button events
    public class CameraFollowObject : MonoBehaviour
    {
        [Header("Target Object")]
        [Tooltip("The GameObject to follow or focus on")]
        public GameObject ObjectToFollow; //!< The GameObject to follow or focus on

        [Header("Camera Distance")]
        [Tooltip("If checked, uses a custom distance. If unchecked, auto-calculates distance based on object bounds to fit at 1/3 screen space")]
        public bool UseCustomDistance = false; //!< If true, uses the specified Distance value instead of auto-calculating based on bounds

        [ShowIf("UseCustomDistance")]
        [Tooltip("Camera distance from the object in meters")]
        public float Distance = 5f; //!< Camera distance from the object in meters

        [Header("Camera Angle")]
        [Tooltip("If checked, uses a custom view angle. If unchecked, uses optimal angle (FocusOnObject) or current camera angle (StartFollowing)")]
        public bool UseCustomViewAngle = false; //!< If true, uses the specified ViewAngle instead of auto-selecting

        [ShowIf("UseCustomViewAngle")]
        [Tooltip("Camera rotation in degrees (X, Y, Z)")]
        public Vector3 ViewAngle = Vector3.zero; //!< Camera rotation in degrees

        [Header("User Control Options")]
        [Tooltip("If true, user can rotate camera around the object while following")]
        public bool AllowRotation = false; //!< If true, user can orbit camera around the object while keeping it centered

        [Tooltip("If true, user can zoom in/out from the object while following")]
        public bool AllowZoom = false; //!< If true, user can adjust distance from the object while following

        [Header("Follow Mode")]
        [Tooltip("If true, automatically starts following when script is enabled")]
        public bool AutoStartFollowing = false; //!< If true, automatically starts following on enable

        [Header("Smoothness")]
        [Tooltip("Controls camera tracking smoothness (0.1 = very smooth/cinematic, 1.0 = tight tracking, 10.0 = near instant)")]
        [Range(0.1f, 10.0f)]
        public float LerpSpeed = 1.0f; //!< Lerp speed multiplier for smooth camera motion

        [Tooltip("If true, camera smoothly transitions to the object when starting to follow. If false, jumps immediately.")]
        public bool SmoothStart = true; //!< Enable smooth transition when starting to follow

        [Header("User Interaction")]
        [Tooltip("If true, following stops when any mouse button is pressed. Useful for allowing user to take control.")]
        public bool StopOnMouseClick = false; //!< Stop following when user clicks any mouse button

        [Tooltip("If true, shows a UI button during camera follow that allows stopping the camera focus")]
        public bool ShowStopButton = false; //!< Show UI button to stop camera focus during following

        [ShowIf("ShowStopButton")]
        [Tooltip("Optional custom prefab for the stop button UI. If null, uses the default rvUICameraFocusButton prefab")]
        public GameObject StopButtonPrefab; //!< Optional custom prefab for stop button, if null uses default from Resources

        [Header("PLC Signal Control")]
        [Tooltip("PLC signal to start/stop following. When signal goes false, following stops")]
        public PLCInputBool PLCSignalStartFollowing; //!< PLC signal to control following (true = follow, false = stop)

        [Tooltip("Public bool that can be controlled externally (e.g., by PLCOutputBool). When false, following stops")]
        public bool FollowActive = false; //!< Public bool to control following state

        // Private references
        private SceneMouseNavigation _nav;
#pragma warning disable 0414
        private bool _wasFollowingLastFrame = false;
#pragma warning restore 0414
        private rvUICameraFocusButton _stopButtonInstance;
        private bool _userRequestedStop = false; // Tracks if user manually stopped via button

        //! Called when script is initialized, finds the SceneMouseNavigation component
        void Awake()
        {
            // Find the main camera's SceneMouseNavigation component (cache GameObject to avoid double Find call)
            GameObject mainCamObj = GameObject.Find("/realvirtual/Main Camera");
            if (mainCamObj != null)
                _nav = mainCamObj.GetComponent<SceneMouseNavigation>();

            if (_nav == null)
            {
                Logger.Warning("CameraFollowObject: Could not find SceneMouseNavigation on /realvirtual/Main Camera", this);
            }
        }

        //! Called when the component is enabled, starts auto-follow if configured
        void OnEnable()
        {
            if (AutoStartFollowing && ObjectToFollow != null)
            {
                StartFollowing();
            }
        }

        //! Called when the component is disabled, cleans up the stop button if it exists
        void OnDisable()
        {
            DestroyStopButton();
        }

        //! Called in FixedUpdate to monitor PLC signal and FollowActive bool.
        //! PLC signal monitoring must be done in FixedUpdate per realvirtual patterns.
        void FixedUpdate()
        {
            // CRITICAL FIX: If user manually stopped following via button, don't auto-restart
            // unless FollowActive is explicitly set to true again by external code or PLC signal
            if (_userRequestedStop && !FollowActive && (PLCSignalStartFollowing == null || !PLCSignalStartFollowing.Value))
            {
                // User stopped manually and nothing is requesting follow - stay stopped
                return;
            }

            // Get current follow state from signals/bool
            bool shouldBeFollowing = GetFollowState();

            // Check if we're currently following
            bool isCurrentlyFollowing = _nav != null && _nav.IsFollowing;

            // Start following if signal/bool is true and we're not following yet
            // Note: StopOnMouseClick is now handled directly in SceneMouseNavigation
            if (shouldBeFollowing && !isCurrentlyFollowing && ObjectToFollow != null)
            {
                // Clear user stop flag when starting following
                _userRequestedStop = false;
                StartFollowing();
                _wasFollowingLastFrame = true;
            }
            // Stop following if signal/bool went false and we are following
            else if (!shouldBeFollowing && isCurrentlyFollowing)
            {
                StopFollowing();
                _wasFollowingLastFrame = false;
            }
            // Update tracking state
            else if (isCurrentlyFollowing)
            {
                _wasFollowingLastFrame = true;
            }
        }

        //! Gets the current follow state from PLC signal and FollowActive bool
        private bool GetFollowState()
        {
            bool plcSignalActive = false;
            bool followBoolActive = FollowActive;

            // Check PLC signal if connected
            if (PLCSignalStartFollowing != null)
            {
                plcSignalActive = PLCSignalStartFollowing.Value;
            }

            // Follow if either PLC signal is true OR FollowActive bool is true
            // Both must be false to stop following
            return plcSignalActive || followBoolActive;
        }

        //! Starts continuous camera following of the target object.
        //! The camera will follow the object's pivot point (transform.position) and keep it centered.
        //! Distance is calculated from object bounds if not specified to ensure the whole object fits in view.
        //! Can be called directly from button OnClick events.
        [Button("Start Following")]
        public void StartFollowing()
        {
            if (ObjectToFollow == null)
            {
                Logger.Warning("CameraFollowObject: No object to follow specified", this);
                return;
            }

            if (_nav == null)
            {
                Logger.Warning("CameraFollowObject: SceneMouseNavigation not found", this);
                return;
            }

            // CRITICAL FIX: Clear user stop flag when starting following
            _userRequestedStop = false;

            // Determine distance parameter (null for auto-calculate or use custom value)
            float? distance = UseCustomDistance ? Distance : (float?)null;

            // Determine view angle parameter (null for auto or use custom value)
            Vector3? viewAngle = UseCustomViewAngle ? ViewAngle : (Vector3?)null;

            // Set the FollowActive boolean to true
            FollowActive = true;

            // Start following with configured settings including lerp speed, smooth start, and stop on click
            _nav.StartFollowing(ObjectToFollow, distance, viewAngle, AllowRotation, AllowZoom, LerpSpeed, SmoothStart, StopOnMouseClick);

            // Show stop button if enabled
            if (ShowStopButton)
            {
                CreateStopButton();
            }
        }

        //! Stops continuous camera following.
        //! Camera returns to normal user-controlled navigation.
        //! Can be called directly from button OnClick events.
        [Button("Stop Following")]
        public void StopFollowing()
        {
            if (_nav == null)
            {
                Logger.Warning("CameraFollowObject: SceneMouseNavigation not found", this);
                return;
            }

            // CRITICAL FIX: Set user stop flag to prevent FixedUpdate from auto-restarting
            _userRequestedStop = true;

            // Set the FollowActive boolean to false
            FollowActive = false;

            // Destroy stop button if it exists
            DestroyStopButton();

            _nav.StopFollowing();
        }

        //! Performs a one-time camera movement to focus on the target object.
        //! The camera will move to view the object's pivot point but will not continuously track it.
        //! Distance is calculated from object bounds if not specified to ensure the whole object fits in view.
        //! Can be called directly from button OnClick events.
        [Button("Focus On Object")]
        public void FocusOnObject()
        {
            if (ObjectToFollow == null)
            {
                Logger.Warning("CameraFollowObject: No object to focus on specified", this);
                return;
            }

            if (_nav == null)
            {
                Logger.Warning("CameraFollowObject: SceneMouseNavigation not found", this);
                return;
            }

            // Determine distance parameter (null for auto-calculate or use custom value)
            float? distance = UseCustomDistance ? Distance : (float?)null;

            // Determine view angle parameter (null for optimal angle or use custom value)
            Vector3? viewAngle = UseCustomViewAngle ? ViewAngle : (Vector3?)null;

            // Focus on object
            _nav.FocusOnObject(ObjectToFollow, distance, viewAngle);
        }

        //! Toggles between following and not following.
        //! If currently following, stops. If not following, starts.
        //! Useful for toggle buttons.
        [Button("Toggle Following")]
        public void ToggleFollowing()
        {
            if (_nav == null)
            {
                Logger.Warning("CameraFollowObject: SceneMouseNavigation not found", this);
                return;
            }

            if (_nav.IsFollowing)
            {
                StopFollowing();
            }
            else
            {
                StartFollowing();
            }
        }

        //! Returns true if currently following the target object
        public bool IsFollowing()
        {
            if (_nav == null)
                return false;

            return _nav.IsFollowing && _nav.FollowedObject == ObjectToFollow;
        }

        //! Sets the target object to follow and optionally starts following immediately
        public void SetObjectToFollow(GameObject obj, bool startImmediately = false)
        {
            ObjectToFollow = obj;

            if (startImmediately && obj != null)
            {
                StartFollowing();
            }
        }

        //! Sets the lerp speed for camera following (can be called during runtime)
        //! Lower values (0.1-0.5) create smooth cinematic motion
        //! Medium values (0.5-2.0) provide balanced tracking
        //! Higher values (2.0-10.0) create tight, responsive tracking
        public void SetLerpSpeed(float speed)
        {
            LerpSpeed = Mathf.Clamp(speed, 0.1f, 10.0f);

            // If currently following, update the active lerp speed
            if (_nav != null && _nav.IsFollowing && _nav.FollowedObject == ObjectToFollow)
            {
                // Restart following with new lerp speed
                StartFollowing();
            }
        }

        //! Creates the stop button UI if it doesn't exist
        private void CreateStopButton()
        {
            // Don't create if already exists
            if (_stopButtonInstance != null)
                return;

            // Try to instantiate from custom prefab or default
            if (StopButtonPrefab != null)
            {
                GameObject instance = Instantiate(StopButtonPrefab);
                _stopButtonInstance = instance.GetComponent<rvUICameraFocusButton>();
            }
            else
            {
                // Use the static Instantiate method from the default prefab
                _stopButtonInstance = rvUICameraFocusButton.Instantiate();
            }

            // Wire the button to call StopFollowing - which will handle button destruction via DestroyStopButton()
            if (_stopButtonInstance != null)
            {
                // Wire directly to the Button component to ensure proper execution order
                UnityEngine.UI.Button button = _stopButtonInstance.GetComponentInChildren<UnityEngine.UI.Button>();
                if (button != null)
                {
                    // Remove the persistent Delete() call and wire up StopFollowing
                    // Note: Don't call Delete() here - StopFollowing() handles button destruction via DestroyStopButton()
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(StopFollowing);
                }
            }
            else
            {
                Logger.Warning("CameraFollowObject: Failed to create stop button", this);
            }
        }

        //! Destroys the stop button UI if it exists
        private void DestroyStopButton()
        {
            if (_stopButtonInstance != null)
            {
                // Remove all listeners from the button before destroying
                UnityEngine.UI.Button button = _stopButtonInstance.GetComponentInChildren<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                }

                // Destroy the button GameObject
                Destroy(_stopButtonInstance.gameObject);
                _stopButtonInstance = null;
            }
        }
    }
}
