using System.Collections.Generic;
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
    [Tooltip("Optional fifth row. If empty, it is created at runtime from the AI Status row.")]
    public TMP_Text statusFaultText;

    string targetObjectId;
    float nextRefreshTime;
    TwinObjectStatusStore boundStatusStore;
    readonly List<TMP_Text> runtimeRows = new List<TMP_Text>();

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

        if (string.IsNullOrWhiteSpace(targetObjectId))
        {
            SetValue(objectNameText, MissingValue);
            SetSnapshotFields(null);
            return;
        }

        RefreshDisplay(TwinDashboardDataResolver.Resolve(targetObjectId));
    }

    void RefreshDisplay(TwinObjectStatus status)
    {
        RefreshDisplay(TwinDashboardDataResolver.Resolve(status.ObjectId));
    }

    void RefreshDisplay(TwinDashboardDataSnapshot snapshot)
    {
        SetValue(objectNameText, snapshot == null ? MissingValue : snapshot.DisplayName);
        SetSnapshotFields(snapshot);
    }

    void SetSnapshotFields(TwinDashboardDataSnapshot snapshot)
    {
        int fieldCount = snapshot != null ? snapshot.Fields.Count : 4;
        TMP_Text[] rows = EnsureRowCapacity(fieldCount);

        for (int i = 0; i < rows.Length; i++)
        {
            TMP_Text row = rows[i];
            if (row == null)
            {
                continue;
            }

            bool hasValue = snapshot != null && i < snapshot.Fields.Count;
            row.gameObject.SetActive(hasValue || i < 4);
            if (hasValue)
            {
                TwinDashboardField field = snapshot.Fields[i];
                SetText(row, field.Label, string.IsNullOrWhiteSpace(field.Value) ? MissingValue : field.Value);
            }
            else if (i < 4)
            {
                SetText(row, "Value", MissingValue);
            }
        }
    }

    TMP_Text[] EnsureRowCapacity(int requiredCount)
    {
        runtimeRows.Clear();
        AddRow(speedText);
        AddRow(temperatureText);
        AddRow(vibrationText);
        AddRow(aiStatusText);
        AddRow(statusFaultText);

        while (runtimeRows.Count < requiredCount)
        {
            TMP_Text row = CreateRuntimeRow(runtimeRows.Count);
            if (row == null)
            {
                break;
            }

            if (runtimeRows.Count == 4)
            {
                statusFaultText = row;
            }

            runtimeRows.Add(row);
        }

        LayoutRows(runtimeRows);
        return runtimeRows.ToArray();
    }

    void AddRow(TMP_Text row)
    {
        if (row != null && !runtimeRows.Contains(row))
        {
            runtimeRows.Add(row);
        }
    }

    TMP_Text CreateRuntimeRow(int rowIndex)
    {
        TMP_Text template = aiStatusText != null ? aiStatusText : vibrationText;
        if (template == null)
        {
            return null;
        }

        GameObject rowObject = Instantiate(template.gameObject, template.transform.parent);
        rowObject.name = $"Dashboard Field {rowIndex + 1}";
        TMP_Text rowText = rowObject.GetComponent<TMP_Text>();
        if (rowText != null)
        {
            rowText.text = string.Empty;
        }

        return rowText;
    }

    static void LayoutRows(IReadOnlyList<TMP_Text> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            TMP_Text row = rows[i];
            if (row == null)
            {
                continue;
            }

            RectTransform rectTransform = row.rectTransform;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 85f - (70f * i));
            rectTransform.sizeDelta = new Vector2(500f, 54f);
        }
    }

    public TMP_Text[] GetMetricTexts()
    {
        return EnsureRowCapacity(statusFaultText != null ? 5 : 4);
    }

    public TMP_Text GetStatusText()
    {
        return statusFaultText != null && statusFaultText.gameObject.activeInHierarchy
            ? statusFaultText
            : aiStatusText;
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
