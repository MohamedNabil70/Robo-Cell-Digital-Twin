using System;
using System.Collections.Generic;
using UnityEngine;

public class TwinObjectRegistry : MonoBehaviour
{
    public static TwinObjectRegistry Instance { get; private set; }

    [SerializeField] bool dontDestroyOnLoad = true;
    public List<TwinObjectRegistryEntry> entries = new List<TwinObjectRegistryEntry>();

    readonly Dictionary<string, TwinObjectRegistryEntry> entriesById =
        new Dictionary<string, TwinObjectRegistryEntry>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        RebuildLookup();
    }

    void OnValidate()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        entriesById.Clear();

        foreach (TwinObjectRegistryEntry entry in entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.objectId))
            {
                continue;
            }

            entriesById[entry.objectId.Trim()] = entry;
        }
    }

    public bool TryGetEntry(string objectId, out TwinObjectRegistryEntry entry)
    {
        if (entriesById.Count != entries.Count)
        {
            RebuildLookup();
        }

        entry = null;
        return !string.IsNullOrWhiteSpace(objectId) &&
               entriesById.TryGetValue(objectId.Trim(), out entry);
    }
}

[Serializable]
public class TwinObjectRegistryEntry
{
    public string objectId;
    public TwinObjectType objectType = TwinObjectType.Conveyor;
    public GameObject targetObject;

    [Header("Component References")]
    public RobotArmController robotArm;
    public ConveyorMotor conveyor;
    public Transform carTransform;

    [Header("Optional IO Tags")]
    public string runningTag = "";
    public string eStopTag = "";
    public string objectDetectedTag = "";
    public string triggerTag = "";

    [Header("Diagnostics")]
    public string lastStatus = "";
    public bool lastAtPickTarget;
    public bool lastAtDropTarget;
    public bool lastCarrying;
    public bool lastFinger1State;
    public bool lastFinger2State;
    public bool lastObjectDetected;
    public long lastSourceTimestamp;
    public string lastAppliedUtc = "";

    public void ResolveReferences()
    {
        if (targetObject == null)
        {
            if (robotArm != null) targetObject = robotArm.gameObject;
            else if (conveyor != null) targetObject = conveyor.gameObject;
            else if (carTransform != null) targetObject = carTransform.gameObject;
        }

        if (targetObject == null)
        {
            return;
        }

        if (robotArm == null)
        {
            robotArm = targetObject.GetComponent<RobotArmController>();
        }

        if (conveyor == null)
        {
            conveyor = targetObject.GetComponent<ConveyorMotor>();
        }

        if (carTransform == null)
        {
            carTransform = targetObject.transform;
        }
    }
}

public enum TwinObjectType
{
    RobotArm,
    Car,
    Conveyor,
    Warehouse
}
