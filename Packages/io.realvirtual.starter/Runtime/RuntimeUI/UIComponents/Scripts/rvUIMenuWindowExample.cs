using UnityEngine.Scripting.APIUpdating;
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

/// <summary>
/// Example demonstrating how to use rvUIMenuWindow to create adaptive UI containers
/// that can switch between horizontal, vertical, and window styles while preserving content.
/// </summary>

namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIMenuWindowExample")]
    public class rvUIMenuWindowExample : MonoBehaviour
    {
        private rvUIMenuWindow menuWindow;
        private RuntimeUIBuilder builder;
    
        void Start()
        {
            builder = RuntimeUIBuilder.Instance;
    
            if (builder == null)
            {
                Debug.LogError("RuntimeUIBuilder.Instance is null!");
                return;
            }
    
            // Example 1: Create menu window with default vertical style
            CreateBasicExample();
    
            // Example 2: Create menu window with initial horizontal style
            // CreateHorizontalExample();
    
            // Example 3: Create menu window with style selector
            // CreateStyleSelectorExample();
        }
    
        /// <summary>
        /// Example 1: Basic menu window with content and manual style switching.
        /// </summary>
        void CreateBasicExample()
        {
            // Create menu window (defaults to vertical style)
            menuWindow = builder.AddMenuWindow(new rvUIMenuSettings()
            {
                MenuStyle = rvUIMenuWindow.Style.Vertical
            });
    
            // Step into the window to add content
            builder.StepIn();
    
            // Add some content
            builder.AddText("My Menu Window");
    
            var btn1 = builder.AddButton("Option 1");
            btn1.OnClick.AddListener(() => Debug.Log("Option 1 clicked"));
    
            var btn2 = builder.AddButton("Option 2");
            btn2.OnClick.AddListener(() => Debug.Log("Option 2 clicked"));
    
            var btn3 = builder.AddButton("Option 3");
            btn3.OnClick.AddListener(() => Debug.Log("Option 3 clicked"));
    
            // Step out of the window
            builder.StepOut();
    
            // Listen for style changes
            menuWindow.OnStyleChanged.AddListener(OnStyleChanged);
    
            // You can switch styles programmatically:
            // menuWindow.SwitchToHorizontal();
            // menuWindow.SwitchToWindow();
            // menuWindow.SwitchToVertical();
        }
    
        /// <summary>
        /// Example 2: Create menu window with initial horizontal layout.
        /// </summary>
        void CreateHorizontalExample()
        {
            // Create menu window with horizontal style
            menuWindow = builder.AddMenuWindow(new rvUIMenuSettings()
            {
                MenuStyle = rvUIMenuWindow.Style.Horizontal
            });
    
            builder.StepIn();
    
            builder.AddButton("File").OnClick.AddListener(() => Debug.Log("File"));
            builder.AddButton("Edit").OnClick.AddListener(() => Debug.Log("Edit"));
            builder.AddButton("View").OnClick.AddListener(() => Debug.Log("View"));
            builder.AddButton("Help").OnClick.AddListener(() => Debug.Log("Help"));
    
            builder.StepOut();
        }
    
        /// <summary>
        /// Example 3: Create menu window with style selector buttons.
        /// </summary>
        void CreateStyleSelectorExample()
        {
            // Create main container
            var mainContainer = builder.AddContainer(RuntimeUIBuilder.ContentType.VerticalMenu);
            builder.StepIn();
    
            // Add style selector buttons
            builder.AddText("Style Selector");
    
            var horizontalBtn = builder.AddButton("Horizontal");
            horizontalBtn.OnClick.AddListener(() => SwitchStyle(rvUIMenuWindow.Style.Horizontal));
    
            var verticalBtn = builder.AddButton("Vertical");
            verticalBtn.OnClick.AddListener(() => SwitchStyle(rvUIMenuWindow.Style.Vertical));
    
            var windowBtn = builder.AddButton("Window");
            windowBtn.OnClick.AddListener(() => SwitchStyle(rvUIMenuWindow.Style.Window));
    
            // Create the adaptive menu window
            menuWindow = builder.AddMenuWindow(new rvUIMenuSettings()
            {
                MenuStyle = rvUIMenuWindow.Style.Window
            });
            builder.StepIn();
    
            // Add content
            builder.AddText("Adaptive Content");
            builder.AddButton("Item A").OnClick.AddListener(() => Debug.Log("Item A"));
            builder.AddButton("Item B").OnClick.AddListener(() => Debug.Log("Item B"));
            builder.AddButton("Item C").OnClick.AddListener(() => Debug.Log("Item C"));
    
            builder.StepOut(); // Exit menu window
            builder.StepOut(); // Exit main container
        }
    
        /// <summary>
        /// Switches the menu window style.
        /// </summary>
        void SwitchStyle(rvUIMenuWindow.Style newStyle)
        {
            if (menuWindow != null)
            {
                menuWindow.SwitchStyle(newStyle);
            }
        }
    
        /// <summary>
        /// Called when the menu window style changes.
        /// </summary>
        void OnStyleChanged(rvUIMenuWindow.Style newStyle)
        {
            Debug.Log($"Menu window style changed to: {newStyle}");
        }
    }
}
