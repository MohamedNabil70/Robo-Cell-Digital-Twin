using UnityEditor;
using UnityEngine;
using System.IO;

namespace realvirtual
{
    public class DeleteScenesWindow : EditorWindow
    {
        [MenuItem("Tools/realvirtual/Settings/Delete Demodata", priority = 923)]
        public static void ShowWindow()
        {
            GetWindow<DeleteScenesWindow>("Delete Demo Data");
        }

        private void OnGUI()
        {
            GUILayout.Label("Warning: If your are using some of the demo data the references in your own model are lost, you need to import realvirtual.io again to restore the data", EditorStyles.wordWrappedLabel);
            GUILayout.Label("Delete all Scenes in realvirtual Folder", EditorStyles.boldLabel);

            if (GUILayout.Button("Delete Scenes"))
            {
                DeleteAllScenesInRealvirtualFolder();
            }
            
            GUILayout.Label("Delete all Meshes and FBX in realvirtual Folder, this will also deleta all 3D Prefabs, including 3D Buttons, Robots and so on", EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("Delete Meshes and FBX"))
            {
                DeleteAllMeshesAndFBXInRealvirtualFolder();
            }
        }

        private void DeleteAllMeshesAndFBXInRealvirtualFolder()
        {
            // With UPM packages, demos are in read-only package folders
            // Users should not delete package content - show info dialog instead
            EditorUtility.DisplayDialog("Information",
                "Demo scenes and meshes are now part of the UPM package and cannot be deleted.\n\n" +
                "To remove them, uninstall the package via Package Manager.",
                "OK");
        }

        private void DeleteAllScenesInRealvirtualFolder()
        {
            // With UPM packages, demos are in read-only package folders
            EditorUtility.DisplayDialog("Information",
                "Demo scenes are now part of the UPM package and cannot be deleted.\n\n" +
                "To remove them, uninstall the package via Package Manager.",
                "OK");
        }

        private static void DeleteFiles(string root, string pattern)
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            string[] files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    File.Delete(file + ".meta");
                    Debug.Log($"Deleted {pattern}: {file}");
                }
            }
        }
    }
}

