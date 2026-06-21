// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using UnityEditor;
using UnityEditor.Toolbars;

namespace realvirtual
{
#if UNITY_2021_2_OR_NEWER
    //! Toolbar dropdown providing quick access to the full realvirtual Tools menu from the Scene View overlay.
    [RealvirtualToolbarButton(order: 20)]
    [EditorToolbarElement(id, typeof(SceneView))]
    class ToolsDropdown : RealvirtualToolbarDropdownBase
    {
        public const string id = "RealvirtualToolsDropdown";

        public ToolsDropdown()
        {
            tooltip = "realvirtual Tools";
            SetMaterialIcon("handyman", 18);
        }

        protected override void OnClicked()
        {
            // Dynamically shows ALL menu items under Tools/realvirtual/ as a popup.
            // This automatically includes items from starter, professional, and any other packages.
            EditorUtility.DisplayPopupMenu(worldBound, "Tools/realvirtual/", null);
        }
    }
#endif
}
