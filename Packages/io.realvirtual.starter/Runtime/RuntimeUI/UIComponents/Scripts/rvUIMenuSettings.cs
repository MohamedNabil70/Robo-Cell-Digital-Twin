using System;
using UnityEngine;


namespace realvirtual
{
    [Serializable]
    public class rvUIMenuSettings
    {
        public rvUIMenuWindow.Style MenuStyle = rvUIMenuWindow.Style.Window;
    
        public rvUIPanelPlacer.Position DefaultPosition = rvUIPanelPlacer.Position.TopLeft;
    
        [Header("Header Settings")] public bool showHeader = true;
        [Header("Header Settings")] public bool useKnob = true;
        [Header("Header Settings")] public bool useTitle = true;
        [Header("Header Settings")] public bool useCloseButton = true;
        [Header("Header Settings")] public string title = "Menu";
    
        [Header("Content Settings")] public bool buttonText = true;
    }
}
