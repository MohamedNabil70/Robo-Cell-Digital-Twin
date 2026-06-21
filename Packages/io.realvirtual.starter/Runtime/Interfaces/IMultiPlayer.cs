// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using UnityEngine;

//! Interface for Placeable Objects in XR / AR Space
namespace realvirtual
{
    public interface IMultiPlayer 
    {
        public void OnMultiplayer(bool isclient, bool isstart);  // Start and Stop of Multiplayer, isclient is true if the object is a client
    }
}
