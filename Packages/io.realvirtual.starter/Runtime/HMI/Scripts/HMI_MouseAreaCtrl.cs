// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using UnityEngine.EventSystems;

namespace realvirtual
{
    //! Class to handle the interaction with the UI
    public class HMI_MouseAreaCtrl : realvirtualBehavior,IPointerExitHandler
    {
        [ReadOnly]public HMI ConnectedButton;
        [ReadOnly]public bool IsMouseOver;
        public void OnPointerExit(PointerEventData eventData)
        {
            if (ConnectedButton == null)
                return;
            ConnectedButton.CloseExtendedArea(eventData.position);
        }

    
    }
}
