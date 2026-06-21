using UnityEngine;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;
using System.Reflection;
using System;
using System.Linq;

namespace realvirtual
{
#if UNITY_2021_2_OR_NEWER
    //! Main toolbar overlay for realvirtual tools in Scene view.
    //! Uses dynamic button registration system - buttons are automatically discovered
    //! from classes marked with [RealvirtualToolbarButton] attribute.
    [Overlay(typeof(SceneView), "realvirtual Toolbar", true)]
    [Icon("Icons/realvirtual")]
    public class RealvirtualToolbarOverlay : ToolbarOverlay
    {
        //! Constructor with dynamic button registration
        //! Automatically includes all buttons registered via ToolbarButtonRegistry
        RealvirtualToolbarOverlay() : base(GetButtonIds())
        { }

        //! Gets all button IDs from the registry
        private static string[] GetButtonIds()
        {
            // Get dynamically registered buttons
            var dynamicButtons = ToolbarButtonRegistry.GetButtonIds();

            // Combine with core buttons (for backward compatibility and ordering)
            var coreButtons = new[]
            {
                QuickEditButton.id,
                DrawModeDropdown.id,
                GizmoToggle.id
            };

            // Merge: core buttons first, then dynamic buttons that aren't already in core
            var allButtons = coreButtons
                .Concat(dynamicButtons.Where(id => !coreButtons.Contains(id)))
                .ToArray();

            return allButtons;
        }

        public override void OnCreated()
        {
            base.OnCreated();
            // Position at top of scene view
            floatingPosition = new Vector2(10, 10);
            collapsed = false;
            displayed = true;
        }
    }

    //! Core toolbar button: Toggle gizmo visibility in Scene view
    [RealvirtualToolbarButton(order: 40)]
    [EditorToolbarElement(id, typeof(SceneView))]
    class GizmoToggle : EditorToolbarToggle
    {
        public const string id = "RealvirtualGizmoToggle";
        
        public GizmoToggle()
        {
            tooltip = "Toggle gizmo visibility";
            // Use the Transform Icon
            var gizmoIcon = EditorGUIUtility.IconContent("Transform Icon");
            if (gizmoIcon != null && gizmoIcon.image != null)
            {
                icon = gizmoIcon.image as Texture2D;
            }

            UpdateState();
            
            // Handle toggle
            this.RegisterValueChangedCallback(evt =>
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    sceneView.drawGizmos = evt.newValue;
                    sceneView.Repaint();
                }
            });
        }
        
        void UpdateState()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                value = sceneView.drawGizmos;
            }
        }
    }

    //! Core toolbar button: Select draw mode (Shaded/Wireframe)
    [RealvirtualToolbarButton(order: 30)]
    [EditorToolbarElement(id, typeof(SceneView))]
    class DrawModeDropdown : EditorToolbarDropdown
    {
        public const string id = "RealvirtualDrawMode";
        
        public DrawModeDropdown()
        {
            tooltip = "Select draw mode";
            // Use shaded icon from Unity
            var iconContent = EditorGUIUtility.IconContent("SceneViewCamera");
            if (iconContent != null && iconContent.image != null)
            {
                icon = iconContent.image as Texture2D;
            }

            clicked += ShowDrawModeMenu;
        }
        
        void ShowDrawModeMenu()
        {
            var menu = new GenericMenu();
            var sceneView = SceneView.lastActiveSceneView;
            
            if (sceneView == null) return;
            
            // Shaded
            menu.AddItem(new GUIContent("Shaded"), 
                sceneView.cameraMode.drawMode == DrawCameraMode.Textured, 
                () => SetDrawMode(DrawCameraMode.Textured));
            
            // Shaded Wireframe
            menu.AddItem(new GUIContent("Shaded Wireframe"), 
                sceneView.cameraMode.drawMode == DrawCameraMode.TexturedWire, 
                () => SetDrawMode(DrawCameraMode.TexturedWire));
            
            menu.ShowAsContext();
        }
        
        void SetDrawMode(DrawCameraMode mode)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.cameraMode = SceneView.GetBuiltinCameraMode(mode);
                sceneView.Repaint();
            }
        }
    }

    //! Core toolbar button: Toggle QuickEdit overlay
    [RealvirtualToolbarButton(order: 10)]
    [EditorToolbarElement(id, typeof(SceneView))]
    class QuickEditButton : EditorToolbarToggle
    {
        public const string id = "RealvirtualQuickEdit";

        public QuickEditButton()
        {
            text = "Quick Edit";
            tooltip = "Toggle QuickEdit overlay (F1)";

            // Try to load realvirtual icon from UPM package path
            var iconPath = RealvirtualAssetPaths.StarterResources("Icons/Icon64.png");
            var realvirtualIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (realvirtualIcon != null)
            {
                icon = realvirtualIcon;
            }
            else
            {
                // Fallback icon if realvirtual icon not found
                icon = EditorGUIUtility.IconContent("d_Settings").image as Texture2D;
            }

            // Register update callback to refresh visual state
            EditorApplication.update += UpdateVisualState;
            UpdateVisualState();

            // Wire up value change handler
            this.RegisterValueChangedCallback(evt => OnValueChanged(evt.newValue));
        }

        void OnValueChanged(bool newValue)
        {
            // Get the QuickEdit overlay instance and toggle it
            var overlay = QuickEditOverlay.Instance;
            if (overlay != null)
            {
                overlay.displayed = newValue;
                if (newValue)
                {
                    overlay.collapsed = false;
                }
            }

            // Update visual state immediately after change
            UpdateVisualState();
        }

        void UpdateVisualState()
        {
            var overlay = QuickEditOverlay.Instance;
            bool isActive = overlay != null && overlay.displayed;

            // Update toggle state to reflect overlay state
            // Unity's EditorToolbarToggle automatically shows blue active state
            if (value != isActive)
            {
                value = isActive;
            }
        }
    }

#endif
}
