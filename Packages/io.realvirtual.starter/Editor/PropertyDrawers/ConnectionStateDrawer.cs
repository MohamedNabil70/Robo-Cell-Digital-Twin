// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  
#if REALVIRTUAL_PROFESSIONAL
using UnityEngine;
using UnityEditor;

namespace realvirtual
{
    /// <summary>
    /// Custom property drawer for connection state strings to show them with appropriate colors
    /// </summary>
    [CustomPropertyDrawer(typeof(ConnectionStateAttribute))]
    public class ConnectionStateDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            string stateText = property.stringValue;
            Color stateColor = GetStateColor(stateText);
            
            // Draw label
            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            EditorGUI.LabelField(labelRect, label);
            
            // Draw colored state text
            Rect valueRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, position.height);
            
            var originalColor = GUI.color;
            GUI.color = stateColor;
            
            var boldStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold
            };
            
            EditorGUI.LabelField(valueRect, stateText, boldStyle);
            
            GUI.color = originalColor;
        }
        
        private Color GetStateColor(string stateText)
        {
            if (string.IsNullOrEmpty(stateText))
                return Color.gray;
                
            if (stateText.Contains("Connected"))
                return Color.green;
            else if (stateText.Contains("Connecting") || stateText.Contains("Reconnecting"))
                return Color.yellow;
            else if (stateText.Contains("Error"))
                return Color.red;
            else if (stateText.Contains("Disconnected") || stateText.Contains("Closing"))
                return Color.gray;
            else
                return Color.white;
        }
    }
}
#endif