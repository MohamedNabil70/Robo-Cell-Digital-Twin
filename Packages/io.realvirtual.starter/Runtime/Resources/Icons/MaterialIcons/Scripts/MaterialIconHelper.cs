// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace realvirtual
{
    //! Extension methods and helper utilities for using Material Icons with TextMeshPro components.
    //! Provides convenient methods to set icons on TMP_Text and TextMeshProUGUI components,
    //! combine icons with text, and manage Material Icons fonts at runtime.
    public static class MaterialIconHelper
    {
        private static TMP_FontAsset _cachedFont;

        //! Sets a Material Icon on a TextMeshProUGUI component
        //! Example: myText.SetMaterialIcon("home");
        public static void SetMaterialIcon(this TextMeshProUGUI text, string iconName)
        {
            if (text == null)
            {
                Debug.LogWarning("MaterialIconHelper: TextMeshProUGUI component is null.");
                return;
            }

            TMP_FontAsset font = GetTMPFont();
            if (font != null)
            {
                text.font = font;
            }

            text.text = MaterialIcons.GetIcon(iconName);
        }

        //! Sets a Material Icon on a TMP_Text component
        public static void SetMaterialIcon(this TMP_Text text, string iconName)
        {
            if (text == null)
            {
                Debug.LogWarning("MaterialIconHelper: TMP_Text component is null.");
                return;
            }

            TMP_FontAsset font = GetTMPFont();
            if (font != null)
            {
                text.font = font;
            }

            text.text = MaterialIcons.GetIcon(iconName);
        }

        //! Sets a Material Icon with text on a TextMeshProUGUI component
        //! Example: myText.SetMaterialIconWithText("save", "Save Document");
        public static void SetMaterialIconWithText(this TextMeshProUGUI text, string iconName, string labelText, string separator = " ")
        {
            if (text == null)
            {
                Debug.LogWarning("MaterialIconHelper: TextMeshProUGUI component is null.");
                return;
            }

            TMP_FontAsset font = GetTMPFont();
            if (font != null)
            {
                text.font = font;
            }

            text.text = MaterialIcons.GetIconWithText(iconName, labelText, separator);
        }

        //! Sets text with a Material Icon suffix on a TextMeshProUGUI component
        //! Example: myText.SetTextWithMaterialIcon("Save Document", "save");
        public static void SetTextWithMaterialIcon(this TextMeshProUGUI text, string labelText, string iconName, string separator = " ")
        {
            if (text == null)
            {
                Debug.LogWarning("MaterialIconHelper: TextMeshProUGUI component is null.");
                return;
            }

            TMP_FontAsset font = GetTMPFont();
            if (font != null)
            {
                text.font = font;
            }

            text.text = MaterialIcons.GetTextWithIcon(labelText, iconName, separator);
        }

        //! Sets a Material Icon with text on a TMP_Text component
        public static void SetMaterialIconWithText(this TMP_Text text, string iconName, string labelText, string separator = " ")
        {
            if (text == null)
            {
                Debug.LogWarning("MaterialIconHelper: TMP_Text component is null.");
                return;
            }

            TMP_FontAsset font = GetTMPFont();
            if (font != null)
            {
                text.font = font;
            }

            text.text = MaterialIcons.GetIconWithText(iconName, labelText, separator);
        }

        //! Updates only the icon text without changing the font
        //! Useful when the font is already set
        public static void UpdateIcon(this TextMeshProUGUI text, string iconName)
        {
            if (text == null)
            {
                Debug.LogWarning("MaterialIconHelper: TextMeshProUGUI component is null.");
                return;
            }

            text.text = MaterialIcons.GetIcon(iconName);
        }

        //! Updates only the icon text without changing the font
        public static void UpdateIcon(this TMP_Text text, string iconName)
        {
            if (text == null)
            {
                Debug.LogWarning("MaterialIconHelper: TMP_Text component is null.");
                return;
            }

            text.text = MaterialIcons.GetIcon(iconName);
        }

        //! Gets or loads the TMP_FontAsset for Material Icons with caching
        //! The font asset should be located at Resources/Icons/MaterialIcons/Fonts/MaterialIcons-RegularSDF
        public static TMP_FontAsset GetTMPFont()
        {
            // Check cache first
            if (_cachedFont != null)
                return _cachedFont;

            // Try to load from Resources
            TMP_FontAsset font = UnityEngine.Resources.Load<TMP_FontAsset>("Icons/MaterialIcons/Fonts/MaterialIcons-RegularSDF");

            if (font == null)
            {
                var savePath = RealvirtualAssetPaths.StarterResources("Icons/MaterialIcons/Fonts/MaterialIcons-RegularSDF.asset");
                Debug.LogWarning("MaterialIconHelper: TMP Font Asset not found at 'Resources/Icons/MaterialIcons/Fonts/MaterialIcons-RegularSDF'. " +
                    "Please create it using Window > TextMeshPro > Font Asset Creator.\n" +
                    "Source Font: MaterialIcons-Regular.ttf\n" +
                    "Character Set: Unicode Range (Hex)\n" +
                    "Character Range: e000-f8ff\n" +
                    $"Save as: {savePath}");
            }
            else
            {
                // Cache it
                _cachedFont = font;
            }

            return font;
        }

        //! Clears the font cache (useful if fonts are reloaded)
        public static void ClearFontCache()
        {
            _cachedFont = null;
        }

        //! Creates a GameObject with TextMeshProUGUI component displaying a Material Icon
        public static GameObject CreateIconObject(string iconName, Transform parent = null)
        {
            GameObject iconObj = new GameObject($"Icon_{iconName}");

            if (parent != null)
                iconObj.transform.SetParent(parent, false);

            TextMeshProUGUI text = iconObj.AddComponent<TextMeshProUGUI>();
            text.SetMaterialIcon(iconName);
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;

            RectTransform rectTransform = iconObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(32, 32);

            return iconObj;
        }

        //! Adds a Material Icon to an existing button
        public static void AddIconToButton(Button button, string iconName)
        {
            if (button == null)
            {
                Debug.LogWarning("MaterialIconHelper: Button is null.");
                return;
            }

            // Check if button already has a TextMeshProUGUI component
            TextMeshProUGUI existingText = button.GetComponentInChildren<TextMeshProUGUI>();

            if (existingText != null)
            {
                // Update existing text
                existingText.SetMaterialIcon(iconName);
            }
            else
            {
                // Create new icon object
                CreateIconObject(iconName, button.transform);
            }
        }
    }
}
