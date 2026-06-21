// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using UnityEngine;
using UnityEngine.EventSystems;

namespace realvirtual
{
    //! HMI menu item for a single action
    public class HMI_MenueItem : HMI, IPointerExitHandler, IPointerEnterHandler, IPointerClickHandler
    {

        [HideInInspector] public HMI_DropDown currentMenue;
       
        public void OnPointerExit(PointerEventData eventData)
        {
           bgImg.color=new Color(Color.r,Color.g,Color.b,Color.a);
           
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            bgImg.color=new Color(ColorMouseOver.r,ColorMouseOver.g,ColorMouseOver.b,ColorMouseOver.a);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            currentMenue.elementClick(this.gameObject.transform.name);
        }
    }
}
