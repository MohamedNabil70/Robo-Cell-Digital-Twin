# RuntimeUI API Quick Reference

> **Note**: This is a quick reference. For comprehensive documentation, examples, and best practices, see `RuntimeUI_System_Overview.md`.

---

## RuntimeUIBuilder (Singleton)

Access via: `RuntimeUIBuilder.Instance`

### Navigation

| Method | Description |
|--------|-------------|
| `MoveCursorTo(rvUIContent content)` | Move cursor to specific element |
| `MoveCursor(int n)` | Move cursor n steps (+/-) |
| `StepIn()` | Enter container (first child) |
| `StepOut()` | Exit container (parent) |

### Content Creation

| Method | Returns | Description |
|--------|---------|-------------|
| `Add(ContentType type)` | `void` | Add content at cursor |
| `AddButton(string text)` | `rvUIMenuButton` | Create button with text |
| `AddText(string text)` | `rvUIText` | Create text label |
| `AddContainer(ContentType type)` | `rvUIContainer` | Create container |
| `AddMenuWindow(Style initialStyle)` | `rvUIMenuWindow` | Create adaptive menu window |
| `AddToggleGroup()` | `rvUIToggleGroup` | Add toggle group component |
| `AddEnumField<T>(label, value, horizontal)` | `EnumField<T>` | Create enum selector |

### Submenu Creation

| Method | Description |
|--------|-------------|
| `CreateSubMenu(SubMenuType, Placement, margin)` | Create submenu with relative positioning |

### Layout & Cleanup

| Method | Description |
|--------|-------------|
| `Clear()` | Clear current container's children |
| `RefreshFromCursor()` | Refresh layout from cursor to root |

---

## ContentType Enum

```csharp
Text            // Text label
Button          // Interactive button
Dropdown        // Dropdown selector
HorizontalMenu  // Horizontal container
VerticalMenu    // Vertical container
Window          // Floating window
MenuWindow      // Adaptive window (can switch styles)
SplitMenu       // Split panel
CollabsibleMenu // Collapsible section
```

---

## SubMenuType Enum

```csharp
Window      // Floating window with header
Horizontal  // Horizontal panel
Vertical    // Vertical panel
```

---

## rvUIMenuButton

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `text` | `string` | Button text |
| `isOn` | `bool` | Toggle state |
| `interactable` | `bool` | Can be clicked |
| `hideText` | `bool` | Hide button text |
| `image` | `Image` | Icon image |
| `colorOff` | `Color` | Color when off |
| `colorOn` | `Color` | Color when on |

### Methods

| Method | Description |
|--------|-------------|
| `SetText(string text)` | Set button text |
| `SetIcon(Sprite icon)` | Set button icon |
| `ToggleOn()` | Toggle button on (if not already on) |
| `ToggleOff()` | Toggle button off (if not already off) |
| `Toggle()` | Toggle button state |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnClick` | `UnityEvent` | Fired when clicked |
| `OnToggleOn` | `UnityEvent` | Fired when toggled ON |
| `OnToggleOff` | `UnityEvent` | Fired when toggled OFF |

---

## rvUIContainer

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetContentRoot()` | `RectTransform` | Get root transform for children |
| `GetUIContents()` | `List<rvUIContent>` | Get all child UI elements |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnChildAdded` | `UnityEvent<rvUIContent>` | Fired when child added |
| `OnChildRemoved` | `UnityEvent<rvUIContent>` | Fired when child removed |

---

## rvUIFloatingMenuPanel

Extends `rvUIContainer`

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `padding` | `float` | Internal padding |
| `hideText` | `bool` | Hide all button text |
| `hideHeader` | `bool` | Header visibility |
| `subMenuPlacement` | `Placement` | Default submenu placement |

### Methods

| Method | Description |
|--------|-------------|
| `ToggleHeader()` | Toggle header visibility |
| `ShowHeader()` | Show header |
| `HideHeader()` | Hide header |
| `Refresh()` | Refresh all elements |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnHeaderShown` | `UnityEvent` | Fired when header shown |
| `OnHeaderHidden` | `UnityEvent` | Fired when header hidden |

---

## rvUIDropdown

Extends `rvUIContainer`

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `title` | `string` | Dropdown title |
| `changeIcon` | `bool` | Update icon to match selection |
| `changeText` | `bool` | Update text to match selection |
| `elements` | `List<DropdownElement>` | Dropdown options |
| `selectedIndex` | `int` | Currently selected index |
| `selectedValue` | `string` | Currently selected text |

### Methods

