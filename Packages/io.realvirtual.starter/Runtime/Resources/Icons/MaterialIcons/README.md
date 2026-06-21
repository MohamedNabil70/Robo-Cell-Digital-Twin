# Material Icons Integration for realvirtual

This folder contains the Material Icons font integration for realvirtual, providing access to 2,200+ professional icons from Google Material Design.

## Overview

Material Icons can be used in both:
- **Runtime UI** - Game/HMI interfaces using TextMeshPro
- **Editor UI** - Custom inspectors and editor windows using IMGUI

## Files in This Directory

- **MaterialIcons.cs** - Core helper class with icon database and utility methods
- **MaterialIconFont/** - Font files (.ttf, .otf) and codepoints mapping
  - `MaterialIcons-Regular.ttf` - Filled icon style (default)
  - `MaterialIconsOutlined-Regular.otf` - Outlined icon style
  - `MaterialIconsSharp-Regular.otf` - Sharp/angular icon style
  - `MaterialIcons-Regular.codepoints.txt` - Icon name to unicode mapping (2,234 icons)

## Quick Start

### 1. Runtime UI (TextMeshPro)

**IMPORTANT**: Before using Material Icons in runtime UI, you must create a TMP Font Asset:

1. Go to: **Window → TextMeshPro → Font Asset Creator**
2. Configure:
   - **Source Font File**: Select `MaterialIcons-Regular.ttf`
   - **Character Set**: Select **Unicode Range (Hex)**
   - **Character Range**: Enter `e000-f8ff`
   - **Rendering Mode**: **SDFAA** (Signed Distance Field)
   - **Atlas Resolution**: **2048 x 2048** or **4096 x 4096**
3. Click **Generate Font Atlas**
4. Click **Save** and save as: `Assets/realvirtual/private/Resources/Icons/MaterialIcons SDF.asset`

**Using in Code:**

```csharp
using realvirtual;
using TMPro;

// Extension method (easiest way)
myTextComponent.SetMaterialIcon("home");

// With text
myTextComponent.SetMaterialIconWithText("save", "Save Document");

// Direct unicode (if font is already set)
myTextComponent.text = MaterialIcons.GetIcon("settings");

// Using constants
myTextComponent.SetMaterialIcon(MaterialIcons.Common.Home);
```

### 2. Editor UI (IMGUI)

No font asset creation needed for editor UI. Works directly:

```csharp
using realvirtual;
using UnityEditor;

public class MyEditorWindow : EditorWindow
{
    private GUIStyle iconStyle;

    void OnEnable()
    {
        // Create icon style (cached for performance)
        iconStyle = MaterialIcons.GetIconStyle(MaterialIcons.IconStyle.Regular, 24);
    }

    void OnGUI()
    {
        // Display icon
        GUILayout.Label(MaterialIcons.GetIcon("home"), iconStyle);

        // Icon button
        if (GUILayout.Button(MaterialIcons.GetIcon("settings"), iconStyle))
        {
            // Button clicked
        }

        // Icon with text
        GUILayout.Label(MaterialIcons.GetIconWithText("save", "Save"));
    }
}
```

## Available Icon Styles

Material Icons come in three visual styles:

- `IconStyle.Regular` - Filled/solid icons (default)
- `IconStyle.Outlined` - Outlined icons
- `IconStyle.Sharp` - Sharp/angular icons

```csharp
// Use different styles
textComponent.SetMaterialIcon("home", MaterialIcons.IconStyle.Outlined);
textComponent.SetMaterialIcon("home", MaterialIcons.IconStyle.Sharp);
```

## Helper Tools

### Material Icon Browser
Browse all 2,200+ available icons with visual preview:

**Menu**: `realvirtual DEV → Material Icon Browser`

Features:
- Search icons by name
- Filter by category
- Switch between icon styles
- Adjustable size and grid layout
- Click to copy icon name or code snippet

### Material Icons Test Window
Test and see code examples:

**Menu**: `realvirtual DEV → Testing → Test Material Icons`

## Common Icon Names

### UI Navigation
```csharp
MaterialIcons.Common.Home          // "home"
MaterialIcons.Common.Menu          // "menu"
MaterialIcons.Common.Settings      // "settings"
MaterialIcons.Common.Search        // "search"
MaterialIcons.Common.ArrowBack     // "arrow_back"
MaterialIcons.Common.ArrowForward  // "arrow_forward"
MaterialIcons.Common.ExpandMore    // "expand_more"
MaterialIcons.Common.ExpandLess    // "expand_less"
```

### Actions
```csharp
MaterialIcons.Common.Add           // "add"
MaterialIcons.Common.Remove        // "remove"
MaterialIcons.Common.Delete        // "delete"
MaterialIcons.Common.Edit          // "edit"
MaterialIcons.Common.Save          // "save"
MaterialIcons.Common.Copy          // "content_copy"
MaterialIcons.Common.Paste         // "content_paste"
MaterialIcons.Common.Refresh       // "refresh"
```

### Status
```csharp
MaterialIcons.Common.Check         // "check"
MaterialIcons.Common.Close         // "close"
MaterialIcons.Common.Info          // "info"
MaterialIcons.Common.Warning       // "warning"
MaterialIcons.Common.Error         // "error"
MaterialIcons.Common.Help          // "help"
```

### Industrial/Automation
```csharp
MaterialIcons.Industrial.Build              // "build"
MaterialIcons.Industrial.Settings           // "settings"
MaterialIcons.Industrial.Speed              // "speed"
MaterialIcons.Industrial.Timeline           // "timeline"
MaterialIcons.Industrial.Power              // "power"
MaterialIcons.Industrial.ViewInAr           // "view_in_ar"
MaterialIcons.Industrial.ThreeDRotation     // "3d_rotation"
MaterialIcons.Industrial.Widgets            // "widgets"
```

## API Reference

### MaterialIcons Class

**Static Methods:**

| Method | Description |
|--------|-------------|
| `GetIcon(string iconName)` | Returns unicode character for icon |
| `GetIconWithText(string iconName, string text)` | Returns icon + text |
| `GetTextWithIcon(string text, string iconName)` | Returns text + icon |
| `IconExists(string iconName)` | Check if icon exists |
| `GetAllIconNames()` | Get list of all icon names |
| `GetIconCount()` | Get total number of icons |
| `SearchIcons(string searchTerm)` | Search for icons |
| `GetFont(IconStyle style)` | Get Font for runtime |
| `GetEditorFont(IconStyle style)` | Get Font for editor (editor only) |
| `GetIconStyle(IconStyle, int fontSize)` | Create GUIStyle for editor (editor only) |

### MaterialIconHelper Class

**Extension Methods for TextMeshPro:**

| Method | Description |
|--------|-------------|
| `SetMaterialIcon(string iconName, IconStyle)` | Set icon on TMP component |
| `SetMaterialIconWithText(string iconName, string text)` | Set icon with text |
| `SetTextWithMaterialIcon(string text, string iconName)` | Set text with icon |
| `UpdateIcon(string iconName)` | Update icon without changing font |

**Helper Methods:**

| Method | Description |
|--------|-------------|
| `GetTMPFont(IconStyle)` | Get or load TMP Font Asset |
| `ClearFontCache()` | Clear cached fonts |
| `CreateIconObject(string iconName, Transform parent)` | Create GameObject with icon |
| `AddIconToButton(Button button, string iconName)` | Add icon to button |

## Finding Icon Names

1. **Use the Material Icon Browser**: `realvirtual DEV → Material Icon Browser`
2. **Search online**: https://fonts.google.com/icons
3. **Browse constants**: Check `MaterialIcons.Common` and `MaterialIcons.Industrial`

Icon names use underscore_case (e.g., `arrow_back`, `content_copy`, `3d_rotation`)

## Performance Tips

1. **Cache icon styles** in editor windows instead of recreating them every frame
2. **Cache font references** when using many icons
3. **Use constants** instead of string literals to avoid typos
4. **Create TMP Font Asset once** and reference it, don't load repeatedly

## Troubleshooting

### Runtime UI Issues

**Problem**: Icons not showing in game/HMI
- **Solution**: Create TMP Font Asset first (see Quick Start section)

**Problem**: Icons show as squares/boxes
- **Solution**: Wrong font or unicode range. Ensure TMP Font Asset includes range `e000-f8ff`

**Problem**: Warning about font not found
- **Solution**: Create and save TMP Font Asset to `Resources/Icons/MaterialIcons SDF.asset`

### Editor UI Issues

**Problem**: Icons show as question marks
- **Solution**: Check font files are in correct location: `Resources/Icons/MaterialIconFont/`

**Problem**: Icon name not recognized
- **Solution**: Use Material Icon Browser to find correct name

## Examples

### Example 1: Button with Icon in Runtime UI

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using realvirtual;

public class IconButton : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI buttonText;

    void Start()
    {
        // Set icon
        buttonText.SetMaterialIconWithText("save", "Save");
    }

    public void UpdateIcon(string iconName)
    {
        buttonText.UpdateIcon(iconName);
    }
}
```

### Example 2: Custom Inspector with Icons

```csharp
using UnityEditor;
using UnityEngine;
using realvirtual;

[CustomEditor(typeof(MyComponent))]
public class MyComponentEditor : Editor
{
    private GUIStyle addIconStyle;
    private GUIStyle removeIconStyle;

    void OnEnable()
    {
        addIconStyle = MaterialIcons.GetIconStyle(MaterialIcons.IconStyle.Regular, 18);
        removeIconStyle = MaterialIcons.GetIconStyle(MaterialIcons.IconStyle.Regular, 18);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(MaterialIcons.GetIcon("add"), addIconStyle,
            GUILayout.Width(30), GUILayout.Height(30)))
        {
            // Add action
        }

        if (GUILayout.Button(MaterialIcons.GetIcon("remove"), removeIconStyle,
            GUILayout.Width(30), GUILayout.Height(30)))
        {
            // Remove action
        }

        EditorGUILayout.EndHorizontal();
    }
}
```

### Example 3: Dynamic Icon Toolbar

```csharp
using UnityEditor;
using UnityEngine;
using realvirtual;

public class IconToolbar : EditorWindow
{
    private GUIStyle toolbarIconStyle;
    private string[] iconNames = { "home", "settings", "info", "help" };

    void OnEnable()
    {
        toolbarIconStyle = MaterialIcons.GetIconStyle(MaterialIcons.IconStyle.Regular, 20);
    }

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        foreach (string iconName in iconNames)
        {
            if (GUILayout.Button(MaterialIcons.GetIcon(iconName),
                toolbarIconStyle, GUILayout.Width(35)))
            {
                HandleIconClick(iconName);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    void HandleIconClick(string iconName)
    {
        Debug.Log($"Clicked: {iconName}");
    }
}
```

## License

Material Icons are created by Google and licensed under Apache License 2.0.
See: https://github.com/google/material-design-icons

## Resources

- **Google Material Icons**: https://fonts.google.com/icons
- **Material Design Guidelines**: https://material.io/design/iconography
- **Icon Repository**: https://github.com/google/material-design-icons
