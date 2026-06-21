// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using NaughtyAttributes;

namespace realvirtual
{
    #region doc
    //! Enables keyboard-controlled movement of GameObjects with smooth interpolation and axis limits.

    //! KeyboardMove provides precise manual control over GameObject positioning using configurable keyboard inputs.
    //! It supports smooth lerped movement, customizable speed settings, and position limits for each axis.
    //! This component is ideal for manual positioning tasks, debugging object placement, and creating
    //! interactive control systems for automation simulations.
    //!
    //! Key Features:
    //! - Independent control of X, Y, and Z axes with dual-key support
    //! - Smooth lerped movement with adjustable smoothing factor
    //! - Position limits for constraining movement within defined boundaries
    //! - Local space movement respecting object rotation
    //! - Visual gizmos showing movement boundaries in Scene view
    //!
    //! Common Applications:
    //! - Manual positioning of robots or machinery during setup
    //! - Debug tool for testing object placement
    //! - Interactive control for demonstration scenarios
    //! - Fine-tuning object positions in virtual commissioning
    //!
    //! Integration Points:
    //! - Can be combined with Drive components for hybrid control
    //! - Works alongside physics-based movement systems
    //! - Supports runtime enabling/disabling for mode switching
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/components-and-scripts/scene-interaction/keyboard-move
    #endregion
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/scene-interaction/keyboard-move")]
    public class KeyboardMove : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Movement speed in meters per second")]
        public float Speed = 1f; //!< Movement speed in meters per second
        [Tooltip("Lerp smoothing factor - higher values provide more responsive movement (1-20 recommended)")]
        public float LerpFactor = 10f; //!< Lerp smoothing factor - higher values provide more responsive movement

        [Header("Input Keys - X Axis")]
        [Tooltip("Primary key for negative X axis movement (left)")]
        public KeyCode XNegativeKey1 = KeyCode.Keypad4; //!< Primary key for negative X axis movement
        [Tooltip("Secondary key for negative X axis movement (optional - if set, both keys required)")]
        public KeyCode XNegativeKey2 = KeyCode.None; //!< Secondary key for negative X axis movement (optional)
        [Tooltip("Primary key for positive X axis movement (right)")]
        public KeyCode XPositiveKey1 = KeyCode.Keypad6; //!< Primary key for positive X axis movement
        [Tooltip("Secondary key for positive X axis movement (optional - if set, both keys required)")]
        public KeyCode XPositiveKey2 = KeyCode.None; //!< Secondary key for positive X axis movement (optional)

        [Header("Input Keys - Y Axis")]
        [Tooltip("Primary key for negative Y axis movement (down)")]
        public KeyCode YNegativeKey1 = KeyCode.None; //!< Primary key for negative Y axis movement
        [Tooltip("Secondary key for negative Y axis movement (optional - if set, both keys required)")]
        public KeyCode YNegativeKey2 = KeyCode.None; //!< Secondary key for negative Y axis movement (optional)
        [Tooltip("Primary key for positive Y axis movement (up)")]
        public KeyCode YPositiveKey1 = KeyCode.None; //!< Primary key for positive Y axis movement
        [Tooltip("Secondary key for positive Y axis movement (optional - if set, both keys required)")]
        public KeyCode YPositiveKey2 = KeyCode.None; //!< Secondary key for positive Y axis movement (optional)

        [Header("Input Keys - Z Axis")]
        [Tooltip("Primary key for negative Z axis movement (backward)")]
        public KeyCode ZNegativeKey1 = KeyCode.Keypad2; //!< Primary key for negative Z axis movement
        [Tooltip("Secondary key for negative Z axis movement (optional - if set, both keys required)")]
        public KeyCode ZNegativeKey2 = KeyCode.None; //!< Secondary key for negative Z axis movement (optional)
        [Tooltip("Primary key for positive Z axis movement (forward)")]
        public KeyCode ZPositiveKey1 = KeyCode.Keypad8; //!< Primary key for positive Z axis movement
        [Tooltip("Secondary key for positive Z axis movement (optional - if set, both keys required)")]
        public KeyCode ZPositiveKey2 = KeyCode.None; //!< Secondary key for positive Z axis movement (optional)

        [Header("Reset Control")]
        [Tooltip("Key to reset GameObject to initial position")]
        public KeyCode ResetKey = KeyCode.Keypad5; //!< Key to reset GameObject to initial position

