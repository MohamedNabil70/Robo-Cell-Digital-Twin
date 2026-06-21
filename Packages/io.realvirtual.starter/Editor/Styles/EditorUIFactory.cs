// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System;

namespace realvirtual
{
    public static class EditorUIFactory
    {
        // Color constants (byte-exact match to existing inline values across editor tools)
        public static readonly Color ColorSuccess   = new Color(0.3f, 0.6f, 0.3f);   // green  — apply / move / confirm
        public static readonly Color ColorDanger    = new Color(0.7f, 0.3f, 0.3f);   // red    — undo / delete / error
        public static readonly Color ColorWarning   = new Color(0.7f, 0.6f, 0.2f);   // yellow — reset / caution
        public static readonly Color ColorPrimary   = new Color(0.2f, 0.4f, 0.8f);   // blue   — select / add / primary
        public static readonly Color ColorHeader    = new Color(0.85f, 0.85f, 0.85f); // light gray — section headers
        public static readonly Color ColorMuted     = new Color(0.7f, 0.7f, 0.7f);   // medium gray — secondary text
        public static readonly Color ColorSeparator = new Color(0.3f, 0.3f, 0.3f);   // dark gray — dividers

        private static StyleSheet _cachedSharedStyleSheet;

        /// <summary>
        /// Load and attach the shared editor stylesheet to a root element.
        /// Call this at the start of CreateGUI() in any editor tool.
        /// Uses cached FindAssets lookup — no hardcoded path needed.
        /// </summary>
        public static void AttachStylesheet(VisualElement root)
        {
            var styleSheet = FindStyleSheet("realvirtual-editor");
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);
        }

        /// <summary>
        /// Find a USS stylesheet by asset name (without extension).
        /// Caches the shared stylesheet; other lookups are cached per-call site.
        /// </summary>
        public static StyleSheet FindStyleSheet(string name)
        {
            if (name == "realvirtual-editor" && _cachedSharedStyleSheet != null)
                return _cachedSharedStyleSheet;

            var guids = AssetDatabase.FindAssets($"t:StyleSheet {name}");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[EditorUIFactory] USS not found: {name}");
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);

            if (name == "realvirtual-editor")
                _cachedSharedStyleSheet = sheet;

            return sheet;
        }

        /// <summary>
        /// Create a section container with a bold header label.
        /// </summary>
        public static VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("rv-editor-section");

            var header = new Label(title);
            header.AddToClassList("rv-editor-section-header");
            section.Add(header);

            return section;
        }

        /// <summary>
        /// Create a 1px horizontal separator.
        /// </summary>
        public static VisualElement CreateSeparator()
        {
            var separator = new VisualElement();
            separator.AddToClassList("rv-editor-separator");
            return separator;
        }

        /// <summary>
        /// Create a horizontal action-button row.
        /// </summary>
        public static VisualElement CreateActionRow()
        {
            var row = new VisualElement();
            row.AddToClassList("rv-editor-action-row");
            return row;
        }

        /// <summary>
        /// Create a header row with a title and optional toggle button.
        /// </summary>
        public static VisualElement CreateHeaderRow(string title, Button toggleButton = null)
        {
            var row = new VisualElement();
            row.AddToClassList("rv-editor-header-row");

            var label = new Label(title);
            label.AddToClassList("rv-editor-section-header");
            row.Add(label);

            if (toggleButton != null)
            {
                toggleButton.AddToClassList("rv-editor-btn-toggle");
                row.Add(toggleButton);
            }

            return row;
        }

        /// <summary>
        /// Create a field + button row (e.g., ObjectField + Select button).
        /// </summary>
        public static VisualElement CreateFieldRow()
        {
            var row = new VisualElement();
            row.AddToClassList("rv-editor-field-row");
            return row;
        }

        /// <summary>
        /// Set button background color based on enabled state.
        /// When disabled, resets to default styling.
        /// </summary>
        public static void SetButtonColor(Button button, bool enabled, Color enabledColor)
        {
            if (enabled)
            {
                button.style.backgroundColor = enabledColor;
                button.style.color = Color.white;
            }
            else
            {
                button.style.backgroundColor = StyleKeyword.Null;
                button.style.color = StyleKeyword.Null;
            }
        }
    }
}
