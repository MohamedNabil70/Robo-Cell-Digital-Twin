# RuntimeUI System - Comprehensive Overview

## Executive Summary

**RuntimeUI** is a cursor-based UI building system for creating dynamic interfaces in Unity at runtime. It provides a fluent API for programmatically constructing UI hierarchies with minimal boilerplate.

**Status**: Production-ready
**Version**: 1.0
**Architecture**: Cursor-based builder pattern with event-driven components

---

## Core Philosophy

1. **Simple & Focused** - No complex frameworks, just build UI trees
2. **Cursor-Based Navigation** - Traverse and build like a file system
3. **Event-Driven** - UnityEvents for loose coupling
4. **Prefab-Based** - Design UI elements in Unity Editor
5. **Fluent API** - Readable, chainable methods

---

## Architecture

### Component Hierarchy

```
rvUIContent (abstract base)
├── rvUIContainer (abstract)
│   ├── rvUIFloatingMenuPanel (windows, menus)
│   └── rvUIDropdown (dropdown menus)
└── rvUIMenuButton (buttons)
└── rvUIText (text labels)
└── rvUIMenuSpacing (spacing elements)
```

### Core Components

#### 1. RuntimeUIBuilder (Singleton)
- **Purpose**: Central API for building UI hierarchies
- **Pattern**: Singleton with cursor-based traversal
- **Prefab System**: Instantiates UI from prefabs
- **Location**: Assets/realvirtual/private/RuntimeUI/UIComponents/Scripts/

#### 2. rvUIContent (Base Class)
- **Purpose**: Base class for all UI elements
- **Key Methods**:
  - `RefreshLayout()` - Update visual appearance
  - `RefreshLayoutBottomUp()` - Recursive bottom-up refresh
  - `GetPathToRoot()` - Get hierarchy path
  - `GetContainer()` - Find parent container
  - `MoveToContainer()` - Reparent to container

#### 3. rvUIContainer (Abstract Container)
- **Purpose**: Base class for containers that hold other UI elements
- **Key Methods**:
  - `GetContentRoot()` - Returns RectTransform where children are added
  - `GetUIContents()` - Get all child UI elements
- **Events**:
  - `OnChildAdded` - Fired when child is added
  - `OnChildRemoved` - Fired when child is removed

#### 4. rvUIFloatingMenuPanel (Concrete Container)
- **Purpose**: Configurable panel/window container
- **Features**:
  - Optional header with hide/show
  - Configurable padding
  - Submenu relative placement
  - Content size fitting
- **Events**:
  - `OnHeaderShown`
  - `OnHeaderHidden`

---

## API Reference

### RuntimeUIBuilder Methods

#### Navigation
```csharp
void MoveCursorTo(rvUIContent content)  // Move to specific element
void MoveCursor(int n)                   // Move n steps (+/-)
void StepIn()                            // Enter container (first child)
void StepOut()                           // Exit container (parent)
```

#### Content Creation
```csharp
void Add(ContentType type)                           // Generic add
rvUIMenuButton AddButton(string text)                // Create button
rvUIText AddText(string text)                        // Create text
rvUIContainer AddContainer(ContentType type)         // Create container
rvUIToggleGroup AddToggleGroup()                     // Add toggle group
EnumField<T> AddEnumField<T>(label, value, horizontal) // Create enum selector
```

#### Submenu Creation
```csharp
rvUIFloatingMenuPanel CreateSubMenu(
    SubMenuType type,                    // Window, Horizontal, Vertical
    rvUIRelativePlacement.Placement placement,  // Above, Below, Left, Right
    float margin = 10f                   // Margin from target
)
```

#### Cleanup & Layout
```csharp
void Clear()              // Clear current container's children
void RefreshFromCursor()  // Refresh layout from cursor to root
```

### ContentType Enum
```csharp
public enum ContentType
{
    Text,              // Text label
    Button,            // Interactive button
    Dropdown,          // Dropdown menu
    HorizontalMenu,    // Horizontal container
    VerticalMenu,      // Vertical container
    Window,            // Floating window
    SplitMenu,         // Split panel (TODO)
    CollabsibleMenu    // Collapsible section (TODO)
}
```

### SubMenuType Enum
```csharp
public enum SubMenuType
{
    Window,      // Floating window with header
    Horizontal,  // Horizontal panel
    Vertical     // Vertical panel
}
```

---

## Usage Patterns

### Pattern 1: Simple Menu

