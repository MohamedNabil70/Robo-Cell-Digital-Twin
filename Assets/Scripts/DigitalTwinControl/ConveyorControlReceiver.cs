using UnityEngine;
using UnityEngine.Serialization;

public class ConveyorControlReceiver : MonoBehaviour
{
    [Header("References")]
    public ConveyorMotor conveyor;
    public string runningTagOverride = "";
    public string objectDetectedTagOverride = "";
    public GameObject cubeOnConveyorPrefab;
    public GameObject turbineOnConveyorPrefab;
    public Transform objectAnchor;
    public Transform cubeExampleTransform;
    public string cubeExampleName = "C1";
    public Transform turbineExampleTransform;
    public string turbineExampleName = "TurbineExample";

    [Header("Behavior")]
    public bool driveIORouterInputTags = true;
    public bool disableConveyorWhenStoppedWithoutRunTag = true;
    public bool forceConveyorToPlcMode = true;
    public bool parentDetectedObjectToAnchor = true;
    public Vector3 conveyorMovementDirection = Vector3.right;
    public Vector3 cubeOnConveyorScale = new Vector3(1f, 1f, 1f);

    [Header("Diagnostics")]
    [FormerlySerializedAs("targetSpeed")]
    [SerializeField] float goalSpeed;
    [SerializeField] float currentSpeed;
    [SerializeField] bool running;
    [SerializeField] bool objectDetected;
    [SerializeField] GameObject objectOnConveyor;
    [SerializeField] string lastObjectId = "";
    [SerializeField] bool warnedMissingObjectPrefab;

    void Awake()
    {
        if (conveyor == null)
        {
            conveyor = GetComponent<ConveyorMotor>();
        }

        if (objectAnchor == null)
        {
            objectAnchor = transform;
        }
    }

    void Update()
    {
        bool canMoveObject = running &&
            objectDetected &&
            objectOnConveyor != null &&
            objectOnConveyor.activeInHierarchy;

        currentSpeed = canMoveObject ? goalSpeed : 0f;
        if (!canMoveObject)
        {
            return;
        }

        objectOnConveyor.transform.position += GetMovementDirection() * currentSpeed * Time.deltaTime;
    }

    public void Configure(ConveyorMotor targetConveyor, string runningTag, string objectDetectedTag)
    {
        conveyor = targetConveyor != null ? targetConveyor : conveyor;
        if (!string.IsNullOrWhiteSpace(runningTag)) runningTagOverride = runningTag;
        if (!string.IsNullOrWhiteSpace(objectDetectedTag)) objectDetectedTagOverride = objectDetectedTag;
    }

    public void ApplyControl(ConveyorControlData control)
    {
        ApplyControl(string.Empty, control);
    }

    public void ApplyControl(string objectId, ConveyorControlData control)
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

        if (!string.IsNullOrWhiteSpace(objectId))
        {
            lastObjectId = objectId;
        }

        if (control.hasCurrentSpeed)
        {
            goalSpeed = Mathf.Max(0f, control.currentSpeed);
        }

        if (control.hasRunning)
        {
            running = control.running;
        }

        if (control.hasObjectDetected)
        {
            objectDetected = control.objectDetected;
            if (objectDetected)
            {
                EnsureObjectOnConveyor(lastObjectId);
            }
            else
            {
                HideObjectOnConveyor();
            }
        }

        if (conveyor == null)
        {
            Debug.LogWarning($"[ConveyorControlReceiver:{name}] No ConveyorMotor assigned.");
            return;
        }

        conveyor.speed = goalSpeed;

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

