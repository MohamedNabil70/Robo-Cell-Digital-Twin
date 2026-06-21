// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Linq;

namespace realvirtual
{
    //! Utility class for managing Newtonsoft.Json package installation and compiler defines
    public static class JsonPackageHelper
    {
        private const string NEWTONSOFT_PACKAGE_ID = "com.unity.nuget.newtonsoft-json";
        private const string REALVIRTUAL_JSON_DEFINE = "REALVIRTUAL_JSON";
        
        private static AddRequest addRequest;
        private static ListRequest listRequest;
        
        //! Installs Newtonsoft.Json package and adds REALVIRTUAL_JSON compiler define
        public static async void InstallNewtonsoftJsonAndDefine()
        {
            EditorUtility.DisplayProgressBar("Installing Newtonsoft.Json", "Checking if package is already installed...", 0.1f);
            
            try
            {
                // First check if package is already installed
                bool isInstalled = await IsNewtonsoftJsonInstalled();
                
                if (!isInstalled)
                {
                    EditorUtility.DisplayProgressBar("Installing Newtonsoft.Json", "Installing package via Package Manager...", 0.3f);
                    Debug.Log("[JsonPackageHelper] Installing Newtonsoft.Json package...");
                    
                    // Install the package
                    addRequest = Client.Add(NEWTONSOFT_PACKAGE_ID);
                    
                    // Wait for completion
                    while (!addRequest.IsCompleted)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                    }
                    
                    if (addRequest.Status == StatusCode.Success)
                    {
                        Debug.Log("[JsonPackageHelper] ✅ Newtonsoft.Json package installed successfully!");
                    }
                    else
                    {
                        Debug.LogError($"[JsonPackageHelper] ❌ Failed to install Newtonsoft.Json package: {addRequest.Error.message}");
                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("Installation Failed", 
                            $"Failed to install Newtonsoft.Json package:\n{addRequest.Error.message}\n\nPlease install it manually via Window > Package Manager", "OK");
                        return;
                    }
                }
                else
                {
                    Debug.Log("[JsonPackageHelper] ✅ Newtonsoft.Json package is already installed");
                }
                
                // Add compiler define
                EditorUtility.DisplayProgressBar("Installing Newtonsoft.Json", "Adding compiler define...", 0.8f);
                AddCompilerDefine();
                
                EditorUtility.ClearProgressBar();
                
                // Show success dialog
                EditorUtility.DisplayDialog("Installation Complete", 
                    "✅ Newtonsoft.Json package and REALVIRTUAL_JSON compiler define have been set up successfully!\n\n" +
                    "The interface is now ready to use. You may need to recompile your scripts.", "OK");
                    
                Debug.Log("[JsonPackageHelper] 🎯 Setup completed successfully! Newtonsoft.Json and REALVIRTUAL_JSON define are ready.");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[JsonPackageHelper] ❌ Error during setup: {ex.Message}");
                EditorUtility.DisplayDialog("Setup Error", 
                    $"An error occurred during setup:\n{ex.Message}\n\nPlease try again or install manually.", "OK");
            }
        }
        
        //! Checks if Newtonsoft.Json package is installed
        private static async System.Threading.Tasks.Task<bool> IsNewtonsoftJsonInstalled()
        {
            listRequest = Client.List();
            
            while (!listRequest.IsCompleted)
            {
                await System.Threading.Tasks.Task.Delay(50);
            }
            
            if (listRequest.Status == StatusCode.Success)
            {
                return listRequest.Result.Any(package => package.name == NEWTONSOFT_PACKAGE_ID);
            }
            
            return false;
        }
        
        //! Adds REALVIRTUAL_JSON to compiler defines for all build targets
        private static void AddCompilerDefine()
        {
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            string defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            
            if (!defines.Contains(REALVIRTUAL_JSON_DEFINE))
            {
                if (!string.IsNullOrEmpty(defines))
                {
                    defines += ";" + REALVIRTUAL_JSON_DEFINE;
                }
                else
                {
                    defines = REALVIRTUAL_JSON_DEFINE;
                }

                PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
                Debug.Log($"[JsonPackageHelper] ✅ Added '{REALVIRTUAL_JSON_DEFINE}' to compiler defines for {namedBuildTarget.TargetName}");
                
                // Also add to other common build targets
                var commonTargets = new[] { 
                    BuildTargetGroup.Standalone, 
                    BuildTargetGroup.Android, 
                    BuildTargetGroup.iOS,
                    BuildTargetGroup.WebGL
                };
                
                foreach (var target in commonTargets)
                {
                    if (target != buildTargetGroup)
                    {
                        var targetNamed = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(target);
                        string targetDefines = PlayerSettings.GetScriptingDefineSymbols(targetNamed);
                        if (!targetDefines.Contains(REALVIRTUAL_JSON_DEFINE))
                        {
                            if (!string.IsNullOrEmpty(targetDefines))
                            {
                                targetDefines += ";" + REALVIRTUAL_JSON_DEFINE;
                            }
                            else
                            {
                                targetDefines = REALVIRTUAL_JSON_DEFINE;
                            }
                            PlayerSettings.SetScriptingDefineSymbols(targetNamed, targetDefines);
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"[JsonPackageHelper] ✅ '{REALVIRTUAL_JSON_DEFINE}' is already in compiler defines");
            }
        }
    }
}
#endif