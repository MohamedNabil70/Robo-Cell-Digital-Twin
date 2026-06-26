using UnityEngine;

public class ConveyorControlReceiver : MonoBehaviour
{
    [Header("References")]
    public ConveyorMotor conveyor;
    public string runningTagOverride = "";
    public string objectDetectedTagOverride = "";

    [Header("Behavior")]
    public bool driveIORouterInputTags = true;
    public bool disableConveyorWhenStoppedWithoutRunTag = true;
    public bool forceConveyorToPlcMode = true;

    [Header("Diagnostics")]
    [SerializeField] float targetSpeed;
    [SerializeField] bool running;
    [SerializeField] bool objectDetected;

    void Awake()
    {
        if (conveyor == null)
        {
            conveyor = GetComponent<ConveyorMotor>();
        }
    }

    public void Configure(ConveyorMotor targetConveyor, string runningTag, string objectDetectedTag)
    {
        conveyor = targetConveyor != null ? targetConveyor : conveyor;
        if (!string.IsNullOrWhiteSpace(runningTag)) runningTagOverride = runningTag;
        if (!string.IsNullOrWhiteSpace(objectDetectedTag)) objectDetectedTagOverride = objectDetectedTag;
    }

    public void ApplyControl(ConveyorControlData control)
    {
        if (control == null)
        {
            Debug.LogWarning($"[ConveyorControlReceiver:{name}] Ignored null conveyor control.");
            return;
        }

        if (conveyor == null)
        {
            conveyor = GetComponent<ConveyorMotor>();
        }

        targetSpeed = Mathf.Max(0f, control.currentSpeed);
        running = control.running;
        objectDetected = control.objectDetected;

        if (conveyor == null)
        {
            Debug.LogWarning($"[ConveyorControlReceiver:{name}] No ConveyorMotor assigned.");
            return;
        }

        conveyor.speed = targetSpeed;

        string runTag = !string.IsNullOrWhiteSpace(runningTagOverride)
            ? runningTagOverride
            : conveyor.plcTag;

        if (forceConveyorToPlcMode && !string.IsNullOrWhiteSpace(runTag))
        {
            conveyor.offlineMode = false;
        }

        if (driveIORouterInputTags && IO_Router.Instance != null)
        {
            if (!string.IsNullOrWhiteSpace(runTag))
            {
                IO_Router.Instance.SimulateInput(runTag, running);
            }

            string objectTag = objectDetectedTagOverride;
            if (!string.IsNullOrWhiteSpace(objectTag))
            {
                IO_Router.Instance.SimulateInput(objectTag, objectDetected);
            }
        }

        if (string.IsNullOrWhiteSpace(runTag) && disableConveyorWhenStoppedWithoutRunTag)
        {
            conveyor.enabled = running;
        }
        else if (running && !conveyor.enabled)
        {
            conveyor.enabled = true;
        }
    }
}
