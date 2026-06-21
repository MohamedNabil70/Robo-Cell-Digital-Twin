using UnityEngine.Scripting.APIUpdating;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug visualization and testing component for RuntimeUIBuilder.
/// Displays cursor position, path to root, and provides navigation controls in the inspector.
/// Draws gizmos to visualize the current cursor's bounds and hierarchy path.
/// </summary>

namespace realvirtual
{
    [ExecuteInEditMode]
    [MovedFrom(false, sourceClassName: "RuntimeUIBuilderDebug")]
    public class RuntimeUIBuilderDebug : MonoBehaviour
    {
        #region Inspector Fields
    
        [Header("References")]
        [Tooltip("The RuntimeUIBuilder to debug")]
        public RuntimeUIBuilder builder;
    
        [Header("Gizmo Settings")]
        [Tooltip("Color for drawing cursor bounds")]
        public Color cursorBoundsColor = Color.green;
    
        [Tooltip("Color for drawing path to root")]
        public Color pathColor = Color.yellow;
    
        [Tooltip("Draw path lines from cursor to root")]
        public bool showPathToRoot = true;
    
        [Tooltip("Draw bounds for all elements in path")]
        public bool showPathBounds = false;
    
        [Tooltip("Show cursor name label in scene view")]
        public bool showCursorLabel = true;
    
        #endregion
    
        #region Gizmo Drawing
    
        void OnDrawGizmos()
        {
            if (builder == null || builder.cursor == null) return;
    
            // Draw cursor bounds
            DrawCursorBounds();
    
            // Draw path to root if enabled
            if (showPathToRoot)
            {
                DrawPathToRoot();
            }
    
            // Draw cursor label
            if (showCursorLabel)
            {
                DrawCursorLabel();
            }
        }
    
        void DrawCursorBounds()
        {
            RectTransform cursorRect = builder.cursor.GetComponent<RectTransform>();
            if (cursorRect == null) return;
    
            Gizmos.color = cursorBoundsColor;
    
            // Get world corners of the RectTransform
            Vector3[] corners = new Vector3[4];
            cursorRect.GetWorldCorners(corners);
    
            // Draw rectangle
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);
    
            // Draw diagonals for emphasis
            Gizmos.color = new Color(cursorBoundsColor.r, cursorBoundsColor.g, cursorBoundsColor.b, 0.3f);
            Gizmos.DrawLine(corners[0], corners[2]);
            Gizmos.DrawLine(corners[1], corners[3]);
        }
    
        void DrawPathToRoot()
        {
            List<rvUIContent> path = builder.cursor.GetPathToRoot();
            if (path.Count <= 1) return;
    
            Gizmos.color = pathColor;
    
            // Draw lines connecting each element in the path
            for (int i = 0; i < path.Count - 1; i++)
            {
                rvUIContent current = path[i];
                rvUIContent parent = path[i + 1];
    
                if (current == null || parent == null) continue;
    
                RectTransform currentRect = current.GetComponent<RectTransform>();
                RectTransform parentRect = parent.GetComponent<RectTransform>();
    
                if (currentRect != null && parentRect != null)
                {
                    Vector3 currentPos = currentRect.position;
                    Vector3 parentPos = parentRect.position;
    
                    Gizmos.DrawLine(currentPos, parentPos);
    
                    // Draw small sphere at connection points
                    Gizmos.DrawSphere(currentPos, 2f);
    
                    // Draw bounds for path elements if enabled
                    if (showPathBounds)
                    {
                        Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.3f);
                        Vector3[] corners = new Vector3[4];
                        parentRect.GetWorldCorners(corners);
                        Gizmos.DrawLine(corners[0], corners[1]);
                        Gizmos.DrawLine(corners[1], corners[2]);
                        Gizmos.DrawLine(corners[2], corners[3]);
                        Gizmos.DrawLine(corners[3], corners[0]);
                        Gizmos.color = pathColor;
                    }
                }
            }
    
            // Draw sphere at root
            if (path[path.Count - 1] != null)
            {
                RectTransform rootRect = path[path.Count - 1].GetComponent<RectTransform>();
                if (rootRect != null)
                {
                    Gizmos.DrawSphere(rootRect.position, 3f);
                }
            }
        }
    
        void DrawCursorLabel()
        {
    #if UNITY_EDITOR
            RectTransform cursorRect = builder.cursor.GetComponent<RectTransform>();
            if (cursorRect == null) return;
    
            Vector3[] corners = new Vector3[4];
            cursorRect.GetWorldCorners(corners);
            Vector3 topCenter = (corners[1] + corners[2]) / 2f;
    
            string label = $"CURSOR: {builder.cursor.gameObject.name}";
            UnityEditor.Handles.Label(topCenter + Vector3.up * 10f, label,
                new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = cursorBoundsColor },
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                });
    #endif
        }
    
        #endregion
    
        #region Public Debug Methods
    
        /// <summary>
        /// Get information about the current cursor as a formatted string
        /// </summary>
        public string GetCursorInfo()
        {
            if (builder == null || builder.cursor == null)
                return "No cursor set";
    
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"Cursor: {builder.cursor.gameObject.name}");
            sb.AppendLine($"Type: {builder.cursor.GetType().Name}");
    
            rvUIContainer container = builder.cursor as rvUIContainer;
            if (container != null)
            {
                List<rvUIContent> children = container.GetUIContents();
                sb.AppendLine($"Children: {children.Count}");
            }
    
            rvUIContainer parent = builder.cursor.GetContainer();
            sb.AppendLine($"Parent: {(parent != null ? parent.gameObject.name : "None")}");
    
            return sb.ToString();
        }
    
        /// <summary>
        /// Get the path from cursor to root as a formatted string
        /// </summary>
        public string GetPathInfo()
        {
            if (builder == null || builder.cursor == null)
                return "No cursor set";
    
            List<rvUIContent> path = builder.cursor.GetPathToRoot();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"Path to Root ({path.Count} elements):");
    
            for (int i = 0; i < path.Count; i++)
            {
                string indent = new string(' ', i * 2);
                sb.AppendLine($"{indent}└─ {path[i].gameObject.name} ({path[i].GetType().Name})");
            }
    
            return sb.ToString();
        }
    
        #endregion
    }
}
