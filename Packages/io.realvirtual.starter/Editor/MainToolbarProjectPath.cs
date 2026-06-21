// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEditor;
using UnityEditor.Toolbars;

namespace realvirtual
{
    //! Adds the realvirtual branding with Tools dropdown to Unity 6's main editor toolbar.
    //! Clicking shows the full realvirtual Tools menu plus project utilities.
    [InitializeOnLoad]
    public static class MainToolbarProjectPath
    {
        private static MainToolbarDropdown toolbarDropdown;
        private const string ElementId = "realvirtual Project Path";

        static MainToolbarProjectPath()
        {
            EditorApplication.delayCall += () =>
            {
                if (ProjectPathMenuItem.IsProjectPathEnabled())
                {
                    UnityEditor.Toolbars.MainToolbar.Refresh(ElementId);
                }
            };
        }

        [MainToolbarElement(ElementId,
            defaultDockPosition = MainToolbarDockPosition.Right,
            defaultDockIndex = -100)]
        public static MainToolbarElement CreateProjectPathElement()
        {
            string projectPath = GetShortProjectPath();

            Texture2D icon = LoadIcon();

            var content = new MainToolbarContent();
            content.image = icon;
            content.text = projectPath;
            content.tooltip = "realvirtual Tools & Project Menu";

            toolbarDropdown = new MainToolbarDropdown(content, ShowDropdownMenu);

            bool shouldDisplay = ProjectPathMenuItem.IsProjectPathEnabled();
            toolbarDropdown.displayed = shouldDisplay;

            EditorApplication.update -= CheckVisibilityUpdate;
            EditorApplication.update += CheckVisibilityUpdate;

            return toolbarDropdown;
        }

        private static void ShowDropdownMenu(Rect buttonRect)
        {
            // Dynamically shows ALL menu items under Tools/realvirtual/ as a popup.
            // This automatically includes items from starter, professional, and any other packages.
            EditorUtility.DisplayPopupMenu(buttonRect, "Tools/realvirtual/", null);
        }

        private static Texture2D LoadIcon()
        {
            string[] iconPaths = new string[]
            {
                RealvirtualAssetPaths.StarterResources("Icons/Icon48.png"),
                RealvirtualAssetPaths.StarterResources("Icons/Icon64.png")
            };

            foreach (var path in iconPaths)
            {
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon != null) return icon;
            }

            var iconGuids = AssetDatabase.FindAssets("Icon48 t:Texture2D");
            if (iconGuids.Length > 0)
            {
                var iconPath = AssetDatabase.GUIDToAssetPath(iconGuids[0]);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            }

            return null;
        }

        private static string GetShortProjectPath()
        {
            string projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            var pathParts = projectPath.Split(System.IO.Path.DirectorySeparatorChar);

            if (pathParts.Length > 0)
            {
                return pathParts[pathParts.Length - 1];
            }
            return projectPath;
        }

        private static void CheckVisibilityUpdate()
        {
            if (toolbarDropdown == null) return;

            bool shouldShow = ProjectPathMenuItem.IsProjectPathEnabled();

            if (toolbarDropdown.displayed != shouldShow)
            {
                toolbarDropdown.displayed = shouldShow;
            }
        }
    }
}
#endif
