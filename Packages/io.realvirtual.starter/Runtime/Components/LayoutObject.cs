// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    [AddComponentMenu("realvirtual/Layout/Layout Object")]
    [SelectionBase]
    //! Marker component for objects placed by the Layout Planner in the WebViewer.

    //! LayoutObject marks a GameObject as a layout-placed component. Objects with this component
    //! are treated as selectable units in the WebViewer Layout Planner — hovering highlights the
    //! entire subtree, and clicking selects it for transform manipulation.
    //!
    //! When Locked is true, the object cannot be moved or selected in the Layout Planner.
    //! Transform and rotation are editable in the Inspector when unlocked.
    public class LayoutObject : realvirtualBehavior
    {
        [Header("Layout Properties")]
        public string Label; //!< Display name of the layout object
        public string CatalogId; //!< Reference to the library catalog entry this object was placed from
        public bool Locked = false; //!< When true, the object cannot be moved or selected in the Layout Planner
    }
}
