using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Lightweight first-person movement for the capsule used by FirstPersonCam.
/// It only moves/rotates the local player rig and does not affect dashboard data.
/// </summary>
public sealed class FirstPersonCapsuleController : MonoBehaviour
{
    [Tooltip("Camera attached to this capsule while first-person mode is active.")]
    public Camera viewCamera;

    [Tooltip("Camera local position while parented to the capsule.")]
    public Vector3 cameraLocalPosition = new Vector3(0f, 0.55f, 0f);

    [Tooltip("Movement speed in local forward/right directions.")]
    public float moveSpeed = 4f;

    [Tooltip("Multiplier while holding Left Shift.")]
    public float sprintMultiplier = 1.6f;

    [Tooltip("Mouse sensitivity in degrees per input delta unit.")]
    public float mouseSensitivity = 0.12f;

    public float minPitch = -82f;
    public float maxPitch = 82f;

    [Tooltip("Lock and hide cursor while this controller is active.")]
    public bool lockCursorWhileActive = true;

    [Tooltip("Escape releases the cursor without leaving first-person mode.")]
    public bool escapeUnlocksCursor = true;

    bool isActive;
    float pitch;
    CharacterController characterController;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        enabled = false;
    }

    void OnDisable()
    {
        if (isActive)
        {
            SetCursorLocked(false);
        }
    }

    void Update()
    {
        if (!isActive)
        {
            return;
        }

        if (viewCamera == null)
        {
            return;
        }

        if (escapeUnlocksCursor && WasEscapePressed())
        {
            SetCursorLocked(false);
        }

        RotateFromMouse();
        MoveFromInput();
        KeepCameraAttached();
    }

    public void Activate(Camera cameraToUse)
    {
        viewCamera = cameraToUse != null ? cameraToUse : viewCamera;
        if (viewCamera == null)
        {
            enabled = false;
            isActive = false;
            return;
        }

        enabled = true;
        isActive = true;
        AttachCamera();
        SyncPitchFromCamera();
        SetCursorLocked(lockCursorWhileActive);
    }

    public void Deactivate()
    {
        isActive = false;
        SetCursorLocked(false);
        enabled = false;
    }

    public void AttachCamera()
    {
        if (viewCamera == null)
        {
            return;
        }

        Transform cameraTransform = viewCamera.transform;
        cameraTransform.SetParent(transform, false);
        cameraTransform.localPosition = cameraLocalPosition;
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void KeepCameraAttached()
    {
        Transform cameraTransform = viewCamera.transform;
        if (cameraTransform.parent != transform)
        {
            cameraTransform.SetParent(transform, false);
        }

        cameraTransform.localPosition = cameraLocalPosition;
    }

    void SyncPitchFromCamera()
    {
        Vector3 localEuler = viewCamera.transform.localEulerAngles;
        pitch = NormalizeAngle(localEuler.x);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void RotateFromMouse()
    {
        Vector2 mouseDelta = ReadMouseDelta();
        if (mouseDelta.sqrMagnitude <= 0f)
        {
            return;
        }

        float yaw = mouseDelta.x * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, minPitch, maxPitch);

        transform.Rotate(Vector3.up, yaw, Space.World);
        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void MoveFromInput()
    {
        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Vector3 direction = transform.right * input.x + transform.forward * input.y;
        float speed = moveSpeed * (IsSprintHeld() ? sprintMultiplier : 1f);
        Vector3 displacement = direction * speed * Time.unscaledDeltaTime;

        if (characterController != null && characterController.enabled)
        {
            characterController.Move(displacement);
        }
        else
        {
            transform.position += displacement;
        }
    }

    Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input.y += 1f;
            }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        input.x += Input.GetAxisRaw("Horizontal");
        input.y += Input.GetAxisRaw("Vertical");
#endif

        return input;
    }

    Vector2 ReadMouseDelta()
    {
        Vector2 delta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            delta += mouse.delta.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        delta.x += Input.GetAxisRaw("Mouse X");
        delta.y += Input.GetAxisRaw("Mouse Y");
#endif

        return delta;
    }

    bool IsSprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.leftShiftKey.isPressed)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftShift))
        {
            return true;
        }
#endif

        return false;
    }

    bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            return true;
        }
#endif

        return false;
    }

    void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}
