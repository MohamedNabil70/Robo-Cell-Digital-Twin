# Categorized Toolbar Overlay System

The realvirtual Categorized Toolbar Overlay System organizes toolbar extensions into logical dropdown categories, solving screen space limitations while maintaining easy access to tools.

## Overview

This system organizes toolbar extensions into categorized dropdowns:
- **Solves screen space limitations** - No matter how many tools are added
- **Logical organization** - Tools grouped by function
- **Context-sensitive** - Categories only appear when they have visible tools
- **Priority-based ordering** - Tools ordered within each category
- **Conditional visibility** - Tools can show/hide based on context

## Toolbar Layout

```
[Quick Edit] [Move Pivot] [Draw Mode] [Gizmos] [Tools ▼] [Analysis ▼] [Debug ▼] [Utilities ▼]
```

**Core Elements** (always visible):
- **Quick Edit** - Toggle QuickEdit overlay
- **Move Pivot** - Open Move Pivot overlay
- **Draw Mode** - Switch between shaded/wireframe rendering
- **Gizmos** - Toggle gizmo visibility

**Category Dropdowns** (appear only when they contain tools):
- **Tools** - Clone Inspector, Scene Cleaner, Mesh Optimizer
- **Analysis** - Performance Analyzer, Dependency Checker, Memory Profiler
- **Debug** - Debug Console, Error Checker, Diagnostics
- **Utilities** - Batch Export, Import Tools, Cleanup Functions

## How to Create Toolbar Extensions

### Method 1: Simple Button Extension

```csharp
[InitializeOnLoad]
[ToolbarOverlayExtension(900)]
public class MyToolButtonExtension : ToolbarButtonExtension
{
    public override string Id => "MyToolButton";
    public override string Text => "My Tool";
    public override string Tooltip => "Opens my custom tool";
    public override int Priority => 900;

    public override Texture2D Icon
    {
        get
        {
            return EditorGUIUtility.IconContent("d_Settings").image as Texture2D;
        }
    }

    static MyToolButtonExtension()
    {
        ToolbarOverlayExtensionSystem.RegisterExtension(new MyToolButtonExtension());
    }

    protected override void OnClick()
    {
        MyCustomWindow.ShowWindow();
    }

    public override bool IsVisible()
    {
        // Only show when a GameObject is selected
        return Selection.activeGameObject != null;
    }
}

// Corresponding EditorToolbarElement
[EditorToolbarElement(MyToolButton.id, typeof(SceneView))]
public class MyToolButton : EditorToolbarButton
{
    public const string id = "RealvirtualMyTool";

    public MyToolButton()
    {
        text = "My Tool";
        tooltip = "Opens my custom tool";
        icon = EditorGUIUtility.IconContent("d_Settings").image as Texture2D;
        clicked += () => MyCustomWindow.ShowWindow();
    }
}
```

### Method 2: Toggle Extension

```csharp
[InitializeOnLoad]
public class MyToggleExtension : ToolbarToggleExtension
{
    public override string Id => "MyToggle";
    public override string Text => "Feature";
    public override string Tooltip => "Toggle my feature";
    public override int Priority => 950;

    static MyToggleExtension()
    {
        ToolbarOverlayExtensionSystem.RegisterExtension(new MyToggleExtension());
    }

    protected override bool GetToggleState()
    {
        // Return current state of your feature
        return MyFeatureManager.IsEnabled;
    }

    protected override void OnToggleChanged(bool newValue)
    {
        // Handle toggle change
        MyFeatureManager.SetEnabled(newValue);
    }
}

// Corresponding EditorToolbarElement
[EditorToolbarElement(MyToggleButton.id, typeof(SceneView))]
public class MyToggleButton : EditorToolbarToggle
{
    public const string id = "RealvirtualMyToggle";

    public MyToggleButton()
    {
        text = "Feature";
        tooltip = "Toggle my feature";
        value = MyFeatureManager.IsEnabled;

        this.RegisterValueChangedCallback(evt =>
        {
            MyFeatureManager.SetEnabled(evt.newValue);
        });
    }
}
```

### Method 3: Custom Element

```csharp
public class CustomDropdownExtension : IToolbarOverlayExtension
{
    public string Id => "CustomDropdown";
    public string Text => "Options";
    public string Tooltip => "Custom dropdown menu";
    public Texture2D Icon => null;
    public int Priority => 1000;

    public VisualElement CreateElement()
    {
        var dropdown = new DropdownField("Options",
            new List<string> { "Option 1", "Option 2", "Option 3" }, 0);

        dropdown.RegisterValueChangedCallback(evt =>
        {
            Debug.Log($"Selected: {evt.newValue}");
        });

        return dropdown;
    }

    public bool IsVisible() => true;
}
```

## Registration Process

### Automatic Registration
Most extensions should use automatic registration in their static constructor:

