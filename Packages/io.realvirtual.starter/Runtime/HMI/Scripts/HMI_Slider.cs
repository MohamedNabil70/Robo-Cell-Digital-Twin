// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using System.Globalization;
using NaughtyAttributes;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace realvirtual
{
    //! class to define a HMI slider
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-slider")]
    [SelectionBase]
    public class HMI_Slider : HMI, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [OnValueChanged("Init")]public float MinValue=0f;//!< minimum value of the slider
        [OnValueChanged("Init")]public float MaxValue=100f;//!< maximum value of the slider
        [OnValueChanged("Init")]public float Value=50;//!< current value of the slider
        [OnValueChanged("Init")]public int FontSizeValue=40;//!< font size of the value text
        public bool ActivateOnMouseEnter = false;//!< if true, the slider is activated when the mouse enters the slider area
        [HideIf("UseInteger")] [Tooltip("Format for the float value. Default is F1")]  public string Format="F1";//!< format for the float value. Default is F1
        [OnValueChanged("Init")]public bool UseInteger = false;//!< if true, the slider is an integer slider
        [OnValueChanged("Init")]public string Title="Title";//!< title of the slider
        [OnValueChanged("Init")] public int FontSizeTitle = 14;//!< font size of the title text
        [OnValueChanged("Init")]public bool DisplayUnit = true;//!< if true, the unit is displayed
        [OnValueChanged("Init")][ShowIf("DisplayUnit")] public string Unit="unit";//!< unit of the slider
        [OnValueChanged("Init")][ShowIf("DisplayUnit")] public int FontSizeUnit=18;//!< font size of the unit text
        [OnValueChanged("Init")] public Color FontColor=Color.white;//!< color of the slider
        [Header("PLC IO's")]
        public PLCInputInt SignalInt;//!< PLC input for the integer value
        public PLCInputFloat SignalFloat;//!< PLC input for the float value
        public PLCOutputInt SignalIntStart;//!< PLC output for the start int value of the slider
        public PLCOutputFloat SignalFloatStart;//!< PLC output for the start float value of the slider
       
        
        private GameObject sliderObj;
        private GameObject slideArea;
        private HMI_MouseAreaCtrl mouseAreaCtrl;
        private Slider slider;
        private GameObject value;
        private TextMeshProUGUI valueText;
        private TextMeshProUGUI valueTitle;
        private TextMeshProUGUI unitText;
        private float lastSliderValue;
        private Image SliderFill;
        private Image SliderHandle;

        public new void Awake()
        {
            Init();
        }
        public override void Init()
        {
            sliderObj = Global.GetComponentByName<Component>(this.gameObject, "rvUISlider").gameObject;
            sliderObj.SetActive(true);
            slider = GetComponentInChildren<Slider>();
            if (slider != null)
            {
                slider.minValue = MinValue;
                slider.maxValue = MaxValue;
                if (Value > MaxValue)
                {
                    Debug.LogError("Current value in " + transform.name + " out of range!");
                    return;
                }
                if (UseInteger)
                {
                    slider.wholeNumbers = true;
                }
                if(SignalInt!=null)
                    SignalInt.Value = (int)slider.value;
                else
                {
                    if (SignalFloat != null)
                    {
                        slider.value = SignalFloatStart.Value;
                        SignalFloat.Value = slider.value;
                    }
                    else
                    {
                        slider.value = SignalIntStart.Value;
                        SignalInt.Value = (int)slider.value;
                    }
                }
                slider.GetComponent<Image>().color=new Color(Color.r,Color.g,Color.b,Color.a);
                SliderFill = slider.transform.Find("Fill Area").Find("Fill").GetComponent<Image>();
                SliderHandle = slider.transform.Find("Handle Slide Area").Find("Handle").GetComponent<Image>();
                SliderFill.color = FontColor;
                SliderHandle.color = FontColor;
            }
           
            sliderObj.SetActive(false);
            slideArea= Global.GetComponentByName<Component>(this.gameObject, "SlideArea").gameObject;
            if (slideArea != null)
            {
                mouseAreaCtrl = slideArea.GetComponent<HMI_MouseAreaCtrl>();
                mouseAreaCtrl.ConnectedButton = this;
            }
            valueText =GetText("Value");
            if (valueText != null)
            {
                valueText.fontSize = FontSizeValue;
                valueText.color = FontColor;
                valueTitle = GetText("Title");
                if (valueTitle != null)
                {
                    valueTitle.text = Title;
                    valueTitle.fontSize = FontSizeTitle;
                    valueTitle.color = new Color(FontColor.r,FontColor.g,FontColor.b,FontColor.a);
                }
                var arrow=GetImage("Arrow");
                arrow.color = FontColor;
                GameObject UnitObj = Global.GetComponentByName<Component>(this.gameObject, "Unit").gameObject;
                UnitObj.SetActive(true);
                unitText = GetText("TextUnit");
                if (DisplayUnit)
                {

                    unitText.text = Unit;
                    unitText.fontSize = FontSizeUnit;
                    unitText.color = FontColor;
                    valueText.alignment = TextAlignmentOptions.MidlineRight;
                }
                else
                {
                    UnitObj.SetActive(false);
                    unitText.text = "";
                    valueText.alignment = TextAlignmentOptions.Center;
                }
            }

            bgImg = GetImage("ValueSlide");
            bgImg.color=new Color(Color.r,Color.g,Color.b,Color.a);
            Update();
        }

        public override void CloseExtendedArea(Vector3 pos)
        {
            CloseSlider();
        }

        public void CloseSlider()
        {
            sliderObj.SetActive(false);
        }
        // Update is called once per frame
        void Update()
        {
            if (slider == null)
                return;
            if(SignalInt!=null)
                SignalInt.Value = (int)slider.value;
            else
            {
                if (SignalFloat != null)
                {
                    SignalFloat.Value = slider.value;
                    SignalFloat.Value = slider.value;
                }
            }
            if (!UseInteger)
            {
                valueText.text = slider.value.ToString(Format,CultureInfo.InvariantCulture);
            }
            else
            {
                valueText.text = slider.value.ToString("F0",CultureInfo.InvariantCulture);
            }

            Value = slider.value;
            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            bgImg.color=new Color(ColorMouseOver.r,ColorMouseOver.g,ColorMouseOver.b,ColorMouseOver.a);
            if (ActivateOnMouseEnter)
            {
                sliderObj.SetActive(true);
                lastSliderValue = slider.value;
               
            }
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!ActivateOnMouseEnter)
            {
                if (!sliderObj.activeSelf)
                {
                    sliderObj.SetActive(true);
                }
            }

        }
        public void OnPointerExit(PointerEventData eventData)
        {
            bgImg.color=new Color(Color.r,Color.g,Color.b,Color.a);
           if (!slideArea.GetComponent<RectTransform>().rect.Contains(slideArea.transform.InverseTransformPoint(eventData.position)))
                Invoke("CloseSlider", 0.5f);
            
        }
    }
}
