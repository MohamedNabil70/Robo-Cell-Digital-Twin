// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace realvirtual
{
    /// <summary>
    /// Checks if demo samples have been imported and prompts user to import them if not.
    /// </summary>
    [InitializeOnLoad]
    public static class SamplesImportChecker
    {
        private const string SAMPLES_CHECK_KEY = "realvirtual_SamplesImportChecked";
        private const string SAMPLES_DISMISSED_KEY = "realvirtual_SamplesImportDismissed";
        private const string STARTER_PACKAGE_NAME = "io.realvirtual.starter";
        private const string PROFESSIONAL_PACKAGE_NAME = "io.realvirtual.professional";

        static SamplesImportChecker()
        {
            // Delay the check to avoid running during compilation
            EditorApplication.delayCall += CheckSamplesOnStartup;
        }

        private static void CheckSamplesOnStartup()
        {
            // Only check once per session
            if (SessionState.GetBool(SAMPLES_CHECK_KEY, false))
                return;

            SessionState.SetBool(SAMPLES_CHECK_KEY, true);

            // Skip if user has dismissed this dialog before
            if (EditorPrefs.GetBool(SAMPLES_DISMISSED_KEY, false))
                return;

            // Check if samples are imported
            if (!AreSamplesImported())
            {
                ShowImportSamplesDialog();
            }
        }

        /// <summary>
        /// Checks if Starter samples are imported.
        /// </summary>
        public static bool AreSamplesImported()
        {
            string starterSamplesPath = "Assets/Samples/realvirtual Starter";
            return AssetDatabase.IsValidFolder(starterSamplesPath);
        }

        /// <summary>
        /// Shows the import samples dialog. Can be called from anywhere when samples are needed.
        /// </summary>
        /// <param name="showDontShowAgain">If true, shows "Don't show again" option</param>
        public static void ShowImportSamplesDialog(bool showDontShowAgain = true)
        {
            ShowImportSamplesDialog(showDontShowAgain, null);
        }

        /// <summary>
        /// Shows the import samples dialog with a callback after successful import.
        /// </summary>
        /// <param name="showDontShowAgain">If true, shows "Don't show again" option</param>
        /// <param name="onImportComplete">Callback invoked after successful import</param>
        public static void ShowImportSamplesDialog(bool showDontShowAgain, Action onImportComplete)
        {
            string message = "The Getting Started demo scene will be imported to help you explore realvirtual.\n\n" +
                "Would you like to import it now?\n\n" +
                "You can import additional demo categories anytime via Package Manager.";

            if (showDontShowAgain)
            {
                int result = EditorUtility.DisplayDialogComplex(
                    "realvirtual Demo Scenes",
                    message,
                    "Import Now",
                    "Don't show again",
                    "Later"
                );

                switch (result)
                {
                    case 0: // Import Now
                        ImportSamplesDirectly(onImportComplete);
                        break;
                    case 1: // Don't show again
                        EditorPrefs.SetBool(SAMPLES_DISMISSED_KEY, true);
                        break;
                    case 2: // Later - do nothing
                        break;
                }
            }
            else
            {
                bool import = EditorUtility.DisplayDialog(
                    "realvirtual Demo Scenes",
                    message,
                    "Import Now",
                    "Cancel"
                );

                if (import)
                {
                    ImportSamplesDirectly(onImportComplete);
                }
            }
        }

        private static void ImportSamplesDirectly(Action onImportComplete = null)
        {
            bool starterImported = ImportGettingStartedSample(STARTER_PACKAGE_NAME);
            bool success = false;

            if (starterImported)
            {
                AssetDatabase.Refresh();
                success = true;
                EditorUtility.DisplayDialog("Import Complete",
                    "The Getting Started demo scene has been imported.\n\n" +
                    "Location: Assets/Samples/realvirtual Starter/\n\n" +
                    "You can import additional demo categories anytime via Package Manager " +
                    "or via Tools > realvirtual > Demo Scenes.",
                    "OK");

                // Open the Demo Scenes Browser so the user can explore all available categories
                DemoScenesWindow.ShowAfterFirstImport();
            }
            else
            {
                ShowFallbackDialog();
            }

            if (success && onImportComplete != null)
            {
                onImportComplete.Invoke();
            }
        }

        //! Imports only the "Getting Started" sample from the given package.
        //! Used for automatic first-time import to minimise download size.
        private static bool ImportGettingStartedSample(string packageName)
        {
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (listRequest.Status != StatusCode.Success)
            {
                Debug.LogWarning($"Failed to list packages: {listRequest.Error?.message}");
                return false;
            }

            var packageInfo = listRequest.Result.FirstOrDefault(p => p.name == packageName);
            if (packageInfo == null)
            {
                Debug.LogWarning($"Package {packageName} not found");
                return false;
            }

            var samples = Sample.FindByPackage(packageName, packageInfo.version);
            if (samples == null || !samples.Any())
            {
                Debug.LogWarning($"No samples found in package {packageName}");
                return false;
            }

            foreach (var sample in samples)
            {
                if (sample.displayName == "Getting Started")
                {
                    if (sample.isImported)
                    {
                        Debug.Log($"Getting Started sample already imported");
                        return true;
                    }

                    if (sample.Import())
                    {
                        Debug.Log($"Imported 'Getting Started' from {packageName}");
                        return true;
                    }

                    Debug.LogWarning($"Failed to import 'Getting Started' from {packageName}");
                    return false;
                }
            }

            // Fallback: import the first available sample if "Getting Started" is not found
            // (handles legacy package.json without category split)
            foreach (var sample in samples)
            {
                if (!sample.isImported)
                {
                    Debug.LogWarning($"[SamplesImportChecker] 'Getting Started' sample not found, importing first available: {sample.displayName}");
                    return sample.Import();
                }
            }

            return false;
        }

        private static void ShowFallbackDialog()
        {
            int result = EditorUtility.DisplayDialogComplex(
                "Import Failed",
                "Could not import samples automatically.\n\n" +
                "Please import manually via Package Manager:\n" +
                "1. Window > Package Manager\n" +
                "2. Select 'realvirtual Starter' package\n" +
                "3. Expand 'Samples' section\n" +
                "4. Click 'Import'",
                "Open Package Manager",
                "Cancel",
                ""
            );

            if (result == 0)
            {
                Window.Open(STARTER_PACKAGE_NAME);
            }
        }

        /// <summary>
        /// Menu item to reset the "Don't show again" preference
        /// </summary>
        [MenuItem("Tools/realvirtual/Settings/Reset Samples Import Reminder", false, 920)]
        private static void ResetSamplesReminder()
        {
            EditorPrefs.DeleteKey(SAMPLES_DISMISSED_KEY);
            EditorUtility.DisplayDialog("Reset Complete",
                "The samples import reminder has been reset.\n" +
                "It will appear again on next Unity startup if samples are not imported.",
                "OK");
        }

        /// <summary>
        /// Menu item to manually import samples
        /// </summary>
        [MenuItem("Tools/realvirtual/Settings/Import Demo Samples...", false, 919)]
        private static void ManualImportSamples()
        {
            if (AreSamplesImported())
            {
                int result = EditorUtility.DisplayDialogComplex(
                    "Samples Already Imported",
                    "Demo samples are already imported.\n\n" +
                    "Location: Assets/Samples/realvirtual Starter/\n\n" +
                    "Do you want to reimport them?",
                    "Reimport",
                    "Cancel",
                    ""
                );

                if (result == 0)
                {
                    ImportSamplesDirectly();
                }
            }
            else
            {
                ImportSamplesDirectly();
            }
        }
    }
}
