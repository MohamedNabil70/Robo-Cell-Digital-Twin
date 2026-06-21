// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

//! Adaptive container that can switch between horizontal, vertical, and window panel styles.
//! When switching styles, all content is preserved and moved to the new panel.
//! The header is not copied during style transitions.
//! Works in both edit mode and play mode.

namespace realvirtual
{
    [ExecuteAlways]
    public class rvUIMenuWindow : rvUIContainer
    {
        
        #region Settings Enum
        
        public rvUIMenuSettings settings;
    
        #endregion
        
        
        #region Style Enum
    
        public enum Style
        {
            Horizontal,  //!< Horizontal menu panel
            Vertical,    //!< Vertical menu panel
            Window       //!< Floating window with header
        }
    
        #endregion
    
        #region Inspector Fields
    
        [Header("Style Configuration")]
        public Style currentStyle = Style.Vertical; //!< Current panel style
    
        [Header("Panel References")]
        [SerializeField] [NaughtyAttributes.ReadOnly] private rvUIFloatingMenuPanel currentPanel; //!< Current active panel
    
        [Header("Events")]
        public UnityEvent<Style> OnStyleChanged = new UnityEvent<Style>(); //!< Fired when style changes
    
        #endregion
        
        #region Applying Settings
    
        public void ApplySettingsToPanel(rvUIFloatingMenuPanel panel)
        {
            // Configure header based on settings
            if (settings.showHeader)
            {
                panel.ShowHeader();
                rvUIHeader header = panel.GetHeader();
                header.useKnob = settings.useKnob;
                header.useTitle = settings.useTitle;
                header.useCloseButton = settings.useCloseButton;
                header.SetTitle(settings.title);
                header.Refresh();
                
            }else
            {
                panel.HideHeader();
            }
            
            panel.hideText = !settings.buttonText;
            panel.RefreshLayout();
            
            Place(settings.DefaultPosition);
            
            
            
            
        }
    
        public void Open()
        {
            gameObject.SetActive(true);
        }
        
        public void Close()
        {
            gameObject.SetActive(false);
        }
    
        public void Place(rvUIPanelPlacer.Position position)
        {
            
            rvUIPanelPlacer placer = GetComponent<rvUIPanelPlacer>();
            if (placer != null)
            {
                placer.Place(position);
            }
        }
    
        #endregion
    
        #region Initialization
        
    
        void Start()
        {
            
            
            if (Application.isPlaying)
            {
                Initialize();
                
                Logger.Message($"Start: Notifying children of initial style {currentStyle}", this);
                NotifyChildrenOfStyleChange(currentStyle);
            }
        }
    
    
        /// <summary>
        /// Initializes the window with a specific style.
        /// </summary>
        /// <param name="style">The initial style to use</param>
        public void Initialize()
        {
            
            Style style = settings.MenuStyle;
            
            // If a panel already exists, switch to the new style
            // Otherwise, initialize a new panel
            if (currentPanel != null)
            {
                SwitchStyle(style);
            }
            else
            {
                currentStyle = style;
                InitializePanel(style);
    
                // Notify children after initialization
                if (currentPanel != null)
                {
                    NotifyChildrenOfStyleChange(style);
                }
            }
        }
    
        /// <summary>
        /// Refreshes the current style and notifies all children.
        /// Call this if children are added after initialization.
        /// </summary>
        [NaughtyAttributes.Button("Refresh Style Notifications")]
        public void RefreshStyleNotifications()
        {
            if (currentPanel != null)
            {
                Logger.Message($"Manually refreshing style notifications for {currentStyle}", this);
                NotifyChildrenOfStyleChange(currentStyle);
            }
            else
            {
                Logger.Warning("Cannot refresh - no panel exists", this);
            }
        }
    
        #endregion
    
        #region Style Switching
    
