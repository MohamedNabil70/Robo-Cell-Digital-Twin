using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;

namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIToggleDropdown")]
    public class rvUIToggleDropdown : rvUIContainer, IRuntimeWindowStyle, IPointerClickHandler
    {
    public rvUIMenuButton mainButton;
    public rvUIMenuButton sideButton;
    public rvUIFloatingMenuPanel dropdownContainer;

    
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (sideButton.IsOn())
            {
                sideButton.ToggleOff();
            }else
            {
                sideButton.ToggleOn();
            }
        }
    }
    
    public override void RefreshLayout()
    {
        
    }
    
    public void OnWindowStyleChanged(rvUIMenuWindow.Style newStyle)
    {
        realvirtual.Logger.Message($"rvUIDropdown.OnWindowStyleChanged called with style: {newStyle}", this);

        if (dropdownContainer == null)
        {
            realvirtual.Logger.Warning("OnWindowStyleChanged: dropdownPanel is null", this);
            return;
        }

        // Adjust placement based on window style
        switch (newStyle)
        {
            case rvUIMenuWindow.Style.Horizontal:
                dropdownContainer.SetRelativePlacement(rvUIRelativePlacement.Placement.Vertical);
                realvirtual.Logger.Message("Dropdown adjusted for horizontal layout (Vertical)", this);
                break;

            case rvUIMenuWindow.Style.Vertical:
                dropdownContainer.SetRelativePlacement(rvUIRelativePlacement.Placement.Horizontal);
                realvirtual.Logger.Message("Dropdown adjusted for vertical layout (Horizontal)", this);
                break;

            case rvUIMenuWindow.Style.Window:
                dropdownContainer.SetRelativePlacement(rvUIRelativePlacement.Placement.Below);
                realvirtual.Logger.Message("Dropdown adjusted for window layout (Below)", this);
                break;
        }

      
    }

    public override RectTransform GetContentRoot()
    {
        return dropdownContainer.GetContentRoot();
    }

    public void CloseDropDown()
    {
        sideButton.ToggleOff();

    }
}
}
