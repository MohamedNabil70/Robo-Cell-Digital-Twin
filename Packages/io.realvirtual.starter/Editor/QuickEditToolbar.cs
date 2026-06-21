// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;
using System;

namespace realvirtual
{
    //! Unity 6 compatible toolbar button for realvirtual QuickEdit
    //! Uses the supported EditorToolbar API instead of reflection-based injection
    [EditorToolbarElement(id, typeof(SceneView))]
    public class QuickEditToolbarButton : EditorToolbarButton
    {
        public const string id = "realvirtual/QuickEditToggle";
        private static Texture2D iconTexture;

        public QuickEditToolbarButton()
        {
            // Load icon
            LoadIcon();

            // Setup button
            icon = iconTexture;
            tooltip = "Toggle realvirtual Quick Edit";

            // Handle click
            clicked += OnClick;

            // Update visual state periodically
            EditorApplication.update += UpdateVisualState;
        }

        private void LoadIcon()
        {
            if (iconTexture != null) return;

            // Try multiple paths with UPM package paths
            string[] paths = new string[]
            {
                RealvirtualAssetPaths.StarterResources("Icons/Icon48.png"),
                RealvirtualAssetPaths.StarterEditorAssets("Icons/button-0local.png")
            };

            foreach (var path in paths)
            {
                iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (iconTexture != null) break;
            }

            // Fallback: search for any realvirtual icon
            if (iconTexture == null)
            {
                var iconGuids = AssetDatabase.FindAssets("Icon48 t:Texture2D");
                if (iconGuids.Length > 0)
                {
                    var iconPath = AssetDatabase.GUIDToAssetPath(iconGuids[0]);
                    iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                }
            }
        }

        private void OnClick()
        {
            // Toggle QuickEdit overlay visibility
            bool currentState = EditorPrefs.GetBool("realvirtual_QuickEditVisible", true);
            bool newState = !currentState;
            EditorPrefs.SetBool("realvirtual_QuickEditVisible", newState);

            // Update overlay
            UpdateOverlayVisibility(newState);
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (this == null) return;

            bool isVisible = EditorPrefs.GetBool("realvirtual_QuickEditVisible", true);

            // Update button style to indicate state
            style.opacity = isVisible ? 1.0f : 0.6f;
        }

        private void UpdateOverlayVisibility(bool visible)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            // Find and update QuickEdit overlay
            try
            {
                var overlays = sceneView.overlayCanvas.overlays;
                foreach (var overlay in overlays)
                {
                    if (overlay != null && overlay.GetType().Name == "QuickEditOverlay")
                    {
                        overlay.displayed = visible;
                        if (visible && overlay.collapsed)
                        {
                            overlay.collapsed = false;
                        }
                        break;
                    }
                }

                sceneView.Repaint();
            }
            catch (Exception e)
            {
                Debug.LogError($"[realvirtual] Error updating overlay: {e.Message}");
            }
        }
    }

    //! Dropdown menu for realvirtual quick settings
    [EditorToolbarElement(id, typeof(SceneView))]
    public class QuickEditToolbarDropdown : EditorToolbarDropdown
    {
        public const string id = "realvirtual/QuickEditMenu";

        public QuickEditToolbarDropdown()
        {
            text = "▼";
            tooltip = "realvirtual Quick Settings";
            clicked += ShowDropdown;

            // Style the dropdown to look minimal
            style.width = 16;
            style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private void ShowDropdown()
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Apply Standard Settings"), false, () => {
                ProjectSettingsTools.SetStandardSettings(true);
            });

            menu.AddItem(new GUIContent("Show Project Path in Toolbar"),
                ProjectPathMenuItem.IsProjectPathEnabled(),
                () => {
                    EditorApplication.ExecuteMenuItem(ProjectPathMenuItem.MenuName);
                });

            menu.ShowAsContext();
        }
    }

    //! Optional: Display project path in toolbar
    [EditorToolbarElement(id, typeof(SceneView))]
    public class ProjectPathToolbarLabel : VisualElement
    {
        public const string id = "realvirtual/ProjectPath";

        private Label pathLabel;

        public ProjectPathToolbarLabel()
        {
            // Only show if enabled
            if (!ProjectPathMenuItem.IsProjectPathEnabled())
            {
                style.display = DisplayStyle.None;
                return;
            }

            pathLabel = new Label();
            pathLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            pathLabel.style.fontSize = 11;
            pathLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            pathLabel.style.marginRight = 4;

            UpdatePath();

            // Make clickable
            pathLabel.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button == 0) // Left click
                {
                    string fullPath = System.IO.Path.GetDirectoryName(Application.dataPath);
                    EditorUtility.RevealInFinder(fullPath);
                }
            });

            // Hover effect
            pathLabel.RegisterCallback<MouseEnterEvent>(evt => {
                pathLabel.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            });

            pathLabel.RegisterCallback<MouseLeaveEvent>(evt => {
                pathLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            });

            Add(pathLabel);

            // Update periodically
            EditorApplication.update += CheckUpdate;
        }

        private void UpdatePath()
        {
            if (pathLabel == null) return;

            string projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            var pathParts = projectPath.Split(System.IO.Path.DirectorySeparatorChar);

            // Show last 1-2 directories depending on space
            if (pathParts.Length > 2)
            {
                projectPath = ".../" + pathParts[pathParts.Length - 2] + "/" + pathParts[pathParts.Length - 1];
            }
            else if (pathParts.Length > 1)
            {
                projectPath = ".../" + pathParts[pathParts.Length - 1];
            }

            pathLabel.text = projectPath;
        }

        private void CheckUpdate()
        {
            if (this == null) return;

            // Check if visibility preference changed
            bool shouldShow = ProjectPathMenuItem.IsProjectPathEnabled();
            bool isShowing = style.display == DisplayStyle.Flex;

            if (shouldShow != isShowing)
            {
                style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
                if (shouldShow) UpdatePath();
            }
        }
    }
}
#endif
