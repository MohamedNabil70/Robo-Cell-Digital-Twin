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
        TwinRobotArmControlPayload payload = ParseJson<TwinRobotArmControlPayload>(rawJson, entry.objectId);
        if (payload == null)
        {
            return false;
        }

        if (entry.robotArm == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Registry entry '{entry.objectId}' has no RobotArmController.");
            return false;
        }

        float[] joints = payload.joints;
        if (joints == null || joints.Length < 6)
        {
            Debug.LogWarning($"[TwinControlRouter] Robot arm '{entry.objectId}' control must include six joint values.");
            return false;
        }

        ApplyJointAngle(entry.robotArm.joint1, entry.robotArm.j1Axis, joints[0]);
        ApplyJointAngle(entry.robotArm.joint2, entry.robotArm.j2Axis, joints[1]);
        ApplyJointAngle(entry.robotArm.joint3, entry.robotArm.j3Axis, joints[2]);
        ApplyJointAngle(entry.robotArm.joint4, entry.robotArm.j4Axis, joints[3]);
        ApplyJointAngle(entry.robotArm.joint5, entry.robotArm.j5Axis, joints[4]);
        ApplyJointAngle(entry.robotArm.joint6, entry.robotArm.j6Axis, joints[5]);

        entry.lastStatus = payload.status ?? string.Empty;
        entry.lastFinger1State = payload.finger1State;
        entry.lastFinger2State = payload.finger2State;
        entry.lastAtPickTarget = payload.atPickTarget;
        entry.lastAtDropTarget = payload.atDropTarget;
        entry.lastSourceTimestamp = payload.timestamp;
        entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        return true;
    }

    bool ApplyCarControl(TwinObjectRegistryEntry entry, string rawJson)
    {
        TwinCarControlPayload payload = ParseJson<TwinCarControlPayload>(rawJson, entry.objectId);
        if (payload == null)
        {
            return false;
        }

        if (entry.carTransform == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Registry entry '{entry.objectId}' has no car Transform.");
            return false;
        }

        Vector3 position = entry.carTransform.position;
        position.x = payload.currentX;
        position.z = payload.currentZ;
        entry.carTransform.position = position;

        entry.lastStatus = payload.status ?? string.Empty;
        entry.lastAtPickTarget = payload.atPickTarget;
        entry.lastAtDropTarget = payload.atDropTarget;
        entry.lastCarrying = payload.carrying;
        entry.lastSourceTimestamp = payload.timestamp;
        entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        return true;
    }

    bool ApplyConveyorControl(TwinObjectRegistryEntry entry, string rawJson)
    {
        TwinConveyorControlPayload payload = ParseJson<TwinConveyorControlPayload>(rawJson, entry.objectId);
        if (payload == null)
        {
            return false;
        }

        if (entry.conveyor == null)
        {
            Debug.LogWarning($"[TwinControlRouter] Registry entry '{entry.objectId}' has no ConveyorMotor.");
            return false;
        }

        entry.conveyor.speed = payload.currentSpeed;
        SimulateTag(entry.runningTag, payload.running);
        SimulateTag(entry.objectDetectedTag, payload.objectDetected);

        entry.lastStatus = payload.running ? "running" : "stopped";
        entry.lastObjectDetected = payload.objectDetected;
        entry.lastSourceTimestamp = payload.timestamp;
        entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        return true;
    }

    bool ApplyWarehouseControl(string cellId, string objectId, string rawJson, TwinObjectRegistryEntry entry)
    {
        TwinWarehouseControlPayload payload = ParseJson<TwinWarehouseControlPayload>(rawJson, objectId);
        if (payload == null)
        {
            return false;
        }

        if (warehouseStateStore == null)
        {
            Debug.LogWarning("[TwinControlRouter] No TwinWarehouseStateStore is assigned.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.objectId))
        {
            payload.objectId = string.IsNullOrWhiteSpace(objectId) ? "warehouse" : objectId;
        }

        warehouseStateStore.UpdateWarehouseState(cellId, payload, rawJson);

        if (applyWarehouseSnapshotsToScene)
        {
            if (warehouseManager == null)
            {
                Debug.LogWarning("[TwinControlRouter] No WarehouseManager is assigned. Warehouse counts were stored but not applied to the scene.");
            }
            else
            {
                warehouseManager.ApplyExternalWarehouseSnapshot(payload.remainingInA, payload.deliveredToB, payload.batchStatus);
            }
        }

        if (entry != null)
        {
            entry.lastStatus = payload.batchStatus ?? string.Empty;
            entry.lastSourceTimestamp = payload.timestamp;
            entry.lastAppliedUtc = DateTime.UtcNow.ToString("O");
        }

        return true;
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
            registry = FindFirstObjectByType<TwinObjectRegistry>();
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
            warehouseStateStore = FindFirstObjectByType<TwinWarehouseStateStore>();
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
            warehouseManager = FindFirstObjectByType<WarehouseManager>();
        }
    }
}
