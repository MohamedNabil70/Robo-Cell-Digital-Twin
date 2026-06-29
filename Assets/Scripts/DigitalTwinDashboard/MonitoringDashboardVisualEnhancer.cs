using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-only dark/gold treatment for Monitoring mode dashboards.
/// This is presentation only; selected-object data still comes from ObjectDashboard.
/// </summary>
public sealed class MonitoringDashboardVisualEnhancer : MonoBehaviour
{
    static readonly Color PanelColor = new Color(0.025f, 0.023f, 0.02f, 0.97f);
    static readonly Color InnerPanelColor = new Color(0.055f, 0.049f, 0.039f, 0.94f);
    static readonly Color RowColor = new Color(0.10f, 0.086f, 0.055f, 0.84f);
    static readonly Color AlternateRowColor = new Color(0.073f, 0.067f, 0.052f, 0.84f);
    static readonly Color GoldColor = new Color(1f, 0.72f, 0.26f, 1f);
    static readonly Color SoftGoldColor = new Color(1f, 0.84f, 0.48f, 1f);
    static readonly Color LabelColor = new Color(0.70f, 0.64f, 0.53f, 1f);
    static readonly Color ValueColor = new Color(0.98f, 0.94f, 0.84f, 1f);
    static readonly Color NormalColor = new Color(0.36f, 0.92f, 0.62f, 1f);
    static readonly Color WarningColor = new Color(1f, 0.72f, 0.22f, 1f);
    static readonly Color DangerColor = new Color(1f, 0.32f, 0.28f, 1f);

    readonly Dictionary<TMP_Text, string> lastPlainValues = new Dictionary<TMP_Text, string>();
    readonly Dictionary<TMP_Text, float> pulseEndTimes = new Dictionary<TMP_Text, float>();

    ObjectDashboard dashboard;
    CanvasGroup canvasGroup;
    Image statusBackground;
    TMP_Text updatedText;
    Sprite panelSprite;
    bool initialized;
    float lastTelemetryChangeTime;

    public void Initialize(ObjectDashboard objectDashboard)
    {
        if (initialized || objectDashboard == null)
        {
            return;
        }

        initialized = true;
        dashboard = objectDashboard;
        lastTelemetryChangeTime = Time.unscaledTime;

        StyleRoot();
        StyleExistingElements();
        BuildDecorations();
        CacheCurrentValues();
        StartCoroutine(AnimateOpen());
    }

    void Update()
    {
        if (!initialized)
        {
            return;
        }

        foreach (TMP_Text metricText in dashboard.GetMetricTexts())
        {
            UpdateMetricText(metricText);
        }

        UpdateStatusStyle();
        UpdatePulseColors();

        if (updatedText != null)
        {
            float age = Mathf.Max(0f, Time.unscaledTime - lastTelemetryChangeTime);
            updatedText.text = age < 0.1f ? "SYNCED NOW" : $"SYNCED {age:0.0}s AGO";
        }
    }

