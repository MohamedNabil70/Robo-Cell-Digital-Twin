// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

namespace realvirtual
{
    //! Interface for components that should be excluded from GLB export.
    //! Any GameObject with a component implementing this interface will be excluded from GLB export,
    //! along with all of its children.
    public interface IExcludeFromGLBExport
    {
        // Marker interface - no methods required
    }
}
