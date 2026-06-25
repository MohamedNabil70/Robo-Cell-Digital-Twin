using System;
using System.Collections.Generic;
using UnityEngine;

public class TwinWarehouseStateStore : MonoBehaviour
{
    public static TwinWarehouseStateStore Instance { get; private set; }

    public event Action<TwinWarehouseState> WarehouseStateUpdated;

    readonly Dictionary<string, TwinWarehouseState> warehouseStates =
        new Dictionary<string, TwinWarehouseState>();

    [SerializeField] bool dontDestroyOnLoad = true;
    [SerializeField] int warehouseMessageCount;
    [SerializeField] string lastWarehouseObjectId = "";
    [SerializeField] int lastRemainingInA;
    [SerializeField] int lastDeliveredToB;
    [SerializeField] string lastBatchStatus = "";

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
    }

    public void UpdateWarehouseState(string cellId, TwinWarehouseControlPayload payload, string rawJson)
    {
        if (payload == null)
        {
            Debug.LogWarning("[TwinWarehouseStateStore] Ignored null warehouse payload.");
            return;
        }

        string objectId = string.IsNullOrWhiteSpace(payload.objectId) ? "warehouse" : payload.objectId;
        DateTime now = DateTime.UtcNow;

        var state = new TwinWarehouseState
        {
            CellId = cellId ?? string.Empty,
            ObjectId = objectId,
            RemainingInA = payload.remainingInA,
            DeliveredToB = payload.deliveredToB,
            BatchStatus = payload.batchStatus ?? string.Empty,
            SourceTimestamp = payload.timestamp,
            RawJson = rawJson ?? string.Empty,
            ReceivedUnixMilliseconds = new DateTimeOffset(now).ToUnixTimeMilliseconds(),
            UtcTimestamp = now.ToString("O")
        };

        warehouseStates[objectId] = state;

        warehouseMessageCount++;
        lastWarehouseObjectId = objectId;
        lastRemainingInA = state.RemainingInA;
        lastDeliveredToB = state.DeliveredToB;
        lastBatchStatus = state.BatchStatus;

        WarehouseStateUpdated?.Invoke(state);
    }

    public bool TryGetWarehouseState(string objectId, out TwinWarehouseState state)
    {
        state = default;
        objectId = string.IsNullOrWhiteSpace(objectId) ? "warehouse" : objectId;
        return warehouseStates.TryGetValue(objectId, out state);
    }
}

[Serializable]
public struct TwinWarehouseState
{
    public string CellId;
    public string ObjectId;
    public int RemainingInA;
    public int DeliveredToB;
    public string BatchStatus;
    public long SourceTimestamp;
    public string RawJson;
    public long ReceivedUnixMilliseconds;
    public string UtcTimestamp;
}
