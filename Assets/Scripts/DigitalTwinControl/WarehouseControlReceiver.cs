using System.Collections.Generic;
using UnityEngine;

public class WarehouseControlReceiver : MonoBehaviour
{
    const int MaxShelfVisibleCount = 25;

    [Header("References")]
    public WarehouseManager warehouseManager;
    public List<GameObject> shelvingAItems = new List<GameObject>();
    public List<GameObject> shelvingBItems = new List<GameObject>();
    public List<GameObject> shelvingACubes = new List<GameObject>();
    public List<GameObject> shelvingBTurbines = new List<GameObject>();

    [Header("Behavior")]
    public bool preferWarehouseManager = true;

    [Header("Diagnostics")]
    [SerializeField] bool hasReceivedWarehouseControl;
    [SerializeField] int lastRemainingInA;
    [SerializeField] int lastRemainingInB;
    [SerializeField] string lastBatchStatus = "";
    [SerializeField] bool warnedIncompleteWarehouseControl;

    void Awake()
    {
        if (warehouseManager == null)
        {
            warehouseManager = WarehouseManager.Instance;
        }
    }

    public void Configure(WarehouseManager manager)
    {
        if (manager != null)
        {
            warehouseManager = manager;
        }
    }

    public void ApplyControl(WarehouseControlData control)
    {
        if (control == null)
        {
            Debug.LogWarning($"[WarehouseControlReceiver:{name}] Ignored null warehouse control.");
            return;
        }

        bool hasA = control.hasRemainingInA;
        bool hasB = control.hasRemainingInB || control.hasDeliveredToB;
        if (!hasA && !hasB)
        {
            Debug.LogWarning($"[WarehouseControlReceiver:{name}] Ignored warehouse control with no remainingInA or remainingInB values.");
            return;
        }

        int remainingInA = hasA ? Mathf.Max(0, control.remainingInA) : lastRemainingInA;
        int remainingInB = hasB
            ? Mathf.Max(0, control.hasRemainingInB ? control.remainingInB : control.deliveredToB)
            : lastRemainingInB;

        if (hasA && remainingInA > MaxShelfVisibleCount)
        {
            Debug.LogWarning($"[WarehouseControlReceiver:{name}] Ignored warehouse control because remainingInA={remainingInA} exceeds {MaxShelfVisibleCount}.");
            return;
        }

        if (hasB && remainingInB > MaxShelfVisibleCount)
        {
            Debug.LogWarning($"[WarehouseControlReceiver:{name}] Ignored warehouse control because remainingInB={remainingInB} exceeds {MaxShelfVisibleCount}.");
            return;
        }

        if (hasA)
        {
            lastRemainingInA = remainingInA;
        }

        if (hasB)
        {
            lastRemainingInB = remainingInB;
        }

        lastBatchStatus = control.batchStatus ?? string.Empty;
        hasReceivedWarehouseControl = true;

        if (preferWarehouseManager && hasA && hasB)
        {
            if (warehouseManager == null)
            {
                warehouseManager = WarehouseManager.Instance;
            }

            if (warehouseManager != null)
            {
                // TODO: Source data names may say RemainingInB or DeliveredToB. The existing WarehouseManager API uses deliveredToB.
                warehouseManager.ApplyExternalWarehouseSnapshot(remainingInA, remainingInB, lastBatchStatus);
                return;
            }
        }
        else if (preferWarehouseManager && !warnedIncompleteWarehouseControl)
        {
            Debug.LogWarning($"[WarehouseControlReceiver:{name}] WarehouseManager snapshot requires both remainingInA and remainingInB. Applying only directly assigned fallback lists for this incomplete payload.");
            warnedIncompleteWarehouseControl = true;
        }

        if (hasA)
        {
            ApplyFallbackItemVisibility(GetShelvingAList(), remainingInA);
        }

        if (hasB)
        {
            ApplyFallbackItemVisibility(GetShelvingBList(), remainingInB);
        }
    }

    List<GameObject> GetShelvingAList()
    {
        return shelvingACubes != null && shelvingACubes.Count > 0 ? shelvingACubes : shelvingAItems;
    }

    List<GameObject> GetShelvingBList()
    {
        return shelvingBTurbines != null && shelvingBTurbines.Count > 0 ? shelvingBTurbines : shelvingBItems;
    }

    static void ApplyFallbackItemVisibility(List<GameObject> items, int visibleCount)
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                items[i].SetActive(i < visibleCount);
            }
        }
    }
}
