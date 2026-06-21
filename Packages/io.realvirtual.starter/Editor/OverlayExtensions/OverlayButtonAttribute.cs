// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using UnityEditor.Overlays;

namespace realvirtual
{
    //! Generic attribute to mark custom overlay buttons for automatic discovery and registration.
    //! This attribute can be applied multiple times to register the same button in different overlays.
    //!
    //! The decimal order system allows precise control over button placement:
    //! - Integer part (e.g., 100) defines the row
    //! - Decimal part (e.g., 0.1, 0.2) defines position within the row (left to right)
    //!
    //! Example usage:
    //! ```csharp
    //! // Single overlay
    //! [OverlayButton(typeof(QuickEditOverlay), order: 100.0, section: "Custom")]
    //! public class MyButton : OverlayButtonBase { }
    //!
    //! // Multiple overlays (same button in different overlays)
    //! [OverlayButton(typeof(QuickEditOverlay), order: 100.0, section: "Components")]
    //! [OverlayButton(typeof(DesOverlay), order: 50.0, section: "Tools")]
    //! public class UniversalButton : OverlayButtonBase { }
    //!
    //! // Same row with multiple buttons
    //! [OverlayButton(typeof(QuickEditOverlay), order: 100.0, section: "Custom")]
    //! public class Button1 : OverlayButtonBase { }  // Left-most
    //!
    //! [OverlayButton(typeof(QuickEditOverlay), order: 100.1, section: "Custom")]
    //! public class Button2 : OverlayButtonBase { }  // Middle
    //!
    //! [OverlayButton(typeof(QuickEditOverlay), order: 100.2, section: "Custom")]
    //! public class Button3 : OverlayButtonBase { }  // Right-most
    //!
    //! // New row
    //! [OverlayButton(typeof(QuickEditOverlay), order: 101.0, section: "Custom")]
    //! public class Button4 : OverlayButtonBase { }  // New row
    //! ```
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class OverlayButtonAttribute : Attribute
    {
        //! Type of overlay this button should appear in (e.g., typeof(QuickEditOverlay))
        public Type OverlayType { get; set; }

        //! Display order of the button (decimal: integer=row, fractional=position in row)
        //! Lower values appear first. Examples:
        //! - 100.0, 100.1, 100.2 = same row, positions 0, 1, 2
        //! - 101.0 = new row
        public double Order { get; set; }

        //! Section name for organizing buttons (e.g., "Transform", "Components", "Custom")
        //! Each overlay defines its own sections. Use "Custom" for new sections.
        public string Section { get; set; }

        //! Whether this button should take full width of the overlay
        public bool FullWidth { get; set; }

        //! Type of component required for this button to be visible (optional)
        //! Example: typeof(Drive) - button only shows when Drive component is selected
        public Type TargetComponentType { get; set; }

        //! Whether this button requires an active selection in the scene
        public bool RequiresSelection { get; set; }

        //! Whether this button is only available in play mode
        public bool PlayModeOnly { get; set; }

        //! Whether this button is only available in edit mode
        public bool EditModeOnly { get; set; }

        //! Whether this button is enabled by default
        public bool EnabledByDefault { get; set; }

        //! Optional group name for further organizing buttons within a section
        public string Group { get; set; }

        //! Creates a new OverlayButtonAttribute for a specific overlay type
        //!
        //! overlayType: Type of overlay (e.g., typeof(QuickEditOverlay))
        //! order: Display order using decimal system (default: 1000.0)
        //! section: Section name (default: "Custom")
        public OverlayButtonAttribute(Type overlayType, double order = 1000.0, string section = "Custom")
        {
            if (overlayType == null)
                throw new ArgumentNullException(nameof(overlayType));

            if (!typeof(Overlay).IsAssignableFrom(overlayType))
                throw new ArgumentException($"Type {overlayType.Name} must inherit from UnityEditor.Overlays.Overlay", nameof(overlayType));

            OverlayType = overlayType;
            Order = order;
            Section = section ?? "Custom";
            FullWidth = false;
            TargetComponentType = null;
            RequiresSelection = false;
            PlayModeOnly = false;
            EditModeOnly = false;
            EnabledByDefault = true;
            Group = string.Empty;
        }
    }
}
