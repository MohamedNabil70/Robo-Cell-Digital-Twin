using System;
using System.Collections.Generic;
using UnityEngine;

public class TwinObjectStatusStore : MonoBehaviour
{
    public static TwinObjectStatusStore Instance { get; private set; }

    public event Action<TwinObjectStatus> StatusUpdated;

    readonly Dictionary<string, TwinObjectStatus> statusesByObject =
        new Dictionary<string, TwinObjectStatus>();

    [SerializeField] bool dontDestroyOnLoad = true;
    [SerializeField] int statusMessageCount;
    [SerializeField] string lastStatusObjectId = "";
    [SerializeField] string lastStatusUtc = "";

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

    public bool UpdateStatus(string cellId, string objectId, string rawJson)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            Debug.LogWarning("[TwinObjectStatusStore] Ignored status with empty objectId.");
            return false;
        }

        var status = TwinObjectStatus.FromJson(cellId, objectId, rawJson);
        statusesByObject[objectId] = status;

        statusMessageCount++;
        lastStatusObjectId = objectId;
        lastStatusUtc = status.UtcTimestamp;

        StatusUpdated?.Invoke(status);
        return true;
    }

    public bool TryGetStatus(string objectId, out TwinObjectStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(objectId) &&
               statusesByObject.TryGetValue(objectId, out status);
    }

    public string GetRawJson(string objectId, string fallback = "")
    {
        return TryGetStatus(objectId, out TwinObjectStatus status) ? status.RawJson : fallback;
    }

    public IReadOnlyDictionary<string, TwinObjectStatus> GetAllStatuses()
    {
        return statusesByObject;
    }
}

[Serializable]
public struct TwinObjectStatus
{
    public string CellId;
    public string ObjectId;
    public string RawJson;
    public string State;
    public string Health;
    public bool Running;
    public float Speed;
    public float Temperature;
    public float Vibration;
    public string AiStatus;
    public float AiConfidence;
    public string Message;
    public string RecommendedAction;
    public long SourceTimestamp;
    public long ReceivedUnixMilliseconds;
    public string UtcTimestamp;

    public static TwinObjectStatus FromJson(string cellId, string objectId, string rawJson)
    {
        TwinStatusPayload payload = null;

        try
        {
            payload = JsonUtility.FromJson<TwinStatusPayload>(rawJson ?? "{}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TwinObjectStatusStore] Could not parse status JSON for '{objectId}': {ex.Message}");
        }

        DateTime now = DateTime.UtcNow;
        string payloadObjectId = payload != null && !string.IsNullOrWhiteSpace(payload.objectId)
            ? payload.objectId
            : objectId;

        return new TwinObjectStatus
        {
            CellId = cellId ?? string.Empty,
            ObjectId = payloadObjectId ?? string.Empty,
            RawJson = rawJson ?? string.Empty,
            State = !string.IsNullOrWhiteSpace(payload?.state)
                ? payload.state
                : payload?.status ?? string.Empty,
            Health = payload?.health ?? string.Empty,
            Running = payload != null && payload.running,
            Speed = payload?.speed ?? 0f,
            Temperature = payload?.temperature ?? 0f,
            Vibration = payload?.vibration ?? 0f,
            AiStatus = !string.IsNullOrWhiteSpace(payload?.aiStatus)
                ? payload.aiStatus
                : payload?.ai_status ?? string.Empty,
            AiConfidence = payload?.aiConfidence ?? 0f,
            Message = payload?.message ?? string.Empty,
            RecommendedAction = payload?.recommendedAction ?? string.Empty,
            SourceTimestamp = payload?.timestamp ?? 0,
            ReceivedUnixMilliseconds = new DateTimeOffset(now).ToUnixTimeMilliseconds(),
            UtcTimestamp = now.ToString("O")
        };
    }
}
