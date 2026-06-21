// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace realvirtual
{
    //! Example custom overlay buttons demonstrating the OverlayButton extension system.
    //!
    //! These examples show various ways to create custom buttons for overlays:
    //! - Simple action buttons
    //! - Toggle buttons with persistent state
    //! - Custom sections with multiple controls
    //! - Component-specific buttons
    //! - Play mode / Edit mode conditional buttons
    //! - Buttons that appear in multiple overlays
    //!
    //! To activate these examples, uncomment the desired class.
    //! The buttons will automatically appear in QuickEdit overlay after Unity recompiles.
    //!
    //! IMPORTANT: These are examples only. Create your own classes in your own namespace
    //! for production use. DO NOT modify this file directly.

    #region Simple Button Examples

    //! Example 1: Simple action button with text
    //! Appears in the "Custom" section at order 100.0
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 100.0, section: "Custom")]
    public class ExampleSimpleButton : OverlayButtonBase
    {
        public override string ButtonText => "My Tool";
        public override string Tooltip => "Click to open my custom tool";

        public override void OnClicked()
        {
            Debug.Log("My Tool button clicked!");
            // Add your custom action here
        }
    }
    */

    //! Example 2: Button with icon (using Unity built-in icon)
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 100.1, section: "Custom")]
    public class ExampleIconButton : OverlayButtonBase
    {
        public override string ButtonText => "Settings";
        public override string Tooltip => "Open settings";
        public override Texture Icon => LoadIcon("d_Settings");

        public override void OnClicked()
        {
            Debug.Log("Settings button clicked!");
        }
    }
    */

    //! Example 3: Icon-only button (no text)
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 100.2, section: "Custom")]
    public class ExampleIconOnlyButton : OverlayButtonBase
    {
        public override string Tooltip => "Icon-only button";
        public override Texture Icon => LoadIcon("d_console.infoicon");
        public override bool IconOnly => true;

        public override void OnClicked()
        {
            EditorUtility.DisplayDialog("Info", "This is an icon-only button!", "OK");
        }
    }
    */

    #endregion

    #region Row Arrangement Examples

    //! Example 4: Multiple buttons in same row
    //! These three buttons will appear in the same row because they have the same integer part (101)
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 101.0, section: "Custom")]
    public class ExampleRowButton1 : OverlayButtonBase
    {
        public override string ButtonText => "Tool 1";
        public override string Tooltip => "First tool in row";
        public override void OnClicked() => Debug.Log("Tool 1");
    }

    [OverlayButton(typeof(QuickEditOverlay), order: 101.1, section: "Custom")]
    public class ExampleRowButton2 : OverlayButtonBase
    {
        public override string ButtonText => "Tool 2";
        public override string Tooltip => "Second tool in row";
        public override void OnClicked() => Debug.Log("Tool 2");
    }

    [OverlayButton(typeof(QuickEditOverlay), order: 101.2, section: "Custom")]
    public class ExampleRowButton3 : OverlayButtonBase
    {
        public override string ButtonText => "Tool 3";
        public override string Tooltip => "Third tool in row";
        public override void OnClicked() => Debug.Log("Tool 3");
    }
    */

    //! Example 5: Full-width button
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 102.0, section: "Custom", fullWidth: true)]
    public class ExampleFullWidthButton : OverlayButtonBase
    {
        public override string ButtonText => "Process All Items";
        public override string Tooltip => "Process all items in scene";

        public override void OnClicked()
        {
            Debug.Log("Processing all items...");
        }
    }
    */

    #endregion

    #region Toggle Button Examples

    //! Example 6: Toggle button with persistent state
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 110.0, section: "Custom")]
    public class ExampleToggleButton : OverlayToggleBase
    {
        private const string PREF_KEY = "ExampleToggle_ShowDebugInfo";

        public override string ButtonText => "Debug Info";
        public override string Tooltip => "Toggle debug information display";
        public override Texture Icon => LoadIcon("d_UnityEditor.ConsoleWindow");

        protected override string PreferenceKey => PREF_KEY;
        protected override bool DefaultValue => false;

        public override void OnValueChanged(bool newValue)
        {
            if (newValue)
            {
                Debug.Log("Debug info enabled");
                // Enable your debug visualization here
            }
            else
            {
                Debug.Log("Debug info disabled");
                // Disable your debug visualization here
            }
        }
    }
    */

    #endregion

    #region Conditional Visibility Examples

    //! Example 7: Component-specific button (only shows when Drive component is selected)
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 120.0, section: "Components", targetComponentType: typeof(Drive))]
    public class ExampleDriveButton : OverlayButtonBase
    {
        public override string ButtonText => "Drive Tools";
        public override string Tooltip => "Advanced drive configuration";

        public override void OnClicked()
        {
            var drive = Selection.activeGameObject?.GetComponent<Drive>();
            if (drive != null)
            {
                Debug.Log($"Configuring drive: {drive.name}");
                // Add your drive-specific logic here
            }
        }
    }
    */

    //! Example 8: Play mode only button
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 130.0, section: "Custom", playModeOnly: true)]
    public class ExamplePlayModeButton : OverlayButtonBase
    {
        public override string ButtonText => "Runtime Tool";
        public override string Tooltip => "This button only appears in play mode";

        public override void OnClicked()
        {
            Debug.Log("Runtime tool activated!");
        }
    }
    */

    //! Example 9: Edit mode only button
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 131.0, section: "Custom", editModeOnly: true)]
    public class ExampleEditModeButton : OverlayButtonBase
    {
        public override string ButtonText => "Editor Tool";
        public override string Tooltip => "This button only appears in edit mode";

        public override void OnClicked()
        {
            Debug.Log("Editor tool activated!");
        }
    }
    */

    //! Example 10: Custom conditional visibility
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 132.0, section: "Custom")]
    public class ExampleConditionalButton : OverlayButtonBase
    {
        public override string ButtonText => "Multi-Select";
        public override string Tooltip => "Only visible with multiple objects selected";

        public override bool ShouldShow(GameObject selectedObject)
        {
            // Only show when multiple objects are selected
            return Selection.gameObjects.Length > 1;
        }

        public override void OnClicked()
        {
            Debug.Log($"Processing {Selection.gameObjects.Length} selected objects");
        }
    }
    */

    #endregion

    #region Custom Section Examples

    //! Example 11: Custom section with multiple controls
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 200.0, section: "Custom")]
    public class ExampleCustomSection : OverlaySectionBase
    {
        public override string SectionTitle => "My Tools";
        public override Texture SectionIcon => LoadIcon("d_Folder Icon");
        public override bool ShowSeparator => true;

        public override VisualElement CreateContent()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;

            // Add description
            var description = CreateLabel("These are my custom tools for automation");
            description.style.marginBottom = 4;
            container.Add(description);

            // Add first row of buttons
            var row1 = CreateButtonRow();
            row1.Add(CreateButton("Generate", "Generate items", OnGenerate));
            row1.Add(CreateButton("Validate", "Validate setup", OnValidate));
            container.Add(row1);

            // Add second row
            var row2 = CreateButtonRow();
            row2.Add(CreateButton("Export", "Export data", OnExport));
            row2.Add(CreateButton("Import", "Import data", OnImport));
            container.Add(row2);

            // Add full-width button
            var processBtn = CreateButton("Process All", "Process entire scene", OnProcessAll);
            processBtn.style.flexGrow = 1;
            var processRow = CreateButtonRow();
            processRow.Add(processBtn);
            container.Add(processRow);

            return container;
        }

        private void OnGenerate() => Debug.Log("Generate clicked");
        private void OnValidate() => Debug.Log("Validate clicked");
        private void OnExport() => Debug.Log("Export clicked");
        private void OnImport() => Debug.Log("Import clicked");
        private void OnProcessAll() => Debug.Log("Process All clicked");
    }
    */

    #endregion

    #region Multi-Overlay Example

    //! Example 12: Button that appears in multiple overlays
    //! (This would also appear in DesOverlay if it existed and was integrated)
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 300.0, section: "Custom")]
    // [OverlayButton(typeof(DesOverlay), order: 50.0, section: "Tools")]  // Add when DesOverlay is ready
    public class ExampleUniversalButton : OverlayButtonBase
    {
        public override string ButtonText => "Universal Tool";
        public override string Tooltip => "This button appears in multiple overlays";

        public override void OnClicked()
        {
            Debug.Log("Universal tool activated from overlay!");
        }
    }
    */

    #endregion

    #region Integration with Existing Components

    //! Example 13: Button that integrates with QuickEdit patterns
    /*
    [OverlayButton(typeof(QuickEditOverlay), order: 400.0, section: "Components")]
    public class ExampleComponentAdder : OverlayButtonBase
    {
        public override string ButtonText => "My Component";
        public override string Tooltip => "Add MyComponent to selected object";
        public override Texture Icon => LoadIcon("d_Prefab Icon");

        public override bool ShouldShow(GameObject selectedObject)
        {
            // Only show if an object is selected
            return selectedObject != null;
        }

        public override void OnClicked()
        {
            // Use the AddComponent helper from OverlaySectionBase
            var selectedObject = Selection.activeGameObject;
            if (selectedObject != null)
            {
                // Replace typeof(Transform) with your actual component type
                Undo.AddComponent(selectedObject, typeof(Transform));
                Debug.Log($"Added component to {selectedObject.name}");
            }
        }
    }
    */

    #endregion
}