        /// <summary>
        /// Switches to a new panel style, preserving all content and panel properties.
        /// The old panel is destroyed and content is moved to the new panel.
        /// </summary>
        /// <param name="newStyle">The style to switch to</param>
        [Button("Switch Style")]
        public void SwitchStyle(Style newStyle)
        {
            // No change needed
            if (currentStyle == newStyle && currentPanel != null)
            {
                Logger.Message($"Already using {newStyle} style", this);
                return;
            }
    
            Logger.Message($"Switching from {currentStyle} to {newStyle}", this);
    
            // Store old panel reference and its properties
            rvUIFloatingMenuPanel oldPanel = currentPanel;
           
            
    
            // Create new panel
            rvUIFloatingMenuPanel newPanel = CreatePanelForStyle(newStyle);
    
            if (newPanel == null)
            {
                Logger.Error($"Failed to create panel for style {newStyle}", this);
                return;
            }
    
           
    
            // Move content from old panel to new panel (if old panel exists)
            if (oldPanel != null)
            {
                MoveContentBetweenPanels(oldPanel, newPanel);
            }
    
            // Update current references
            currentPanel = newPanel;
            Style oldStyle = currentStyle;
            currentStyle = newStyle;
    
            // Destroy old panel
            if (oldPanel != null)
            {
                DestroyImmediate(oldPanel.gameObject);
            }
    
            // Refresh layout (this will apply hideText to all buttons)
            RefreshLayout();
    
            // Fire event
            OnStyleChanged?.Invoke(newStyle);
    
            // Notify all children that implement IRuntimeWindowStyle
            NotifyChildrenOfStyleChange(newStyle);
    
            Logger.Message($"Style switched from {oldStyle} to {newStyle}", this);
        }
    
        /// <summary>
        /// Switches to horizontal style.
        /// </summary>
        public void SwitchToHorizontal()
        {
            SwitchStyle(Style.Horizontal);
        }
    
        /// <summary>
        /// Switches to vertical style.
        /// </summary>
        public void SwitchToVertical()
        {
            SwitchStyle(Style.Vertical);
        }
    
        /// <summary>
        /// Switches to window style.
        /// </summary>
        public void SwitchToWindow()
        {
            SwitchStyle(Style.Window);
        }
    
        /// <summary>
        /// Notifies all child content that implements IRuntimeWindowStyle about the style change.
        /// Recursively walks through all children and notifies them.
        /// </summary>
        void NotifyChildrenOfStyleChange(Style newStyle)
        {
            if (currentPanel == null)
            {
                Logger.Warning("NotifyChildrenOfStyleChange: currentPanel is null", this);
                return;
            }
    
            // Get all UI content children recursively
            List<rvUIContent> allChildren = GetAllChildrenRecursive(currentPanel);
    
            Logger.Message($"Found {allChildren.Count} total children to check for style change notification", this);
    
            int notifiedCount = 0;
            foreach (var child in allChildren)
            {
                if (child != null)
                {
                    bool implementsInterface = child is IRuntimeWindowStyle;
                    Logger.Message($"  - {child.GetType().Name} on '{child.gameObject.name}': implements IRuntimeWindowStyle = {implementsInterface}", this);
    
                    if (implementsInterface)
                    {
                        IRuntimeWindowStyle styleAware = child as IRuntimeWindowStyle;
                        Logger.Message($"    -> Calling OnWindowStyleChanged({newStyle})", this);
                        styleAware.OnWindowStyleChanged(newStyle);
                        notifiedCount++;
                    }
                }
            }
    
            if (notifiedCount > 0)
            {
                Logger.Message($"Successfully notified {notifiedCount} children of style change to {newStyle}", this);
            }
            else
            {
                Logger.Warning($"No children implementing IRuntimeWindowStyle found! Total children checked: {allChildren.Count}", this);
            }
        }
    
        /// <summary>
        /// Recursively gets all children from a container.
        /// </summary>
        List<rvUIContent> GetAllChildrenRecursive(rvUIContent root)
        {
            List<rvUIContent> allChildren = new List<rvUIContent>();
    
            if (root == null)
            {
                return allChildren;
            }
    
            // Get direct children
            List<rvUIContent> directChildren = root.GetChildContents();
    
            foreach (var child in directChildren)
            {
                if (child != null)
                {
                    allChildren.Add(child);
    
                    // Recursively get children of children
                    List<rvUIContent> grandChildren = GetAllChildrenRecursive(child);
                    allChildren.AddRange(grandChildren);
                }
            }
    
            return allChildren;
        }
    
        #endregion
    
        #region Panel Management
    
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
        /// Initializes the first panel for the window.
        /// </summary>
        void InitializePanel(Style style)
        {
            if (RuntimeUIBuilder.Instance == null)
            {
                Logger.Warning("RuntimeUIBuilder.Instance is null - cannot initialize panel", this);
                return;
            }
    
            currentPanel = CreatePanelForStyle(style);
            currentStyle = style;
    
            if (currentPanel != null)
            {
                Logger.Message($"Initialized panel with {style} style", this);
            }
        }
    
