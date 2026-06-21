// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz


using UnityEngine;
using UnityEditor;
using System.IO;

namespace realvirtual
{
    public class InstalledPackages
    {
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            // Skip during play mode domain reload - not needed
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Import TMP Essential Resources if they don't exist
            ImportTMPEssentialResources();
        }

        private static void ImportTMPEssentialResources()
        {
            // Check if TMP Essential Resources already exist
            string tmpEssentialsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

            if (File.Exists(tmpEssentialsPath))
            {
                return; // Resources already imported
            }

            // Try to import TMP Essential Resources using reflection to avoid direct dependency
            var tmpPackageImporterType = System.Type.GetType("TMPro.TMP_PackageResourceImporter, Unity.TextMeshPro.Editor");

            if (tmpPackageImporterType != null)
            {
                var importMethod = tmpPackageImporterType.GetMethod("ImportProjectResourcesMenu",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                if (importMethod != null)
                {
                    try
                    {
                        Debug.Log("realvirtual: Importing TextMesh Pro Essential Resources...");
                        importMethod.Invoke(null, null);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"realvirtual: Could not auto-import TMP Essential Resources. Please import manually via Window > TextMeshPro > Import TMP Essential Resources. Error: {e.Message}");
                    }
                }
            }
        }
    }
}
