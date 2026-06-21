// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEditor;
using UnityEngine;

namespace realvirtual
{

[CustomEditor(typeof(rvUIIcon))]
public class rvUIIconEditor : Editor
{
    private rvUIIcon iconComponent;

    void OnEnable()
    {
        iconComponent = (rvUIIcon)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw color field
        EditorGUILayout.PropertyField(serializedObject.FindProperty("color"));

        // Draw mode field
        EditorGUILayout.PropertyField(serializedObject.FindProperty("mode"));

        // Draw fields conditionally based on mode
        if (iconComponent.mode == rvUIIcon.Mode.Sprite)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sprite"));
        }
        else if (iconComponent.mode == rvUIIcon.Mode.MaterialIcon)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

            EditorGUILayout.Space(10);


            // Button to open Material Icon Browser
            if (GUILayout.Button("Open Material Icon Browser", GUILayout.Height(30)))
            {
                OpenMaterialIconBrowser();
            }

            // Display current icon if unicode is set
            if (!string.IsNullOrEmpty(iconComponent.icon))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"Current Unicode: {iconComponent.icon}", MessageType.Info);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OpenMaterialIconBrowser()
    {
        MaterialIconBrowser.ShowWindow((unicode) =>
        {
            // Callback when icon is double-clicked in browser
            Undo.RecordObject(iconComponent, "Change Material Icon");
            iconComponent.icon = unicode;
            iconComponent.Apply();
            EditorUtility.SetDirty(iconComponent);
        });
    }
} // class rvUIIconEditor

} // namespace realvirtual
