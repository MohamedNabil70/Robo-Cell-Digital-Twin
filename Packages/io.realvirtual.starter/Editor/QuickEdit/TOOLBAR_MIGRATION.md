# QuickEdit Toolbar - Unity 6 Migration

## Summary

The QuickEdit toolbar has been migrated to use Unity 6's supported EditorToolbar API instead of reflection-based injection methods.

## Changes Made

### 1. New Unity 6 Implementation
**File**: `Assets/realvirtual/private/Editor/QuickEditToolbar.cs`

Three new toolbar elements using Unity's supported `EditorToolbarElement` API:
- `QuickEditToolbarButton` - Toggle QuickEdit overlay visibility
- `QuickEditToolbarDropdown` - Quick access to settings menu
- `ProjectPathToolbarLabel` - Display project path (optional)

### 2. Legacy Code Disabled for Unity 6
**File**: `Assets/realvirtual/private/Editor/QuickEditToolbarIMGUI.cs`

- Wrapped entire file with `#if !UNITY_6000_0_OR_NEWER` preprocessor directive
- Code still active for Unity 2021-2022 LTS versions
- Added comments explaining the migration

### 3. Updated References
**File**: `Assets/realvirtual/private/Editor/ProjectPathMenuItem.cs`

- Added version-specific handling for toolbar refresh
- Unity 6 uses `RepaintAllViews()` instead of `QuickEditToolbarIMGUI.ForceRefresh()`

## How to Use (Unity 6+)

### Enable Toolbar Elements

The new toolbar buttons are registered as EditorToolbarElements but need to be enabled:

1. **Via Scene View Toolbar**:
   - Right-click on the Scene view toolbar
   - Look for "realvirtual" toolbar elements
   - Enable:
     - "realvirtual/QuickEditToggle" - Main toggle button
     - "realvirtual/QuickEditMenu" - Dropdown menu
     - "realvirtual/ProjectPath" - Project path display (optional)

2. **Via Unity Overlay Menu**:
   - Click the ⋮ icon in Scene view top-right
   - Enable "realvirtual Quick Edit" overlay

### Functionality

All previous functionality is preserved:
- Toggle QuickEdit overlay with button click
- Access settings via dropdown menu
- Click project path to open folder in Explorer/Finder
- Settings persistence via EditorPrefs

## Technical Details

### EditorToolbar API Benefits
- **Officially supported** by Unity - no future breakage
- **Better integration** with Unity's UI system
- **Automatic layout** handling by Unity
- **Per-window persistence** of toolbar configuration

### Compatibility Matrix
| Unity Version | Implementation |
|---------------|----------------|
| 2021.x | QuickEditToolbarIMGUI (IMGUI) |
| 2022.x LTS | QuickEditToolbarIMGUI (IMGUI) |
| 6.x LTS | QuickEditToolbar (EditorToolbar API) |
| 6.x+ | QuickEditToolbar (EditorToolbar API) |

## Troubleshooting

### Toolbar buttons not visible
1. Right-click Scene view toolbar
2. Check for "realvirtual" elements in context menu
3. Enable desired toolbar elements

### Overlay not responding to button
1. Ensure QuickEdit overlay is not manually hidden via overlay menu
2. Check EditorPrefs value: `realvirtual_QuickEditVisible`
3. Reset by deleting the EditorPrefs key

### Project path not showing
1. Enable via menu: `realvirtual/Settings/Show Project Path in Toolbar`
2. Enable toolbar element: `realvirtual/ProjectPath`

## Migration Notes for Developers

### Extending the Toolbar

To add new toolbar elements for Unity 6+:

```csharp
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Toolbars;

[EditorToolbarElement(id, typeof(SceneView))]
public class MyCustomToolbarButton : EditorToolbarButton
{
    public const string id = "realvirtual/MyButton";

    public MyCustomToolbarButton()
    {
        text = "My Button";
        clicked += OnClick;
    }

    private void OnClick()
    {
        // Your logic here
    }
}
#endif
```

### Version-Specific Code

Always use preprocessor directives for version-specific implementations:

```csharp
#if UNITY_6000_0_OR_NEWER
    // Unity 6+ code using EditorToolbar
#else
    // Unity 2021-2022 code using IMGUI
#endif
```

## References

- [Unity EditorToolbar Documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorToolbar.html)
- [Unity Overlay System](https://docs.unity3d.com/6000.0/Documentation/Manual/overlays.html)
- [EditorToolbarElement API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorToolbarElement.html)
