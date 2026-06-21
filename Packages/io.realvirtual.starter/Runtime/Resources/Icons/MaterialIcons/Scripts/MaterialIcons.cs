// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
    //! Material Icons helper class providing easy access to Google Material Design Icons.
    //! Supports both runtime UI (TextMeshPro) and editor UI (IMGUI). Use GetIcon() to get
    //! unicode characters for icons, or use the extension methods for easy integration
    //! with TextMeshPro components.
    public static class MaterialIcons
    {
        private static Dictionary<string, string> _iconDatabase;
        private static bool _isInitialized = false;

        // Cached font reference
        private static Font _cachedFont;

        //! Initializes the icon database by loading the codepoints file
        private static void Initialize()
        {
            if (_isInitialized)
                return;

            _iconDatabase = new Dictionary<string, string>();

            try
            {
                // Load the codepoints file (Unity Resources.Load strips the .txt extension automatically)
                TextAsset codepointsFile = UnityEngine.Resources.Load<TextAsset>("Icons/MaterialIcons/Fonts/MaterialIcons-Regular.codepoints");

                if (codepointsFile == null)
                {
                    // Silently fail on initialization - file may not be included in build
                    _isInitialized = true;
                    return;
                }

                // Parse the codepoints file
                string[] lines = codepointsFile.text.Split('\n');
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] parts = line.Trim().Split(' ');
                    if (parts.Length >= 2)
                    {
                        string iconName = parts[0];
                        string hexCode = parts[1];
                        _iconDatabase[iconName] = hexCode;
                    }
                }

                _isInitialized = true;
                // Debug.Log($"MaterialIcons: Loaded {_iconDatabase.Count} icons from codepoints file.");
            }
            catch (Exception e)
            {
                Debug.LogError($"MaterialIcons: Error loading codepoints file: {e.Message}");
                _isInitialized = true;
            }
        }

        //! Gets the unicode character for a Material Icon by name
        //! Example: GetIcon("home") returns "\ue88a"
        public static string GetIcon(string iconName)
        {
            if (!_isInitialized)
                Initialize();

            if (_iconDatabase == null || !_iconDatabase.ContainsKey(iconName))
            {
                // Return a fallback character silently - database may not be loaded
                return "?";
            }

            string hexCode = _iconDatabase[iconName];
            int unicode = Convert.ToInt32(hexCode, 16);
            return char.ConvertFromUtf32(unicode);
        }
        
        
        
        

        //! Gets the unicode hex code for a Material Icon by name
        //! Example: GetIconUnicode("home") returns "e88a"
        public static string GetIconUnicode(string iconName)
        {
            if (!_isInitialized)
                Initialize();

            if (_iconDatabase == null || !_iconDatabase.ContainsKey(iconName))
            {
                // Return empty string silently - database may not be loaded
                return "";
            }

            return _iconDatabase[iconName];
        }

        //! Gets the unicode character with a prefix text
        //! Example: GetIconWithText("home", "Home") returns "\ue88a Home"
        public static string GetIconWithText(string iconName, string text, string separator = " ")
        {
            return GetIcon(iconName) + separator + text;
        }

        //! Gets the unicode character with a suffix text
        //! Example: GetTextWithIcon("Home", "home") returns "Home \ue88a"
        public static string GetTextWithIcon(string text, string iconName, string separator = " ")
        {
            return text + separator + GetIcon(iconName);
        }

        //! Checks if an icon name exists in the database
        public static bool IconExists(string iconName)
        {
            if (!_isInitialized)
                Initialize();

            return _iconDatabase != null && _iconDatabase.ContainsKey(iconName);
        }

        //! Gets all available icon names
        public static List<string> GetAllIconNames()
        {
            if (!_isInitialized)
                Initialize();

            return _iconDatabase != null ? new List<string>(_iconDatabase.Keys) : new List<string>();
        }

        //! Gets the total number of available icons
        public static int GetIconCount()
        {
            if (!_isInitialized)
                Initialize();

            return _iconDatabase != null ? _iconDatabase.Count : 0;
        }

        //! Searches for icons containing the search term
        public static List<string> SearchIcons(string searchTerm)
        {
            if (!_isInitialized)
                Initialize();

            List<string> results = new List<string>();

            if (_iconDatabase == null || string.IsNullOrEmpty(searchTerm))
                return results;

            searchTerm = searchTerm.ToLower();

            foreach (string iconName in _iconDatabase.Keys)
            {
                if (iconName.ToLower().Contains(searchTerm))
                    results.Add(iconName);
            }

            return results;
        }

        //! Gets the Material Icons font for runtime use (loads from Resources)
        public static Font GetFont()
        {
            // Load font silently - may not be included in all builds
            Font font = UnityEngine.Resources.Load<Font>("Icons/MaterialIcons/Fonts/MaterialIcons-Regular");
            return font;
        }

