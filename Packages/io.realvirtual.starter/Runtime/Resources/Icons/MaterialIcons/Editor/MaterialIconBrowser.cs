// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    //! Editor window for browsing and previewing Material Design Icons.
    //! Provides a searchable grid view of all 2200+ Material Icons.
    //! Click icons to copy their names to clipboard for easy use in code.
    public class MaterialIconBrowser : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private int iconsPerRow = 8;
        private float iconSize = 48f;
        private List<string> allIconNames = new List<string>();
        private List<string> filteredIconNames = new List<string>();
        private GUIStyle iconButtonStyle;
        private GUIStyle labelStyle;
        private GUIStyle iconStyle;
        private Font currentFont;
        private string selectedIcon = "";
        private bool showCategories = false;

        // Icon categories
        private Dictionary<string, List<string>> categories = new Dictionary<string, List<string>>();
        private string selectedCategory = "All";

        // Callback for icon selection
        private static System.Action<string> onIconSelected;

        // Double-click tracking
        private double lastClickTime = 0;
        private string lastClickedIcon = "";
        private const double doubleClickTime = 0.3;

        #if REALVIRTUAL_DEV
        [MenuItem("Tools/realvirtual/Material Icon Browser", false, 101)]
        public static void ShowWindow()
        {
            MaterialIconBrowser window = GetWindow<MaterialIconBrowser>();
            window.titleContent = new GUIContent("Material Icon Browser");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        #endif

        //! Opens the Material Icon Browser with a callback for icon selection
        //! The callback receives the unicode hex code when an icon is double-clicked
        public static void ShowWindow(System.Action<string> iconSelectedCallback)
        {
            onIconSelected = iconSelectedCallback;
            MaterialIconBrowser window = GetWindow<MaterialIconBrowser>();
            window.titleContent = new GUIContent("Material Icon Browser - Double-click to select");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            LoadAllIconNames();
            LoadFont();
            CategorizeIcons();
            FilterIcons();
        }

        private void LoadAllIconNames()
        {
            allIconNames = MaterialIcons.GetAllIconNames();
            allIconNames.Sort();
        }

        private void LoadFont()
        {
            currentFont = MaterialIcons.GetEditorFont();
        }

        private void CategorizeIcons()
        {
            categories.Clear();
            categories["All"] = new List<string>(allIconNames);

            // Auto-categorize based on common prefixes and patterns
            foreach (string iconName in allIconNames)
            {
                string category = DetermineCategory(iconName);
                if (!categories.ContainsKey(category))
                    categories[category] = new List<string>();

                categories[category].Add(iconName);
            }
        }

        private string DetermineCategory(string iconName)
        {
            // Categorize based on common prefixes
            if (iconName.StartsWith("arrow_") || iconName.StartsWith("chevron_") || iconName.StartsWith("navigate_"))
                return "Navigation";
            if (iconName.StartsWith("play_") || iconName.StartsWith("pause") || iconName.StartsWith("stop") ||
                iconName.StartsWith("volume_") || iconName.StartsWith("mic_") || iconName.Contains("music"))
                return "Media";
            if (iconName.StartsWith("phone_") || iconName.StartsWith("email") || iconName.StartsWith("chat") ||
                iconName.StartsWith("message") || iconName.Contains("mail"))
                return "Communication";
            if (iconName.StartsWith("folder") || iconName.StartsWith("file_") || iconName.StartsWith("cloud_") ||
                iconName.StartsWith("save") || iconName.StartsWith("download") || iconName.StartsWith("upload"))
                return "File";
            if (iconName.StartsWith("person") || iconName.StartsWith("people") || iconName.StartsWith("account_") ||
                iconName.StartsWith("face"))
                return "Social";
            if (iconName.StartsWith("settings") || iconName.StartsWith("build") || iconName.StartsWith("tune"))
                return "Settings";
            if (iconName.StartsWith("location") || iconName.StartsWith("place") || iconName.StartsWith("map"))
                return "Maps";
            if (iconName.Contains("alarm") || iconName.Contains("schedule") || iconName.Contains("timer") ||
                iconName.Contains("event"))
                return "Time";
            if (iconName.StartsWith("battery") || iconName.StartsWith("wifi") || iconName.StartsWith("bluetooth") ||
                iconName.StartsWith("signal") || iconName.Contains("network"))
                return "Device";
            if (iconName.StartsWith("warning") || iconName.StartsWith("error") || iconName.StartsWith("info") ||
                iconName.StartsWith("check") || iconName.StartsWith("report"))
                return "Alert";
            if (iconName.StartsWith("camera") || iconName.StartsWith("photo") || iconName.StartsWith("image") ||
                iconName.StartsWith("crop") || iconName.StartsWith("filter"))
                return "Image";
            if (iconName.StartsWith("shopping") || iconName.StartsWith("store") || iconName.StartsWith("payment") ||
                iconName.StartsWith("attach_money"))
                return "Commerce";

            return "Other";
        }

        private void FilterIcons()
        {
            filteredIconNames.Clear();

            List<string> sourceList = selectedCategory == "All" ? allIconNames :
                (categories.ContainsKey(selectedCategory) ? categories[selectedCategory] : allIconNames);

            foreach (string iconName in sourceList)
            {
                bool matchesFilter = string.IsNullOrEmpty(searchFilter) ||
                                   iconName.ToLower().Contains(searchFilter.ToLower());

                if (matchesFilter)
                {
                    filteredIconNames.Add(iconName);
                }
            }
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical();

            // Header controls
            DrawHeader();

            EditorGUILayout.Space(5);

            // Stats and selected icon info
            DrawInfoBar();

            EditorGUILayout.Space(5);

            // Icon grid
            DrawIconGrid();

            EditorGUILayout.EndVertical();
        }

        private void InitializeStyles()
        {
            if (iconButtonStyle == null)
            {
                iconButtonStyle = new GUIStyle(GUI.skin.button);
                iconButtonStyle.padding = new RectOffset(4, 4, 4, 4);
                iconButtonStyle.margin = new RectOffset(2, 2, 2, 2);
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.miniLabel);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.wordWrap = true;
            }

            if (iconStyle == null || iconStyle.font != currentFont)
            {
                iconStyle = new GUIStyle(EditorStyles.label);
                iconStyle.font = currentFont;
                iconStyle.fontSize = (int)iconSize;
                iconStyle.alignment = TextAnchor.MiddleCenter;
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search field
            GUILayout.Label("Search:", GUILayout.Width(50));
            string newSearchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (newSearchFilter != searchFilter)
            {
                searchFilter = newSearchFilter;
                FilterIcons();
            }

            // Clear search button
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchFilter = "";
                FilterIcons();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            // Second toolbar row
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Category toggle
            showCategories = GUILayout.Toggle(showCategories, "Categories", EditorStyles.toolbarButton, GUILayout.Width(80));

            if (showCategories)
            {
                // Category dropdown
                string[] categoryNames = categories.Keys.OrderBy(k => k == "All" ? "" : k).ToArray();
                int currentIndex = Array.IndexOf(categoryNames, selectedCategory);
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUILayout.Popup(currentIndex, categoryNames, EditorStyles.toolbarPopup, GUILayout.Width(150));
                if (newIndex != currentIndex && newIndex >= 0)
                {
                    selectedCategory = categoryNames[newIndex];
                    FilterIcons();
                }
            }

            GUILayout.FlexibleSpace();

            // Icon size slider
            GUILayout.Label("Size:", GUILayout.Width(35));
            float newSize = GUILayout.HorizontalSlider(iconSize, 24f, 96f, GUILayout.Width(100));
            if (Math.Abs(newSize - iconSize) > 0.1f)
            {
                iconSize = newSize;
                iconStyle = null; // Force style recreation
            }

            // Icons per row
            GUILayout.Label("Per Row:", GUILayout.Width(60));
            iconsPerRow = EditorGUILayout.IntSlider(iconsPerRow, 4, 16, GUILayout.Width(120));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawInfoBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Showing {filteredIconNames.Count} of {allIconNames.Count} icons",
                EditorStyles.miniLabel, GUILayout.Width(200));

            if (!string.IsNullOrEmpty(selectedIcon))
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"Selected: {selectedIcon}", EditorStyles.boldLabel);

                if (GUILayout.Button("Copy Name", GUILayout.Width(80)))
                {
                    EditorGUIUtility.systemCopyBuffer = selectedIcon;
                    ShowNotification(new GUIContent($"Copied: {selectedIcon}"));
                }

                if (GUILayout.Button("Copy Code", GUILayout.Width(80)))
                {
                    string code = $"MaterialIcons.GetIcon(\"{selectedIcon}\")";
                    EditorGUIUtility.systemCopyBuffer = code;
                    ShowNotification(new GUIContent("Copied code snippet"));
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawIconGrid()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            float windowWidth = position.width - 20;
            float cellWidth = windowWidth / iconsPerRow;
            float cellHeight = iconSize + 45;

            int currentCol = 0;

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < filteredIconNames.Count; i++)
            {
                string iconName = filteredIconNames[i];

                if (currentCol >= iconsPerRow)
                {
                    currentCol = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                DrawIcon(iconName, cellWidth, cellHeight);
                currentCol++;
            }

            // Fill remaining space
            while (currentCol < iconsPerRow && filteredIconNames.Count > 0)
            {
                GUILayout.FlexibleSpace();
                currentCol++;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawIcon(string iconName, float cellWidth, float cellHeight)
        {
            bool isSelected = iconName == selectedIcon;

            EditorGUILayout.BeginVertical(isSelected ? EditorStyles.helpBox : GUIStyle.none,
                GUILayout.Width(cellWidth), GUILayout.Height(cellHeight));

            // Icon display
            Rect iconRect = GUILayoutUtility.GetRect(iconSize, iconSize, GUILayout.ExpandWidth(false));
            iconRect.x += (cellWidth - iconSize) / 2;

            string iconChar = MaterialIcons.GetIcon(iconName);

            // Draw icon with button behavior
            if (GUI.Button(iconRect, iconChar, iconStyle))
            {
                // Check for double-click
                bool isDoubleClick = false;
                if (lastClickedIcon == iconName && (EditorApplication.timeSinceStartup - lastClickTime) < doubleClickTime)
                {
                    isDoubleClick = true;
                }

                lastClickedIcon = iconName;
                lastClickTime = EditorApplication.timeSinceStartup;

                if (isDoubleClick && onIconSelected != null)
                {
                    // Double-clicked - return unicode via callback
                    string unicode = MaterialIcons.GetIconUnicode(iconName);
                    onIconSelected(unicode);
                    ShowNotification(new GUIContent($"Selected: {iconName} ({unicode})"));
                    Close();
                }
                else
                {
                    // Single click - select icon
                    selectedIcon = iconName;
                    EditorGUIUtility.systemCopyBuffer = iconName;
                    ShowNotification(new GUIContent($"Selected: {iconName}"));
                }
            }

            // Tooltip on hover
            if (iconRect.Contains(Event.current.mousePosition))
            {
                GUI.Label(iconRect, new GUIContent("", iconName));
            }

            // Icon name label
            string displayName = iconName.Length > 12 ? iconName.Substring(0, 9) + "..." : iconName;
            Rect labelRect = GUILayoutUtility.GetRect(cellWidth - 8, 30);

            if (GUI.Button(labelRect, displayName, labelStyle))
            {
                selectedIcon = iconName;
                EditorGUIUtility.systemCopyBuffer = iconName;
                ShowNotification(new GUIContent($"Copied: {iconName}"));
            }

            EditorGUILayout.EndVertical();
        }
    }
}

#endif
