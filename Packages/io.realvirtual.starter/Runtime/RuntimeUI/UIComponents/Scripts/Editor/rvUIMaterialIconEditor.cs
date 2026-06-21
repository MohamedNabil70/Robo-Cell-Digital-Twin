// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEditor;
using UnityEngine;

namespace realvirtual
{

[CustomEditor(typeof(rvUIMaterialIcon))]
public class rvUIMaterialIconEditor : Editor
{
    private rvUIMaterialIcon iconComponent;

    void OnEnable()
    {
        iconComponent = (rvUIMaterialIcon)target;
    }

    public override void OnInspectorGUI()
    {
        // Draw default inspector for basic fields
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // Button to open Material Icon Browser
        if (GUILayout.Button("Open Material Icon Browser", GUILayout.Height(30)))
        {
            OpenMaterialIconBrowser();
        }

        // Display current icon if unicode is set
        if (!string.IsNullOrEmpty(iconComponent.unicode))
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox($"Current Unicode: {iconComponent.unicode}", MessageType.Info);
        }
    }

    private void OpenMaterialIconBrowser()
    {
        MaterialIconBrowser.ShowWindow((unicode) =>
        {
            // Callback when icon is double-clicked in browser
            Undo.RecordObject(iconComponent, "Change Material Icon");
            iconComponent.SetIconByUnicode(unicode);
            EditorUtility.SetDirty(iconComponent);
        });
    }
} // class rvUIMaterialIconEditor

} // namespace realvirtual
