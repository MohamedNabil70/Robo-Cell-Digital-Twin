// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using NaughtyAttributes;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UI.Toggle;

namespace realvirtual
{
    //! Class for the HMI switch
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-switch")]
    public class HMI_Switch : HMI, IPointerEnterHandler, IPointerExitHandler
    {

        public Color ColorOn;//!< Color of the switch when it is on
        public PLCInputBool SignalOn;//!< PLC input signal for the switch
        public PLCOutputBool SignalInit;//!< PLC output signal for the start value of the switch
        [OnValueChanged("Init")] public string ButtonText;//!< Text of the switch
        [OnValueChanged("Init")] public int TextSize;//!< Text size of the switch
        [ReadOnly]public bool IsOn = false;

        private Toggle checkbox;
        private Image toggleColors;

        new void Awake()
        {
            Init();
            ToggleChanged(IsOn);
        }
        public override void Init()
        {
            checkbox = GetComponentInChildren<Toggle>();
            if (checkbox != null)
            {
                checkbox.onValueChanged.RemoveListener(ToggleChanged);
                checkbox.onValueChanged.AddListener(ToggleChanged);
                toggleColors = GetImage("Switch");
                TextMeshProUGUI ToggleLabel = GetText("Label");
                ToggleLabel.text = ButtonText;
                ToggleLabel.fontSize = TextSize;
                if(SignalInit!=null)
                {
                    IsOn = (bool)SignalInit.GetValue();
                }
            }
        }
        public void ToggleChanged(bool value)
            {
                if (checkbox != null)
                {
                    checkbox.isOn = value;
                    IsOn = value;
                    #if UNITY_EDITOR
                    EditorUtility.SetDirty(this);
                    #endif
                    if (value)
                    {
                        toggleColors.color = new Color(ColorOn.r, ColorOn.g, ColorOn.b,
                            ColorOn.a);
                    }
                    else
                    {
                        toggleColors.color = new Color(Color.r, Color.g,
                            Color.b,
                            Color.a);
                    }
                }
            }

            // Update is called once per frame
            void Update()
            {
                if (SignalOn != null)
                {
                    SignalOn.SetValue( checkbox.isOn);
                }
            }
            
            public void OnPointerEnter(PointerEventData eventData)
            {
                toggleColors.color = new Color(ColorMouseOver.r, ColorMouseOver.g, ColorMouseOver.b,
                    ColorMouseOver.a);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (checkbox.isOn)
                {
                    toggleColors.color = new Color(ColorOn.r, ColorOn.g, ColorOn.b,
                        ColorOn.a);
                }
                else
                {
                    toggleColors.color = new Color(Color.r, Color.g, Color.b,
                        Color.a);
                }
            }
        }
    }
