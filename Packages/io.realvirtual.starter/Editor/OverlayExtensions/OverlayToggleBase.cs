// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace realvirtual
{
    //! Base class for creating custom overlay toggle buttons with persistent state.
    //! Inherit from this class to create toggle buttons that maintain their state across sessions.
    //!
    //! Example usage:
    //! ```csharp
    //! [OverlayButton(typeof(QuickEditOverlay), order: 110.0, section: "Custom")]
    //! public class MyToggleButton : OverlayToggleBase
    //! {
    //!     private const string PREF_KEY = "MyToggle_State";
    //!
    //!     public override string ButtonText => "My Toggle";
    //!     public override string Tooltip => "Toggle my feature";
    //!     public override Texture Icon => LoadIcon("d_Toggle Icon");
    //!
    //!     protected override string PreferenceKey => PREF_KEY;
    //!
    //!     public override void OnValueChanged(bool newValue)
    //!     {
    //!         // Handle toggle state change
    //!         if (newValue)
    //!             Debug.Log("Feature enabled");
    //!         else
    //!             Debug.Log("Feature disabled");
    //!     }
    //! }
    //! ```
    public abstract class OverlayToggleBase : IOverlayButton
    {
        private Toggle toggleControl;

        //! Text displayed on the button (can be null if using icon only)
        public virtual string ButtonText => null;

        //! Tooltip text shown on hover
        public virtual string Tooltip => string.Empty;

        //! Icon texture for the button (can be null if using text only)
        public virtual Texture Icon => null;

        //! Additional CSS class names to apply to the toggle (for custom styling)
        public virtual string CssClass => null;

        //! Whether this button uses icon only (no text)
        public virtual bool IconOnly => false;

        //! EditorPrefs key for storing toggle state (override this for persistent state)
        //! If null, state is not persisted across sessions
        protected virtual string PreferenceKey => null;

        //! Default value when no preference is stored
        protected virtual bool DefaultValue => false;

        //! Called when the toggle value changes
        //! newValue: The new toggle state (true/false)
        public abstract void OnValueChanged(bool newValue);

        //! Optional: Determines if the toggle should be shown based on current selection
        public virtual bool ShouldShow(GameObject selectedObject)
        {
            return true;
        }

        //! Optional: Determines if the toggle should be enabled
        public virtual bool ShouldEnable()
        {
            return true;
        }

        //! Gets the current toggle value from EditorPrefs (or default if no preference set)
        protected bool GetValue()
        {
            if (string.IsNullOrEmpty(PreferenceKey))
                return DefaultValue;

            return EditorPrefs.GetBool(PreferenceKey, DefaultValue);
        }

        //! Sets the toggle value and saves to EditorPrefs
        protected void SetValue(bool value)
        {
            if (!string.IsNullOrEmpty(PreferenceKey))
            {
                EditorPrefs.SetBool(PreferenceKey, value);
            }

            if (toggleControl != null)
            {
                toggleControl.value = value;
            }
        }

        //! Creates the visual element for this toggle
        public virtual VisualElement CreateButton()
        {
            toggleControl = new Toggle();
            toggleControl.tooltip = Tooltip;
            toggleControl.value = GetValue();
            toggleControl.AddToClassList("quickedit-button"); // Use existing QuickEdit styling

            // Add custom CSS class if provided
            if (!string.IsNullOrEmpty(CssClass))
                toggleControl.AddToClassList(CssClass);

            // Set icon if provided
            if (Icon != null)
            {
                var iconElement = new VisualElement();
                iconElement.style.backgroundImage = Background.FromTexture2D(Icon as Texture2D);
                iconElement.style.width = 16;
                iconElement.style.height = 16;
                iconElement.style.marginRight = string.IsNullOrEmpty(ButtonText) ? 0 : 4;
                toggleControl.Add(iconElement);
            }

            // Set text if provided
            if (!string.IsNullOrEmpty(ButtonText) && !IconOnly)
            {
                toggleControl.text = ButtonText;
            }

            // Set enabled state
            toggleControl.SetEnabled(ShouldEnable());

            // Register value changed callback
            toggleControl.RegisterValueChangedCallback(evt =>
            {
                SetValue(evt.newValue);
                OnValueChanged(evt.newValue);
            });

            return toggleControl;
        }

        //! Helper method to load an icon from the project
        protected Texture LoadIcon(string path)
        {
            // Try loading from AssetDatabase first
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
                return texture;

            // Try loading from Resources
            texture = UnityEngine.Resources.Load<Texture2D>(path);
            if (texture != null)
                return texture;

            // Try Unity built-in icon
            var iconContent = EditorGUIUtility.IconContent(path);
            if (iconContent != null && iconContent.image != null)
                return iconContent.image;

            Logger.Warning($"Could not load icon from path: {path}", null);
            return null;
        }

        //! Helper method to create an icon element
        protected VisualElement CreateIconElement(Texture icon, int size = 16)
        {
            var iconElement = new VisualElement();
            iconElement.style.backgroundImage = Background.FromTexture2D(icon as Texture2D);
            iconElement.style.width = size;
            iconElement.style.height = size;
            return iconElement;
        }

        //! Programmatically refresh the toggle state from preferences
        public void RefreshValue()
        {
            if (toggleControl != null)
            {
                toggleControl.value = GetValue();
            }
        }
    }
}
