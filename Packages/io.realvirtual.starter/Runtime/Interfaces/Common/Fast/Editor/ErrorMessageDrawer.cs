// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace realvirtual
{
    //! Property drawer for ErrorMessage attribute - displays text with red background
    [CustomPropertyDrawer(typeof(ErrorMessageAttribute))]
    public class ErrorMessageDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "ErrorMessage attribute can only be used on string fields");
                return;
            }

            string errorText = property.stringValue;
            
            if (!string.IsNullOrEmpty(errorText))
            {
                // Save the original colors
                Color originalBackgroundColor = GUI.backgroundColor;
                Color originalContentColor = GUI.contentColor;
                
                // Set red background
                GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 1f);
                
                // Create a box style with padding
                GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(4, 4, 4, 4),
                    margin = new RectOffset(0, 0, 0, 0)
                };
                
                // Draw the red background box
                GUI.Box(position, GUIContent.none, boxStyle);
                
                // Create text style with white color
                GUIStyle textStyle = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.white },
                    focused = { textColor = Color.white },
                    hover = { textColor = Color.white },
                    active = { textColor = Color.white },
                    wordWrap = true,
                    richText = false,
                    padding = new RectOffset(6, 6, 4, 4),
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                
                // Draw the error text with "Error:" prefix
                EditorGUI.LabelField(position, "Error: " + errorText, textStyle);
                
                // Restore original colors
                GUI.backgroundColor = originalBackgroundColor;
                GUI.contentColor = originalContentColor;
            }
            else
            {
                // If no error, draw nothing (field should be hidden by ShowIf anyway)
                EditorGUI.LabelField(position, "");
            }
        }
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            string errorText = property.stringValue;
            
            if (string.IsNullOrEmpty(errorText))
                return 0f;
            
            // Calculate height based on text content (similar to ResizableTextArea)
            GUIStyle style = EditorStyles.textArea;
            float width = EditorGUIUtility.currentViewWidth - 20f; // Account for inspector margins
            float height = style.CalcHeight(new GUIContent(errorText), width);
            
            // Ensure minimum height
            return Mathf.Max(height, EditorGUIUtility.singleLineHeight * 2);
        }
    }
}
#endif