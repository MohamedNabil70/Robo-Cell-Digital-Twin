// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Example Drive component demonstrating PrePost FixedUpdate event usage for precise timing control
    public class ExampleDriveWithPrePostFixed : realvirtualBehavior, IPreFixedUpdate, IPostFixedUpdate
    {
        [Header("Drive Settings")]
        public float TargetSpeed = 100f; //!< Target speed in millimeters per second
        public float Acceleration = 50f; //!< Acceleration in millimeters per second squared

        [Header("PLC Signals")]
        public PLCInputFloat PLCTargetSpeed; //!< PLC input for target speed control
        public PLCOutputFloat PLCCurrentSpeed; //!< PLC output for current speed feedback
        public PLCOutputBool PLCAtTarget; //!< PLC output indicating target speed reached

        [Header("Debug")]
        public bool EnableDebugLogging = false; //!< Enable debug logging for timing analysis

        private float _currentSpeed = 0f;
        private float _lastTargetSpeed = 0f;
        private bool _inputsChanged = false;
        private Vector3 _lastPosition;

        //! Called before Unity's FixedUpdate - validates inputs and prepares calculations
        //! IMPLEMENTS IPreFixedUpdate::PreFixedUpdate
        public void PreFixedUpdate()
        {
            // Read PLC inputs and validate before physics calculations
            float targetFromPLC = PLCTargetSpeed?.Value ?? TargetSpeed;

            if (Mathf.Abs(targetFromPLC - _lastTargetSpeed) > 0.01f)
            {
                _inputsChanged = true;
                _lastTargetSpeed = targetFromPLC;

                if (EnableDebugLogging)
                    Logger.Message($"PreFixed: Target speed changed to {targetFromPLC:F2}", this);
            }

            // Prepare movement calculations
            if (_inputsChanged)
            {
                // Calculate required acceleration to reach target
                float speedDiff = _lastTargetSpeed - _currentSpeed;
                float maxAccelThisFrame = Acceleration * Time.fixedDeltaTime;

                if (Mathf.Abs(speedDiff) <= maxAccelThisFrame)
                {
                    _currentSpeed = _lastTargetSpeed;
                }
                else
                {
                    _currentSpeed += Mathf.Sign(speedDiff) * maxAccelThisFrame;
                }
            }

            _lastPosition = transform.position;
        }

        //! Called after Unity's FixedUpdate - processes physics results and updates outputs
        //! IMPLEMENTS IPostFixedUpdate::PostFixedUpdate
        public void PostFixedUpdate()
        {
            // Calculate actual movement that occurred during FixedUpdate
            Vector3 actualMovement = transform.position - _lastPosition;
            float actualSpeed = actualMovement.magnitude / Time.fixedDeltaTime * 1000f; // Convert to mm/s

            // Update PLC outputs with actual results
            if (PLCCurrentSpeed != null)
                PLCCurrentSpeed.Value = actualSpeed;

            // Check if we've reached the target speed (within tolerance)
            bool atTarget = Mathf.Abs(_currentSpeed - _lastTargetSpeed) < 1f;
            if (PLCAtTarget != null)
                PLCAtTarget.Value = atTarget;

            if (EnableDebugLogging && _inputsChanged)
            {
                Logger.Message($"PostFixed: Current speed {_currentSpeed:F2}, Actual speed {actualSpeed:F2}, At target: {atTarget}", this);
            }

            _inputsChanged = false;
        }

        void FixedUpdate()
        {
            // Apply the calculated movement
            // This demonstrates that our PreFixedUpdate calculations are used in FixedUpdate
            Vector3 movement = Vector3.forward * (_currentSpeed / 1000f) * Time.fixedDeltaTime;
            transform.Translate(movement, Space.Self);
        }

        void Start()
        {
            _lastPosition = transform.position;
            Logger.Message("ExampleDriveWithPrePostFixed started - using PrePost FixedUpdate timing", this);
        }

        [ContextMenu("Show Timing Info")]
        public void ShowTimingInfo()
        {
            Logger.Message($"Current Speed: {_currentSpeed:F2} mm/s, Target: {_lastTargetSpeed:F2} mm/s", this);
            Logger.Message($"At Target: {(Mathf.Abs(_currentSpeed - _lastTargetSpeed) < 1f)}", this);
        }
    }
}