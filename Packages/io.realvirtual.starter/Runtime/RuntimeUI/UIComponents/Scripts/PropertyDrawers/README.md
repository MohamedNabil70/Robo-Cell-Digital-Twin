# Property Drawer System (REMOVED)

The experimental runtime property drawer system has been removed from RuntimeUI to keep the system simple and focused.

## Migration

If you need to create custom inspectors:

### Option 1: Build UI Directly (Recommended)
Use RuntimeUIBuilder directly to create your custom UI:

```csharp
public class MyComponent : MonoBehaviour
{
    public bool myProperty = true;

    void Start()
    {
        BuildUI();
    }

    void BuildUI()
    {
        var builder = RuntimeUIBuilder.Instance;

        var window = builder.AddContainer(RuntimeUIBuilder.ContentType.Window);
        builder.StepIn();

        builder.AddText("My Property");

        var toggleBtn = builder.AddButton(myProperty ? "True" : "False");
        toggleBtn.OnClick.AddListener((isOn) => {
            myProperty = !myProperty;
            toggleBtn.SetText(myProperty ? "True" : "False");
        });

        builder.StepOut();
    }
}
```

### Option 2: Create Custom Components
Extend `rvUIContent` or `rvUIContainer` for reusable components:

```csharp
public class rvUIPropertyField : rvUIContainer
{
    public object target;
    public string propertyName;

    public void Initialize(object target, string propertyName)
    {
        this.target = target;
        this.propertyName = propertyName;
        BuildUI();
    }

    void BuildUI()
    {
        // Custom implementation
    }

    public override RectTransform GetContentRoot()
    {
        return GetComponent<RectTransform>();
    }

    public override void RefreshLayout()
    {
        // Refresh implementation
    }
}
```

## See Also

- **`RuntimeUI_System_Overview.md`** - Complete RuntimeUI documentation
- **`RuntimeUI_API.md`** - API quick reference
- **`rvUIContent.cs`** - Base class for custom components

---

**Note**: The files `QUICKSTART.md.DEPRECATED` and `README.md.DEPRECATED` contain old documentation for the removed system.