    void StyleRoot()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        RectTransform root = transform as RectTransform;
        if (root != null)
        {
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(640f, 430f);
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }
    }

    void StyleExistingElements()
    {
        Transform backgroundTransform = FindChildRecursive(transform, "Background1");
        Image background = backgroundTransform != null ? backgroundTransform.GetComponent<Image>() : null;
        if (background != null)
        {
            panelSprite = background.sprite;
            background.color = PanelColor;
            background.raycastTarget = true;
            if (panelSprite != null)
            {
                background.type = Image.Type.Sliced;
            }

            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
            backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(640f, 430f);

            Shadow shadow = background.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = background.gameObject.AddComponent<Shadow>();
            }
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(14f, -14f);

            Outline outline = background.GetComponent<Outline>();
            if (outline == null)
            {
                outline = background.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.58f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        ConfigureText(dashboard.objectNameText, 36f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, SoftGoldColor);
        SetRect(dashboard.objectNameText, new Vector2(-92f, 155f), new Vector2(390f, 58f));

        TMP_Text[] metricTexts = dashboard.GetMetricTexts();
        for (int i = 0; i < metricTexts.Length; i++)
        {
            ConfigureText(metricTexts[i], 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, ValueColor);
            SetRect(metricTexts[i], new Vector2(0f, 70f - (58f * i)), new Vector2(540f, 42f));
        }

        Transform closeTransform = FindChildRecursive(transform, "Close button");
        if (closeTransform != null)
        {
            RectTransform closeRect = closeTransform as RectTransform;
            if (closeRect != null)
            {
                closeRect.anchorMin = new Vector2(0.5f, 0.5f);
                closeRect.anchorMax = new Vector2(0.5f, 0.5f);
                closeRect.pivot = new Vector2(0.5f, 0.5f);
                closeRect.anchoredPosition = new Vector2(278f, 176f);
                closeRect.sizeDelta = new Vector2(40f, 40f);
                closeRect.localRotation = Quaternion.identity;
                closeRect.localScale = Vector3.one;
            }

            Image closeImage = closeTransform.GetComponent<Image>();
            if (closeImage != null)
            {
                closeImage.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.18f);
            }

            TMP_Text closeText = closeTransform.GetComponentInChildren<TMP_Text>();
            if (closeText != null)
            {
                closeText.text = "X";
                ConfigureText(closeText, 26f, FontStyles.Bold, TextAlignmentOptions.Center, SoftGoldColor);
                StretchToParent(closeText.rectTransform);
            }
        }
    }

    void BuildDecorations()
    {
        RectTransform contentRoot = dashboard.objectNameText != null
            ? dashboard.objectNameText.rectTransform.parent as RectTransform
            : transform as RectTransform;

        if (contentRoot == null)
        {
            return;
        }

        CreateImage(contentRoot, "Monitoring Inner Panel", InnerPanelColor, new Vector2(0f, -18f), new Vector2(590f, 325f));
        CreateImage(contentRoot, "Gold Top Rule", GoldColor, new Vector2(0f, 124f), new Vector2(565f, 3f));
        CreateImage(contentRoot, "Gold Glow Rule", new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.18f), new Vector2(0f, 119f), new Vector2(565f, 12f));
        CreateImage(contentRoot, "Left Gold Rail", new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.72f), new Vector2(-294f, -18f), new Vector2(4f, 325f));

        TMP_Text[] metricTexts = dashboard.GetMetricTexts();
        for (int i = 0; i < metricTexts.Length; i++)
        {
            Image row = CreateImage(
                contentRoot,
                $"Gold Metric Row {i + 1}",
                i % 2 == 0 ? RowColor : AlternateRowColor,
                metricTexts[i].rectTransform.anchoredPosition,
                new Vector2(560f, 45f));
            row.transform.SetSiblingIndex(Mathf.Max(0, metricTexts[i].transform.GetSiblingIndex()));

            if (metricTexts[i] == dashboard.GetStatusText())
            {
                statusBackground = row;
            }
        }

        Image badge = CreateImage(
            contentRoot,
            "Monitoring Badge",
            new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.16f),
            new Vector2(168f, 155f),
            new Vector2(154f, 34f));

        TMP_Text badgeText = CreateText(badge.rectTransform, "Monitoring Badge Text", dashboard.objectNameText);
        badgeText.text = "MONITORING";
        ConfigureText(badgeText, 16f, FontStyles.Bold, TextAlignmentOptions.Center, GoldColor);
        StretchToParent(badgeText.rectTransform);

        updatedText = CreateText(contentRoot, "Monitoring Updated", dashboard.objectNameText);
        ConfigureText(updatedText, 14f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, LabelColor);
        SetRect(updatedText, new Vector2(120f, -184f), new Vector2(360f, 26f));
    }

    IEnumerator AnimateOpen()
    {
        const float duration = 0.22f;
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        Vector3 fullScale = transform.localScale;
        transform.localScale = fullScale * 0.94f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            canvasGroup.alpha = eased;
            transform.localScale = Vector3.Lerp(fullScale * 0.94f, fullScale, eased);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        transform.localScale = fullScale;
    }

    void CacheCurrentValues()
    {
        foreach (TMP_Text metricText in dashboard.GetMetricTexts())
        {
            if (metricText != null)
            {
                lastPlainValues[metricText] = StripRichText(metricText.text);
            }
        }
    }

    void UpdateMetricText(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        string plainText = StripRichText(text.text);
        string label = ExtractLabel(plainText);
        string value = ExtractValue(plainText);
        string previousValue = lastPlainValues.TryGetValue(text, out string cached) ? ExtractValue(cached) : null;

        if (!string.Equals(value, previousValue, System.StringComparison.Ordinal))
        {
            lastPlainValues[text] = plainText;
            pulseEndTimes[text] = Time.unscaledTime + 0.34f;
            lastTelemetryChangeTime = Time.unscaledTime;
        }

        string desired =
            $"<color=#{ColorUtility.ToHtmlStringRGB(LabelColor)}>{label.ToUpperInvariant()}</color>  <color=#{ColorUtility.ToHtmlStringRGB(ValueColor)}><b>{value}</b></color>";
        if (!string.Equals(text.text, desired, System.StringComparison.Ordinal))
        {
            text.text = desired;
        }
    }

    void UpdateStatusStyle()
    {
        TMP_Text statusText = dashboard.GetStatusText();
        if (statusText == null || statusBackground == null)
        {
            return;
        }

        string status = ExtractValue(StripRichText(statusText.text)).ToLowerInvariant();
        Color statusColor = status.Contains("maintenance") || status.Contains("fault") || status.Contains("critical") || status.Contains("error")
            ? DangerColor
            : status.Contains("warn") || status.Contains("degraded")
                ? WarningColor
                : status.Contains("normal") || status.Contains("healthy")
                    ? NormalColor
                    : GoldColor;

        statusBackground.color = new Color(statusColor.r, statusColor.g, statusColor.b, 0.20f);
    }

    void UpdatePulseColors()
    {
        foreach (TMP_Text text in dashboard.GetMetricTexts())
        {
            if (text == null)
            {
                continue;
            }

            if (pulseEndTimes.TryGetValue(text, out float pulseEnd) && Time.unscaledTime < pulseEnd)
            {
                float remaining = Mathf.Clamp01((pulseEnd - Time.unscaledTime) / 0.34f);
                text.color = Color.Lerp(ValueColor, GoldColor, remaining);
            }
            else
            {
                text.color = ValueColor;
            }
        }
    }

    Image CreateImage(RectTransform parent, string name, Color color, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (panelSprite != null)
        {
            image.sprite = panelSprite;
            image.type = Image.Type.Sliced;
        }

        return image;
    }

    static TMP_Text CreateText(RectTransform parent, string name, TMP_Text template)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
        }
        text.raycastTarget = false;
        return text;
    }

    static void ConfigureText(TMP_Text text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
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

    static void SetRect(TMP_Text text, Vector2 position, Vector2 size)
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

    static void StretchToParent(RectTransform rectTransform)
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

    static Transform FindChildRecursive(Transform parent, string childName)
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

    static string StripRichText(string value)
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

    static string ExtractValue(string text)
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

        int doubleSpace = text.IndexOf("  ", System.StringComparison.Ordinal);
        return doubleSpace >= 0 && doubleSpace < text.Length - 2
            ? text.Substring(doubleSpace + 2).Trim()
            : text.Trim();
    }

    static string ExtractLabel(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "VALUE";
        }

        int separator = text.IndexOf(':');
        if (separator > 0)
        {
            return text.Substring(0, separator).Trim();
        }

        int doubleSpace = text.IndexOf("  ", System.StringComparison.Ordinal);
        return doubleSpace > 0 ? text.Substring(0, doubleSpace).Trim() : "VALUE";
    }
}
