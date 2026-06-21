// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Collections.Generic;
using realvirtual;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

//! Dropdown selector component for RuntimeUI.
//! Displays a button that opens a menu of selectable options.
//! Supports icons and text for each dropdown element.
//! Automatically adjusts dropdown panel placement when parent window style changes.

namespace realvirtual
{
    public class rvUIDropdown : rvUIContainer, IRuntimeWindowStyle
    {
        [Header("Dropdown Configuration")]
        public string title = "Dropdown"; //!< Title text for the dropdown button
        public bool changeIcon = true; //!< Update button icon to match selected item
        public bool changeText = true; //!< Update button text to match selected item
    
        [Header("Dropdown Elements")]
        public List<DropdownElement> elements = new List<DropdownElement>(); //!< List of dropdown options
    
        [Header("References")]
        [SerializeField] private RectTransform contentRoot; //!< Root transform where dropdown panel is placed
        [SerializeField] private rvUIMenuButton mainButton; //!< Main dropdown button
        [SerializeField] private rvUIFloatingMenuPanel dropdownPanel; //!< Panel containing dropdown options
    
        [Header("State")]
        [NaughtyAttributes.ReadOnly] public int selectedIndex = -1; //!< Currently selected element index
        [NaughtyAttributes.ReadOnly] public string selectedValue; //!< Currently selected element text
    
        [Header("Events")]
        public UnityEvent<string> OnElementSelected = new UnityEvent<string>(); //!< Fired when an element is selected
        public UnityEvent<int> OnIndexChanged = new UnityEvent<int>(); //!< Fired when selection index changes
    
        private rvUIToggleGroup toggleGroup;
        [ReadOnly] [SerializeField] private List<rvUIMenuButton> optionButtons = new List<rvUIMenuButton>();
    
        #region Initialization
    
        void Start()
        {
            // Auto-initialize at runtime only
            if (Application.isPlaying && elements != null && elements.Count > 0)
            {
                Initialize();
            }
        }
    
        /// <summary>
        /// Initializes the dropdown with current elements and creates the UI.
        /// Call this manually in edit mode, or it will be called automatically at Start() in play mode.
        /// </summary>
        [NaughtyAttributes.Button("Rebuild Dropdown")]
        public void Initialize()
        {
            if (RuntimeUIBuilder.Instance == null)
            {
                realvirtual.Logger.Warning("RuntimeUIBuilder.Instance is null - cannot initialize dropdown", this);
                return;
            }
    
    #if UNITY_EDITOR
            // Don't initialize if we're editing a prefab asset directly
            if (!Application.isPlaying)
            {
                var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject);
                bool isInPrefabAsset = stage == null && UnityEditor.AssetDatabase.Contains(gameObject);
    
                if (isInPrefabAsset)
                {
                    realvirtual.Logger.Warning("Cannot initialize dropdown in prefab asset mode - add to scene first", this);
                    return;
                }
            }
    #endif
    
            CreateDropdownUI();
    
            // Select first element by default if none selected
            if (selectedIndex < 0 && elements.Count > 0)
            {
                SelectElement(0);
            }
        }
    
        #endregion
    
        #region UI Building
    
        void CreateDropdownUI()
        {
            // Clear existing buttons
            ClearOptionButtons();
    
            // Find or get main button
            if (mainButton == null)
            {
                mainButton = GetComponent<rvUIMenuButton>();
            }
    
            if (mainButton == null)
            {
                realvirtual.Logger.Warning("No rvUIMenuButton found on rvUIDropdown", this);
                return;
            }
    
            // Set main button properties
            if (!string.IsNullOrEmpty(title))
            {
                mainButton.SetText(title);
            }
    
            // Wire up main button to toggle dropdown
            mainButton.OnClick.RemoveListener(ToggleDropdown);
            mainButton.OnClick.AddListener(ToggleDropdown);
    
            // Find or create dropdown panel
            if (dropdownPanel == null)
            {
                dropdownPanel = GetComponentInChildren<rvUIFloatingMenuPanel>(true);
            }
    
            if (dropdownPanel == null)
            {
                realvirtual.Logger.Warning("No rvUIFloatingMenuPanel found in rvUIDropdown children", this);
                return;
            }
    
            // Get or create toggle group on the dropdownPanel itself
            toggleGroup = dropdownPanel.GetComponent<rvUIToggleGroup>();
            // Configure toggle group settings
            toggleGroup.allowSwitchOff = false;
            toggleGroup.autoassignButtons = true;
    
           
    
            // Create option buttons
            RuntimeUIBuilder.Instance.MoveCursorTo(dropdownPanel);
            RuntimeUIBuilder.Instance.StepIn();
    
            for (int i = 0; i < elements.Count; i++)
            {
                DropdownElement element = elements[i];
                if (element == null) continue;
    
                // Create button for this option
                rvUIMenuButton optionButton = RuntimeUIBuilder.Instance.AddButton(element.text);
               
                if (element.icon != null)
                { 
                    optionButton.SetSpriteIcon(element.icon);
                }
                if(!string.IsNullOrEmpty(element.materialIcon))
                {
                    optionButton.SetMaterialIcon(element.materialIcon);
                }
    
                // Set text explicitly to ensure refresh
                optionButton.SetText(element.text);
                optionButton.hideText = false;
    
                // Wire up selection event
                int index = i; // Capture for closure
                optionButton.OnToggleOn.AddListener(() => OnOptionSelected(index));
                
    
                // Refresh button layout first (this sets up button's internal listeners)
                optionButton.RefreshLayout();
                    
                optionButtons.Add(optionButton);
                
            }
            
            
    
            RuntimeUIBuilder.Instance.StepOut();
    
            toggleGroup.Refresh();
    
            RefreshLayout();
        }
    