```csharp
[InitializeOnLoad]
public class MyExtension : ToolbarButtonExtension
{
    static MyExtension()
    {
        ToolbarOverlayExtensionSystem.RegisterExtension(new MyExtension());
    }

    // Implementation...
}
```

### Manual Registration
For dynamic or conditional registration:

```csharp
public static class MyExtensionManager
{
    [InitializeOnLoadMethod]
    static void RegisterExtensions()
    {
        if (ShouldRegisterExtension())
        {
            ToolbarOverlayExtensionSystem.RegisterExtension(new MyExtension());
        }
    }
}
```

## Adding Extensions to Toolbar Overlay

To add your extension to the actual toolbar overlay, you need to:

1. **Create the EditorToolbarElement**:
```csharp
[EditorToolbarElement(MyButton.id, typeof(SceneView))]
public class MyButton : EditorToolbarButton
{
    public const string id = "RealvirtualMyButton";
    // Implementation...
}
```

2. **Add to the toolbar overlay constructor**:
Update `RealvirtualToolbarOverlay.GetAllToolbarElements()` to include your button ID:

```csharp
var extensionElements = new string[]
{
    CloneInspectorToolbarButton.id,
    MyButton.id  // Add your button here
};
```

## Priority Guidelines

Use these priority ranges to maintain proper ordering:

- **Core realvirtual elements**: 100-400
  - QuickEdit: 100
  - MovePivot: 200
  - DrawMode: 300
  - Gizmos: 400

- **Tool extensions**: 800-999
  - Clone Inspector: 850
  - Analysis tools: 860-880
  - Utility tools: 900-950

- **Custom extensions**: 1000+

## Icon Guidelines

### Recommended Icon Sources
```csharp
// Unity built-in icons (preferred)
EditorGUIUtility.IconContent("d_Settings").image as Texture2D
EditorGUIUtility.IconContent("d_Search Icon").image as Texture2D
EditorGUIUtility.IconContent("d_UnityEditor.FindDependencies").image as Texture2D

// realvirtual icons
AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/realvirtual/private/Resources/Icons/Icon64.png")
```

### Icon Best Practices
- Use Unity's built-in dark theme icons (`d_` prefix)
- Keep icons 16x16 or 24x24 pixels
- Use monochrome icons for consistency
- Test icons in both light and dark themes

## Context-Sensitive Extensions

### Selection-Based Visibility
```csharp
public override bool IsVisible()
{
    // Only show for Drive components
    var selected = Selection.activeGameObject;
    return selected != null && selected.GetComponent<Drive>() != null;
}
```

### Play Mode Visibility
```csharp
public override bool IsVisible()
{
    return Application.isPlaying;
}
```

### Professional Version Only
```csharp
public override bool IsVisible()
{
    #if REALVIRTUAL_PROFESSIONAL
        return true;
    #else
        return false;
    #endif
}
```

## Advanced Examples

### Multi-State Button
```csharp
public class MultiStateButton : ToolbarButtonExtension
{
    private int _currentState = 0;
    private readonly string[] _states = { "State A", "State B", "State C" };

    public override string Text => _states[_currentState];

    protected override void OnClick()
    {
        _currentState = (_currentState + 1) % _states.Length;
        // Update button text by recreating the toolbar
        SceneView.lastActiveSceneView?.Repaint();
    }
}
```

### Dropdown with Custom Options
```csharp
[EditorToolbarElement(CustomDropdown.id, typeof(SceneView))]
public class CustomDropdown : EditorToolbarDropdown
{
    public const string id = "RealvirtualCustomDropdown";

    public CustomDropdown()
    {
        text = "Options";
        tooltip = "Select an option";
        clicked += ShowDropdownMenu;
    }

    void ShowDropdownMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Option 1"), false, () => HandleOption(1));
        menu.AddItem(new GUIContent("Option 2"), false, () => HandleOption(2));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Reset"), false, Reset);
        menu.ShowAsContext();
    }

    void HandleOption(int option)
    {
        Debug.Log($"Selected option {option}");
    }

    void Reset()
    {
        Debug.Log("Reset triggered");
    }
}
```

## Troubleshooting

### Extension Not Appearing
1. Check that the extension is registered in a static constructor with `[InitializeOnLoad]`
2. Verify the EditorToolbarElement ID is added to `GetAllToolbarElements()`
3. Ensure `IsVisible()` returns true
4. Check console for any compilation errors

### Icon Not Displaying
1. Verify the icon path is correct
2. Check that the texture is marked as readable
3. Use Unity built-in icons as fallbacks
4. Test with a simple colored texture

### Button Not Responding
1. Check that the `clicked` event is properly assigned
2. Verify there are no exceptions in the click handler
3. Ensure the target window/functionality exists

This system provides a clean, maintainable way to extend the realvirtual toolbar overlay while keeping extensions organized and modular.