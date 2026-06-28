using System;
using Newtonsoft.Json.Linq;
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
                Car1_Target = legacy.Car1_Target,
                Car2_Target = legacy.Car2_Target,
                currentX = legacy.currentX,
                status = legacy.status,
                atPickTarget = legacy.atPickTarget,
                atDropTarget = legacy.atDropTarget,
                carrying = legacy.carrying
            };
            timestamp = legacy.timestamp;
        }

        if (!PopulateCarPresence(rawJson, entry.objectId, control))
        {
            Debug.LogWarning($"[TwinControlRouter] Ignored incomplete car control for '{entry.objectId}'. Include control.Car1_Target/Car2_Target, control.currentX fallback, or a supported car control field.");
            return false;
        }

        CarControlReceiver receiver = GetOrAddCarReceiver(entry);
        if (receiver == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Car '{entry.objectId}' has no CarControlReceiver and no target object to add one.");
            return false;
        }

        receiver.ApplyControl(entry.objectId, control);

        entry.lastStatus = control.status ?? string.Empty;
        if (control.hasAtPickTarget) entry.lastAtPickTarget = control.atPickTarget;
        if (control.hasAtDropTarget) entry.lastAtDropTarget = control.atDropTarget;
        if (control.hasCarrying) entry.lastCarrying = control.carrying;
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

        if (!PopulateConveyorPresence(rawJson, control))
        {
            Debug.LogWarning($"[TwinControlRouter] Ignored incomplete conveyor control for '{entry.objectId}'. Include control.objectDetected, control.running, or control.currentSpeed.");
            return false;
        }

        ConveyorControlReceiver receiver = GetOrAddConveyorReceiver(entry);
        if (receiver == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Conveyor '{entry.objectId}' has no ConveyorControlReceiver and no target object to add one.");
            return false;
        }

        receiver.ApplyControl(entry.objectId, control);

        if (control.hasRunning) entry.lastStatus = control.running ? "running" : "stopped";
        if (control.hasObjectDetected) entry.lastObjectDetected = control.objectDetected;
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

        if (!PopulateWarehousePresence(rawJson, control))
        {
            Debug.LogWarning($"[TwinControlRouter] Ignored incomplete warehouse control for '{objectId}'. Include control.remainingInA and/or control.remainingInB.");
            return false;
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
            deliveredToB = control.hasRemainingInB ? control.remainingInB : control.deliveredToB,
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
                    control.hasRemainingInB ? control.remainingInB : control.deliveredToB,
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

    static bool PopulateCarPresence(string rawJson, string topicObjectId, CarControlData control)
    {
        if (control == null || !TryGetControlObject(rawJson, out JObject controlJson))
        {
            return false;
        }

        bool hasCommand = false;
        bool isCar1 = string.Equals(topicObjectId, "car1", StringComparison.OrdinalIgnoreCase);
        bool isCar2 = string.Equals(topicObjectId, "car2", StringComparison.OrdinalIgnoreCase);
        bool hasCar1Target = TryGetFloat(controlJson, "Car1_Target", out float car1Target);
        bool hasCar2Target = TryGetFloat(controlJson, "Car2_Target", out float car2Target);

        if (isCar1 && hasCar1Target)
        {
            control.Car1_Target = car1Target;
            control.targetZ = car1Target;
            control.hasTargetZ = true;
            hasCommand = true;
        }
        else if (isCar2 && hasCar2Target)
        {
            control.Car2_Target = car2Target;
            control.targetZ = car2Target;
            control.hasTargetZ = true;
            hasCommand = true;
        }
        else
        {
            if (isCar1 && hasCar2Target)
            {
                Debug.LogWarning("[TwinControlRouter] Ignored Car2_Target on car1 topic. Use control.Car1_Target for factory/cell1/twin/car1/control.");
            }
            else if (isCar2 && hasCar1Target)
            {
                Debug.LogWarning("[TwinControlRouter] Ignored Car1_Target on car2 topic. Use control.Car2_Target for factory/cell1/twin/car2/control.");
            }

            if (TryGetFloat(controlJson, "currentX", out float currentX))
            {
                control.currentX = currentX;
                control.targetZ = currentX;
                control.hasTargetZ = true;
                control.hasCurrentX = true;
                hasCommand = true;
            }
        }

        if (TryGetString(controlJson, "status", out string status))
        {
            control.status = status;
            hasCommand = true;
        }

        if (TryGetBool(controlJson, "carrying", out bool carrying))
        {
            control.carrying = carrying;
            control.hasCarrying = true;
            hasCommand = true;
        }

        if (TryGetBool(controlJson, "atPickTarget", out bool atPickTarget))
        {
            control.atPickTarget = atPickTarget;
            control.hasAtPickTarget = true;
            hasCommand = true;
        }

        if (TryGetBool(controlJson, "atDropTarget", out bool atDropTarget))
        {
            control.atDropTarget = atDropTarget;
            control.hasAtDropTarget = true;
            hasCommand = true;
        }

        return hasCommand;
    }

    static bool PopulateConveyorPresence(string rawJson, ConveyorControlData control)
    {
        if (control == null || !TryGetControlObject(rawJson, out JObject controlJson))
        {
            return false;
        }

        bool hasCommand = false;
        if (TryGetFloat(controlJson, "currentSpeed", out float currentSpeed))
        {
            control.currentSpeed = currentSpeed;
            control.hasCurrentSpeed = true;
            hasCommand = true;
        }

        if (TryGetBool(controlJson, "running", out bool running))
        {
            control.running = running;
            control.hasRunning = true;
            hasCommand = true;
        }

        if (TryGetBool(controlJson, "objectDetected", out bool objectDetected))
        {
            control.objectDetected = objectDetected;
            control.hasObjectDetected = true;
            hasCommand = true;
        }

        return hasCommand;
    }

    static bool PopulateWarehousePresence(string rawJson, WarehouseControlData control)
    {
        if (control == null || !TryGetControlObject(rawJson, out JObject controlJson))
        {
            return false;
        }

        bool hasCommand = false;
        if (TryGetInt(controlJson, "remainingInA", out int remainingInA))
        {
            control.remainingInA = remainingInA;
            control.hasRemainingInA = true;
            hasCommand = true;
        }

        if (TryGetInt(controlJson, "remainingInB", out int remainingInB))
        {
            control.remainingInB = remainingInB;
            control.hasRemainingInB = true;
            hasCommand = true;
        }

        if (TryGetInt(controlJson, "deliveredToB", out int deliveredToB))
        {
            control.deliveredToB = deliveredToB;
            control.hasDeliveredToB = true;
            hasCommand = true;
        }

        if (TryGetString(controlJson, "batchStatus", out string batchStatus))
        {
            control.batchStatus = batchStatus;
            hasCommand = true;
        }

        return hasCommand;
    }

    static bool TryGetControlObject(string rawJson, out JObject controlObject)
    {
        controlObject = null;
        try
        {
            JObject root = JObject.Parse(rawJson ?? "{}");
            controlObject = GetObjectProperty(root, "control") ?? root;
            return controlObject != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TwinControlRouter] Cannot inspect control JSON fields: {ex.Message}");
            return false;
        }
    }

    static JObject GetObjectProperty(JObject obj, string name)
    {
        JToken value = GetPropertyValue(obj, name);
        return value as JObject;
    }

    static bool TryGetFloat(JObject obj, string name, out float value)
    {
        value = 0f;
        JToken token = GetPropertyValue(obj, name);
        if (token == null || token.Type == JTokenType.Null)
        {
            return false;
        }

        try
        {
            value = token.Value<float>();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TwinControlRouter] Ignored invalid float field '{name}': {ex.Message}");
            return false;
        }
    }

    static bool TryGetInt(JObject obj, string name, out int value)
    {
        value = 0;
        JToken token = GetPropertyValue(obj, name);
        if (token == null || token.Type == JTokenType.Null)
        {
            return false;
        }

        try
        {
            value = token.Value<int>();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TwinControlRouter] Ignored invalid int field '{name}': {ex.Message}");
            return false;
        }
    }

    static bool TryGetBool(JObject obj, string name, out bool value)
    {
        value = false;
        JToken token = GetPropertyValue(obj, name);
        if (token == null || token.Type == JTokenType.Null)
        {
            return false;
        }

        try
        {
            value = token.Value<bool>();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TwinControlRouter] Ignored invalid bool field '{name}': {ex.Message}");
            return false;
        }
    }

    static bool TryGetString(JObject obj, string name, out string value)
    {
        value = null;
        JToken token = GetPropertyValue(obj, name);
        if (token == null || token.Type == JTokenType.Null)
        {
            return false;
        }

        value = token.Value<string>();
        return true;
    }

    static JToken GetPropertyValue(JObject obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (JProperty property in obj.Properties())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
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
