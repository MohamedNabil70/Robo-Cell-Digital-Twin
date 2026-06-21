// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace realvirtual
{
    [System.Serializable]
    public class RealVirtualHMIEvent: UnityEvent<HMI>
    {}
    //! Foundation for industrial human-machine interface components providing interactive controls for automation systems.
    //! This professional base class enables creation of industrial HMI elements with signal integration, event handling,
    //! and visual feedback. Supports both 2D screen-based and 3D spatial interfaces for operator panels, machine controls,
    //! and process visualization in virtual commissioning and training scenarios.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components")]
    public class HMI : BehaviorInterface,IUISkinEdit
    {
        [OnValueChanged("Init")]public Color Color;//!< Color of the HMI element
        public Color ColorMouseOver;//!< Color of the HMI element when the mouse is over it
        public RealVirtualHMIEvent EventOnValueChanged;//!< Event that is triggered when the value of the HMI element changes
        [HideInInspector]public Image bgImg;
        
        private Canvas canvasInParent;
        private Canvas canvaslocal = null;

        public TextMeshProUGUI GetText(string objName)
        {
            GameObject obj = GetChildByName(objName);
            if (obj == null)
            {
                return null;
            }
            return obj.GetComponent<TextMeshProUGUI>();
        }
        
        public Image GetImage(string objName)
        {
            GameObject obj = GetChildByName(objName);
            if (obj == null)
            {
                return null;
            }
            return obj.GetComponent<Image>();
        }

        public virtual void CloseExtendedArea(Vector3 pos)
        {
           
        }

        public virtual void Init()
        {
            
        }
#if UNITY_EDITOR
        public void Reset()
        {
            Init();
        }
#endif

        public void OnValueChanged()
        {
            EventOnValueChanged.Invoke(this);
        }

        
        public Canvas GetCanvas(GameObject HMIelement, ref RectTransform rectTransform)
        {
            canvasInParent = HMIelement.transform.parent.gameObject.GetComponent<Canvas>();
            if (canvasInParent == null)
            {
                if (gameObject.GetComponent<Canvas>() == null)
                    canvaslocal = HMIelement.AddComponent<Canvas>();
                else
                    canvaslocal = HMIelement.GetComponent<Canvas>();
            }
            else
            {
                rectTransform = canvasInParent.GetComponent<RectTransform>();
                if (HMIelement.GetComponent<Canvas>() != null)
                {
                    DestroyImmediate(HMIelement.GetComponent<Canvas>() );
                }
            }
            
            if (canvaslocal != null)
            {

                canvaslocal.renderMode = RenderMode.WorldSpace;
                canvaslocal.worldCamera = Camera.main;
                canvaslocal.sortingLayerName = "Default";
                canvaslocal.sortingOrder = 0;
                rectTransform = canvaslocal.GetComponent<RectTransform>();
                return canvaslocal;
            }

            return canvasInParent;
        }

        public void OnTransformParentChanged()
        {
            if(canvaslocal != null && gameObject.GetComponentInParent<Canvas>()!=null)
                DestroyImmediate(canvaslocal);
        }

        public void UpdateUISkinParameter(RealvirtualUISkin skin)
        {
            Color = skin.WindowButtonColor;
            ColorMouseOver = skin.WindowHoverColor;
            if (bgImg != null)
                bgImg.color = new Color(Color.r, Color.g, Color.b, Color.a);
        }
    }
}