```csharp
public class GameMenu : MonoBehaviour
{
    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        var builder = RuntimeUIBuilder.Instance;

        // Create vertical menu
        var menu = builder.AddContainer(RuntimeUIBuilder.ContentType.VerticalMenu);
        builder.StepIn();

        // Add buttons
        var playBtn = builder.AddButton("Play");
        playBtn.OnClick.AddListener((isOn) => StartGame());

        var optionsBtn = builder.AddButton("Options");
        optionsBtn.OnClick.AddListener((isOn) => ShowOptions());

        var quitBtn = builder.AddButton("Quit");
        quitBtn.OnClick.AddListener((isOn) => QuitGame());

        builder.StepOut();
    }

    void StartGame() { /* ... */ }
    void ShowOptions() { /* ... */ }
    void QuitGame() { /* ... */ }
}
```

### Pattern 2: Window with Toolbar

```csharp
void BuildEditorWindow()
{
    var builder = RuntimeUIBuilder.Instance;

    // Create window
    var window = builder.AddContainer(RuntimeUIBuilder.ContentType.Window);
    builder.StepIn();

    // Title
    builder.AddText("My Editor Window");

    // Toolbar
    var toolbar = builder.AddContainer(RuntimeUIBuilder.ContentType.HorizontalMenu);
    builder.StepIn();
        builder.AddButton("New").OnClick.AddListener((isOn) => CreateNew());
        builder.AddButton("Save").OnClick.AddListener((isOn) => Save());
        builder.AddButton("Load").OnClick.AddListener((isOn) => Load());
    builder.StepOut();

    // Content area
    var content = builder.AddContainer(RuntimeUIBuilder.ContentType.VerticalMenu);
    builder.StepIn();
        // Add your content here
    builder.StepOut();

    builder.StepOut(); // Exit window
}
```

### Pattern 3: Context Menu with Submenu

```csharp
void BuildContextMenu()
{
    var builder = RuntimeUIBuilder.Instance;

    // Main menu
    var menu = builder.AddContainer(RuntimeUIBuilder.ContentType.VerticalMenu);
    builder.StepIn();

    // File button
    var fileBtn = builder.AddButton("File");

    // Create submenu for File button
    var fileSubmenu = builder.CreateSubMenu(
        RuntimeUIBuilder.SubMenuType.Vertical,
        rvUIRelativePlacement.Placement.Right,
        10f
    );

    builder.StepIn();
        builder.AddButton("New").OnClick.AddListener((isOn) => NewFile());
        builder.AddButton("Open").OnClick.AddListener((isOn) => OpenFile());
        builder.AddButton("Save").OnClick.AddListener((isOn) => SaveFile());
    builder.StepOut();

    // Continue with other menu items
    builder.StepOut();
}
```

### Pattern 4: Dynamic List

```csharp
public class InventoryUI : MonoBehaviour
{
    private List<string> items = new List<string>();
    private rvUIContainer itemContainer;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        var builder = RuntimeUIBuilder.Instance;

        itemContainer = builder.AddContainer(RuntimeUIBuilder.ContentType.VerticalMenu);
        builder.StepIn();

        // Add items
        foreach (var item in items)
        {
            CreateItemButton(item);
        }

        builder.StepOut();
    }

    void CreateItemButton(string itemName)
    {
        var builder = RuntimeUIBuilder.Instance;
        builder.MoveCursorTo(itemContainer);
        builder.StepIn();

        var btn = builder.AddButton(itemName);
        btn.OnClick.AddListener((isOn) => SelectItem(itemName));
    }

    public void AddItem(string item)
    {
        items.Add(item);
        CreateItemButton(item);
        RuntimeUIBuilder.Instance.RefreshFromCursor();
    }

    void SelectItem(string item) { /* ... */ }
}
```

### Pattern 5: Enum Selector

```csharp
public enum Quality { Low, Medium, High, Ultra }

void BuildQualitySelector()
{
    var builder = RuntimeUIBuilder.Instance;

    // Create enum field (button group)
    var enumField = builder.AddEnumField<Quality>(
        "Graphics Quality",
        Quality.High,
        useHorizontalLayout: true
    );

    // Listen for value changes
    enumField.OnValueChanged.AddListener((newValue) => {
        Debug.Log($"Quality changed to: {newValue}");
        ApplyQualitySettings((Quality)newValue);
    });
}
```

---

## Events System

### Button Events (rvUIMenuButton)

```csharp
var button = builder.AddButton("Click Me");

// OnClick - fires when button is clicked (toggle state as parameter)
button.OnClick.AddListener((bool isOn) => {
    Debug.Log($"Clicked! Toggle state: {isOn}");
});

// OnToggleOn - fires when button is toggled ON
button.OnToggleOn.AddListener(() => {
    Debug.Log("Button is now ON");
});

// OnToggleOff - fires when button is toggled OFF
button.OnToggleOff.AddListener(() => {
    Debug.Log("Button is now OFF");
});
```

### Container Events (rvUIContainer)

