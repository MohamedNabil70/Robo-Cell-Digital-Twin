using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies an optional industrial HUD treatment to an ObjectDashboard at runtime.
/// The source prefab is never modified, so the style can be disabled safely.
/// </summary>
public sealed class DashboardVisualEnhancer : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.025f, 0.047f, 0.085f, 0.96f);
    private static readonly Color RowColor = new Color(0.055f, 0.09f, 0.145f, 0.82f);
    private static readonly Color AlternateRowColor = new Color(0.04f, 0.072f, 0.12f, 0.82f);
    private static readonly Color AccentColor = new Color(0.13f, 0.83f, 0.93f, 1f);
    private static readonly Color LabelColor = new Color(0.58f, 0.68f, 0.78f, 1f);
    private static readonly Color ValueColor = new Color(0.92f, 0.97f, 1f, 1f);
    private static readonly Color NormalColor = new Color(0.20f, 0.88f, 0.55f, 1f);
    private static readonly Color WarningColor = new Color(1f, 0.68f, 0.18f, 1f);
    private static readonly Color DangerColor = new Color(1f, 0.28f, 0.32f, 1f);

    private readonly Dictionary<TMP_Text, string> lastPlainValues = new Dictionary<TMP_Text, string>();
    private readonly Dictionary<TMP_Text, float> pulseEndTimes = new Dictionary<TMP_Text, float>();

    private ObjectDashboard dashboard;
    private Camera targetCamera;
    private Vector3 machineAnchor;
    private Vector3 baseLocalScale;
    private CanvasGroup canvasGroup;
    private Sprite decorationSprite;
    private Image statusBackground;
    private TMP_Text updatedText;
    private LineRenderer tetherLine;
    private Material tetherMaterial;
    private bool initialized;
    private bool openingAnimationComplete;
    private float lastTelemetryChangeTime;

    public void Initialize(ObjectDashboard objectDashboard, Vector3 anchor, Camera camera)
    {
        if (initialized || objectDashboard == null)
        {
            return;
        }

        initialized = true;
        dashboard = objectDashboard;
        machineAnchor = anchor;
        targetCamera = camera != null ? camera : Camera.main;
        baseLocalScale = transform.localScale;
        lastTelemetryChangeTime = Time.unscaledTime;

        StyleExistingElements();
        BuildDecorations();
        BuildTether();
        CacheCurrentValues();
        StartCoroutine(AnimateOpen());
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        UpdateMetricText(dashboard.speedText, "SPEED", "m/s");
        UpdateMetricText(dashboard.temperatureText, "TEMPERATURE", "C");
        UpdateMetricText(dashboard.vibrationText, "VIBRATION", "mm/s");
        UpdateMetricText(dashboard.aiStatusText, "AI STATUS", string.Empty);
        UpdateStatusStyle();
        UpdatePulseColors();

        if (updatedText != null)
        {
            float age = Mathf.Max(0f, Time.unscaledTime - lastTelemetryChangeTime);
            updatedText.text = age < 0.1f ? "UPDATED NOW" : $"UPDATED {age:0.0}s AGO";
        }
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            transform.rotation = targetCamera.transform.rotation;

            if (openingAnimationComplete)
            {
                float distance = Vector3.Distance(targetCamera.transform.position, transform.position);
                float scaleFactor = Mathf.Clamp(distance / 110f, 0.8f, 1.3f);
                Vector3 targetScale = baseLocalScale * scaleFactor;
                transform.localScale = Vector3.Lerp(
                    transform.localScale,
                    targetScale,
                    1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            }
        }

        if (tetherLine != null)
        {
            tetherLine.SetPosition(0, machineAnchor);
            tetherLine.SetPosition(1, transform.position - transform.up * 0.35f);
        }
    }

    private void OnDestroy()
    {
        if (tetherMaterial != null)
        {
            Destroy(tetherMaterial);
        }
    }

    private void StyleExistingElements()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Transform backgroundTransform = FindChildRecursive(transform, "Background1");
        Image background = backgroundTransform != null ? backgroundTransform.GetComponent<Image>() : null;
        if (background != null)
        {
            background.color = PanelColor;
            background.raycastTarget = true;

            decorationSprite = background.sprite;
            if (decorationSprite != null)
            {
                background.type = Image.Type.Sliced;
            }

            Shadow shadow = background.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = background.gameObject.AddComponent<Shadow>();
            }
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(10f, -10f);

            Outline outline = background.GetComponent<Outline>();
            if (outline == null)
            {
                outline = background.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.45f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        ConfigureText(dashboard.objectNameText, 38f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, ValueColor);
        SetRect(dashboard.objectNameText, new Vector2(-85f, 205f), new Vector2(370f, 62f));

        ConfigureMetric(dashboard.speedText, new Vector2(0f, 85f));
        ConfigureMetric(dashboard.temperatureText, new Vector2(0f, 15f));
        ConfigureMetric(dashboard.vibrationText, new Vector2(0f, -55f));
        ConfigureMetric(dashboard.aiStatusText, new Vector2(0f, -125f));

        Transform closeTransform = FindChildRecursive(transform, "Close button");
        if (closeTransform != null)
        {
            RectTransform closeRect = closeTransform as RectTransform;
            if (closeRect != null)
            {
                closeRect.localRotation = Quaternion.identity;
                closeRect.localScale = Vector3.one;
                closeRect.anchorMin = new Vector2(0.5f, 0.5f);
                closeRect.anchorMax = new Vector2(0.5f, 0.5f);
                closeRect.pivot = new Vector2(0.5f, 0.5f);
                closeRect.anchoredPosition = new Vector2(258f, 215f);
                closeRect.sizeDelta = new Vector2(42f, 42f);
            }

            Image closeImage = closeTransform.GetComponent<Image>();
            if (closeImage != null)
            {
                closeImage.color = new Color(DangerColor.r, DangerColor.g, DangerColor.b, 0.22f);
            }

            TMP_Text closeText = closeTransform.GetComponentInChildren<TMP_Text>();
            if (closeText != null)
            {
                closeText.text = "X";
                ConfigureText(
                    closeText,
                    30f,
                    FontStyles.Bold,
                    TextAlignmentOptions.Center,
                    ValueColor);
                StretchToParent(closeText.rectTransform);
            }
        }
    }

    private void BuildDecorations()
    {
        RectTransform contentRoot = dashboard.objectNameText != null
            ? dashboard.objectNameText.rectTransform.parent as RectTransform
            : transform as RectTransform;

        if (contentRoot == null)
        {
            return;
        }

        CreateImage(contentRoot, "HUD Accent", AccentColor, new Vector2(0f, 163f), new Vector2(540f, 4f));

        CreateMetricBackground(contentRoot, "Speed Row", dashboard.speedText, RowColor);
        CreateMetricBackground(contentRoot, "Temperature Row", dashboard.temperatureText, AlternateRowColor);
        CreateMetricBackground(contentRoot, "Vibration Row", dashboard.vibrationText, RowColor);
        statusBackground = CreateMetricBackground(
            contentRoot,
            "AI Status Row",
            dashboard.aiStatusText,
            new Color(NormalColor.r, NormalColor.g, NormalColor.b, 0.18f));

        Image liveBadge = CreateImage(
            contentRoot,
            "Live Badge",
            new Color(NormalColor.r, NormalColor.g, NormalColor.b, 0.16f),
            new Vector2(150f, 205f),
            new Vector2(105f, 34f));

        TMP_Text liveText = CreateText(liveBadge.rectTransform, "Live Text", dashboard.objectNameText);
        liveText.text = "LIVE";
        liveText.fontSize = 18f;
        liveText.fontStyle = FontStyles.Bold;
        liveText.color = NormalColor;
        liveText.alignment = TextAlignmentOptions.Center;

        updatedText = CreateText(contentRoot, "Last Updated", dashboard.objectNameText);
        updatedText.fontSize = 16f;
        updatedText.fontStyle = FontStyles.Bold;
        updatedText.color = LabelColor;
        updatedText.alignment = TextAlignmentOptions.MidlineRight;
        SetRect(updatedText, new Vector2(135f, -215f), new Vector2(260f, 28f));
    }

    private void BuildTether()
    {
        tetherLine = gameObject.AddComponent<LineRenderer>();
        tetherLine.useWorldSpace = true;
        tetherLine.positionCount = 2;
        tetherLine.startWidth = 0.025f;
        tetherLine.endWidth = 0.008f;
        tetherLine.numCapVertices = 4;
        tetherLine.startColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.75f);
        tetherLine.endColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.12f);
        tetherLine.sortingOrder = 20;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            tetherMaterial = new Material(shader)
            {
                name = "Dashboard Tether (Runtime)"
            };
            tetherLine.material = tetherMaterial;
        }
    }

    private IEnumerator AnimateOpen()
    {
        const float duration = 0.18f;
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        transform.localScale = baseLocalScale * 0.82f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            canvasGroup.alpha = eased;
            transform.localScale = Vector3.Lerp(baseLocalScale * 0.82f, baseLocalScale, eased);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        transform.localScale = baseLocalScale;
        openingAnimationComplete = true;
    }

    private void ConfigureMetric(TMP_Text text, Vector2 position)
    {
        ConfigureText(text, 26f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, ValueColor);
        SetRect(text, position, new Vector2(500f, 54f));
    }

    private static void ConfigureText(
        TMP_Text text,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.margin = Vector4.zero;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private void CacheCurrentValues()
    {
        CacheValue(dashboard.speedText);
        CacheValue(dashboard.temperatureText);
        CacheValue(dashboard.vibrationText);
        CacheValue(dashboard.aiStatusText);
    }

    private void CacheValue(TMP_Text text)
    {
        if (text != null)
        {
            lastPlainValues[text] = StripRichText(text.text);
        }
    }

    private void UpdateMetricText(TMP_Text text, string label, string unit)
    {
        if (text == null)
        {
            return;
        }

        string plainText = StripRichText(text.text);
        string value = ExtractValue(plainText);
        string previousValue = lastPlainValues.TryGetValue(text, out string cached) ? ExtractValue(cached) : null;

        if (!string.Equals(value, previousValue, System.StringComparison.Ordinal))
        {
            lastPlainValues[text] = plainText;
            pulseEndTimes[text] = Time.unscaledTime + 0.32f;
            lastTelemetryChangeTime = Time.unscaledTime;
        }

        string displayValue = AddUnitIfNumeric(value, unit);
        string desired = $"<color=#{ColorUtility.ToHtmlStringRGB(LabelColor)}>{label}</color>  <b>{displayValue}</b>";
        if (!string.Equals(text.text, desired, System.StringComparison.Ordinal))
        {
            text.text = desired;
        }
    }

    private void UpdateStatusStyle()
    {
        if (dashboard.aiStatusText == null)
        {
            return;
        }

        string status = ExtractValue(StripRichText(dashboard.aiStatusText.text)).ToLowerInvariant();
        Color statusColor = status.Contains("maintenance") || status.Contains("fault") || status.Contains("error")
            ? DangerColor
            : status.Contains("warn")
                ? WarningColor
                : status.Contains("normal") || status.Contains("healthy")
                    ? NormalColor
                    : LabelColor;

        if (statusBackground != null)
        {
            statusBackground.color = new Color(statusColor.r, statusColor.g, statusColor.b, 0.18f);
        }
    }

    private void UpdatePulseColors()
    {
        foreach (TMP_Text text in new[]
                 {
                     dashboard.speedText,
                     dashboard.temperatureText,
                     dashboard.vibrationText,
                     dashboard.aiStatusText
                 })
        {
            if (text == null)
            {
                continue;
            }

            if (pulseEndTimes.TryGetValue(text, out float pulseEnd) && Time.unscaledTime < pulseEnd)
            {
                float remaining = Mathf.Clamp01((pulseEnd - Time.unscaledTime) / 0.32f);
                text.color = Color.Lerp(ValueColor, AccentColor, remaining);
            }
            else
            {
                text.color = ValueColor;
            }
        }
    }

    private Image CreateMetricBackground(
        RectTransform parent,
        string name,
        TMP_Text target,
        Color color)
    {
        if (target == null)
        {
            return null;
        }

        Image image = CreateImage(
            parent,
            name,
            color,
            target.rectTransform.anchoredPosition,
            target.rectTransform.sizeDelta + new Vector2(18f, 4f));
        image.transform.SetSiblingIndex(Mathf.Max(0, target.transform.GetSiblingIndex()));
        return image;
    }

    private Image CreateImage(
        RectTransform parent,
        string name,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;

        Image image = gameObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (decorationSprite != null)
        {
            image.sprite = decorationSprite;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    private static TMP_Text CreateText(RectTransform parent, string name, TMP_Text template)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = parent.sizeDelta;

        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
        }
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(TMP_Text text, Vector2 position, Vector2 size)
    {
        if (text == null)
        {
            return;
        }

        RectTransform rectTransform = text.rectTransform;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string StripRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder result = new System.Text.StringBuilder(value.Length);
        bool insideTag = false;
        foreach (char character in value)
        {
            if (character == '<')
            {
                insideTag = true;
            }
            else if (character == '>')
            {
                insideTag = false;
            }
            else if (!insideTag)
            {
                result.Append(character);
            }
        }

        return result.ToString();
    }

    private static string ExtractValue(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "N/A";
        }

        int separator = text.IndexOf(':');
        if (separator >= 0 && separator < text.Length - 1)
        {
            return text.Substring(separator + 1).Trim();
        }

        // Enhanced text uses two spaces between its label and value after tags are stripped.
        int doubleSpace = text.IndexOf("  ", System.StringComparison.Ordinal);
        return doubleSpace >= 0 && doubleSpace < text.Length - 2
            ? text.Substring(doubleSpace + 2).Trim()
            : text.Trim();
    }

    private static string AddUnitIfNumeric(string value, string unit)
    {
        if (string.IsNullOrEmpty(unit) || string.Equals(value, "N/A", System.StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return float.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out _)
            ? $"{value} {unit}"
            : value;
    }
}