        [Header("Movement Limits (Unity Units = Meters)")]
        [MinMaxSlider(-100f, 100f)]
        [Tooltip("Minimum and maximum X axis position limits relative to initial position (meters)")]
        public Vector2 XAxisLimits = new Vector2(-10f, 10f); //!< Minimum and maximum X axis position limits in meters
        [MinMaxSlider(-100f, 100f)]
        [Tooltip("Minimum and maximum Y axis position limits relative to initial position (meters)")]
        public Vector2 YAxisLimits = new Vector2(-10f, 10f); //!< Minimum and maximum Y axis position limits in meters
        [MinMaxSlider(-100f, 100f)]
        [Tooltip("Minimum and maximum Z axis position limits relative to initial position (meters)")]
        public Vector2 ZAxisLimits = new Vector2(-10f, 10f); //!< Minimum and maximum Z axis position limits in meters

        private Vector3 targetPosition;
        private Vector3 initialPosition;

        void Start()
        {
            initialPosition = transform.position;
            targetPosition = initialPosition;
        }

        void Update()
        {
            HandleInput();
            MoveToTarget();
        }

        private bool IsKeyPressed(KeyCode key1, KeyCode key2)
        {
            bool key1Pressed = key1 != KeyCode.None && Input.GetKey(key1);
            bool key2Pressed = key2 != KeyCode.None && Input.GetKey(key2);

            // If both keys are defined, both must be pressed
            if (key1 != KeyCode.None && key2 != KeyCode.None)
                return key1Pressed && key2Pressed;

            // If only one key is defined, only that key needs to be pressed
            return key1Pressed || key2Pressed;
        }

        private void HandleInput()
        {
            // Check for reset key
            if (ResetKey != KeyCode.None && Input.GetKeyDown(ResetKey))
            {
                ResetToInitialPosition();
                return;
            }

            Vector3 movement = Vector3.zero;

            // X axis movement
            if (IsKeyPressed(XNegativeKey1, XNegativeKey2))
                movement.x = -1f;
            else if (IsKeyPressed(XPositiveKey1, XPositiveKey2))
                movement.x = 1f;

            // Y axis movement
            if (IsKeyPressed(YNegativeKey1, YNegativeKey2))
                movement.y = -1f;
            else if (IsKeyPressed(YPositiveKey1, YPositiveKey2))
                movement.y = 1f;

            // Z axis movement
            if (IsKeyPressed(ZNegativeKey1, ZNegativeKey2))
                movement.z = -1f;
            else if (IsKeyPressed(ZPositiveKey1, ZPositiveKey2))
                movement.z = 1f;

            // Apply movement with speed in local space
            if (movement != Vector3.zero)
            {
                Vector3 localDeltaMovement = movement * Speed * Time.deltaTime;
                Vector3 worldDeltaMovement = transform.TransformDirection(localDeltaMovement);
                targetPosition += worldDeltaMovement;

                // Clamp to limits in local space
                Vector3 localTargetPosition = transform.InverseTransformPoint(targetPosition);
                Vector3 localInitialPosition = transform.InverseTransformPoint(initialPosition);

                localTargetPosition.x = Mathf.Clamp(localTargetPosition.x, localInitialPosition.x + XAxisLimits.x, localInitialPosition.x + XAxisLimits.y);
                localTargetPosition.y = Mathf.Clamp(localTargetPosition.y, localInitialPosition.y + YAxisLimits.x, localInitialPosition.y + YAxisLimits.y);
                localTargetPosition.z = Mathf.Clamp(localTargetPosition.z, localInitialPosition.z + ZAxisLimits.x, localInitialPosition.z + ZAxisLimits.y);

                targetPosition = transform.TransformPoint(localTargetPosition);
            }
        }

        private void MoveToTarget()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, LerpFactor * Time.deltaTime);
        }

        //! Resets the GameObject to its initial position.
        public void ResetToInitialPosition()
        {
            transform.position = initialPosition;
            targetPosition = initialPosition;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? initialPosition : transform.position;

            // Calculate bounds in world space
            Vector3 minBounds = center + transform.TransformDirection(new Vector3(XAxisLimits.x, YAxisLimits.x, ZAxisLimits.x));
            Vector3 maxBounds = center + transform.TransformDirection(new Vector3(XAxisLimits.y, YAxisLimits.y, ZAxisLimits.y));

            // Draw wireframe box showing movement area
            Gizmos.color = Color.cyan;
            Vector3 size = maxBounds - minBounds;
            Vector3 boxCenter = (minBounds + maxBounds) * 0.5f;

            // Draw the movement bounds as a wireframe cube
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);

            // Reset matrix and draw center point
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, 0.1f);

            // Draw current target position if playing
            if (Application.isPlaying)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(targetPosition, 0.05f);
            }
        }
    }
}