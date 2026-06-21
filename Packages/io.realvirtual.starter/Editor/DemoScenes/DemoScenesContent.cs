// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace realvirtual
{
    #region doc
    //! UI Toolkit content class for the Demo Scenes Browser window.

    //! DemoScenesContent builds and manages the scroll view listing all demo categories and scenes.
    //! It is designed as a shared content object so it can be hosted in both DemoScenesWindow
    //! and future embedded panels without duplicating logic.
    //!
    //! Features:
    //! - Displays STARTER and PROFESSIONAL sections with a separator between them.
    //! - Each category shows an import status badge (IMPORTED / Import button).
    //! - Imported categories list all scenes with an Open button.
    //! - Non-imported categories show scene names and descriptions grayed out.
    //! - The Client.List() Package Manager call is made per ImportCategory() click (not on every BuildContent()).
    //!
    //! Lifecycle: create, call CreateGUI(root), call Cleanup() in OnDestroy.
    #endregion
    internal class DemoScenesContent
    {
        private ScrollView _scrollView;

        //! Builds the full UI tree inside the given root element.
        //! Call this once from CreateGUI() of the hosting EditorWindow.
        public void CreateGUI(VisualElement root)
        {
            EditorUIFactory.AttachStylesheet(root);
            root.AddToClassList("rv-editor-root");

            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.style.flexGrow = 1;
            root.Add(_scrollView);

            BuildContent();

            // Bottom action row
            root.Add(EditorUIFactory.CreateSeparator());
            var actionRow = EditorUIFactory.CreateActionRow();

            var pmBtn = MaterialIcons.CreateIconButton(
                "inventory_2", "Open Package Manager",
                () => Window.Open("io.realvirtual.starter"), 14);
            pmBtn.style.flexGrow = 1;
            pmBtn.style.height = 28;
            actionRow.Add(pmBtn);

            var refreshBtn = MaterialIcons.CreateIconButton(
                "refresh", "Refresh",
                () => { _scrollView.Clear(); BuildContent(); }, 14);
            refreshBtn.style.flexGrow = 1;
            refreshBtn.style.height = 28;
            refreshBtn.style.marginLeft = 4;
            actionRow.Add(refreshBtn);

            root.Add(actionRow);
        }

        //! Clears the scroll view and rebuilds all category / scene rows from DemoScenesData.
        private void BuildContent()
        {
            DemoScenesData.Package? lastPackage = null;

            foreach (var category in DemoScenesData.Categories)
            {
                // Package-header (STARTER / PROFESSIONAL) — insert once per package block
                if (category.Package != lastPackage)
                {
                    if (lastPackage != null)
                        _scrollView.Add(EditorUIFactory.CreateSeparator());

                    string pkgLabel = category.Package == DemoScenesData.Package.Starter
                        ? "STARTER" : "PROFESSIONAL";
                    _scrollView.Add(EditorUIFactory.CreateSection(pkgLabel));
                    lastPackage = category.Package;
                }

                bool isImported = IsCategoryImported(category);

                // --- Category header row: icon + name left, status right ---
                var catRow = new VisualElement();
                catRow.AddToClassList("rv-editor-row-between");
                catRow.style.marginTop = 6;
                catRow.style.marginBottom = 2;

                var leftRow = new VisualElement();
                leftRow.AddToClassList("rv-editor-row");

                var icon = MaterialIcons.CreateIconLabel(isImported ? "check_circle" : "download", 16);
                icon.style.marginRight = 6;
                icon.style.color = isImported ? EditorUIFactory.ColorSuccess : EditorUIFactory.ColorMuted;
                leftRow.Add(icon);

                var catLabel = new Label(category.DisplayName);
                catLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                catLabel.style.fontSize = 12;
                leftRow.Add(catLabel);
                catRow.Add(leftRow);

                if (!isImported)
                {
                    var importBtn = new Button(() => ImportCategory(category)) { text = "Import" };
                    importBtn.AddToClassList("rv-editor-btn-action");
                    importBtn.style.width = 70;
                    EditorUIFactory.SetButtonColor(importBtn, true, EditorUIFactory.ColorPrimary);
                    catRow.Add(importBtn);
                }
                else
                {
                    var badge = new Label("IMPORTED");
                    badge.style.color = EditorUIFactory.ColorSuccess;
                    badge.style.fontSize = 10;
                    catRow.Add(badge);
                }
                _scrollView.Add(catRow);

                // Category description
                var descLabel = new Label(category.Description);
                descLabel.style.color = EditorUIFactory.ColorMuted;
                descLabel.style.fontSize = 10;
                descLabel.style.marginLeft = 22;
                descLabel.style.marginBottom = 4;
                _scrollView.Add(descLabel);

                // Scenes list container
                var scenesContainer = new VisualElement();
                scenesContainer.AddToClassList("rv-editor-section-bg");
                scenesContainer.style.marginLeft = 10;
                scenesContainer.style.marginRight = 4;
                scenesContainer.style.paddingTop = 4;
                scenesContainer.style.paddingBottom = 4;

                foreach (var scene in category.Scenes)
                {
                    var sceneRow = new VisualElement();
                    sceneRow.AddToClassList("rv-editor-row-between");
                    sceneRow.style.paddingLeft = 8;
                    sceneRow.style.paddingRight = 4;
                    sceneRow.style.marginBottom = 2;

                    var infoCol = new VisualElement();
                    infoCol.style.flexGrow = 1;

                    var sceneName = new Label(scene.SceneName);
                    sceneName.style.unityFontStyleAndWeight = FontStyle.Bold;
                    sceneName.style.fontSize = 11;
                    if (!isImported)
                        sceneName.style.color = EditorUIFactory.ColorMuted;
                    infoCol.Add(sceneName);

                    var sceneDesc = new Label(scene.Description);
                    sceneDesc.style.color = EditorUIFactory.ColorMuted;
                    sceneDesc.style.fontSize = 10;
                    sceneDesc.style.whiteSpace = WhiteSpace.Normal;
                    infoCol.Add(sceneDesc);

                    sceneRow.Add(infoCol);

                    if (isImported)
                    {
                        // Capture loop variables for lambda
                        var capturedCategory = category;
                        var capturedScene = scene;
                        var openBtn = MaterialIcons.CreateIconButton(
                            "play_arrow", "Open",
                            () => OpenScene(capturedCategory, capturedScene), 12);
                        openBtn.style.width = 60;
                        openBtn.style.height = 22;
                        openBtn.style.flexShrink = 0;
                        sceneRow.Add(openBtn);
                    }

                    scenesContainer.Add(sceneRow);
                }

                _scrollView.Add(scenesContainer);
            }
        }

        //! Returns true if the given category folder exists under Assets/Samples/{package}/{version}/{sampleDisplayName}.
        //! Also checks the legacy flat folder structure (Demo Scenes / Demo Scenes (Pro)) for backward compatibility.
        private bool IsCategoryImported(DemoScenesData.DemoCategory category)
        {
            string packageDisplayName = category.Package == DemoScenesData.Package.Starter
                ? "realvirtual Starter" : "realvirtual Professional";
            string basePath = $"Assets/Samples/{packageDisplayName}";

            if (!AssetDatabase.IsValidFolder(basePath))
                return false;

            // Legacy folder names from pre-reorganisation single-sample structure
            string legacyFolder = category.Package == DemoScenesData.Package.Starter
                ? "Demo Scenes" : "Demo Scenes (Pro)";

            var versionFolders = AssetDatabase.GetSubFolders(basePath);
            foreach (var vf in versionFolders)
            {
                // New structure: category subfolder matches SampleDisplayName
                string catPath = $"{vf}/{category.SampleDisplayName}";
                if (AssetDatabase.IsValidFolder(catPath))
                    return true;

                // Legacy structure: all scenes in a single flat folder
                string legacyPath = $"{vf}/{legacyFolder}";
                if (AssetDatabase.IsValidFolder(legacyPath))
                    return true;
            }
            return false;
        }

        //! Opens the .unity file for the given scene in the scene's installed category folder.
        //! Searches new category subfolders and legacy flat folder for backward compatibility.
        private void OpenScene(DemoScenesData.DemoCategory category, DemoScenesData.DemoScene scene)
        {
            string packageDisplayName = category.Package == DemoScenesData.Package.Starter
                ? "realvirtual Starter" : "realvirtual Professional";
            string basePath = $"Assets/Samples/{packageDisplayName}";

            if (!AssetDatabase.IsValidFolder(basePath))
                return;

            string legacyFolder = category.Package == DemoScenesData.Package.Starter
                ? "Demo Scenes" : "Demo Scenes (Pro)";

            var versionFolders = AssetDatabase.GetSubFolders(basePath);
            foreach (var vf in versionFolders)
            {
                // New structure: category subfolder
                string scenePath = $"{vf}/{category.SampleDisplayName}/{scene.SceneName}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(scenePath);
                    return;
                }

                // Legacy structure: flat folder
                string legacyScenePath = $"{vf}/{legacyFolder}/{scene.SceneName}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(legacyScenePath) != null)
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(legacyScenePath);
                    return;
                }
            }

            Debug.LogWarning($"[DemoScenes] Scene not found: {scene.SceneName} in category {category.SampleDisplayName}");
        }

        //! Triggers Package Manager import for a single category sample.
        //! Uses a synchronous Client.List() call (existing pattern in the codebase).
        private void ImportCategory(DemoScenesData.DemoCategory category)
        {
            string packageName = category.Package == DemoScenesData.Package.Starter
                ? "io.realvirtual.starter" : "io.realvirtual.professional";

            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
                Thread.Sleep(10);

            if (listRequest.Status != StatusCode.Success)
            {
                Debug.LogWarning($"[DemoScenes] Failed to list packages: {listRequest.Error?.message}");
                return;
            }

            var packageInfo = listRequest.Result.FirstOrDefault(p => p.name == packageName);
            if (packageInfo == null)
            {
                Debug.LogWarning($"[DemoScenes] Package not found: {packageName}");
                return;
            }

            var samples = Sample.FindByPackage(packageName, packageInfo.version);
            if (samples == null)
            {
                Debug.LogWarning($"[DemoScenes] No samples found for: {packageName}");
                return;
            }

            bool found = false;
            foreach (var sample in samples)
            {
                if (sample.displayName == category.SampleDisplayName)
                {
                    found = true;
                    if (!sample.isImported)
                    {
                        sample.Import();
                        AssetDatabase.Refresh();
                    }
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"[DemoScenes] Sample not found: {category.SampleDisplayName}");
                return;
            }

            // Rebuild the list to reflect updated import status
            _scrollView.Clear();
            BuildContent();
        }

        //! Called from OnDestroy() of the hosting window. Reserved for future cleanup.
        public void Cleanup() { }
    }
}
