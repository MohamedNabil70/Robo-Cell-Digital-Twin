using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen read-first factory analysis overlay. It summarizes current status
/// data and remains command-free; safety command behavior can be layered on later.
/// </summary>
public sealed class FactoryAnalysisPanel : MonoBehaviour
{
    const float RefreshInterval = 0.5f;

    static readonly Color BackdropColor = new Color(0.003f, 0.003f, 0.004f, 0.92f);
    static readonly Color PanelColor = new Color(0.012f, 0.011f, 0.010f, 0.94f);
    static readonly Color CardColor = new Color(0.045f, 0.038f, 0.026f, 0.88f);
    static readonly Color GoldColor = new Color(1f, 0.72f, 0.26f, 1f);
    static readonly Color SoftGoldColor = new Color(1f, 0.86f, 0.50f, 1f);
    static readonly Color MutedGoldColor = new Color(0.72f, 0.64f, 0.48f, 1f);
    static readonly Color TextColor = new Color(0.98f, 0.94f, 0.84f, 1f);
    static readonly Color NormalColor = new Color(0.36f, 0.92f, 0.62f, 1f);
    static readonly Color WarningColor = new Color(1f, 0.72f, 0.22f, 1f);
    static readonly Color FaultColor = new Color(1f, 0.32f, 0.28f, 1f);

    [Header("Behavior")]
    public bool hideOnStart = true;
    public bool enableStaleOfflineState = false;
    public float staleTimeoutSeconds = 8f;

    [Header("Theme")]
    [Range(0.35f, 0.95f)]
    public float backdropAlpha = 0.92f;

    Canvas canvas;
    CanvasGroup canvasGroup;
    RectTransform contentRoot;
    TMP_Text statusText;
    TMP_Text aiSummaryText;
    TMP_Text alertsText;
    TMP_Text actionsText;
    TMP_Text healthText;
    TMP_Text telemetryText;
    GameObject majorFaultPanel;
    TMP_Text majorFaultText;
    float nextRefreshTime;
    bool built;

    void Awake()
    {
        EnsureBuilt();
        if (hideOnStart)
        {
            HidePanel();
        }
    }

    void OnEnable()
    {
        if (TwinObjectStatusStore.Instance != null)
        {
            TwinObjectStatusStore.Instance.StatusUpdated += OnStatusUpdated;
        }
    }

    void OnDisable()
    {
        if (TwinObjectStatusStore.Instance != null)
        {
            TwinObjectStatusStore.Instance.StatusUpdated -= OnStatusUpdated;
        }
    }

