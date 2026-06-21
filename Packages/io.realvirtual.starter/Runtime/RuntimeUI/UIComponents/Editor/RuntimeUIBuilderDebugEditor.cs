#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace realvirtual
{

[CustomEditor(typeof(RuntimeUIBuilderDebug))]
public class RuntimeUIBuilderDebugEditor : Editor
{
    private RuntimeUIBuilderDebug debugComponent;
    private rvUIRelativePlacement.Placement subMenuPlacement = rvUIRelativePlacement.Placement.Right;
    private float subMenuMargin = 10f;

    void OnEnable()
    {
        debugComponent = (RuntimeUIBuilderDebug)target;
    }

    public override void OnInspectorGUI()
    {
        // Draw default inspector for basic fields
        DrawDefaultInspector();

        if (debugComponent.builder == null)
        {
            EditorGUILayout.HelpBox("Assign a RuntimeUIBuilder to enable debug controls", MessageType.Warning);
            return;
        }

        if (debugComponent.builder.cursor == null)
        {
            EditorGUILayout.HelpBox("RuntimeUIBuilder has no cursor set", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(10);
        DrawSeparator();

        // Cursor Info
        DrawCursorInfo();

        EditorGUILayout.Space(10);
        DrawSeparator();

        // Simple Navigation
        DrawSimpleNavigation();

        EditorGUILayout.Space(10);
        DrawSeparator();

        // Add Content
        DrawAddContent();

        EditorGUILayout.Space(10);
        DrawSeparator();

        // SubMenu
        DrawSubMenuControls();

        EditorGUILayout.Space(10);
        DrawSeparator();

        // Clear
        DrawClearButton();
    }

    void DrawSeparator()
    {
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    void DrawCursorInfo()
    {
        EditorGUILayout.LabelField("Cursor", EditorStyles.boldLabel);

        rvUIContent cursor = debugComponent.builder.cursor;
        EditorGUILayout.LabelField($"  {cursor.gameObject.name} ({cursor.GetType().Name})");

        rvUIContainer container = cursor as rvUIContainer;
        if (container != null)
        {
            List<rvUIContent> children = container.GetUIContents();
            EditorGUILayout.LabelField($"  Children: {children.Count}");
        }
    }

    void DrawSimpleNavigation()
    {
        EditorGUILayout.LabelField("Navigation", EditorStyles.boldLabel);

        // Up and Down
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("▲ Up", GUILayout.Height(30)))
        {
            debugComponent.builder.MoveCursor(-1);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("▼ Down", GUILayout.Height(30)))
        {
            debugComponent.builder.MoveCursor(1);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Step In and Out
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = debugComponent.builder.cursor is rvUIContainer;
        if (GUILayout.Button("→ Step In", GUILayout.Height(30)))
        {
            debugComponent.builder.StepIn();
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        GUI.enabled = debugComponent.builder.cursor.GetContainer() != null;
        if (GUILayout.Button("← Step Out", GUILayout.Height(30)))
        {
            debugComponent.builder.StepOut();
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    void DrawAddContent()
    {
        EditorGUILayout.LabelField("Add", EditorStyles.boldLabel);

        // Content
        EditorGUILayout.LabelField("Content:", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Button"))
        {
            debugComponent.builder.Add(RuntimeUIBuilder.ContentType.Button);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Text"))
        {
            debugComponent.builder.Add(RuntimeUIBuilder.ContentType.Text);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Dropdown"))
        {
            debugComponent.builder.Add(RuntimeUIBuilder.ContentType.Dropdown);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Containers
        EditorGUILayout.LabelField("Container:", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Horizontal"))
        {
            debugComponent.builder.Add(RuntimeUIBuilder.ContentType.HorizontalMenu);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Vertical"))
        {
            debugComponent.builder.Add(RuntimeUIBuilder.ContentType.VerticalMenu);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Window"))
        {
            debugComponent.builder.Add(RuntimeUIBuilder.ContentType.Window);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawSubMenuControls()
    {
        EditorGUILayout.LabelField("SubMenu", EditorStyles.boldLabel);

        // SubMenu type buttons
        EditorGUILayout.LabelField("Type:", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Window"))
        {
            rvUIFloatingMenuPanel submenu = debugComponent.builder.AddSubMenu(RuntimeUIBuilder.SubMenuType.Window, subMenuPlacement, subMenuMargin);
            if (submenu != null) SceneView.RepaintAll();
        }
        if (GUILayout.Button("Horizontal"))
        {
            rvUIFloatingMenuPanel submenu = debugComponent.builder.AddSubMenu(RuntimeUIBuilder.SubMenuType.Horizontal, subMenuPlacement, subMenuMargin);
            if (submenu != null) SceneView.RepaintAll();
        }
        if (GUILayout.Button("Vertical"))
        {
            rvUIFloatingMenuPanel submenu = debugComponent.builder.AddSubMenu(RuntimeUIBuilder.SubMenuType.Vertical, subMenuPlacement, subMenuMargin);
            if (submenu != null) SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Placement selector
        subMenuPlacement = (rvUIRelativePlacement.Placement)EditorGUILayout.EnumPopup("Placement:", subMenuPlacement);

        // Margin slider
        subMenuMargin = EditorGUILayout.Slider("Margin:", subMenuMargin, 0f, 50f);
    }

    void DrawClearButton()
    {
        EditorGUILayout.LabelField("Clear", EditorStyles.boldLabel);

        if (GUILayout.Button("Clear All Content", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Clear All Content",
                "Are you sure you want to clear all content from the current container?",
                "Yes", "Cancel"))
            {
                debugComponent.builder.Clear();
                SceneView.RepaintAll();
            }
        }
    }
} // class RuntimeUIBuilderDebugEditor

} // namespace realvirtual

#endif