```csharp
var container = builder.AddContainer(RuntimeUIBuilder.ContentType.VerticalMenu);

// OnChildAdded - fires when a child is added
container.OnChildAdded.AddListener((rvUIContent child) => {
    Debug.Log($"Child added: {child.gameObject.name}");
});

// OnChildRemoved - fires when a child is removed
container.OnChildRemoved.AddListener((rvUIContent child) => {
    Debug.Log($"Child removed: {child.gameObject.name}");
});
```

### Panel Events (rvUIFloatingMenuPanel)

```csharp
var panel = builder.AddContainer(RuntimeUIBuilder.ContentType.Window) as rvUIFloatingMenuPanel;

// OnHeaderShown - fires when header is shown
panel.OnHeaderShown.AddListener(() => {
    Debug.Log("Header is now visible");
});

// OnHeaderHidden - fires when header is hidden
panel.OnHeaderHidden.AddListener(() => {
    Debug.Log("Header is now hidden");
});

// Programmatic control
panel.ToggleHeader();  // Toggle visibility
panel.ShowHeader();    // Show header
panel.HideHeader();    // Hide header
```

---

## Extensibility Guide

### Creating Custom Content Components

```csharp
using UnityEngine;

public class rvUIProgressBar : rvUIContent
{
    [Range(0, 1)] public float value = 0.5f;

    public override void RefreshLayout()
    {
        // Update visual representation
        UpdateProgressBar();
    }

    void UpdateProgressBar()
    {
        // Implementation here
    }
}
```

### Creating Custom Containers

```csharp
using UnityEngine;
using System.Collections.Generic;

public class rvUITabPanel : rvUIContainer
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform tabBar;

    public List<Tab> tabs = new List<Tab>();
    public int activeTabIndex = 0;

    public override RectTransform GetContentRoot()
    {
        return contentRoot != null ? contentRoot : GetComponent<RectTransform>();
    }

    public override void RefreshLayout()
    {
        // Refresh tab buttons
        RefreshTabButtons();

        // Show active tab content
        ShowTab(activeTabIndex);

        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    void RefreshTabButtons() { /* ... */ }
    void ShowTab(int index) { /* ... */ }

    [System.Serializable]
    public class Tab
    {
        public string title;
        public rvUIContainer content;
    }
}
```

### Extending RuntimeUIBuilder

```csharp
// Add extension methods for convenience
public static class RuntimeUIBuilderExtensions
{
    public static rvUIProgressBar AddProgressBar(this RuntimeUIBuilder builder, float initialValue)
    {
        builder.Add(RuntimeUIBuilder.ContentType.Custom);
        var progressBar = builder.cursor.gameObject.AddComponent<rvUIProgressBar>();
        progressBar.value = initialValue;
        progressBar.RefreshLayout();
        return progressBar;
    }

    public static rvUITabPanel AddTabPanel(this RuntimeUIBuilder builder)
    {
        builder.Add(RuntimeUIBuilder.ContentType.Window);
        var tabPanel = builder.cursor.gameObject.AddComponent<rvUITabPanel>();
        tabPanel.RefreshLayout();
        return tabPanel;
    }
}

// Usage:
var progressBar = builder.AddProgressBar(0.75f);
var tabPanel = builder.AddTabPanel();
```

---

## Best Practices

### 1. Always StepIn/StepOut

```csharp
// ✅ GOOD: Clear hierarchy
var container = builder.AddContainer(type);
builder.StepIn();
    // Add children
builder.StepOut();

// ❌ BAD: Confusing cursor state
var container = builder.AddContainer(type);
// Forgot to StepIn - children added as siblings!
```

### 2. Cache Builder Reference

```csharp
// ✅ GOOD: Cache once
var builder = RuntimeUIBuilder.Instance;
builder.AddButton("A");
builder.AddButton("B");

// ❌ BAD: Multiple singleton access
RuntimeUIBuilder.Instance.AddButton("A");
RuntimeUIBuilder.Instance.AddButton("B");
```

### 3. Use Helper Methods

```csharp
// ✅ GOOD: Fluent helper
var btn = builder.AddButton("Click Me");
btn.OnClick.AddListener(HandleClick);

// ❌ BAD: Manual setup
builder.Add(RuntimeUIBuilder.ContentType.Button);
var btn = builder.cursor as rvUIMenuButton;
btn.text = "Click Me";
btn.RefreshLayout();
btn.OnClick.AddListener(HandleClick);
```

### 4. Decouple Logic from UI

```csharp
// ✅ GOOD: Separate concerns
button.OnClick.AddListener((isOn) => gameManager.StartGame());

// ❌ BAD: Business logic in UI callback
button.OnClick.AddListener((isOn) => {
    // 50 lines of game startup logic here...
});
```

### 5. Refresh After Dynamic Changes

```csharp
// ✅ GOOD: Refresh after modifications
builder.AddButton("New Item");
builder.RefreshFromCursor();

// ❌ BAD: Forget to refresh (layout may be wrong)
builder.AddButton("New Item");
```

### 6. Clean Up Event Listeners

