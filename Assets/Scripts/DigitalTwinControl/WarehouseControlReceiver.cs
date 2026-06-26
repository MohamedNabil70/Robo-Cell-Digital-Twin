using System.Collections.Generic;
using UnityEngine;

public class WarehouseControlReceiver : MonoBehaviour
{
    [Header("References")]
    public WarehouseManager warehouseManager;
    public List<GameObject> shelvingAItems = new List<GameObject>();
    public List<GameObject> shelvingBItems = new List<GameObject>();

    [Header("Behavior")]
    public bool preferWarehouseManager = true;

    [Header("Diagnostics")]
    [SerializeField] int lastRemainingInA;
    [SerializeField] int lastRemainingInB;
    [SerializeField] string lastBatchStatus = "";

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

        int remainingInA = Mathf.Max(0, control.remainingInA);
        int remainingInB = Mathf.Max(0, control.remainingInB);
        if (remainingInB == 0 && control.deliveredToB > 0)
        {
            remainingInB = control.deliveredToB;
        }

        lastRemainingInA = remainingInA;
        lastRemainingInB = remainingInB;
        lastBatchStatus = control.batchStatus ?? string.Empty;

        if (preferWarehouseManager)
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

        ApplyFallbackItemVisibility(shelvingAItems, remainingInA);
        ApplyFallbackItemVisibility(shelvingBItems, remainingInB);
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
