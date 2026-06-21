// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEditor;
using UnityEditor.Toolbars;

namespace realvirtual
{
#if UNITY_2021_2_OR_NEWER
    // ========================================
    // EXAMPLE CUSTOM TOOLBAR BUTTONS
    // ========================================
    // These examples demonstrate how to create custom toolbar buttons
    // for the realvirtual Toolbar overlay. Simply inherit from one of the
    // base classes and add the [RealvirtualToolbarButton] attribute.
    //
    // To create your own custom buttons:
    // 1. Create a new class that inherits from RealvirtualToolbarButtonBase,
    //    RealvirtualToolbarToggleBase, or RealvirtualToolbarDropdownBase
    // 2. Add [RealvirtualToolbarButton(order: X)] attribute (order determines position)
    // 3. Add [EditorToolbarElement(id, typeof(SceneView))] attribute
    // 4. Define a unique ID as a public const string
    // 5. Implement the required constructor and OnClicked/OnValueChanged method
    //
    // The button will automatically appear in the toolbar after Unity recompiles.
    // ========================================

    #region Example Button - Simple Action
    //! Example: Simple button that logs a message when clicked
    //! This demonstrates the most basic custom toolbar button
    //! NOTE: Examples are commented out to prevent them from appearing in the toolbar.
    //! Uncomment to test or use as templates for your own buttons.
    /*
    [RealvirtualToolbarButton(order: 100)]
    [EditorToolbarElement(id, typeof(SceneView))]
    public class ExampleSimpleButton : RealvirtualToolbarButtonBase
    {
        public const string id = "RealvirtualExampleSimple";

        public ExampleSimpleButton()
        {
            text = "Example";
            tooltip = "Example button - click to log message";
            SetIcon("d_console.infoicon"); // Use Unity's built-in icon
        }

        protected override void OnClicked()
        {
            Logger.Message("Example button clicked!", null);
        }
    }
    */
    #endregion

    #region Example Toggle - Boolean State
    //! Example: Toggle button that enables/disables a feature
    //! This demonstrates a toggle-style button with persistent state
    /*
    [RealvirtualToolbarButton(order: 110)]
    [EditorToolbarElement(id, typeof(SceneView))]
    public class ExampleToggleButton : RealvirtualToolbarToggleBase
    {
        public const string id = "RealvirtualExampleToggle";
        private const string PREF_KEY = "RealvirtualExampleToggle_State";

        public ExampleToggleButton()
        {
            text = "Toggle Example";
            tooltip = "Example toggle - enables/disables a feature";
            SetIcon("d_ToggleUVOverlay");

            // Restore state from EditorPrefs
            value = EditorPrefs.GetBool(PREF_KEY, false);
        }

        protected override void OnValueChanged(bool newValue)
        {
            // Save state to EditorPrefs
            EditorPrefs.SetBool(PREF_KEY, newValue);
            Logger.Message($"Example toggle is now: {(newValue ? "ON" : "OFF")}", null);

            // You could enable/disable a feature here
            // For example: Global.SomeFeature = newValue;
        }
    }
    */
    #endregion

    #region Example Dropdown - Menu Selection
    //! Example: Dropdown button that shows a menu of options
    //! This demonstrates a dropdown-style button with menu items
    /*
    [RealvirtualToolbarButton(order: 120)]
    [EditorToolbarElement(id, typeof(SceneView))]
    public class ExampleDropdownButton : RealvirtualToolbarDropdownBase
    {
        public const string id = "RealvirtualExampleDropdown";

        public ExampleDropdownButton()
        {
            text = "Dropdown Example";
            tooltip = "Example dropdown - select an option";
            SetIcon("d_ViewToolOrbit");
        }

        protected override void OnClicked()
        {
            // Create and show a menu
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Option 1"), false, () =>
            {
                Logger.Message("Option 1 selected", null);
            });

            menu.AddItem(new GUIContent("Option 2"), false, () =>
            {
                Logger.Message("Option 2 selected", null);
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Advanced/Sub Option A"), false, () =>
            {
                Logger.Message("Sub Option A selected", null);
            });

            menu.AddItem(new GUIContent("Advanced/Sub Option B"), false, () =>
            {
                Logger.Message("Sub Option B selected", null);
            });

            menu.ShowAsContext();
        }
    }
    */
    #endregion

    #region Example Selection-Aware Button
    //! Example: Button that only works when an object is selected
    //! This demonstrates how to create selection-aware buttons
    /*
    [RealvirtualToolbarButton(order: 130)]
    [EditorToolbarElement(id, typeof(SceneView))]
    public class ExampleSelectionButton : RealvirtualToolbarButtonBase
    {
        public const string id = "RealvirtualExampleSelection";

        public ExampleSelectionButton()
        {
            text = "Selection Info";
            tooltip = "Shows info about selected object (requires selection)";
            SetIcon("d_UnityEditor.InspectorWindow");
        }

        protected override void OnClicked()
        {
            var selected = GetSelectedObject();
            if (selected != null)
            {
                Logger.Message($"Selected: {selected.name} (Type: {selected.GetType().Name})", null);

                // Example: Show all components
                var components = selected.GetComponents<Component>();
                Logger.Message($"  Components: {components.Length}", null);
                foreach (var comp in components)
                {
                    Logger.Message($"    - {comp.GetType().Name}", null);
                }
            }
            else
            {
                Logger.Warning("No object selected!", null);
            }
        }
    }
    */
    #endregion

    #region Example Custom Icon Button
    //! Example: Button with custom icon loaded from project assets
    //! This demonstrates loading a custom icon from the project
    /*
    [RealvirtualToolbarButton(order: 140)]
    [EditorToolbarElement(id, typeof(SceneView))]
    public class ExampleCustomIconButton : RealvirtualToolbarButtonBase
    {
        public const string id = "RealvirtualExampleCustomIcon";

        public ExampleCustomIconButton()
        {
            text = "Custom Icon";
            tooltip = "Example button with custom icon";

            // Try to load a custom icon from the project
            // If not found, falls back to a Unity built-in icon
            SetIconFromPath(RealvirtualAssetPaths.StarterResources("Icons/realvirtual.png"));

            // If the custom icon path doesn't exist, use a fallback
            if (icon == null)
            {
                SetIcon("d_Favorite");
            }
        }

        protected override void OnClicked()
        {
            Logger.Message("Custom icon button clicked!", null);
        }
    }
    */
    #endregion

    // ========================================
    // TO CREATE YOUR OWN BUTTON:
    // ========================================
    // 1. Copy one of the examples above
    // 2. Rename the class
    // 3. Change the 'order' value (lower = appears first)
    // 4. Change the 'id' to be unique
    // 5. Update text, tooltip, and icon
    // 6. Implement your custom logic in OnClicked() or OnValueChanged()
    // 7. Save and let Unity recompile
    // 8. Your button will automatically appear in the toolbar!
    //
    // TIP: Use "realvirtual DEV/Toolbar/Show Registered Buttons" menu
    //      to see all registered buttons and their order
    // ========================================
#endif
}
