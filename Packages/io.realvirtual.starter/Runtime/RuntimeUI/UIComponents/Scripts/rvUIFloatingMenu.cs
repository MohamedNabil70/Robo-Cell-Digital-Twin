using System;
using NaughtyAttributes;
using realvirtual;
using UnityEditor;
using UnityEngine;


namespace realvirtual
{
    public class rvUIFloatingMenu : rvUIContainer
    {
    
        public enum MenuType
        {
            Horizontal,
            Vertical,
            Window,
        }
        
        
        public MenuType type;
        
        public rvUIRelativePlacement.Placement placement;
        
        public rvUIFloatingMenuPanel currentPanel;
    
    
        
        public override void RefreshLayout()
        {
            // Refresh the current panel if it exists
            if (currentPanel != null)
            {
                currentPanel.RefreshLayout();
            }
    
            // Force layout rebuild for this container
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }
        
        public override RectTransform GetContentRoot()
        {
            return currentPanel?.GetContentRoot();
        }
        
        
        public void SetPlacement(rvUIRelativePlacement.Placement placement){
            this.placement = placement;
        }
        
        
        [Button]
        public void ChangeToVertical()
        {
            SetType(MenuType.Vertical);
        }
        
        [Button]
        public void ChangeToHorizontal()
        {
            SetType(MenuType.Horizontal);
        }
        
        [Button]
        public void ChangeToWindow()
        {
            SetType(MenuType.Window);
        }
    
        
        public void SetType(MenuType newType)
        {
            if (type != newType)
            {
                type = newType;
                //this.SetDirty();
            }
        }
    
    
        
    }
}
