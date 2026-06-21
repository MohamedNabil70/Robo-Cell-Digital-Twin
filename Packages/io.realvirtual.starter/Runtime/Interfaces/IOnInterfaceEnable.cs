// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

namespace realvirtual
{
    //! Interface for components that need late initialization after all scenes are loaded
    //! This is used by FastInterfaceBase to delay interface activation until the realvirtualController
    //! explicitly calls OnInterfaceEnable after PostAllScenesLoaded
    public interface IOnInterfaceEnable
    {
        //! Called by realvirtualController after all scenes are loaded to enable the interface
        void OnInterfaceEnable();
        
        //! Returns true if the interface has been initialized via OnInterfaceEnable
        bool IsInterfaceReady { get; }
    }
}