#if UNITY_EDITOR
        //! Gets the Material Icons font for editor use with caching
        public static Font GetEditorFont()
        {
            if (_cachedFont != null)
                return _cachedFont;

            Font font = UnityEngine.Resources.Load<Font>("Icons/MaterialIcons/Fonts/MaterialIcons-Regular");

            if (font == null)
            {
                // Try direct asset path for editor
                var fontPath = RealvirtualAssetPaths.StarterResources("Icons/MaterialIcons/Fonts/MaterialIcons-Regular.ttf");
                font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            }

            if (font != null)
                _cachedFont = font;
            // Silently fail if font not found - may not be included in build

            return font;
        }

        //! Creates a Label element with Material Icon character and proper font for UI Toolkit
        //! Example: var iconLabel = MaterialIcons.CreateIconLabel("home", 16);
        public static UnityEngine.UIElements.Label CreateIconLabel(string iconName, int fontSize = 16)
        {
            var iconChar = GetIcon(iconName);
            var label = new UnityEngine.UIElements.Label(iconChar);
            var font = GetEditorFont();
            if (font != null)
            {
                label.style.unityFontDefinition = new UnityEngine.UIElements.StyleFontDefinition(font);
            }
            label.style.fontSize = fontSize;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        //! Creates a Button with Material Icon and text for UI Toolkit
        //! Example: var button = MaterialIcons.CreateIconButton("save", "Save", () => Save(), 16);
        public static UnityEngine.UIElements.Button CreateIconButton(string iconName, string text, System.Action onClick, int fontSize = 16)
        {
            var button = new UnityEngine.UIElements.Button(onClick);
            button.style.flexDirection = UnityEngine.UIElements.FlexDirection.Row;
            button.style.alignItems = UnityEngine.UIElements.Align.Center;
            button.style.justifyContent = UnityEngine.UIElements.Justify.Center;

            var iconLabel = CreateIconLabel(iconName, fontSize);
            iconLabel.style.marginRight = 6;
            button.Add(iconLabel);

            var textLabel = new UnityEngine.UIElements.Label(text);
            button.Add(textLabel);

            return button;
        }

        //! Creates a Button with only a Material Icon for UI Toolkit
        //! Example: var button = MaterialIcons.CreateIconOnlyButton("delete", () => Delete(), 20);
        public static UnityEngine.UIElements.Button CreateIconOnlyButton(string iconName, System.Action onClick, int fontSize = 20)
        {
            var button = new UnityEngine.UIElements.Button(onClick);
            button.style.justifyContent = UnityEngine.UIElements.Justify.Center;
            button.style.alignItems = UnityEngine.UIElements.Align.Center;

            var iconLabel = CreateIconLabel(iconName, fontSize);
            button.Add(iconLabel);

            return button;
        }

        //! Creates a GUIStyle for displaying Material Icons in editor UI
        public static GUIStyle GetIconStyle(int fontSize = 20, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GUIStyle iconStyle = new GUIStyle(EditorStyles.label);
            iconStyle.font = GetEditorFont();
            iconStyle.fontSize = fontSize;
            iconStyle.alignment = alignment;
            iconStyle.normal.textColor = EditorStyles.label.normal.textColor;
            return iconStyle;
        }

        //! Draws a Material Icon in IMGUI with specified size and alignment
        //! Example: MaterialIcons.DrawIcon("home", 20);
        public static void DrawIcon(string iconName, int fontSize = 20, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GUIStyle style = GetIconStyle(fontSize, alignment);
            GUILayout.Label(GetIcon(iconName), style, GUILayout.Width(fontSize), GUILayout.Height(fontSize));
        }

        //! Draws a Material Icon in IMGUI at a specific rect
        //! Example: MaterialIcons.DrawIcon(rect, "home", 20);
        public static void DrawIcon(Rect rect, string iconName, int fontSize = 20, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GUIStyle style = GetIconStyle(fontSize, alignment);
            GUI.Label(rect, GetIcon(iconName), style);
        }

        //! Draws a Material Icon button in IMGUI that returns true when clicked
        //! Example: if (MaterialIcons.IconButton("save", 20)) { /* save action */ }
        public static bool IconButton(string iconName, int fontSize = 20, params GUILayoutOption[] options)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.font = GetEditorFont();
            style.fontSize = fontSize;
            return GUILayout.Button(GetIcon(iconName), style, options);
        }

        //! Draws a Material Icon with text label in IMGUI
        //! Example: MaterialIcons.DrawIconWithLabel("save", "Save File", 20);
        public static void DrawIconWithLabel(string iconName, string label, int fontSize = 18)
        {
            GUIStyle iconStyle = GetIconStyle(fontSize, TextAnchor.MiddleLeft);
            GUILayout.BeginHorizontal();
            GUILayout.Label(GetIcon(iconName), iconStyle, GUILayout.Width(fontSize + 4));
            GUILayout.Label(label, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
        }
#endif

        // Common icon name constants for easy access
        public static class Common
        {
            public const string Home = "home";
            public const string Settings = "settings";
            public const string Search = "search";
            public const string Menu = "menu";
            public const string Close = "close";
            public const string Add = "add";
            public const string Remove = "remove";
            public const string Delete = "delete";
            public const string Edit = "edit";
            public const string Save = "save";
            public const string Cancel = "cancel";
            public const string Check = "check";
            public const string Info = "info";
            public const string Warning = "warning";
            public const string Error = "error";
            public const string Help = "help";
            public const string Favorite = "favorite";
            public const string Star = "star";
            public const string ArrowBack = "arrow_back";
            public const string ArrowForward = "arrow_forward";
            public const string ArrowUp = "arrow_upward";
            public const string ArrowDown = "arrow_downward";
            public const string ChevronLeft = "chevron_left";
            public const string ChevronRight = "chevron_right";
            public const string ExpandMore = "expand_more";
            public const string ExpandLess = "expand_less";
            public const string Refresh = "refresh";
            public const string Visibility = "visibility";
            public const string VisibilityOff = "visibility_off";
            public const string Lock = "lock";
            public const string LockOpen = "lock_open";
            public const string Folder = "folder";
            public const string FolderOpen = "folder_open";
            public const string Download = "download";
            public const string Upload = "upload";
            public const string Share = "share";
            public const string Copy = "content_copy";
            public const string Paste = "content_paste";
            public const string Cut = "content_cut";
            public const string PlayArrow = "play_arrow";
            public const string Pause = "pause";
            public const string Stop = "stop";
            public const string SkipNext = "skip_next";
            public const string SkipPrevious = "skip_previous";
        }

        // Industrial/Automation specific icons
        public static class Industrial
        {
            public const string Build = "build";
            public const string Settings = "settings";
            public const string SettingsApplications = "settings_applications";
            public const string Precision = "precision_manufacturing";
            public const string Memory = "memory";
            public const string DeveloperBoard = "developer_board";
            public const string RouterIcon = "router";
            public const string Cable = "cable";
            public const string Power = "power";
            public const string PowerOff = "power_off";
            public const string Speed = "speed";
            public const string Timeline = "timeline";
            public const string Tune = "tune";
            public const string Widgets = "widgets";
            public const string ViewInAr = "view_in_ar";
            public const string ThreeDRotation = "3d_rotation";
            public const string Straighten = "straighten";
            public const string GridOn = "grid_on";
            public const string GridOff = "grid_off";
        }
    }
}