    void Update()
    {
        if (!built || !gameObject.activeInHierarchy || Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        Refresh();
        nextRefreshTime = Time.unscaledTime + RefreshInterval;
    }

    public void ShowPanel()
    {
        EnsureBuilt();
        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        Refresh();
    }

    public void HidePanel()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    public void TogglePanel()
    {
        if (gameObject.activeInHierarchy)
        {
            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }

    void OnStatusUpdated(TwinObjectStatus status)
    {
        if (gameObject.activeInHierarchy)
        {
            Refresh();
        }
    }

    void EnsureBuilt()
    {
        if (built)
        {
            return;
        }

        built = true;
        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 420;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform rootRect = GetComponent<RectTransform>();
        Stretch(rootRect);

        Image backdrop = CreateImage(rootRect, "Factory Analysis Backdrop", BackdropColor, Vector2.zero, Vector2.zero, true);
        backdrop.color = new Color(BackdropColor.r, BackdropColor.g, BackdropColor.b, backdropAlpha);
        Stretch(backdrop.rectTransform);

        contentRoot = CreateRect(rootRect, "Factory Analysis Content");
        contentRoot.anchorMin = new Vector2(0.035f, 0.055f);
        contentRoot.anchorMax = new Vector2(0.965f, 0.945f);
        contentRoot.offsetMin = Vector2.zero;
        contentRoot.offsetMax = Vector2.zero;

        Image panel = contentRoot.gameObject.AddComponent<Image>();
        panel.color = PanelColor;
        panel.raycastTarget = true;

        Outline panelOutline = contentRoot.gameObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.50f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = CreateText(contentRoot, "Factory Analysis Title", "FACTORY ANALYSIS", 34f, SoftGoldColor, FontStyles.Bold);
        SetAnchored(title.rectTransform, new Vector2(34f, -34f), new Vector2(620f, 52f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        TMP_Text subtitle = CreateText(contentRoot, "Factory Analysis Subtitle", "READ-ONLY OPERATIONAL INTELLIGENCE", 15f, MutedGoldColor, FontStyles.Bold);
        SetAnchored(subtitle.rectTransform, new Vector2(36f, -78f), new Vector2(620f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        Button closeButton = CreateButton(contentRoot, "Factory Analysis Close Button", "X", new Vector2(-34f, -34f), new Vector2(44f, 44f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        closeButton.onClick.AddListener(HidePanel);

        statusText = CreateCard(contentRoot, "Factory Status Card", "Factory Status", new Vector2(34f, -128f), new Vector2(400f, 132f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        aiSummaryText = CreateCard(contentRoot, "AI Summary Card", "AI Summary", new Vector2(458f, -128f), new Vector2(560f, 132f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        majorFaultPanel = CreateCardObject(contentRoot, "Major Fault Safety Action Panel", "Major Fault Safety Action", new Vector2(-34f, -128f), new Vector2(600f, 132f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), out majorFaultText);

        alertsText = CreateCard(contentRoot, "Current Alerts Card", "Current Alerts", new Vector2(34f, -282f), new Vector2(620f, 245f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        actionsText = CreateCard(contentRoot, "Recommended Actions Card", "Recommended Actions", new Vector2(682f, -282f), new Vector2(620f, 245f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        healthText = CreateCard(contentRoot, "Object Health Summary Card", "Object Health Summary", new Vector2(34f, 34f), new Vector2(620f, 245f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        telemetryText = CreateCard(contentRoot, "Latest Telemetry Snapshot Card", "Latest Telemetry Snapshot", new Vector2(682f, 34f), new Vector2(980f, 245f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));

        Refresh();
    }

    void Refresh()
    {
        AnalysisSnapshot snapshot = BuildSnapshot();
        Color statusColor = snapshot.Status == "Fault"
            ? FaultColor
            : snapshot.Status == "Warning"
                ? WarningColor
                : snapshot.Status == "Offline" ? MutedGoldColor : NormalColor;

        statusText.text =
            $"<color=#{ColorUtility.ToHtmlStringRGB(statusColor)}><b>{snapshot.Status}</b></color>\n" +
            $"Objects monitored: {snapshot.ObjectCount}\n" +
            $"Major faults: {snapshot.MajorFaultCount}\n" +
            $"Warnings: {snapshot.WarningCount}";

        aiSummaryText.text = snapshot.AiSummary;
        alertsText.text = snapshot.Alerts;
        actionsText.text = snapshot.Actions;
        healthText.text = snapshot.HealthSummary;
        telemetryText.text = snapshot.TelemetrySnapshot;

        if (majorFaultPanel != null)
        {
            majorFaultPanel.SetActive(snapshot.HasMajorFault);
        }

        if (majorFaultText != null)
        {
            majorFaultText.text = snapshot.MajorFaultSummary;
        }
    }

    AnalysisSnapshot BuildSnapshot()
    {
        var snapshot = new AnalysisSnapshot
        {
            Status = "Offline",
            AiSummary = "No status data has been received yet.",
            Alerts = "No current alerts.",
            Actions = "No actions recommended.",
            HealthSummary = "No objects available.",
            TelemetrySnapshot = "No telemetry available.",
            MajorFaultSummary = string.Empty
        };

        TwinObjectStatusStore store = TwinObjectStatusStore.Instance;
        if (store == null || store.GetAllStatuses().Count == 0)
        {
            return snapshot;
        }

        IReadOnlyDictionary<string, TwinObjectStatus> statuses = store.GetAllStatuses();
        var alerts = new List<string>();
        var actions = new List<string>();
        var health = new StringBuilder();
        var telemetry = new StringBuilder();
        bool hasWarning = false;
        bool hasMajorFault = false;
        bool hasRunningConveyor = false;
        TwinObjectStatus majorFaultStatus = default;

        foreach (KeyValuePair<string, TwinObjectStatus> pair in statuses)
        {
            TwinObjectStatus status = pair.Value;
            snapshot.ObjectCount++;

            bool isArm = Contains(status.ObjectType, "arm") || Contains(status.ObjectId, "arm");
            bool isConveyor = Contains(status.ObjectType, "conveyor") || Contains(status.ObjectId, "conveyor");
            bool stale = enableStaleOfflineState && IsStale(status);
            bool majorFault = isArm && (Contains(status.HealthState, "failure") || status.HealthScore > 0f && status.HealthScore <= 30f);
            bool warning = IsWarning(status, isArm, isConveyor);

            if (isConveyor && status.Speed > 0f)
            {
                hasRunningConveyor = true;
            }

            if (majorFault)
            {
                hasMajorFault = true;
                snapshot.MajorFaultCount++;
                if (string.IsNullOrWhiteSpace(majorFaultStatus.ObjectId))
                {
                    majorFaultStatus = status;
                }

                alerts.Add($"{status.ObjectId}: MAJOR FAULT ({ValueOr(status.FaultType, status.HealthState, "failure")})");
                actions.Add($"Request safe stop before continuing operation near {status.ObjectId}.");
            }
            else if (warning)
            {
                hasWarning = true;
                snapshot.WarningCount++;
                alerts.Add($"{status.ObjectId}: {ValueOr(status.FaultType, status.AiStatus, status.HealthState, "Warning")}");
                actions.Add($"Inspect {status.ObjectId}; keep control path disabled unless a major fault is confirmed.");
            }

            if (stale)
            {
                alerts.Add($"{status.ObjectId}: telemetry stale");
            }

            health.Append(status.ObjectId);
            health.Append("  ");
            health.Append(ValueOr(status.ObjectType, "object"));
            health.Append("  health=");
            health.Append(ValueOr(status.HealthState, status.Health, "N/A"));
            health.Append("  score=");
            health.Append(status.HealthScore > 0f ? status.HealthScore.ToString("0.##", CultureInfo.InvariantCulture) : "N/A");
            health.Append("  maintenance=");
            health.Append(status.MaintenanceRequired ? "Yes" : "No");
            health.AppendLine();

            telemetry.Append(status.ObjectId);
            telemetry.Append(": speed=");
            telemetry.Append(status.Speed.ToString("0.##", CultureInfo.InvariantCulture));
            telemetry.Append(" temp=");
            telemetry.Append(status.Temperature.ToString("0.##", CultureInfo.InvariantCulture));
            telemetry.Append(" vib=");
            telemetry.Append(status.Vibration.ToString("0.##", CultureInfo.InvariantCulture));
            telemetry.Append(" ai=");
            telemetry.Append(ValueOr(status.AiStatus, "N/A"));
            telemetry.Append(" state=");
            telemetry.Append(ValueOr(status.State, "N/A"));
            telemetry.AppendLine();
        }

        snapshot.HasMajorFault = hasMajorFault;
        snapshot.Status = hasMajorFault ? "Fault" : hasWarning ? "Warning" : hasRunningConveyor ? "Running" : "Offline";
        snapshot.AiSummary = hasMajorFault
            ? "Major-fault conditions detected. Analysis remains read-only until a confirmed safety action is implemented/enabled."
            : hasWarning
                ? "Warnings detected. Continue monitoring and inspect affected assets."
                : "Factory signals look normal. No major fault action is available.";
        snapshot.Alerts = alerts.Count > 0 ? string.Join("\n", alerts.ToArray()) : "No current alerts.";
        snapshot.Actions = actions.Count > 0 ? string.Join("\n", actions.ToArray()) : "No actions recommended.";
        snapshot.HealthSummary = health.Length > 0 ? health.ToString() : "No objects available.";
        snapshot.TelemetrySnapshot = telemetry.Length > 0 ? telemetry.ToString() : "No telemetry available.";

        if (hasMajorFault)
        {
            snapshot.MajorFaultSummary =
                $"Affected object: {majorFaultStatus.ObjectId}\n" +
                $"fault_type: {ValueOr(majorFaultStatus.FaultType, "N/A")}\n" +
                $"health_score: {(majorFaultStatus.HealthScore > 0f ? majorFaultStatus.HealthScore.ToString("0.##", CultureInfo.InvariantCulture) : "N/A")}\n" +
                $"health_state: {ValueOr(majorFaultStatus.HealthState, "N/A")}\n" +
                $"Recommended action: request safe stop before continuing operation.";
        }

        return snapshot;
    }

    bool IsWarning(TwinObjectStatus status, bool isArm, bool isConveyor)
    {
        if (isArm && status.MaintenanceRequired)
        {
            return true;
        }

        if (isArm && !IsNormalOrEmpty(status.FaultType))
        {
            return true;
        }

        return isConveyor && Contains(status.AiStatus, "warning");
    }

    bool IsStale(TwinObjectStatus status)
    {
        if (status.ReceivedUnixMilliseconds <= 0)
        {
            return true;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now - status.ReceivedUnixMilliseconds > staleTimeoutSeconds * 1000f;
    }

    TMP_Text CreateCard(RectTransform parent, string name, string title, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        CreateCardObject(parent, name, title, position, size, anchorMin, anchorMax, pivot, out TMP_Text body);
        return body;
    }

    GameObject CreateCardObject(RectTransform parent, string name, string title, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, out TMP_Text body)
    {
        RectTransform card = CreateRect(parent, name);
        SetAnchored(card, position, size, anchorMin, anchorMax, pivot);

        Image image = card.gameObject.AddComponent<Image>();
        image.color = CardColor;
        image.raycastTarget = true;

        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.28f);
        outline.effectDistance = new Vector2(1f, -1f);

        TMP_Text heading = CreateText(card, $"{name} Heading", title.ToUpperInvariant(), 16f, GoldColor, FontStyles.Bold);
        SetAnchored(heading.rectTransform, new Vector2(18f, -14f), new Vector2(size.x - 36f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));

        body = CreateText(card, $"{name} Body", string.Empty, 17f, TextColor, FontStyles.Normal);
        SetAnchored(body.rectTransform, new Vector2(18f, -46f), new Vector2(size.x - 36f, size.y - 58f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Ellipsis;
        return card.gameObject;
    }

    Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        RectTransform rect = CreateRect(parent, name);
        SetAnchored(rect, position, size, anchorMin, anchorMax, pivot);

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.08f, 0.065f, 0.038f, 0.92f);

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.78f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.14f, 0.11f, 0.055f, 1f);
        colors.pressedColor = new Color(0.22f, 0.16f, 0.06f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TMP_Text text = CreateText(rect, $"{name} Label", label, 22f, SoftGoldColor, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        return button;
    }

    Image CreateImage(RectTransform parent, string name, Color color, Vector2 position, Vector2 size, bool stretch)
    {
        RectTransform rect = CreateRect(parent, name);
        if (stretch)
        {
            Stretch(rect);
        }
        else
        {
            SetAnchored(rect, position, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    TMP_Text CreateText(RectTransform parent, string name, string value, float fontSize, Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static RectTransform CreateRect(RectTransform parent, string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void SetAnchored(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    static bool Contains(string value, string fragment)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsNormalOrEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "none", StringComparison.OrdinalIgnoreCase);
    }

    static string ValueOr(params string[] values)
    {
        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "N/A";
    }

    struct AnalysisSnapshot
    {
        public string Status;
        public string AiSummary;
        public string Alerts;
        public string Actions;
        public string HealthSummary;
        public string TelemetrySnapshot;
        public string MajorFaultSummary;
        public int ObjectCount;
        public int WarningCount;
        public int MajorFaultCount;
        public bool HasMajorFault;
    }
}