| Method | Description |
|--------|-------------|
| `Initialize()` | Initialize dropdown UI |
| `SelectElement(int index)` | Select element by index |
| `SelectElement(string text)` | Select element by text |
| `AddElement(string text, Sprite icon)` | Add dropdown element |
| `RemoveElement(int index)` | Remove element |
| `ClearElements()` | Clear all elements |
| `Rebuild()` | Rebuild dropdown UI |
| `ShowDropdown()` | Show dropdown panel |
| `HideDropdown()` | Hide dropdown panel |
| `ToggleDropdown()` | Toggle dropdown visibility |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnElementSelected` | `UnityEvent<string>` | Fired when element selected |
| `OnIndexChanged` | `UnityEvent<int>` | Fired when index changes |

---

## rvUIMenuWindow

Extends `rvUIContainer`

Adaptive container that can dynamically switch between horizontal, vertical, and window panel styles while preserving all content.

### Style Enum

```csharp
Horizontal  // Horizontal menu panel
Vertical    // Vertical menu panel
Window      // Floating window with header
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `currentStyle` | `Style` | Current panel style |

### Methods

| Method | Description |
|--------|-------------|
| `Initialize(Style)` | Initialize with specific style |
| `SwitchStyle(Style)` | Switch to new style (preserves content) |
| `SwitchToHorizontal()` | Switch to horizontal style |
| `SwitchToVertical()` | Switch to vertical style |
| `SwitchToWindow()` | Switch to window style |
| `GetCurrentStyle()` | Get current style |
| `GetCurrentPanel()` | Get current active panel |
| `IsStyle(Style)` | Check if currently in specific style |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnStyleChanged` | `UnityEvent<Style>` | Fired when style changes |

### Example

```csharp
// Create adaptive menu window
var menuWindow = builder.AddMenuWindow(rvUIMenuWindow.Style.Vertical);
builder.StepIn();
    builder.AddButton("Item 1");
    builder.AddButton("Item 2");
    builder.AddButton("Item 3");
builder.StepOut();

// Listen for style changes
menuWindow.OnStyleChanged.AddListener((newStyle) => {
    Debug.Log($"Style changed to: {newStyle}");
});

// Switch styles programmatically (content is preserved)
menuWindow.SwitchToHorizontal();  // Now horizontal layout
menuWindow.SwitchToWindow();      // Now window with header
menuWindow.SwitchToVertical();    // Back to vertical
```

---

## rvUIToggleGroup

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `buttons` | `List<rvUIMenuButton>` | Buttons in group |
| `allowSwitchOff` | `bool` | Allow deselecting all |
| `autoassignButtons` | `bool` | Auto-find child buttons |

### Methods

| Method | Description |
|--------|-------------|
| `AddButton(rvUIMenuButton)` | Add button to group |
| `GetActiveToggle()` | Get active Unity Toggle |
| `GetActiveButton()` | Get active rvUIMenuButton |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnActiveToggleChanged` | `UnityEvent` | Fired when active toggle changes |

---

## rvUIText

### Methods

| Method | Description |
|--------|-------------|
| `SetText(string text)` | Set text content |

---

## rvUIContent (Base Class)

All UI elements inherit from this.

### Methods

| Method | Description |
|--------|-------------|
| `RefreshLayout()` | Update visual appearance (abstract) |
| `RefreshLayoutRecursive()` | Refresh this and all children |
| `RefreshLayoutBottomUp()` | Recursive bottom-up refresh |
| `GetChildContents()` | Get all child UI elements |
| `MoveToContainer(container, refresh)` | Reparent to container |
| `GetContainer()` | Find parent container |
| `GetPathToRoot()` | Get hierarchy path to root |

---

## EnumField<T>

Generic enum selector with button group.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `T` | Current enum value |
| `UseHorizontalLayout` | `bool` | Use horizontal layout |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnValueChanged` | `UnityEvent<object>` | Fired when value changes |

### Methods

| Method | Description |
|--------|-------------|
| `BuildUI(RuntimeUIBuilder)` | Build the UI |
| `SetValue(T value)` | Set enum value |

---

## rvUIRelativePlacement

Positions UI element relative to a target.

### Placement Enum

```csharp
Above       // Above target
Below       // Below target
Left        // Left of target
Right       // Right of target
Horizontal  // Auto horizontal
Vertical    // Auto vertical
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `target` | `RectTransform` | Target to position relative to |
| `margin` | `float` | Distance from target |

### Methods

| Method | Description |
|--------|-------------|
| `SetPlacement(Placement)` | Set placement type |

---

## Quick Start Example

```csharp
using UnityEngine;

public class MyUI : MonoBehaviour
{
    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        var builder = RuntimeUIBuilder.Instance;

        // Create menu
        var menu = builder.AddContainer(RuntimeUIBuilder.ContentType.VerticalMenu);
        builder.StepIn();

        // Add buttons
        var playBtn = builder.AddButton("Play");
        playBtn.OnClick.AddListener(() => Debug.Log("Play clicked!"));

        var optionsBtn = builder.AddButton("Options");
        optionsBtn.OnClick.AddListener(() => Debug.Log("Options clicked!"));

        builder.StepOut();
    }
}
```

---

## See Also

- **`RuntimeUI_System_Overview.md`** - Comprehensive documentation, patterns, best practices
- **`rvUIContent.cs`** - Base class implementation
- **`RuntimeUIBuilder.cs`** - Builder implementation

---

**Version**: 1.0
**Last Updated**: 2025-10-21
