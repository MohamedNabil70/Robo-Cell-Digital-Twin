using UnityEngine.Scripting.APIUpdating;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using realvirtual;
using TMPro;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif


/// <summary>
/// Traversal-based UI builder that uses a cursor to navigate and build UI hierarchies.
/// The builder maintains a cursor position in the UI tree and provides methods to:
/// - Navigate the tree (MoveCursor, MovePrevious, MoveNext)
/// - Add content at the cursor position
/// - Refresh layouts from cursor to root (bottom-up)
/// Works in both edit mode and play mode.
/// </summary>

namespace realvirtual
{
    [ExecuteAlways]
    [MovedFrom(false, sourceClassName: "RuntimeUIBuilder")]
    public class RuntimeUIBuilder : MonoBehaviour
    {
        #region Prefab References
    
        [Header("UI Content Prefabs")]
        public rvUIMenuButton ButtonPrefab; //!< Prefab for creating button UI elements
        public rvUIDropdown DropdownPrefab; //!< Prefab for creating dropdown menu UI elements
        public rvUIMenuSpacing SpacingPrefab; //!< Prefab for creating spacing elements between UI components
        public rvUIText TextPrefab; //!< Prefab for creating text label UI elements
        public rvUIToggleDropdown ToggleDropdownPrefab;
        public rvUIGenericInputField InputFieldPrefab;
        public rvUITooltip TooltipPrefab;
        
        
        [Header("UI Container Prefabs")]
        public rvUIMenuWindow WindowPrefab; //!< Prefab for creating menu window containers with title bar and styling
        public rvUIFloatingMenuPanel EmptyWindowPrefab; //!< Prefab for creating empty floating window containers
        public rvUIFloatingMenuPanel EmptyHorizontalPrefab; //!< Prefab for creating horizontal layout containers
        public rvUIFloatingMenuPanel EmptyVerticalPrefab; //!< Prefab for creating vertical layout containers
    
        #endregion
        
        public ColorScheme colorScheme;
    
        private rvUIArea overlayArea;
        private rvUIArea tooltipArea;
    
        #region Singleton
    
        /// <summary>
        /// Singleton instance of the RuntimeUIBuilder. Only one instance should exist in the scene.
        /// </summary>
        public static RuntimeUIBuilder Instance { get; private set; }
    
        void OnEnable()
        {
            // Singleton pattern: ensure only one instance exists
            // OnEnable is called in both edit mode and play mode
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                // In edit mode, prefer to keep the existing instance
                if (!Application.isPlaying)
                {
                    realvirtual.Logger.Warning($"Multiple RuntimeUIBuilder instances detected in edit mode. Using existing instance on {Instance.gameObject.name}", this);
                }
                else
                {
                    realvirtual.Logger.Warning($"Multiple RuntimeUIBuilder instances detected. Destroying duplicate on {gameObject.name}", this);
                    Destroy(this);
                }
            }
        }
    
        void OnDisable()
        {
            // Clear singleton reference if this is the active instance
            if (Instance == this)
            {
                Instance = null;
            }
        }
    
        void OnDestroy()
        {
            // Clear singleton reference if this is the active instance
            if (Instance == this)
            {
                Instance = null;
            }
        }
    
        #endregion
    
        #region Content Types
    
        public enum ContentType
        {
            Text,
            Button,
            Dropdown,
            ToggleDropdown,
            InputField,
            HorizontalMenu,
            VerticalMenu,
            Window,
            MenuWindow,
            SplitMenu,
            CollabsibleMenu,
            Space,
            Tooltip
        }
    
        public enum Orientation
        {
            Horizontal,
            Vertical
        }
    
        #endregion
    
        #region Cursor Management
    
        /// <summary>
        /// Current cursor position in the UI tree. All Add operations insert content at this location.
        /// </summary>
        public rvUIContent cursor;
    
