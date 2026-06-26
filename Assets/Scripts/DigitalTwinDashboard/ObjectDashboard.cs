using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays the latest MQTT status snapshot for one digital-twin object.
/// Status is read only; this dashboard never publishes commands.
/// </summary>
public sealed class ObjectDashboard : MonoBehaviour
{
    const float RefreshInterval = 0.5f;
    const string MissingValue = "N/A";

    [Header("TextMeshPro Fields")]
    public TMP_Text objectNameText;
    public TMP_Text speedText;
    public TMP_Text temperatureText;
    public TMP_Text vibrationText;
    public TMP_Text aiStatusText;

    string targetObjectId;
    float nextRefreshTime;
    TwinObjectStatusStore boundStatusStore;

    void OnEnable()
    {
        BindStatusStore();
        nextRefreshTime = Time.unscaledTime;
    }

    void OnDisable()
    {
        if (boundStatusStore != null)
        {
            boundStatusStore.StatusUpdated -= OnStatusUpdated;
            boundStatusStore = null;
        }
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        RefreshDisplay();
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
    }

    public void SetTargetObject(string objectId)
    {
        targetObjectId = objectId?.Trim();
        BindStatusStore();
        RefreshDisplay();
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
    }

    public void Close()
    {
        if (DashboardManager.Instance != null)
        {
            DashboardManager.Instance.CloseDashboard();
            return;
        }

        Debug.LogWarning("DashboardManager is unavailable; destroying this dashboard directly.", this);
        Destroy(gameObject);
    }

    void BindStatusStore()
    {
        TwinObjectStatusStore statusStore = TwinObjectStatusStore.Instance;
        if (boundStatusStore == statusStore)
        {
            return;
        }

        if (boundStatusStore != null)
        {
            boundStatusStore.StatusUpdated -= OnStatusUpdated;
        }

        boundStatusStore = statusStore;

        if (boundStatusStore != null)
        {
            boundStatusStore.StatusUpdated += OnStatusUpdated;
        }
    }

    void OnStatusUpdated(TwinObjectStatus status)
    {
        if (string.IsNullOrWhiteSpace(targetObjectId) ||
            !string.Equals(status.ObjectId, targetObjectId, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RefreshDisplay(status);
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
    }

    void RefreshDisplay()
    {
        BindStatusStore();

        if (string.IsNullOrWhiteSpace(targetObjectId) ||
            boundStatusStore == null ||
            !boundStatusStore.TryGetStatus(targetObjectId, out TwinObjectStatus status))
        {
            SetValue(objectNameText, string.IsNullOrWhiteSpace(targetObjectId) ? MissingValue : targetObjectId);
            SetTelemetryText(MissingValue, MissingValue, MissingValue, MissingValue);
            return;
        }

        RefreshDisplay(status);
    }

    void RefreshDisplay(TwinObjectStatus status)
    {
        string displayName = !string.IsNullOrWhiteSpace(status.ObjectId)
            ? status.ObjectId
            : targetObjectId;

        SetValue(objectNameText, string.IsNullOrWhiteSpace(displayName) ? MissingValue : displayName);
        SetTelemetryText(
            FormatMetric(status.Speed, "m/s"),
            FormatMetric(status.Temperature, "C"),
            FormatMetric(status.Vibration, "mm/s"),
            !string.IsNullOrWhiteSpace(status.AiStatus) ? status.AiStatus : MissingValue);
    }

    void SetTelemetryText(string speed, string temperature, string vibration, string aiStatus)
    {
        SetText(speedText, "Speed", speed);
        SetText(temperatureText, "Temperature", temperature);
        SetText(vibrationText, "Vibration", vibration);
        SetText(aiStatusText, "AI Status", aiStatus);
    }

    static string FormatMetric(float value, string unit)
    {
        string formatted = value.ToString("0.##", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(unit) ? formatted : $"{formatted} {unit.Trim()}";
    }

    static void SetText(TMP_Text field, string label, string value)
    {
        if (field != null)
        {
            field.text = $"{label}: {value}";
        }
    }

    static void SetValue(TMP_Text field, string value)
    {
        if (field != null)
        {
            field.text = value;
        }
    }
}
