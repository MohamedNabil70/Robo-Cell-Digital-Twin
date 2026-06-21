// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

namespace realvirtual
{
    //! Connection states for interface communication
    public enum InterfaceState
    {
        Disconnected,    //!< Interface is not connected
        Connecting,      //!< Interface is attempting to connect
        Connected,
        Reconnecting,    //!< Interface is attempting to reconnect after error
        Error,           //!< Interface has encountered an error
        Closing          //!< Interface is in the process of closing
    }
}