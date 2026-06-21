// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace realvirtual
{
    //! Base class for creating custom overlay sections with multiple controls.
    //! Inherit from this class to create complex UI sections with multiple buttons,
    //! labels, fields, and other visual elements organized in your own layout.
    //!
    //! Example usage:
    //! ```csharp
    //! [OverlayButton(typeof(QuickEditOverlay), order: 200.0, section: "Custom")]
    //! public class AdvancedToolsSection : OverlaySectionBase
    //! {
    //!     public override string SectionTitle => "Advanced Tools";
    //!     public override Texture SectionIcon => LoadIcon("d_Settings");
    //!
    //!     public override VisualElement CreateContent()
    //!     {
    //!         var container = new VisualElement();
    //!         container.style.flexDirection = FlexDirection.Column;
    //!
    //!         // Add a header
    //!         var header = new Label("Advanced Tools");
    //!         header.style.unityFontStyleAndWeight = FontStyle.Bold;
    //!         container.Add(header);
    //!
    //!         // Add a row of buttons
    //!         var row1 = CreateButtonRow();
    //!         row1.Add(CreateButton("Tool 1", "First tool", OnTool1Click));
    //!         row1.Add(CreateButton("Tool 2", "Second tool", OnTool2Click));
    //!         container.Add(row1);
    //!
    //!         // Add a full-width button
    //!         container.Add(CreateButton("Process All", "Process all items", OnProcessAll));
    //!
    //!         return container;
    //!     }
    //!
    //!     private void OnTool1Click() { /* ... */ }
    //!     private void OnTool2Click() { /* ... */ }
    //!     private void OnProcessAll() { /* ... */ }
    //! }
    //! ```
    public abstract class OverlaySectionBase : IOverlayButton
    {
        //! Title of the section (optional, can be null)
        public virtual string SectionTitle => null;

        //! Icon for the section header (optional, can be null)
        public virtual Texture SectionIcon => null;

        //! Whether to show a separator line above this section
        public virtual bool ShowSeparator => true;

        //! Additional CSS class names to apply to the section container
        public virtual string CssClass => null;

        //! Creates the content for this section
        //! Return a VisualElement containing all the controls for this section
        public abstract VisualElement CreateContent();

        //! Optional: Determines if the section should be shown based on current selection
        public virtual bool ShouldShow(GameObject selectedObject)
        {
            return true;
        }

        //! Optional: Determines if the section should be enabled
        public virtual bool ShouldEnable()
        {
            return true;
        }

        //! Creates the complete section visual element with optional title and separator
        public virtual VisualElement CreateButton()
        {
            var sectionContainer = new VisualElement();
            sectionContainer.style.flexDirection = FlexDirection.Column;

            // Add custom CSS class if provided
            if (!string.IsNullOrEmpty(CssClass))
                sectionContainer.AddToClassList(CssClass);

            // Add separator if requested
            if (ShowSeparator)
            {
                var separator = CreateSeparator();
                sectionContainer.Add(separator);
            }

            // Add section title if provided
            if (!string.IsNullOrEmpty(SectionTitle))
            {
                var titleContainer = new VisualElement();
                titleContainer.style.flexDirection = FlexDirection.Row;
                titleContainer.style.marginBottom = 4;
                titleContainer.style.marginTop = 4;

                // Add icon if provided
                if (SectionIcon != null)
                {
                    var iconElement = new VisualElement();
                    iconElement.style.backgroundImage = Background.FromTexture2D(SectionIcon as Texture2D);
                    iconElement.style.width = 16;
                    iconElement.style.height = 16;
                    iconElement.style.marginRight = 4;
                    titleContainer.Add(iconElement);
                }

                var title = new Label(SectionTitle);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleContainer.Add(title);

                sectionContainer.Add(titleContainer);
            }

            // Add the custom content
            var content = CreateContent();
            if (content != null)
            {
                sectionContainer.Add(content);
            }

            // Set enabled state
            sectionContainer.SetEnabled(ShouldEnable());

            return sectionContainer;
        }

        //! Helper method to create a separator line
        protected VisualElement CreateSeparator()
        {
            var separatorContainer = new VisualElement();
            separatorContainer.style.paddingTop = 4;
            separatorContainer.style.paddingBottom = 4;

            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            separatorContainer.Add(separator);
            return separatorContainer;
        }

        //! Helper method to create a button row container
        protected VisualElement CreateButtonRow()
        {
            var row = new VisualElement();
            row.AddToClassList("quickedit-button-row");
            return row;
        }

        //! Helper method to create a button
        protected Button CreateButton(string text, string tooltip, System.Action onClick, string cssClass = null)
        {
            var button = new Button(onClick);
            button.text = text;
            button.tooltip = tooltip;
            button.AddToClassList("quickedit-button");

            if (!string.IsNullOrEmpty(cssClass))
                button.AddToClassList(cssClass);

            return button;
        }

        //! Helper method to create an icon button
        protected Button CreateIconButton(Texture icon, string tooltip, System.Action onClick, string cssClass = null)
        {
            var button = new Button(onClick);
            button.tooltip = tooltip;
            button.AddToClassList("quickedit-button");

            if (!string.IsNullOrEmpty(cssClass))
                button.AddToClassList(cssClass);

            if (icon != null)
            {
                var iconElement = new VisualElement();
                iconElement.style.backgroundImage = Background.FromTexture2D(icon as Texture2D);
                iconElement.style.width = 16;
                iconElement.style.height = 16;
                button.Add(iconElement);
            }

            return button;
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

        //! Helper method to create a label
        protected Label CreateLabel(string text, FontStyle fontStyle = FontStyle.Normal)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = fontStyle;
            return label;
        }

        //! Helper method to add a component to selected object (similar to QuickEdit)
        protected void AddComponent(System.Type componentType)
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject != null)
            {
                Undo.AddComponent(selectedObject, componentType);
                Logger.Message($"Added {componentType.Name} to {selectedObject.name}", selectedObject);
            }
        }
    }
}
