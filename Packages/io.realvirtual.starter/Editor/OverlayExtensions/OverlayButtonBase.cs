// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace realvirtual
{
    //! Base class for creating custom overlay buttons.
    //! Inherit from this class to create simple action buttons that execute code when clicked.
    //!
    //! Example usage:
    //! ```csharp
    //! [OverlayButton(typeof(QuickEditOverlay), order: 100.0, section: "Custom")]
    //! public class MyCustomButton : OverlayButtonBase
    //! {
    //!     public override string ButtonText => "My Tool";
    //!     public override string Tooltip => "Open my custom tool";
    //!     public override Texture Icon => LoadIcon("my-icon.png");
    //!
    //!     public override void OnClicked()
    //!     {
    //!         // Your button action here
    //!         Debug.Log("Button clicked!");
    //!     }
    //!
    //!     public override bool ShouldShow(GameObject selectedObject)
    //!     {
    //!         // Optional: conditional visibility
    //!         return selectedObject != null;
    //!     }
    //! }
    //! ```
    public abstract class OverlayButtonBase : IOverlayButton
    {
        //! Text displayed on the button (can be null if using icon only)
        public virtual string ButtonText => null;

        //! Tooltip text shown on hover
        public virtual string Tooltip => string.Empty;

        //! Icon texture for the button (can be null if using text only)
        public virtual Texture Icon => null;

        //! Additional CSS class names to apply to the button (for custom styling)
        public virtual string CssClass => null;

        //! Whether this button uses icon only (no text)
        public virtual bool IconOnly => false;

        //! Called when the button is clicked
        public abstract void OnClicked();

        //! Optional: Determines if the button should be shown based on current selection
        //! selectedObject: Currently selected GameObject (can be null)
        //! Returns true if button should be visible
        public virtual bool ShouldShow(GameObject selectedObject)
        {
            return true;
        }

        //! Optional: Determines if the button should be enabled
        //! Returns true if button should be interactable
        public virtual bool ShouldEnable()
        {
            return true;
        }

        //! Creates the visual element for this button
        //! This is called by the overlay when constructing the UI
        public virtual VisualElement CreateButton()
        {
            var button = new Button(OnClicked);
            button.tooltip = Tooltip;
            button.AddToClassList("quickedit-button"); // Use existing QuickEdit styling

            // Add custom CSS class if provided
            if (!string.IsNullOrEmpty(CssClass))
                button.AddToClassList(CssClass);

            // Set icon if provided
            if (Icon != null)
            {
                var iconElement = new VisualElement();
                iconElement.style.backgroundImage = Background.FromTexture2D(Icon as Texture2D);
                iconElement.style.width = 16;
                iconElement.style.height = 16;
                iconElement.style.marginRight = string.IsNullOrEmpty(ButtonText) ? 0 : 4;
                button.Add(iconElement);
            }

            // Set text if provided
            if (!string.IsNullOrEmpty(ButtonText) && !IconOnly)
            {
                button.text = ButtonText;
            }

            // Set enabled state
            button.SetEnabled(ShouldEnable());

            return button;
        }

        //! Helper method to load an icon from the project
        //! path: Path to the icon file (e.g., "Assets/MyIcons/icon.png" or "Icons/icon" for Resources folder)
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
    }

    //! Interface that all overlay buttons must implement
    //! This allows the registry to work with different button types uniformly
    public interface IOverlayButton
    {
        //! Creates the visual element for this button
        VisualElement CreateButton();

        //! Determines if the button should be shown
        bool ShouldShow(GameObject selectedObject);

        //! Determines if the button should be enabled
        bool ShouldEnable();
    }
}