        void ClearOptionButtons()
        {
            // Remove listeners
            foreach (var btn in optionButtons)
            {
                if (btn != null)
                {
                    btn.OnToggleOn.RemoveAllListeners();
                }
            }
    
            // Destroy buttons
            foreach (var btn in optionButtons)
            {
                if (btn != null)
                {
                    // Use appropriate destroy method based on mode
                    if (Application.isPlaying)
                    {
                        Destroy(btn.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(btn.gameObject);
                    }
                }
            }
    
            optionButtons.Clear();
        }
    
        #endregion
    
        #region Selection
    
        /// <summary>
        /// Selects a dropdown element by index.
        /// </summary>
        public void SelectElement(int index)
        {
            if (index < 0 || index >= elements.Count)
            {
                realvirtual.Logger.Warning($"Invalid dropdown index: {index}", this);
                return;
            }
    
            selectedIndex = index;
            selectedValue = elements[index].text;
    
            // Update option button states
            if (index < optionButtons.Count && optionButtons[index] != null)
            {
                optionButtons[index].ToggleOn();
            }
    
            // Update main button appearance
            UpdateMainButton();
    
            // Fire events
            OnElementSelected?.Invoke(selectedValue);
            OnIndexChanged?.Invoke(selectedIndex);
        }
    
        /// <summary>
        /// Selects a dropdown element by text value.
        /// </summary>
        public void SelectElement(string text)
        {
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].text == text)
                {
                    SelectElement(i);
                    return;
                }
            }
            realvirtual.Logger.Warning($"Dropdown element not found: {text}", this);
        }
    
        void OnOptionSelected(int index)
        {
            SelectElement(index);
    
            // Invoke action if present
            if (index >= 0 && index < elements.Count && elements[index].action != null)
            {
                elements[index].action.Invoke();
            }
    
            // Hide dropdown panel after selection
            HideDropdown();
        }
    
        void UpdateMainButton()
        {
            if (mainButton == null || selectedIndex < 0 || selectedIndex >= elements.Count)
                return;
    
            DropdownElement selected = elements[selectedIndex];
    
            // Update text
            if (changeText && !string.IsNullOrEmpty(selected.text))
            {
                mainButton.SetText(selected.text);
            }
    
            // Update icon
            if (changeIcon)
            {
                mainButton.icon.CopyFromOther(optionButtons[selectedIndex].icon);
            }
        }
    
        #endregion
    
        #region Element Management
    
        /// <summary>
        /// Adds a new dropdown element.
        /// </summary>
        public void AddElement(string text, Sprite icon = null)
        {
            elements.Add(new DropdownElement { text = text, icon = icon });
        }
    
        /// <summary>
        /// Adds a new dropdown element with an action callback.
        /// </summary>
        /// <param name="name">Display text for the element</param>
        /// <param name="icon">Optional icon sprite</param>
        /// <param name="action">Action to invoke when element is selected</param>
        public void AddElement(string name, Sprite icon, Action action)
        {
            elements.Add(new DropdownElement(name, icon, action));
        }
        
        public void AddElement(string name, string icon, Action action)
        {
            DropdownElement ele = new DropdownElement();
            ele.text = name;
            ele.materialIcon = icon;
            ele.action = action;
            ele.icon = null;
            elements.Add(ele);
        }
    
        /// <summary>
        /// Removes an element by index.
        /// </summary>
        public void RemoveElement(int index)
        {
            if (index >= 0 && index < elements.Count)
            {
                elements.RemoveAt(index);
    
                // Adjust selected index if needed
                if (selectedIndex == index)
                {
                    selectedIndex = -1;
                    selectedValue = null;
                }
                else if (selectedIndex > index)
                {
                    selectedIndex--;
                }
            }
        }
    
        /// <summary>
        /// Clears all dropdown elements.
        /// </summary>
        [NaughtyAttributes.Button("Clear All Elements")]
        public void ClearElements()
        {
            elements.Clear();
            selectedIndex = -1;
            selectedValue = null;
            ClearOptionButtons();
        }
    
        /// <summary>
        /// Rebuilds the dropdown UI after modifying elements.
        /// Same as Initialize() but provided for clarity.
        /// </summary>
        public void Rebuild()
        {
            Initialize();
        }
    
        #endregion
    
        #region Layout and Display
    
        public override void RefreshLayout()
        {
            //UpdateMainButton();
    
            // Mark layout for rebuild (deferred, safer than immediate)
            //LayoutRebuilder.MarkLayoutForRebuild(GetComponent<RectTransform>());
        }
    
        public override RectTransform GetContentRoot()
        {
            if (contentRoot != null)
                return contentRoot;
    
            // Try to find dropdown panel as content root
            if (dropdownPanel != null)
                return dropdownPanel.GetContentRoot();
    
            // Fallback to this component's RectTransform
            return GetComponent<RectTransform>();
        }
    
        /// <summary>
        /// Shows the dropdown panel.
        /// </summary>
        public void ShowDropdown()
        {
            if (dropdownPanel != null)
            {
                dropdownPanel.gameObject.SetActive(true);
            }
            SetMainButtonToggleState(true);
        }
    
        /// <summary>
        /// Hides the dropdown panel.
        /// </summary>
        public void HideDropdown()
        {
            if (dropdownPanel != null)
            {
                dropdownPanel.gameObject.SetActive(false);
            }
            SetMainButtonToggleState(false);
        }
    
        /// <summary>
        /// Toggles the dropdown panel visibility.
        /// </summary>
        public void ToggleDropdown()
        {
            if (dropdownPanel != null)
            {
                bool isVisible = dropdownPanel.gameObject.activeSelf;
                dropdownPanel.gameObject.SetActive(!isVisible);
                SetMainButtonToggleState(!isVisible);
            }
        }
    
        /// <summary>
        /// Sets the main button toggle state without triggering OnClick events.
        /// </summary>
        void SetMainButtonToggleState(bool isOn)
        {
            if (mainButton == null) return;
    
            // Set isOn directly to avoid triggering OnValueChanged which calls OnClick
            if (isOn)
            {
                mainButton.ToggleOn();
            }else{
                mainButton.ToggleOff();
            }
    
            // Update toggle component directly without notifications
            Toggle toggle = mainButton.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(isOn);
            }
    
            // Refresh visuals manually
            Image targetGraphic = mainButton.GetComponent<Image>();
            if (targetGraphic != null)
            {
                targetGraphic.color = mainButton.GetCurrentColor();
            }
        }
    
        #endregion
    
        #region Helper Methods
    
        /// <summary>
        /// Gets the currently selected element.
        /// </summary>
        public DropdownElement GetSelectedElement()
        {
            if (selectedIndex >= 0 && selectedIndex < elements.Count)
            {
                return elements[selectedIndex];
            }
            return null;
        }
    
        /// <summary>
        /// Gets the number of elements in the dropdown.
        /// </summary>
        public int GetElementCount()
        {
            return elements != null ? elements.Count : 0;
        }
    
        #endregion
    
        #region IRuntimeWindowStyle Implementation
    
        /// <summary>
        /// Called when the parent window's style changes.
        /// Adjusts the dropdown panel's relative placement based on the new window style.
        /// IMPLEMENTS IRuntimeWindowStyle::OnWindowStyleChanged
        /// </summary>
        /// <param name="newStyle">The new window style</param>
        public void OnWindowStyleChanged(rvUIMenuWindow.Style newStyle)
        {
            realvirtual.Logger.Message($"rvUIDropdown.OnWindowStyleChanged called with style: {newStyle}", this);
    
            if (dropdownPanel == null)
            {
                realvirtual.Logger.Warning("OnWindowStyleChanged: dropdownPanel is null", this);
                return;
            }
    
            // Adjust placement based on window style
            switch (newStyle)
            {
                case rvUIMenuWindow.Style.Horizontal:
                    dropdownPanel.SetRelativePlacement(rvUIRelativePlacement.Placement.Vertical);
                    realvirtual.Logger.Message("Dropdown adjusted for horizontal layout (Vertical)", this);
                    break;
    
                case rvUIMenuWindow.Style.Vertical:
                    dropdownPanel.SetRelativePlacement(rvUIRelativePlacement.Placement.Horizontal);
                    realvirtual.Logger.Message("Dropdown adjusted for vertical layout (Horizontal)", this);
                    break;
    
                case rvUIMenuWindow.Style.Window:
                    dropdownPanel.SetRelativePlacement(rvUIRelativePlacement.Placement.Below);
                    realvirtual.Logger.Message("Dropdown adjusted for window layout (Below)", this);
                    break;
            }
    
          
        }
    
        #endregion
    
        #region Data Class
    
        [Serializable]
        public class DropdownElement
        {
            public string text; //!< Display text for the element
            public Sprite icon; //!< Optional icon sprite
            [NonSerialized] public Action action; //!< Optional action callback invoked when element is selected
            public string materialIcon;
    
            public DropdownElement() { }
    
            public DropdownElement(string text, Sprite icon = null, Action action = null)
            {
                this.text = text;
                this.icon = icon;
                this.action = action;
            }
        }
    
        #endregion
    }
}
