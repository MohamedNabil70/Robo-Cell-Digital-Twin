// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    #region doc
    //! EditorWindow that shows the Demo Scenes Browser for realvirtual Starter and Professional packages.

    //! Opens via Tools > realvirtual > Demo Scenes or automatically after the first Getting Started import.
    //! The window lists all available demo categories with their import status and allows 1-click import
    //! and 1-click scene opening directly from the editor.
    //!
    //! The actual UI logic is in DemoScenesContent. This class is a thin wrapper that follows
    //! the Content/Window pattern used by other realvirtual editor windows.
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/getting-started
    #endregion
    [HelpURL("https://doc.realvirtual.io/getting-started")]
    public class DemoScenesWindow : EditorWindow
    {
        private DemoScenesContent _content;

        //! Opens the Demo Scenes Browser from the main menu.
        [MenuItem("Tools/realvirtual/Demo Scenes", false, 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<DemoScenesWindow>("Demo Scenes");
            window.minSize = new Vector2(420, 500);
            window.Show();
        }

        //! Opens the Demo Scenes Browser automatically after the first Getting Started sample import.
        //! Called from SamplesImportChecker after a successful first-time import.
        public static void ShowAfterFirstImport()
        {
            var window = GetWindow<DemoScenesWindow>("Demo Scenes");
            window.minSize = new Vector2(420, 500);
            window.Show();
            window.Focus();
        }

        private void CreateGUI()
        {
            _content = new DemoScenesContent();
            _content.CreateGUI(rootVisualElement);
        }

        private void OnDestroy()
        {
            _content?.Cleanup();
        }
    }
}
