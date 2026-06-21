using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Generic enum field component for RuntimeUI.
/// Creates a button group for selecting enum values with type safety and events.
/// </summary>
/// <typeparam name="T">Enum type to display</typeparam>

namespace realvirtual
{
    public class EnumField<T> where T : System.Enum
    {
        #region Properties
    
        /// <summary>
        /// Current enum value
        /// </summary>
        public T Value { get; private set; }
    
        /// <summary>
        /// Label text displayed above the enum buttons
        /// </summary>
        public string Label { get; set; }
    
        /// <summary>
        /// Container holding the enum field UI
        /// </summary>
        public rvUIContainer Container { get; private set; }
    
        /// <summary>
        /// Event fired when the enum value changes
        /// </summary>
        public event Action<T> OnValueChanged;
    
        /// <summary>
        /// Whether to use horizontal or vertical layout for buttons
        /// </summary>
        public bool UseHorizontalLayout { get; set; } = true;
    
        #endregion
    
        #region Private Fields
    
        private RuntimeUIBuilder builder;
        private rvUIMenuButton[] buttons;
        private T[] enumValues;
    
        #endregion
    
        #region Constructor
    
        /// <summary>
        /// Creates a new EnumField with the specified initial value
        /// </summary>
        /// <param name="label">Label text</param>
        /// <param name="initialValue">Initial enum value</param>
        public EnumField(string label, T initialValue)
        {
            Label = label;
            Value = initialValue;
            enumValues = (T[])Enum.GetValues(typeof(T));
        }
    
        #endregion
    
        #region UI Building
    
        /// <summary>
        /// Builds the UI for this enum field using the provided builder
        /// </summary>
        /// <param name="builder">RuntimeUIBuilder to use</param>
        public void BuildUI(RuntimeUIBuilder builder)
        {
            this.builder = builder;
    
            // Add label if provided
            if (!string.IsNullOrEmpty(Label))
            {
                builder.AddText(Label);
            }
    
            // Create container for buttons
            var containerType = UseHorizontalLayout
                ? RuntimeUIBuilder.ContentType.HorizontalMenu
                : RuntimeUIBuilder.ContentType.VerticalMenu;
    
            Container = builder.AddContainer(containerType);
            builder.StepIn();
    
            // Create buttons for each enum value
            buttons = new rvUIMenuButton[enumValues.Length];
            for (int i = 0; i < enumValues.Length; i++)
            {
                T enumValue = enumValues[i];
                string enumName = enumValue.ToString();
    
                var button = builder.AddButton(enumName);
                buttons[i] = button;
    
                // Set initial state
    
                // Wire up click event
                int index = i; // Capture for closure
                button.OnClick.AddListener(() => OnButtonClicked(index));
            }
    
            builder.StepOut();
    
            // Refresh button states
            RefreshButtonStates();
        }
    
        #endregion
    
        #region Value Management
    
        /// <summary>
        /// Sets the enum value and updates the UI
        /// </summary>
        /// <param name="newValue">New enum value</param>
        public void SetValue(T newValue)
        {
            if (!EqualityComparer<T>.Default.Equals(Value, newValue))
            {
                Value = newValue;
                RefreshButtonStates();
                OnValueChanged?.Invoke(newValue);
            }
        }
    
        /// <summary>
        /// Gets the current enum value
        /// </summary>
        public T GetValue()
        {
            return Value;
        }
    
        #endregion
    
        #region Private Methods
    
        private void OnButtonClicked(int buttonIndex)
        {
            T newValue = enumValues[buttonIndex];
            SetValue(newValue);
        }
    
        private void RefreshButtonStates()
        {
            if (buttons == null) return;
    
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    bool isSelected = EqualityComparer<T>.Default.Equals(enumValues[i], Value);
                    buttons[i].RefreshLayout();
                }
            }
        }
    
        #endregion
    }
}
