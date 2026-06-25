using System;
using System.Collections.Generic;
using UnityEngine;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    public event Action<TelemetryRecord> TelemetryUpdated;

    readonly Dictionary<string, Dictionary<string, TelemetryRecord>> telemetryByObject =
        new Dictionary<string, Dictionary<string, TelemetryRecord>>();

    [SerializeField] bool dontDestroyOnLoad = true;

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

    public void UpdateTelemetry(string objectId, string key, string value)
    {
        UpdateTelemetry(string.Empty, objectId, key, value);
    }

    public void UpdateTelemetry(string cellId, string objectId, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            Debug.LogWarning("[TelemetryManager] Ignored telemetry with empty objectId.");
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning($"[TelemetryManager] Ignored telemetry for '{objectId}' with empty key.");
            return;
        }

        if (!telemetryByObject.TryGetValue(objectId, out Dictionary<string, TelemetryRecord> objectTelemetry))
        {
            objectTelemetry = new Dictionary<string, TelemetryRecord>();
            telemetryByObject[objectId] = objectTelemetry;
        }

        var record = new TelemetryRecord(cellId, objectId, key, value ?? string.Empty, DateTime.UtcNow);
        objectTelemetry[key] = record;
        TelemetryUpdated?.Invoke(record);
    }

    public string GetTelemetry(string objectId, string key, string fallback = "")
    {
        return TryGetTelemetry(objectId, key, out string value) ? value : fallback;
    }

    public bool TryGetTelemetry(string objectId, string key, out string value)
    {
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!telemetryByObject.TryGetValue(objectId, out Dictionary<string, TelemetryRecord> objectTelemetry))
        {
            return false;
        }

        if (!objectTelemetry.TryGetValue(key, out TelemetryRecord record))
        {
            return false;
        }

        value = record.Value;
        return true;
    }

    public bool TryGetRecord(string objectId, string key, out TelemetryRecord record)
    {
        record = default;

        return !string.IsNullOrWhiteSpace(objectId) &&
               !string.IsNullOrWhiteSpace(key) &&
               telemetryByObject.TryGetValue(objectId, out Dictionary<string, TelemetryRecord> objectTelemetry) &&
               objectTelemetry.TryGetValue(key, out record);
    }

    public IReadOnlyDictionary<string, TelemetryRecord> GetObjectTelemetry(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId) ||
            !telemetryByObject.TryGetValue(objectId, out Dictionary<string, TelemetryRecord> objectTelemetry))
        {
            return null;
        }

        return objectTelemetry;
    }
}

[Serializable]
public struct TelemetryRecord
{
    public string CellId;
    public string ObjectId;
    public string Key;
    public string Value;
    public long UnixMilliseconds;
    public string UtcTimestamp;

    public TelemetryRecord(string cellId, string objectId, string key, string value, DateTime utcTimestamp)
    {
        CellId = cellId ?? string.Empty;
        ObjectId = objectId ?? string.Empty;
        Key = key ?? string.Empty;
        Value = value ?? string.Empty;
        UnixMilliseconds = new DateTimeOffset(utcTimestamp).ToUnixTimeMilliseconds();
        UtcTimestamp = utcTimestamp.ToString("O");
    }
}
