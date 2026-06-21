// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;

namespace realvirtual
{
    //! Attribute to mark custom toolbar buttons for automatic discovery and registration.
    //! Apply this attribute to classes that inherit from RealvirtualToolbarButtonBase to automatically
    //! add them to the realvirtual Toolbar overlay in the Scene view.
    //!
    //! Example usage:
    //! [RealvirtualToolbarButton(order: 100, group: "Custom")]
    //! public class MyCustomButton : RealvirtualToolbarButtonBase
    //! {
    //!     // Implementation
    //! }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RealvirtualToolbarButtonAttribute : Attribute
    {
        //! Display order of the button in the toolbar (lower values appear first)
        public int Order { get; set; }

        //! Optional group name for organizing buttons
        public string Group { get; set; }

        //! Whether this button should be enabled by default
        public bool EnabledByDefault { get; set; }

        //! Whether this button requires an active selection in the scene
        public bool RequiresSelection { get; set; }

        //! Whether this button is only available in play mode
        public bool PlayModeOnly { get; set; }

        //! Whether this button is only available in edit mode
        public bool EditModeOnly { get; set; }

        //! Creates a new RealvirtualToolbarButtonAttribute with specified order
        public RealvirtualToolbarButtonAttribute(int order = 1000)
        {
            Order = order;
            Group = string.Empty;
            EnabledByDefault = true;
            RequiresSelection = false;
            PlayModeOnly = false;
            EditModeOnly = false;
        }
    }
}
