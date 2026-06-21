// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

#if REALVIRTUAL_MCP
using Newtonsoft.Json.Linq;
using realvirtual.MCP;
using TMPro;
using UnityEngine;

namespace realvirtual.MCP.Tools
{
    //! MCP tools for creating and managing SceneNote text labels in the scene.
    //!
    //! SceneNotes are TextMeshPro 3D text objects placed flat on the floor with
    //! origin at the top-left corner. They provide readable scene descriptions
    //! and annotations visible in the Scene View.
    public static class SceneNoteTools
    {
        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return path;
        }

        //! Creates a SceneNote - a readable text label placed flat on the floor.
        //! Origin is at the top-left corner of the text. Text faces upward (readable from above).
        [McpTool("Create a scene note text label on the floor")]
        public static string SceneNoteCreate(
            [McpParam("Text content (supports TMP rich text like <b>bold</b>)")] string text,
            [McpParam("Parent GameObject name or path (optional)")] string parent = "",
            [McpParam("Local X position")] float x = 0,
            [McpParam("Local Z position")] float z = 0,
            [McpParam("Font size in world units (default: 1.0)")] float fontSize = 1.0f,
            [McpParam("Name for the GameObject")] string name = "SceneNote")
        {
            // Create GameObject
            var go = new GameObject(name);

            // Add TextMeshPro component
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.raycastTarget = false;

            // Set RectTransform for top-left origin with minimal size (overflow handles the rest)
            var rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(0.01f, 0.01f);

            // Parent if specified
            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = ToolHelpers.FindGameObject(parent);
                if (parentGo == null)
                {
                    Object.DestroyImmediate(go);
                    return ToolHelpers.Error($"Parent '{parent}' not found");
                }
                go.transform.SetParent(parentGo.transform, false);
            }

            // Position: flat on floor, origin at top-left
            // Rotation 90 around X makes text face upward (readable from top-down view)
            go.transform.localPosition = new Vector3(x, 0.01f, z);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "MCP: Create SceneNote");
#endif

            string path = GetGameObjectPath(go);
            var worldPos = go.transform.position;

            return new JObject
            {
                ["name"] = go.name,
                ["path"] = path,
                ["status"] = "ok",
                ["worldPosition"] = new JObject
                {
                    ["x"] = worldPos.x,
                    ["y"] = worldPos.y,
                    ["z"] = worldPos.z
                },
                ["fontSize"] = fontSize,
                ["text"] = text
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Updates the text content of an existing SceneNote (TextMeshPro).
        [McpTool("Update scene note text")]
        public static string SceneNoteSetText(
            [McpParam("SceneNote GameObject name or path")] string name,
            [McpParam("New text content")] string text)
        {
            var go = ToolHelpers.FindGameObject(name);
            if (go == null)
                return ToolHelpers.Error($"GameObject '{name}' not found");

            var tmp = go.GetComponent<TextMeshPro>();
            if (tmp == null)
                return ToolHelpers.Error($"No TextMeshPro component on '{name}'");

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.Undo.RecordObject(tmp, "MCP: Update SceneNote text");
#endif

            tmp.text = text;
            return new JObject
            {
                ["name"] = go.name,
                ["path"] = GetGameObjectPath(go),
                ["status"] = "ok",
                ["text"] = text
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        //! Lists all SceneNotes (TextMeshPro 3D text objects) in the scene.
        [McpTool("List all scene notes")]
        public static string SceneNoteList()
        {
            var tmps = Object.FindObjectsOfType<TextMeshPro>();
            var arr = new JArray();

            foreach (var tmp in tmps)
            {
                var pos = tmp.transform.position;
                arr.Add(new JObject
                {
                    ["name"] = tmp.gameObject.name,
                    ["path"] = GetGameObjectPath(tmp.gameObject),
                    ["text"] = tmp.text,
                    ["fontSize"] = tmp.fontSize,
                    ["position"] = new JObject
                    {
                        ["x"] = pos.x,
                        ["y"] = pos.y,
                        ["z"] = pos.z
                    }
                });
            }

            return new JObject
            {
                ["sceneNotes"] = arr,
                ["count"] = tmps.Length
            }.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
#endif
