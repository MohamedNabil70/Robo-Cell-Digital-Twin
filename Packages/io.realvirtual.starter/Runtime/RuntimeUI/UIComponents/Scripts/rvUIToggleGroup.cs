using UnityEngine.Scripting.APIUpdating;
using System;
using System.Collections.Generic;
using realvirtual;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;



namespace realvirtual
{
    [RequireComponent(typeof(ToggleGroup))]
    [MovedFrom(false, sourceClassName: "rvUIToggleGroup")]
    public class rvUIToggleGroup : MonoBehaviour
    {
        public List<rvUIMenuButton> buttons = new List<rvUIMenuButton>();
    
        public bool allowSwitchOff = false;
        public bool autoassignButtons = false;
    
        public UnityEvent OnActiveToggleChanged = new UnityEvent();
    
        void Start()
        {
            SetupToggleListeners();
        }
    
        public void AddButton(rvUIMenuButton button)
        {
            if (button == null)
            {
                realvirtual.Logger.Warning("Cannot add null button to toggle group", this);
                return;
            }
    
            if (buttons == null)
            {
                buttons = new List<rvUIMenuButton>();
            }
    
            // Add button to list if not already present
            if (!buttons.Contains(button))
            {
                buttons.Add(button);
    
                // Assign button's toggle to this group if auto-assign is enabled
                if (autoassignButtons)
                {
                    Toggle toggle = button.GetComponentInChildren<Toggle>();
                    if (toggle != null)
                    {
                        ToggleGroup tg = GetComponent<ToggleGroup>();
                        toggle.group = tg;
    
                        // Setup listener for this toggle
                        toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
                        toggle.onValueChanged.AddListener(OnToggleValueChanged);
                    }
                }
            }
        }
    
    
        private void OnValidate()
        {
            Refresh();
        }
    
        public void Refresh()
        {
            if (autoassignButtons)
            {
                buttons = new List<rvUIMenuButton>(GetComponentsInChildren<rvUIMenuButton>());
            }
    
    
            ToggleGroup tg = GetComponent<ToggleGroup>();
            tg.allowSwitchOff = allowSwitchOff;
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    Toggle t = button.GetComponentInChildren<Toggle>(true);
                    t.group = tg;
                }
            }
        }
    
        void SetupToggleListeners()
        {
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    Toggle toggle = button.GetComponentInChildren<Toggle>(true);
                    if (toggle != null)
                    {
                        // Remove listener first to avoid duplicates
                        toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
                        toggle.onValueChanged.AddListener(OnToggleValueChanged);
                    }
                }
            }
        }
    
        void OnToggleValueChanged(bool isOn)
        {
            // Only invoke when a toggle is turned ON (becomes active)
            if (isOn)
            {
                OnActiveToggleChanged?.Invoke();
            }
        }
    
        public Toggle GetActiveToggle()
        {
            ToggleGroup tg = GetComponent<ToggleGroup>();
            return tg.GetFirstActiveToggle();
        }
    
        public rvUIMenuButton GetActiveButton()
        {
            Toggle activeToggle = GetActiveToggle();
            if (activeToggle == null) return null;
    
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    Toggle toggle = button.GetComponentInChildren<Toggle>(true);
                    if (toggle == activeToggle)
                    {
                        return button;
                    }
                }
            }
            return null;
        }
    
    }
}
