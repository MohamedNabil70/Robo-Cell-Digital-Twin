using System;
using UnityEngine;

public class TwinControlRouter : MonoBehaviour
{
    public static TwinControlRouter Instance { get; private set; }

    [Header("References")]
    public TwinObjectRegistry registry;
    public TwinWarehouseStateStore warehouseStateStore;
    public WarehouseManager warehouseManager;

    [Header("Behavior")]
    public bool autoCreateStores = true;
    public bool autoAddReceiverComponents = true;
    public bool logAppliedControl = false;
    public bool applyWarehouseSnapshotsToScene = true;

    [Header("Diagnostics")]
    [SerializeField] int appliedControlCount;
    [SerializeField] string lastControlObjectId = "";
    [SerializeField] string lastControlType = "";
    [SerializeField] string lastControlUtc = "";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureReferences();
    }

    public bool ApplyControl(string cellId, string objectId, string rawJson)
    {
        EnsureReferences();

        if (string.IsNullOrWhiteSpace(objectId))
        {
            Debug.LogWarning("[TwinControlRouter] Ignored control message with empty objectId.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            Debug.LogWarning($"[TwinControlRouter] Ignored empty control JSON for '{objectId}'.");
            return false;
        }

        TwinControlEnvelope envelope = ParseJson<TwinControlEnvelope>(rawJson, objectId);
        string payloadObjectId = envelope?.objectId;
        if (!string.IsNullOrWhiteSpace(payloadObjectId) &&
            !string.Equals(payloadObjectId.Trim(), objectId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[TwinControlRouter] Topic objectId '{objectId}' differs from payload objectId '{payloadObjectId}'. Routing by topic.");
        }

        if (registry == null)
        {
            Debug.LogWarning("[TwinControlRouter] No TwinObjectRegistry is assigned.");
            return false;
        }

        if (!registry.TryGetEntry(objectId, out TwinObjectRegistryEntry entry))
        {
            if (objectId == "warehouse")
            {
                return ApplyWarehouseControl(cellId, objectId, rawJson, null);
            }

            Debug.LogWarning($"[TwinControlRouter] No registry entry found for '{objectId}'. Add it to TwinObjectRegistry.");
            return false;
        }

        entry.ResolveReferences();

        bool applied = false;
        switch (entry.objectType)
        {
            case TwinObjectType.RobotArm:
                applied = ApplyRobotArmControl(entry, rawJson);
                break;
            case TwinObjectType.Car:
                applied = ApplyCarControl(entry, rawJson);
                break;
            case TwinObjectType.Conveyor:
                applied = ApplyConveyorControl(entry, rawJson);
                break;
            case TwinObjectType.Warehouse:
                applied = ApplyWarehouseControl(cellId, objectId, rawJson, entry);
                break;
        }

        if (applied)
        {
            appliedControlCount++;
            lastControlObjectId = objectId;
            lastControlType = entry.objectType.ToString();
            lastControlUtc = DateTime.UtcNow.ToString("O");

            if (logAppliedControl)
            {
                Debug.Log($"[TwinControlRouter] Applied {entry.objectType} control for '{objectId}'.");
            }
        }

        return applied;
    }

    bool ApplyRobotArmControl(TwinObjectRegistryEntry entry, string rawJson)
    {
        TwinArmControlMessage message = ParseJson<TwinArmControlMessage>(rawJson, entry.objectId);
        ArmControlData control = message?.control;
        long timestamp = message != null ? message.timestamp : 0;

        if (control == null)
        {
            TwinRobotArmControlPayload legacy = ParseJson<TwinRobotArmControlPayload>(rawJson, entry.objectId);
            if (legacy?.joints == null || legacy.joints.Length < 6)
            {
                Debug.LogWarning($"[TwinControlRouter] Robot arm '{entry.objectId}' control must include semantic control.currentJ1..currentJ6 values.");
                return false;
            }

            control = new ArmControlData
            {
                currentJ1 = legacy.joints[0],
                currentJ2 = legacy.joints[1],
                currentJ3 = legacy.joints[2],
                currentJ4 = legacy.joints[3],
                currentJ5 = legacy.joints[4],
                currentJ6 = legacy.joints[5],
                finger1State = legacy.finger1State,
                finger2State = legacy.finger2State,
                status = legacy.status,
                atPickTarget = legacy.atPickTarget,
                atDropTarget = legacy.atDropTarget
            };
            timestamp = legacy.timestamp;
        }

        ArmControlReceiver receiver = GetOrAddArmReceiver(entry);
        if (receiver == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Robot arm '{entry.objectId}' has no ArmControlReceiver and no target object to add one.");
            return false;
        }

        receiver.ApplyControl(control);

        entry.lastStatus = control.status ?? string.Empty;
        entry.lastFinger1State = control.finger1State;
        entry.lastFinger2State = control.finger2State;
        entry.lastAtPickTarget = control.atPickTarget;
        entry.lastAtDropTarget = control.atDropTarget;
        entry.lastSourceTimestamp = timestamp;
        entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        return true;
    }

    bool ApplyCarControl(TwinObjectRegistryEntry entry, string rawJson)
    {
        TwinCarControlMessage message = ParseJson<TwinCarControlMessage>(rawJson, entry.objectId);
        CarControlData control = message?.control;
        long timestamp = message != null ? message.timestamp : 0;

        if (control == null)
        {
            TwinCarControlPayload legacy = ParseJson<TwinCarControlPayload>(rawJson, entry.objectId);
            if (legacy == null)
            {
                return false;
            }

            control = new CarControlData
            {
                currentX = legacy.currentX,
                currentZ = legacy.currentZ,
                status = legacy.status,
                atPickTarget = legacy.atPickTarget,
                atDropTarget = legacy.atDropTarget,
                carrying = legacy.carrying
            };
            timestamp = legacy.timestamp;
        }

        CarControlReceiver receiver = GetOrAddCarReceiver(entry);
        if (receiver == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Car '{entry.objectId}' has no CarControlReceiver and no target object to add one.");
            return false;
        }

        receiver.ApplyControl(entry.objectId, control);

        entry.lastStatus = control.status ?? string.Empty;
        entry.lastAtPickTarget = control.atPickTarget;
        entry.lastAtDropTarget = control.atDropTarget;
        entry.lastCarrying = control.carrying;
        entry.lastSourceTimestamp = timestamp;
        entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        return true;
    }

    bool ApplyConveyorControl(TwinObjectRegistryEntry entry, string rawJson)
    {
        TwinConveyorControlMessage message = ParseJson<TwinConveyorControlMessage>(rawJson, entry.objectId);
        ConveyorControlData control = message?.control;
        long timestamp = message != null ? message.timestamp : 0;

        if (control == null)
        {
            TwinConveyorControlPayload legacy = ParseJson<TwinConveyorControlPayload>(rawJson, entry.objectId);
            if (legacy == null)
            {
                return false;
            }

            control = new ConveyorControlData
            {
                currentSpeed = legacy.currentSpeed,
                running = legacy.running,
                objectDetected = legacy.objectDetected
            };
            timestamp = legacy.timestamp;
        }

        ConveyorControlReceiver receiver = GetOrAddConveyorReceiver(entry);
        if (receiver == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Conveyor '{entry.objectId}' has no ConveyorControlReceiver and no target object to add one.");
            return false;
        }

        receiver.ApplyControl(control);

        entry.lastStatus = control.running ? "running" : "stopped";
        entry.lastObjectDetected = control.objectDetected;
        entry.lastSourceTimestamp = timestamp;
        entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        return true;
    }

    bool ApplyWarehouseControl(string cellId, string objectId, string rawJson, TwinObjectRegistryEntry entry)
    {
        TwinWarehouseControlMessage message = ParseJson<TwinWarehouseControlMessage>(rawJson, objectId);
        WarehouseControlData control = message?.control;
        long timestamp = message != null ? message.timestamp : 0;

        if (control == null)
        {
            TwinWarehouseControlPayload legacy = ParseJson<TwinWarehouseControlPayload>(rawJson, objectId);
            if (legacy == null)
            {
                return false;
            }

            control = new WarehouseControlData
            {
                remainingInA = legacy.remainingInA,
                remainingInB = legacy.remainingInB > 0 ? legacy.remainingInB : legacy.deliveredToB,
                deliveredToB = legacy.deliveredToB,
                batchStatus = legacy.batchStatus
            };
            timestamp = legacy.timestamp;
        }

        string resolvedObjectId = objectId;
        if (message != null && !string.IsNullOrWhiteSpace(message.objectId))
        {
            resolvedObjectId = message.objectId;
        }

        var payload = new TwinWarehouseControlPayload
        {
            objectId = string.IsNullOrWhiteSpace(resolvedObjectId) ? "warehouse" : resolvedObjectId,
            remainingInA = control.remainingInA,
            remainingInB = control.remainingInB,
            deliveredToB = control.remainingInB > 0 ? control.remainingInB : control.deliveredToB,
            batchStatus = control.batchStatus,
            timestamp = timestamp
        };

        if (warehouseStateStore == null)
        {
            Debug.LogWarning("[TwinControlRouter] No TwinWarehouseStateStore is assigned. Warehouse control will still update visuals if a receiver exists.");
        }
        else
        {
            warehouseStateStore.UpdateWarehouseState(cellId, payload, rawJson);
        }

        bool isLegacyWholeWarehouse = entry == null ||
            string.Equals(objectId, "warehouse", StringComparison.OrdinalIgnoreCase);

        if (applyWarehouseSnapshotsToScene && isLegacyWholeWarehouse)
        {
            WarehouseControlReceiver receiver = GetOrAddWarehouseReceiver(entry);
            if (receiver != null)
            {
                receiver.ApplyControl(control);
            }
            else if (warehouseManager != null)
            {
                warehouseManager.ApplyExternalWarehouseSnapshot(
                    control.remainingInA,
                    control.remainingInB > 0 ? control.remainingInB : control.deliveredToB,
                    control.batchStatus);
            }
            else
            {
                Debug.LogWarning("[TwinControlRouter] No WarehouseControlReceiver or WarehouseManager is assigned. Warehouse control was stored but not applied to the scene.");
            }
        }

        if (entry != null)
        {
            entry.lastStatus = control.batchStatus ?? string.Empty;
            entry.lastSourceTimestamp = timestamp;
            entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        }

        return true;
    }

    ArmControlReceiver GetOrAddArmReceiver(TwinObjectRegistryEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        entry.ResolveReferences();
        if (entry.armReceiver == null && entry.targetObject != null)
        {
            entry.armReceiver = entry.targetObject.GetComponent<ArmControlReceiver>();
        }

        if (entry.armReceiver == null && autoAddReceiverComponents && entry.targetObject != null)
        {
            entry.armReceiver = entry.targetObject.AddComponent<ArmControlReceiver>();
        }

        if (entry.armReceiver != null)
        {
            entry.armReceiver.Configure(entry.robotArm);
        }

        return entry.armReceiver;
    }

    CarControlReceiver GetOrAddCarReceiver(TwinObjectRegistryEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        entry.ResolveReferences();
        GameObject target = entry.targetObject;
        if (target == null && entry.carTransform != null)
        {
            target = entry.carTransform.gameObject;
            entry.targetObject = target;
        }

        if (entry.carReceiver == null && target != null)
        {
            entry.carReceiver = target.GetComponent<CarControlReceiver>();
        }

        if (entry.carReceiver == null && autoAddReceiverComponents && target != null)
        {
            entry.carReceiver = target.AddComponent<CarControlReceiver>();
        }

        if (entry.carReceiver != null)
        {
            entry.carReceiver.Configure(entry.carTransform);
        }

        return entry.carReceiver;
    }

    ConveyorControlReceiver GetOrAddConveyorReceiver(TwinObjectRegistryEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        entry.ResolveReferences();
        if (entry.conveyorReceiver == null && entry.targetObject != null)
        {
            entry.conveyorReceiver = entry.targetObject.GetComponent<ConveyorControlReceiver>();
        }

        if (entry.conveyorReceiver == null && autoAddReceiverComponents && entry.targetObject != null)
        {
            entry.conveyorReceiver = entry.targetObject.AddComponent<ConveyorControlReceiver>();
        }

        if (entry.conveyorReceiver != null)
        {
            entry.conveyorReceiver.Configure(entry.conveyor, entry.runningTag, entry.objectDetectedTag);
        }

        return entry.conveyorReceiver;
    }

    WarehouseControlReceiver GetOrAddWarehouseReceiver(TwinObjectRegistryEntry entry)
    {
        GameObject target = entry?.targetObject;
        if (target == null && warehouseManager != null)
        {
            target = warehouseManager.gameObject;
        }

        WarehouseControlReceiver receiver = entry?.warehouseReceiver;
        if (receiver == null && target != null)
        {
            receiver = target.GetComponent<WarehouseControlReceiver>();
        }

        if (receiver == null && autoAddReceiverComponents && target != null)
        {
            receiver = target.AddComponent<WarehouseControlReceiver>();
        }

        if (receiver != null)
        {
            receiver.Configure(warehouseManager);
            if (entry != null)
            {
                entry.warehouseReceiver = receiver;
            }
        }

        return receiver;
    }

    static void ApplyJointAngle(Transform joint, RobotArmController.RotAxis axis, float angle)
    {
        if (joint == null)
        {
            return;
        }

        Vector3 euler = joint.localEulerAngles;
        switch (axis)
        {
            case RobotArmController.RotAxis.X:
                euler.x = angle;
                break;
            case RobotArmController.RotAxis.Y:
                euler.y = angle;
                break;
            default:
                euler.z = angle;
                break;
        }

        joint.localEulerAngles = euler;
    }

    static T ParseJson<T>(string rawJson, string objectId) where T : class
    {
        try
        {
            return JsonUtility.FromJson<T>(rawJson);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TwinControlRouter] Invalid control JSON for '{objectId}': {ex.Message}");
            return null;
        }
    }

    static void SimulateTag(string tag, bool value)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (IO_Router.Instance == null)
        {
            Debug.LogWarning($"[TwinControlRouter] IO_Router not found. Cannot simulate '{tag}'={value}.");
            return;
        }

        IO_Router.Instance.SimulateInput(tag, value);
    }

    void EnsureReferences()
    {
        if (registry == null)
        {
            registry = TwinObjectRegistry.Instance;
        }

        if (registry == null && autoCreateStores)
        {
            registry = FindAnyObjectByType<TwinObjectRegistry>();
        }

        if (registry == null && autoCreateStores)
        {
            var registryObject = new GameObject("TwinObjectRegistry");
            registry = registryObject.AddComponent<TwinObjectRegistry>();
            Debug.Log("[TwinControlRouter] Created TwinObjectRegistry. Add object mappings in the Inspector.");
        }

        if (warehouseStateStore == null)
        {
            warehouseStateStore = TwinWarehouseStateStore.Instance;
        }

        if (warehouseStateStore == null && autoCreateStores)
        {
            warehouseStateStore = FindAnyObjectByType<TwinWarehouseStateStore>();
        }

        if (warehouseStateStore == null && autoCreateStores)
        {
            var storeObject = new GameObject("TwinWarehouseStateStore");
            warehouseStateStore = storeObject.AddComponent<TwinWarehouseStateStore>();
        }

        if (warehouseManager == null)
        {
            warehouseManager = WarehouseManager.Instance;
        }

        if (warehouseManager == null && autoCreateStores)
        {
            warehouseManager = FindAnyObjectByType<WarehouseManager>();
        }
    }
}
