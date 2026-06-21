// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;
using System;

namespace realvirtual
{
#if UNITY_2021_2_OR_NEWER
    //! Base class for creating custom toolbar buttons for the realvirtual Toolbar overlay.
    //! Inherit from this class and add the [RealvirtualToolbarButton] attribute to automatically
    //! register your button with the toolbar.
    //!
    //! Example:
    //! [RealvirtualToolbarButton(order: 100)]
    //! public class MyButton : RealvirtualToolbarButtonBase
    //! {
    //!     public MyButton()
    //!     {
    //!         text = "My Button";
    //!         tooltip = "Does something awesome";
    //!         SetIcon("d_Settings");
    //!     }
    //!
    //!     protected override void OnClicked()
    //!     {
    //!         Debug.Log("Button clicked!");
    //!     }
    //! }
    public abstract class RealvirtualToolbarButtonBase : EditorToolbarButton
    {
        //! Unique identifier for this button type (auto-generated from class name)
        public string ButtonId { get; private set; }

        //! Constructor - automatically sets up the button ID and click handler
        protected RealvirtualToolbarButtonBase()
        {
            // Generate unique ID from class name
            ButtonId = "Realvirtual_" + GetType().Name;

            // Wire up click handler
            clicked += OnClicked;
        }

        //! Override this method to handle button clicks
        protected abstract void OnClicked();

        //! Helper method to set icon from Unity's built-in icons
        //! param name="iconName">Name of the Unity icon (e.g., "d_Settings", "ViewToolOrbit")</param>
        protected void SetIcon(string iconName)
        {
            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                icon = iconContent.image as Texture2D;
            }
        }

        //! Helper method to load icon from asset path
        //! param name="assetPath">Path to the icon asset (e.g., "Assets/Icons/myicon.png")</param>
        protected void SetIconFromPath(string assetPath)
        {
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (iconTexture != null)
            {
                icon = iconTexture;
            }
        }

        //! Helper method to set a Material Icon as the button text with specified font size
        //! param name="iconName">Material icon name (e.g., "home", "settings", "more_time")</param>
        //! param name="fontSize">Font size in pixels (default: 18, recommended range: 16-24)</param>
        protected void SetMaterialIcon(string iconName, int fontSize = 18)
        {
            text = MaterialIcons.GetIcon(iconName);

            var font = MaterialIcons.GetEditorFont();
            if (font != null)
            {
                style.unityFontDefinition = new StyleFontDefinition(font);
                style.fontSize = fontSize;
            }
            // Silently use fallback text if font not available
        }

        //! Gets the currently selected GameObject in the scene
        protected GameObject GetSelectedObject()
        {
            return Selection.activeGameObject;
        }

        //! Checks if there is an active selection in the scene
        protected bool HasSelection()
        {
            return Selection.activeGameObject != null;
        }

        //! Gets the active SceneView
        protected SceneView GetSceneView()
        {
            return SceneView.lastActiveSceneView;
        }

        //! Helper method to repaint the scene view
        protected void RepaintSceneView()
        {
            var sceneView = GetSceneView();
            if (sceneView != null)
            {
                sceneView.Repaint();
            }
        }
    }

    //! Base class for creating custom toolbar toggle buttons
    //! Inherit from this class to create toggle-style buttons
    public abstract class RealvirtualToolbarToggleBase : EditorToolbarToggle
    {
        //! Unique identifier for this button type (auto-generated from class name)
        public string ButtonId { get; private set; }

        //! Constructor - automatically sets up the button ID and value change handler
        protected RealvirtualToolbarToggleBase()
        {
            // Generate unique ID from class name
            ButtonId = "Realvirtual_" + GetType().Name;

            // Wire up value change handler
            this.RegisterValueChangedCallback(evt => OnValueChanged(evt.newValue));
        }

        //! Override this method to handle toggle value changes
        //! param name="newValue">The new toggle state</param>
        protected abstract void OnValueChanged(bool newValue);

        //! Helper method to set icon from Unity's built-in icons
        //! param name="iconName">Name of the Unity icon</param>
        protected void SetIcon(string iconName)
        {
            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                icon = iconContent.image as Texture2D;
            }
        }

        //! Helper method to load icon from asset path
        //! param name="assetPath">Path to the icon asset</param>
        protected void SetIconFromPath(string assetPath)
        {
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (iconTexture != null)
            {
                icon = iconTexture;
            }
        }

        //! Helper method to set a Material Icon as the button text with specified font size
        //! param name="iconName">Material icon name (e.g., "home", "settings", "more_time")</param>
        //! param name="fontSize">Font size in pixels (default: 18, recommended range: 16-24)</param>
        protected void SetMaterialIcon(string iconName, int fontSize = 18)
        {
            text = MaterialIcons.GetIcon(iconName);

            var font = MaterialIcons.GetEditorFont();
            if (font != null)
            {
                style.unityFontDefinition = new StyleFontDefinition(font);
                style.fontSize = fontSize;
            }
            // Silently use fallback text if font not available
        }

        //! Gets the active SceneView
        protected SceneView GetSceneView()
        {
            return SceneView.lastActiveSceneView;
        }

        //! Helper method to repaint the scene view
        protected void RepaintSceneView()
        {
            var sceneView = GetSceneView();
            if (sceneView != null)
            {
                sceneView.Repaint();
            }
        }
    }

    //! Base class for creating custom toolbar dropdown buttons
    //! Inherit from this class to create dropdown-style buttons
    public abstract class RealvirtualToolbarDropdownBase : EditorToolbarDropdown
    {
        //! Unique identifier for this button type (auto-generated from class name)
        public string ButtonId { get; private set; }

        //! Constructor - automatically sets up the button ID and click handler
        protected RealvirtualToolbarDropdownBase()
        {
            // Generate unique ID from class name
            ButtonId = "Realvirtual_" + GetType().Name;

            // Wire up click handler
            clicked += OnClicked;
        }

        //! Override this method to handle dropdown clicks (typically shows a menu)
        protected abstract void OnClicked();

        //! Helper method to set icon from Unity's built-in icons
        //! param name="iconName">Name of the Unity icon</param>
        protected void SetIcon(string iconName)
        {
            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                icon = iconContent.image as Texture2D;
            }
        }

        //! Helper method to load icon from asset path
        //! param name="assetPath">Path to the icon asset</param>
        protected void SetIconFromPath(string assetPath)
        {
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (iconTexture != null)
            {
                icon = iconTexture;
            }
        }

        //! Helper method to set a Material Icon as the button text with specified font size
        //! param name="iconName">Material icon name (e.g., "home", "settings", "more_time")</param>
        //! param name="fontSize">Font size in pixels (default: 18, recommended range: 16-24)</param>
        protected void SetMaterialIcon(string iconName, int fontSize = 18)
        {
            text = MaterialIcons.GetIcon(iconName);

            var font = MaterialIcons.GetEditorFont();
            if (font != null)
            {
                style.unityFontDefinition = new StyleFontDefinition(font);
                style.fontSize = fontSize;
            }
            // Silently use fallback text if font not available
        }

        //! Gets the active SceneView
        protected SceneView GetSceneView()
        {
            return SceneView.lastActiveSceneView;
        }

        //! Helper method to repaint the scene view
        protected void RepaintSceneView()
        {
            var sceneView = GetSceneView();
            if (sceneView != null)
            {
                sceneView.Repaint();
            }
        }
    }
#endif
}