        /// <summary>
        /// Moves the cursor to a specific UI content element.
        /// </summary>
        /// <param name="content">The target UI content to move cursor to</param>
        public void MoveCursorTo(rvUIContent content)
        {
            cursor = content;
        }
    
        /// <summary>
        /// Moves the cursor by n steps in the tree hierarchy.
        /// Negative values move backward (previous siblings/parent), positive values move forward (next siblings/children).
        /// </summary>
        /// <param name="n">Number of steps to move (+ or -)</param>
        public void MoveCursor(int n)
        {
            if (n < 0)
            {
                for (int i = 0; i < -n; i++)
                {
                    MovePrevious(cursor);
                }
            }
            else if (n > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    MoveNext(cursor);
                }
            }
        }
    
        #endregion
    
        #region Tree Navigation
    
        /// <summary>
        /// Moves cursor to previous sibling or parent container.
        /// </summary>
        void MovePrevious(rvUIContent content)
        {
            if (content == null) return;
    
            // Get the parent container
            rvUIContainer container = content.GetContainer();
            if (container == null) return;
    
            // Get all siblings (children of the container)
            List<rvUIContent> siblings = container.GetUIContents();
    
            // Find current content index
            int currentIndex = siblings.IndexOf(content);
    
            if (currentIndex > 0)
            {
                // Move to previous sibling
                cursor = siblings[currentIndex - 1];
            }
            else
            {
                // No previous sibling, move to parent container
                cursor = container;
            }
        }
    
        /// <summary>
        /// Moves cursor to next sibling or first child (depth-first traversal).
        /// </summary>
        void MoveNext(rvUIContent content)
        {
            if (content == null) return;
    
            // Check if content is a container with children
            rvUIContainer container = content as rvUIContainer;
            if (container != null)
            {
                List<rvUIContent> children = container.GetUIContents();
                if (children.Count > 0)
                {
                    // Move to first child
                    cursor = children[0];
                    return;
                }
            }
    
            // No children, try to move to next sibling
            rvUIContainer parentContainer = content.GetContainer();
            if (parentContainer == null) return;
    
            List<rvUIContent> siblings = parentContainer.GetUIContents();
            int currentIndex = siblings.IndexOf(content);
    
            if (currentIndex >= 0 && currentIndex < siblings.Count - 1)
            {
                // Move to next sibling
                cursor = siblings[currentIndex + 1];
            }
            else
            {
                // No next sibling, try to move to parent's next sibling
                MoveToParentNextSibling(parentContainer);
            }
        }
    
        /// <summary>
        /// Helper method to move up the hierarchy to find the next available node.
        /// </summary>
        void MoveToParentNextSibling(rvUIContent content)
        {
            if (content == null) return;
    
            rvUIContainer parentContainer = content.GetContainer();
            if (parentContainer == null) return;
    
            List<rvUIContent> siblings = parentContainer.GetUIContents();
            int currentIndex = siblings.IndexOf(content);
    
            if (currentIndex >= 0 && currentIndex < siblings.Count - 1)
            {
                // Found next sibling at parent level
                cursor = siblings[currentIndex + 1];
            }
            else
            {
                // Continue moving up
                MoveToParentNextSibling(parentContainer);
            }
        }
    
        /// <summary>
        /// Steps into the current container by moving cursor to its first child.
        /// Only works if cursor is a container with children.
        /// </summary>
        public void StepIn()
        {
            if (cursor == null) return;
    
            rvUIContainer container = cursor as rvUIContainer;
            if (container == null) return;
    
            List<rvUIContent> children = container.GetUIContents();
            if (children.Count > 0)
            {
                cursor = children[0];
            }
        }
    
        /// <summary>
        /// Steps out of the current container by moving cursor to the parent container.
        /// </summary>
        public void StepOut()
        {
            if (cursor == null) return;
    
            rvUIContainer parent = cursor.GetContainer();
            if (parent != null)
            {
                cursor = parent;
            }
        }
    
        #endregion
    
        #region Layout Refresh
    
        /// <summary>
        /// Refreshes layout from cursor to root using bottom-up approach.
        /// This ensures layout changes propagate correctly from modified elements up to the root container.
        /// Each node in the path is refreshed individually without re-refreshing children (avoids redundant work).
        /// </summary>
        public void RefreshFromCursor()
        {
            if (cursor == null) return;
    
            List<rvUIContent> pathToRoot = cursor.GetPathToRoot();
    
            // Refresh each node in the path from leaf (cursor) to root
            // Use RefreshLayout() not RefreshLayoutBottomUp() to avoid redundant recursive refreshes
            foreach (var content in pathToRoot)
            {
                if (content != null)
                {
                    content.RefreshLayout();
                }
            }
        }
    
        #endregion
    
        #region Content Creation
    
        /// <summary>
        /// Helper method to instantiate a prefab that works in both edit mode and play mode.
        /// In edit mode, uses PrefabUtility to maintain prefab connections.
        /// In play mode, uses standard Instantiate.
        /// </summary>
        T InstantiatePrefab<T>(T prefab) where T : Object
        {
            if (prefab == null)
            {
                return null;
            }
    
    #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In edit mode, use PrefabUtility to maintain prefab connection
                return PrefabUtility.InstantiatePrefab(prefab) as T;
            }
    #endif
            // In play mode, use standard Instantiate
            return Instantiate(prefab);
        }
    
        /// <summary>
        /// Adds a new UI content element at the cursor position and updates cursor to the new element.
        /// Automatically triggers layout refresh from the new content to root.
        /// Works in both edit mode and play mode.
        /// </summary>
        /// <param name="type">The type of content to create</param>
        public void Add(ContentType type)
        {
            rvUIContent content;
            switch (type)
            {
                case ContentType.Text:
                    content = InstantiatePrefab(TextPrefab);
                    break;
    
                case ContentType.Button:
                    content = InstantiatePrefab(ButtonPrefab);
                    break;
    
                case ContentType.Dropdown:
                    content = InstantiatePrefab(DropdownPrefab);
                    break;
                
                case ContentType.ToggleDropdown:
                    content = InstantiatePrefab(ToggleDropdownPrefab);
                    break;
                
                case ContentType.Tooltip:
                    content = InstantiatePrefab(TooltipPrefab);
                    break;
                
                case ContentType.InputField:
                    content = InstantiatePrefab(InputFieldPrefab);
                    break;
                
                case ContentType.Space:
                    content = InstantiatePrefab(SpacingPrefab);
                    break;
    
                case ContentType.HorizontalMenu:
                    content = InstantiatePrefab(EmptyHorizontalPrefab);
                    break;
    
                case ContentType.VerticalMenu:
                    content = InstantiatePrefab(EmptyVerticalPrefab);
                    break;
    
                case ContentType.Window:
                    content = InstantiatePrefab(EmptyWindowPrefab);
                    break;
    
                case ContentType.MenuWindow:
                    content = InstantiatePrefab(WindowPrefab);
                    break;
    
                case ContentType.SplitMenu:
                    content = InstantiatePrefab(EmptyWindowPrefab);
                    break;
    
                case ContentType.CollabsibleMenu:
                    content = InstantiatePrefab(EmptyWindowPrefab);
                    break;
    
                default:
                    content = InstantiatePrefab(TextPrefab);
                    break;
            }
    
            if (content != null)
            {
                // Determine target container based on cursor type
                rvUIContainer targetContainer = cursor as rvUIContainer;
    
                if (targetContainer == null)
                {
                    // Cursor is not a container, add as sibling in parent container
                    targetContainer = cursor.GetContainer();
                }
                // else: Cursor IS a container, add as child to it
    
                if (targetContainer != null)
                {
                    content.MoveToContainer(targetContainer, false);
                    cursor = content;
    
                    // Fire OnChildAdded event
                    targetContainer.OnChildAdded?.Invoke(content);
    
                    // Refresh layout from the newly added content up to root
                    RefreshFromCursor();
                }
                else
                {
                    realvirtual.Logger.Warning("Cannot add content - cursor has no valid container", this);
                }
            }
        }
    
        public rvUIToggleGroup AddToggleGroup(bool allowSwitchOff = false, bool autoassignButtons = false)
        {
            return AddToggleGroup(cursor, allowSwitchOff, autoassignButtons);
        }
    
        public rvUIToggleGroup AddToggleGroup(rvUIContent content, bool allowSwitchOff = false, bool autoassignButtons = false)
        {
            rvUIToggleGroup tg = content.gameObject.AddComponent<rvUIToggleGroup>();
            tg.allowSwitchOff = allowSwitchOff;
            tg.autoassignButtons = autoassignButtons;
            return tg;
        }
    
        /// <summary>
        /// Clears all content from the container where cursor is located.
        /// If cursor is a container, clears its children. Otherwise, clears siblings in parent container.
        /// After clearing, moves cursor to the container.
        /// </summary>
        public void Clear()
        {
            if (cursor == null) return;
    
            rvUIContainer targetContainer = cursor as rvUIContainer;
            if (targetContainer == null)
            {
                // Cursor is not a container, get its parent
                targetContainer = cursor.GetContainer();
            }
    
            if (targetContainer == null) return;
    
            // Get all children
            List<rvUIContent> children = targetContainer.GetUIContents();
    
            // Destroy all children and fire OnChildRemoved events
            foreach (var child in children)
            {
                if (child != null)
                {
                    targetContainer.OnChildRemoved?.Invoke(child);
                    DestroyImmediate(child.gameObject);
                }
            }
    
            // Move cursor to the now-empty container
            cursor = targetContainer;
    
            // Refresh layout
            RefreshFromCursor();
        }
    
        public enum SubMenuType
        {
            Window,
            Horizontal,
            Vertical
        }
    
        /// <summary>
        /// Creates a submenu (rvUIFloatingMenuPanel) for the current cursor content.
        /// The submenu is placed in the top-level root container and uses rvUIRelativePlacement
        /// to position itself relative to the cursor content.
        /// </summary>
        /// <param name="subMenuType">The type of submenu panel to create</param>
        /// <param name="placement">The relative placement type (Above, Below, Left, Right, Horizontal, Vertical)</param>
        /// <param name="margin">The margin distance from the target</param>
        /// <returns>The created submenu container, or null if creation failed</returns>
        public rvUIFloatingMenuPanel AddSubMenu(SubMenuType subMenuType, realvirtual.rvUIRelativePlacement.Placement placement, float margin = 10f)
        {
            if (cursor == null)
            {
                realvirtual.Logger.Warning("Cannot create submenu - no cursor set", this);
                return null;
            }
    
            // Find the root container (top-level container)
            rvUIContainer rootContainer = GetArea(rvUIArea.Area.Overlay);
            if (rootContainer == null)
            {
                realvirtual.Logger.Warning("Cannot create submenu - no root container found", this);
                return null;
            }
            
    
            // Get the RectTransform of the cursor content (this will be the target)
            RectTransform targetRect = cursor.GetComponent<RectTransform>();
            if (targetRect == null)
            {
                realvirtual.Logger.Warning("Cannot create submenu - cursor has no RectTransform", this);
                return null;
            }
    
            // Select the appropriate prefab based on type
            rvUIFloatingMenuPanel prefabToUse = null;
            switch (subMenuType)
            {
                case SubMenuType.Window:
                    prefabToUse = EmptyWindowPrefab;
                    break;
                case SubMenuType.Horizontal:
                    prefabToUse = EmptyHorizontalPrefab;
                    break;
                case SubMenuType.Vertical:
                    prefabToUse = EmptyVerticalPrefab;
                    break;
            }
    
            if (prefabToUse == null)
            {
                realvirtual.Logger.Warning($"No prefab assigned for submenu type {subMenuType}", this);
                return null;
            }
    
            // Create a new submenu panel (works in both edit mode and play mode)
            rvUIFloatingMenuPanel submenu = InstantiatePrefab(prefabToUse);
            if (submenu == null)
            {
                realvirtual.Logger.Warning("Failed to instantiate submenu prefab", this);
                return null;
            }
    
            // Place submenu in the root container
            submenu.MoveToContainer(rootContainer, false);
    
            submenu.SetRelativePlacement(rvUIRelativePlacement.Placement.Below, targetRect, margin);
    
            // Move cursor to the new submenu
            cursor = submenu;
    
            // Refresh layout
            RefreshFromCursor();
    
            return submenu;
        }
    
        void InitAreas()
        {
            if (overlayArea == null)
            {
                rvUIArea[] areas = FindObjectsByType<rvUIArea>(FindObjectsSortMode.None);
                foreach (var a in areas)
                {
                    if (a.area == rvUIArea.Area.Overlay)
                    {
                        overlayArea = a;
                    }
                    else if (a.area == rvUIArea.Area.Tooltip)
                    {
                        tooltipArea = a;
                    }
                    
                }
            }
    
        }
    
    
        rvUIContainer GetArea(rvUIArea.Area area)
        {
            InitAreas();
            switch (area)
            {
                case rvUIArea.Area.Overlay:
                    return overlayArea;
                default:
                    return null;
            }
        }
    
        /// <summary>
        /// Gets the root (top-level) container in the hierarchy.
        /// </summary>
        /// <returns>The root container, or null if not found</returns>
        rvUIContainer GetRootContainer()
        {
            if (cursor == null) return null;
    
            // Get path to root
            List<rvUIContent> path = cursor.GetPathToRoot();
    
            // The last element in the path is the root
            if (path.Count > 0)
            {
                // Find the root container (last container in the path)
                for (int i = path.Count - 1; i >= 0; i--)
                {
                    rvUIContainer container = path[i] as rvUIContainer;
                    if (container != null)
                    {
                        return container;
                    }
                }
            }
    
            return null;
        }
    
        #endregion
    
        #region Fluent Helper Methods
    
        /// <summary>
        /// Creates a button with the specified text and returns it for event wiring.
        /// </summary>
        /// <param name="text">The button text</param>
        /// <returns>The created button component</returns>
        public rvUIMenuButton AddButton(string text)
        {
            Add(ContentType.Button);
            rvUIMenuButton button = cursor as rvUIMenuButton;
            if (button != null)
            {
                button.text = text;
                button.RefreshLayout();
            }
            return button;
        }
        
        public rvUIDropdown AddDropdown()
        {
            Add(ContentType.Dropdown);
            rvUIDropdown dropdown = cursor as rvUIDropdown;
            return dropdown;
        }
    
        public rvUITooltip AddTooltip(string text, RectTransform parent, rvUIRelativePlacement.Placement placement)
        {
            rvUIContent originalcursor = cursor;
            MoveCursorTo(tooltipArea);
            Add(ContentType.Tooltip);
            rvUITooltip tooltip = cursor as rvUITooltip;
            tooltip.Init(text, parent, placement);
            cursor = originalcursor;
            return tooltip;
        }
        
        public rvUIToggleDropdown AddToggleDropdown()
        {
            Add(ContentType.ToggleDropdown);
            rvUIToggleDropdown toggleDropdown = cursor as rvUIToggleDropdown;
            return toggleDropdown;
        }
        
        public rvUIGenericInputField AddInputField(TMP_InputField.ContentType contentType)
        {
            Add(ContentType.InputField);
            rvUIGenericInputField inputField = cursor as rvUIGenericInputField;
            inputField.inputField.contentType = contentType;
            return inputField;
        }
    
        public rvUIMenuSpacing AddSpace()
        {
            Add(ContentType.Space);
            rvUIMenuSpacing spacing = cursor as rvUIMenuSpacing;
            return spacing;
            
        }
    
        /// <summary>
        /// Creates a button with the specified text and icon and returns it for event wiring.
        /// </summary>
        /// <param name="text">The button text</param>
        /// <param name="icon">The button icon sprite</param>
        /// <returns>The created button component</returns>
        public rvUIMenuButton AddButton(string text, Sprite icon, rvUIToggleGroup toggleGroup = null)
        {
            Add(ContentType.Button);
            rvUIMenuButton button = cursor as rvUIMenuButton;
            if (button != null)
            {
                button.text = text;
                if (icon != null)
                {
                    button.SetSpriteIcon(icon);
                }
                button.RefreshLayout();
    
                if (toggleGroup != null)
                {
                    toggleGroup.AddButton(button);
                }
            }
            return button;
        }
        
        public rvUIMenuButton AddButton(string text, string materialIcon, rvUIToggleGroup toggleGroup = null)
        {
            Add(ContentType.Button);
            rvUIMenuButton button = cursor as rvUIMenuButton;
            if (button != null)
            {
                button.text = text;
                if (materialIcon != null)
                {
                    button.SetMaterialIcon(materialIcon);
                }
                button.RefreshLayout();
                
                if (toggleGroup != null)
                {
                    toggleGroup.AddButton(button);
                }
            }
            return button;
        }
    
        /// <summary>
        /// Creates a text element with the specified content and returns it.
        /// </summary>
        /// <param name="text">The text content</param>
        /// <returns>The created text component</returns>
        public rvUIText AddText(string text, bool keepRefreshing = true)
        {
            Add(ContentType.Text);
            rvUIText textComponent = cursor as rvUIText;
            if (textComponent != null)
            {
                textComponent.SetText(text);
                textComponent.updateInRuntime = keepRefreshing;
            }
            return textComponent;
        }
    
        /// <summary>
        /// Creates a container and returns it for adding children.
        /// </summary>
        /// <param name="type">The type of container to create</param>
        /// <returns>The created container</returns>
        public rvUIContainer AddContainer(ContentType type)
        {
            Add(type);
            return cursor as rvUIContainer;
        }
    
        public rvUIFloatingMenuPanel AddVerticalBox()
        {
            Add(ContentType.VerticalMenu);
            rvUIFloatingMenuPanel panel = cursor as rvUIFloatingMenuPanel;
            
            panel.HideHeader();
            
            return panel;
        }
        
        public rvUIFloatingMenuPanel AddHorizontalBox()
        {
            Add(ContentType.HorizontalMenu);
            rvUIFloatingMenuPanel panel = cursor as rvUIFloatingMenuPanel;
            
            panel.HideHeader();
            
            return panel;
        }
    
        /// <summary>
        /// Creates a generic enum field with button group for selecting values.
        /// Returns EnumField for event wiring and value management.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <param name="label">Label text</param>
        /// <param name="initialValue">Initial enum value</param>
        /// <param name="useHorizontalLayout">True for horizontal layout, false for vertical</param>
        /// <returns>The created EnumField instance</returns>
        public EnumField<T> AddEnumField<T>(string label, T initialValue, bool useHorizontalLayout = true) where T : System.Enum
        {
            EnumField<T> enumField = new EnumField<T>(label, initialValue);
            enumField.UseHorizontalLayout = useHorizontalLayout;
            enumField.BuildUI(this);
            return enumField;
        }
    
        /// <summary>
        /// Creates an adaptive menu window that can switch between horizontal, vertical, and window styles.
        /// Content is preserved when switching styles.
        /// </summary>
        /// <param name="initialStyle">The initial style for the window</param>
        /// <returns>The created rvUIMenuWindow instance</returns>
        public rvUIMenuWindow AddMenuWindow(rvUIMenuSettings settings)
        {
            Add(ContentType.MenuWindow);
            rvUIMenuWindow menuWindow = cursor as rvUIMenuWindow;
            
            menuWindow.settings = settings;
            menuWindow.Initialize();
            
            
            return menuWindow;
        }
    
        #endregion
    
        
    }
}
