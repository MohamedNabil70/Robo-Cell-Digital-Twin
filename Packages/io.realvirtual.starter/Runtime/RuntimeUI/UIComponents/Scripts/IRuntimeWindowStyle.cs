// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

/// <summary>
/// Interface for UI content that needs to be notified when its parent window's style changes.
/// Implement this interface on rvUIContent components that need to adjust their behavior,
/// layout, or positioning based on the window style (Horizontal, Vertical, or Window).
/// </summary>

namespace realvirtual
{
    public interface IRuntimeWindowStyle
    {
        /// <summary>
        /// Called when the parent window's style has changed.
        /// Implementing components should adjust their layout, positioning, or behavior accordingly.
        /// </summary>
        /// <param name="newStyle">The new window style</param>
        void OnWindowStyleChanged(rvUIMenuWindow.Style newStyle);
    }
}