    void EnsureObjectOnConveyor(string objectId)
    {
        bool wasVisible = objectOnConveyor != null && objectOnConveyor.activeInHierarchy;
        if (objectOnConveyor == null)
        {
            GameObject prefab = GetObjectPrefab(objectId);
            if (prefab == null)
            {
                if (!warnedMissingObjectPrefab)
                {
                    Debug.LogWarning($"[ConveyorControlReceiver:{name}] objectDetected=true but no conveyor object prefab/reference is assigned for '{objectId}'.");
                    warnedMissingObjectPrefab = true;
                }

                return;
            }

            objectOnConveyor = Instantiate(prefab);
            objectOnConveyor.name = $"{name}_{(IsConveyor2(objectId) ? "Turbine" : "Cube")}_OnConveyor";
            objectOnConveyor.SetActive(true);
        }

        if (wasVisible)
        {
            ApplyObjectVisualTransform(objectOnConveyor, objectId);
            MakeConveyorVisualKinematic(objectOnConveyor);
            return;
        }

        Transform parent = objectAnchor != null ? objectAnchor : transform;
        if (parentDetectedObjectToAnchor && parent != null)
        {
            objectOnConveyor.transform.SetParent(parent, false);
            objectOnConveyor.transform.localPosition = Vector3.zero;
        }
        else if (parent != null)
        {
            objectOnConveyor.transform.position = parent.position;
        }

        ApplyObjectVisualTransform(objectOnConveyor, objectId);
        MakeConveyorVisualKinematic(objectOnConveyor);
        objectOnConveyor.SetActive(true);
    }

    void HideObjectOnConveyor()
    {
        currentSpeed = 0f;

        if (objectOnConveyor != null)
        {
            objectOnConveyor.SetActive(false);
        }
    }

    GameObject GetObjectPrefab(string objectId)
    {
        if (IsConveyor2(objectId))
        {
            if (turbineOnConveyorPrefab != null)
            {
                return turbineOnConveyorPrefab;
            }

            Transform example = GetTurbineExampleTransform();
            return example != null ? example.gameObject : cubeOnConveyorPrefab;
        }

        return cubeOnConveyorPrefab != null ? cubeOnConveyorPrefab : turbineOnConveyorPrefab;
    }

    void ApplyObjectVisualTransform(GameObject target, string objectId)
    {
        if (target == null)
        {
            return;
        }

        if (IsConveyor2(objectId))
        {
            Transform example = GetTurbineExampleTransform();
            if (example != null)
            {
                target.transform.localRotation = example.localRotation;
                target.transform.localScale = example.localScale;
                return;
            }
        }

        target.transform.localRotation = Quaternion.identity;
        Transform cubeExample = GetCubeExampleTransform();
        target.transform.localScale = cubeExample != null ? cubeExample.localScale : cubeOnConveyorScale;
    }

    Transform GetCubeExampleTransform()
    {
        if (cubeExampleTransform != null)
        {
            return cubeExampleTransform;
        }

        if (string.IsNullOrWhiteSpace(cubeExampleName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == cubeExampleName)
            {
                cubeExampleTransform = candidate;
                return cubeExampleTransform;
            }
        }

        return null;
    }

    Transform GetTurbineExampleTransform()
    {
        if (turbineExampleTransform != null)
        {
            return turbineExampleTransform;
        }

        if (string.IsNullOrWhiteSpace(turbineExampleName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == turbineExampleName)
            {
                turbineExampleTransform = candidate;
                return turbineExampleTransform;
            }
        }

        return null;
    }

    static void MakeConveyorVisualKinematic(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Rigidbody[] bodies = target.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
            {
                continue;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    Vector3 GetMovementDirection()
    {
        if (conveyor != null)
        {
            switch (conveyor.axis)
            {
                case ConveyorMotor.MoveAxis.NegX: return Vector3.left;
                case ConveyorMotor.MoveAxis.Y: return Vector3.up;
                case ConveyorMotor.MoveAxis.NegY: return Vector3.down;
                case ConveyorMotor.MoveAxis.Z: return Vector3.forward;
                case ConveyorMotor.MoveAxis.NegZ: return Vector3.back;
                default: return Vector3.right;
            }
        }

        return conveyorMovementDirection.sqrMagnitude > 0.0001f
            ? conveyorMovementDirection.normalized
            : Vector3.right;
    }

    static bool IsConveyor2(string objectId) =>
        string.Equals(objectId, "conveyor2", System.StringComparison.OrdinalIgnoreCase);
}