        /// <summary>
        /// Creates a new panel for the specified style using RuntimeUIBuilder prefabs.
        /// </summary>
        rvUIFloatingMenuPanel CreatePanelForStyle(Style style)
        {
            if (RuntimeUIBuilder.Instance == null)
            {
                Logger.Error("RuntimeUIBuilder.Instance is null", this);
                return null;
            }
    
            rvUIFloatingMenuPanel prefab = null;
    
            switch (style)
            {
                case Style.Horizontal:
                    prefab = RuntimeUIBuilder.Instance.EmptyHorizontalPrefab;
                    break;
    
                case Style.Vertical:
                    prefab = RuntimeUIBuilder.Instance.EmptyVerticalPrefab;
                    break;
    
                case Style.Window:
                    prefab = RuntimeUIBuilder.Instance.EmptyWindowPrefab;
                    break;
            }
    
            if (prefab == null)
            {
                Logger.Error($"No prefab assigned for style {style} in RuntimeUIBuilder", this);
                return null;
            }
    
            // Instantiate the panel (works in both edit mode and play mode)
            rvUIFloatingMenuPanel newPanel = InstantiatePrefab(prefab);
    
            // Parent it to this MenuWindow
            RectTransform panelRect = newPanel.GetComponent<RectTransform>();
            panelRect.SetParent(transform, false);
    
            
            
            ApplySettingsToPanel(newPanel);
            
    
            return newPanel;
        }
    
        /// <summary>
        /// Moves all content from old panel to new panel, excluding the header.
        /// </summary>
        void MoveContentBetweenPanels(rvUIFloatingMenuPanel oldPanel, rvUIFloatingMenuPanel newPanel)
        {
            if (oldPanel == null || newPanel == null)
            {
                Logger.Warning("Cannot move content - panel is null", this);
                return;
            }
    
            // Get all content from old panel
            List<rvUIContent> contents = oldPanel.GetUIContents();
    
            Logger.Message($"Moving {contents.Count} content items from old panel to new panel", this);
    
            // Move each content item to the new panel
            foreach (var content in contents)
            {
                if (content != null)
                {
                    content.MoveToContainer(newPanel, false);
                }
            }
    
            Logger.Message($"Content moved successfully", this);
        }
    
        #endregion
    
        #region Container Implementation
    
        public override RectTransform GetContentRoot()
        {
            // Delegate to current panel's content root
            if (currentPanel != null)
            {
                return currentPanel.GetContentRoot();
            }
    
            // Fallback to this component's RectTransform
            return GetComponent<RectTransform>();
        }
    
        public override void RefreshLayout()
        {
            if (currentPanel != null)
            {
                currentPanel.RefreshLayout();
            }
        }
    
        #endregion
    
        #region Utility Methods
    
        /// <summary>
        /// Gets the current panel style.
        /// </summary>
        public Style GetCurrentStyle()
        {
            return currentStyle;
        }
    
        /// <summary>
        /// Gets the current active panel.
        /// </summary>
        public rvUIFloatingMenuPanel GetCurrentPanel()
        {
            return currentPanel;
        }
    
        /// <summary>
        /// Checks if the window is currently in a specific style.
        /// </summary>
        public bool IsStyle(Style style)
        {
            return currentStyle == style;
        }
    
        #endregion
    
        #region Inspector Helpers
    
        [Button("Switch to Horizontal")]
        void EditorSwitchToHorizontal()
        {
            SwitchToHorizontal();
        }
    
        [Button("Switch to Vertical")]
        void EditorSwitchToVertical()
        {
            SwitchToVertical();
        }
    
        [Button("Switch to Window")]
        void EditorSwitchToWindow()
        {
            SwitchToWindow();
        }
    
        #endregion
    
        public void PlaceRelativeTo(rvUIContent other, rvUIRelativePlacement.Placement placement)
        {
            rvUIRelativePlacement placer = GetComponentInChildren<rvUIRelativePlacement>();
            
            if (placer != null)
            {
                placer.PlaceRelativeTo((RectTransform)other.transform, placement, 10f);
            }
        }
    
        public void ClearRelativePlacement()
        {
            rvUIRelativePlacement placer = GetComponentInChildren<rvUIRelativePlacement>();
            
            if (placer != null)
            {
                placer.ClearRelativePlacement();
            }
            
        }
    }
    
}
