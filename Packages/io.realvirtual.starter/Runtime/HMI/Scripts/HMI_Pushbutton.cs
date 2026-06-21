// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System.Collections.Generic;
using NaughtyAttributes;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace realvirtual
{
    //! Industrial pushbutton HMI component with PLC signal integration and multi-state visual feedback.
    //! This professional interface element simulates industrial control panel pushbuttons with configurable
    //! minimum activation time, multiple color states based on PLC signals, and realistic button behavior.
    //! Essential for operator panels, machine start/stop controls, and safety acknowledgments in virtual
    //! commissioning. Supports both momentary and latched operation modes with full signal connectivity.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-puschbutton")]
    public class HMI_Pushbutton : HMI, IPointerExitHandler,IPointerEnterHandler,IPointerDownHandler,IPointerUpHandler
    {
       //!< Signal that is set high when the button is pushed
        public float MinHighTime=1;//!< Minimum time the signal is high
        [OnValueChanged("Init")]public string ButtonText="Button Text";//!< Text of the button
        [OnValueChanged("Init")] public int TextSize=18;
        [OnValueChanged("Init")] public Color FontColor=Color.white;//!< Text size of the button
        [Header("PLC IO's")]
        [OnValueChanged("Init")]public Signal SignalButtonPushed;
        public PLCOutputBool Color1Signal;//!< Signal that set the color of the button
        public Color Color1;//!< Color of the button when signal 1 is high
        public PLCOutputBool Color2Signal;//!< Signal that set the color of the button
        public Color Color2;//!< Color of the button when signal 2 is high
        public PLCOutputBool Color3Signal;//!< Signal that set the color of the button
        public Color Color3;//!< Color of the button when signal 3 is high

        private rvUIButton currentButton;
        private float timeStarted;
        private TextMeshProUGUI buttonText;
        private bool isPressed = false;
        private bool isHovered = false;
        private GraphicRaycaster graphicRaycaster;
        public delegate void OnClickDelegate();
        public event OnClickDelegate ClickEvent;
        
        public new void Awake()
        {
           Init();
           graphicRaycaster = GetComponent<GraphicRaycaster>();
        }

        public override void Init()
        {
            currentButton = GetComponentInChildren<rvUIButton>();
            currentButton.button.onClick.RemoveListener(OnClick);
            currentButton.button.onClick.AddListener(OnClick);
            ColorBlock currentButtonColors = currentButton.button.colors;
            currentButtonColors.pressedColor = new Color(ColorMouseOver.r, ColorMouseOver.g, Color.b,
                ColorMouseOver.a);
            buttonText = currentButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = ButtonText;
                buttonText.fontSize = TextSize;
                buttonText.color = FontColor;
            }
            bgImg = GetImage("Button");
            bgImg.color=new Color(Color.r,Color.g,Color.b,Color.a);
        }
        public void OnClick()
        {
            if (currentButton.isActiveAndEnabled)
            {
                if (SignalButtonPushed != null)
                {
                    SignalButtonPushed.SetValue(true);
                    timeStarted = Time.time;
                    Invoke("minTimereached", MinHighTime);
                    isPressed = true;
                }
                if (ClickEvent != null)
                    ClickEvent();
            }
        }

        private void minTimereached()
        {
           
            if(!Input.GetMouseButton(0))
                SignalButtonPushed.SetValue(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            bgImg.color=new Color(Color.r,Color.g,Color.b,Color.a);
            isHovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            bgImg.color=new Color(ColorMouseOver.r,ColorMouseOver.g,ColorMouseOver.b,ColorMouseOver.a);
            #if UNITY_EDITOR
            EditorUtility.SetDirty(bgImg);
            #endif
        }

        public void Update()
        {
            if(Input.GetMouseButtonDown(0) && !isPressed)
            {
                if (SignalButtonPushed != null)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(bgImg.rectTransform, Input.mousePosition) ||
                        isHovered)
                    {
                        SignalButtonPushed.SetValue(true);
                        timeStarted = Time.time;
                        Invoke("minTimereached", MinHighTime);
                        isPressed = true;
                    }
                }
            }

            if (!Input.GetMouseButtonDown(0) && isPressed && !RectTransformUtility.RectangleContainsScreenPoint(bgImg.rectTransform, Input.mousePosition ))
                isPressed = false;
            
            if (Color1Signal != null && Color1Signal.Value)
            {
                bgImg.color=new Color(Color1.r,Color1.g,Color1.b,Color1.a);
            }
            else if (Color2Signal != null && Color2Signal.Value)
            {
                bgImg.color=new Color(Color2.r,Color2.g,Color2.b,Color2.a);
            }
            else if (Color3Signal != null && Color3Signal.Value)
            {
                bgImg.color=new Color(Color3.r,Color3.g,Color3.b,Color3.a);
            }
            
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
           
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (SignalButtonPushed != null)
            {
                //check when time is up and button is no longer pressed
                if (Time.time > timeStarted + MinHighTime )
                {
                    SignalButtonPushed.SetValue(false);
                    isPressed = false;
                }
            }
        }
    }
}