```csharp
void OnDestroy()
{
    // ✅ GOOD: Remove listeners
    playButton.OnClick.RemoveListener(HandlePlay);
}
```

---

## Performance Considerations

### 1. Avoid Frequent Rebuilds
```csharp
// ❌ BAD: Rebuild every frame
void Update()
{
    RebuildEntireUI();
}

// ✅ GOOD: Rebuild only when data changes
void OnDataChanged()
{
    RebuildEntireUI();
}
```

### 2. Use RefreshFromCursor Instead of Full Rebuild
```csharp
// ✅ GOOD: Targeted refresh
builder.AddButton("New");
builder.RefreshFromCursor();  // Only refreshes cursor to root

// ❌ BAD: Full hierarchy refresh
builder.AddButton("New");
rootContainer.RefreshLayoutBottomUp();  // Refreshes entire tree
```

### 3. Pool UI Elements (Advanced)
```csharp
// For frequently created/destroyed UI elements
private Queue<rvUIMenuButton> buttonPool = new Queue<rvUIMenuButton>();

rvUIMenuButton GetButton()
{
    if (buttonPool.Count > 0)
        return buttonPool.Dequeue();

    return builder.AddButton("");
}

void ReturnButton(rvUIMenuButton btn)
{
    btn.gameObject.SetActive(false);
    buttonPool.Enqueue(btn);
}
```

---

## Troubleshooting

### Issue: UI Not Appearing
**Symptoms**: Empty screen, no UI visible
**Solutions**:
- ✅ Check `RuntimeUIBuilder.Instance` is not null
- ✅ Verify prefabs are assigned in RuntimeUIBuilder inspector
- ✅ Ensure EventSystem exists in scene
- ✅ Check cursor is set to a valid container
- ✅ Verify UI is not behind other elements (Canvas sort order)

### Issue: Layout Not Updating
**Symptoms**: UI elements overlap, wrong sizes
**Solutions**:
- ✅ Call `RefreshFromCursor()` after adding elements
- ✅ Check ContentSizeFitter settings on containers
- ✅ Verify RectTransform anchors are correct
- ✅ Force rebuild: `LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform)`

### Issue: Events Not Firing
**Symptoms**: Button clicks don't work
**Solutions**:
- ✅ Check `button.interactable` is true
- ✅ Verify EventSystem exists and is active
- ✅ Check if button has Graphic Raycaster
- ✅ Ensure listeners are added AFTER button creation

### Issue: Cursor in Wrong Position
**Symptoms**: Elements added to wrong parent
**Solutions**:
- ✅ Always pair StepIn() with StepOut()
- ✅ Use MoveCursorTo() to reset cursor explicitly
- ✅ Debug cursor position: `Debug.Log(builder.cursor.gameObject.name)`

---

## Migration Guide

### From Unity UI Toolkit
```csharp
// UIToolkit style
var button = new Button();
button.text = "Click";
button.clicked += HandleClick;
root.Add(button);

// RuntimeUI style
var button = builder.AddButton("Click");
button.OnClick.AddListener((isOn) => HandleClick());
```

### From Unity uGUI
```csharp
// uGUI style
var go = Instantiate(buttonPrefab, parent);
var button = go.GetComponent<Button>();
button.onClick.AddListener(HandleClick);

// RuntimeUI style
var button = builder.AddButton("Click");
button.OnClick.AddListener((isOn) => HandleClick());
```

---

## Future Roadmap

### Planned Features
- ✅ Toggle groups (DONE - rvUIToggleGroup)
- ✅ Dropdown menus (DONE - rvUIDropdown)
- ⏳ Input fields
- ⏳ Sliders
- ⏳ Scroll views
- ⏳ Grid layouts
- ⏳ Tree views
- ⏳ Drag & drop support
- ⏳ Animation system
- ⏳ Theme/skin system

### Under Consideration
- Component templates
- UI state serialization
- Undo/redo support
- Accessibility features
- Multi-language support

---

## Version History

### v1.0 (Current)
- Initial release
- Core cursor-based builder
- Basic content types (Button, Text, Container)
- Event system (OnClick, OnToggleOn/Off, OnChildAdded/Removed)
- Submenu creation with relative placement
- Layout refresh system
- Toggle groups
- Dropdown menus
- Enum field selector

---

## Support & Resources

**Questions?** Review examples in this document
**Issues?** Check troubleshooting section
**Custom Components?** See extensibility guide

**Key Files:**
- `RuntimeUIBuilder.cs` - Main builder API
- `rvUIContent.cs` - Base content class
- `rvUIContainer.cs` - Base container class
- `rvUIFloatingMenuPanel.cs` - Panel/window implementation
- `rvUIMenuButton.cs` - Button implementation

---

**Last Updated**: 2025-10-21
**Version**: 1.0
**Status**: Production Ready
