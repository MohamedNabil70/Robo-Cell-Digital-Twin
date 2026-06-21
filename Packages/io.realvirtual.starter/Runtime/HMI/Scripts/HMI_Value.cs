// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System.Globalization;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace realvirtual
{
    //! HMI element to display a float or int value
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-value")]
    public class HMI_Value : HMI        
    {
        [OnValueChanged("Init")]public string Title;//!< Title of the value
        [OnValueChanged("Init")]public int TitleFontSize;//!< Font size of the title
        
        [OnValueChanged("Init")]public bool DisplayUnit = false;//!< Display unit of the value
        [OnValueChanged("Init")][ShowIf("DisplayUnit")] public string Unit;//!< Unit of the value
        [OnValueChanged("Init")][ShowIf("DisplayUnit")] public int UnitFontSize;//!< Font size of the unit
        [OnValueChanged("Init")] public int ValueFontSize;//!< Font size of the value
        [Tooltip("Format for the float value. Default is F2")] public string Format;//!< Format for the float value. Default is F2
        public bool FollowCamera = true;//!< If true, the value will always face the camera
        [HideInInspector][OnValueChanged("Init")]public int NumberDecimalPlaces=2;
        [OnValueChanged("Init")]public bool ObjectTracking;//!< If true, the value will be placed at the position of the object

        [ShowIf("ObjectTracking")]
        [OnValueChanged("Init")]public GameObject TrackedObject;//!< Object to track
        [ShowIf("ObjectTracking")]
        public Vector3 Offset;//!< Offset of the value position to the tracked object
        [Header("PLC IO's")]
        public PLCOutputInt SignalInt;//!< PLC output for int values
        public PLCOutputFloat SignalFloat;//!< PLC output for float values
        
        private TextMeshProUGUI value;
        private TextMeshProUGUI title;
        private TextMeshProUGUI unitText;
       
        private Canvas currentCanvas;
        private RectTransform CanvasrectTransform;
        
        private HMI_Controller controller;

        new void Awake()
        {
            Init();
           
        }
        
        public override void Init()
        {
            controller = FindAnyObjectByType<HMI_Controller>();
            currentCanvas = GetCanvas(gameObject, ref CanvasrectTransform);
            bgImg = GetImage("ValueDisplay");
            bgImg.color = Color;
            value = GetText("Value");
            value.fontSize = ValueFontSize;
            
            title = GetText("Title");
            title.text = Title;
            title.fontSize = TitleFontSize;
            GameObject UnitObj = Global.GetComponentByName<Component>(this.gameObject, "Unit").gameObject;
            UnitObj.SetActive(true);
            unitText = GetText("TextUnit");
            if (DisplayUnit)
            {
                unitText.text = Unit;
                unitText.fontSize = UnitFontSize;
                value.alignment = TextAlignmentOptions.MidlineRight;
            }
            else
            {
                UnitObj.SetActive(false);
                unitText.text = "";
                value.alignment = TextAlignmentOptions.Center;
            }
            if (SignalInt != null)
            {
                value.text = Mathf.Abs(SignalInt.Value).ToString();
            }

            if (SignalFloat != null)
            {
                value.text = Mathf.Abs(SignalFloat.Value).ToString(Format,CultureInfo.InvariantCulture);
            }
            if (ObjectTracking && TrackedObject != null)
            {
                controller.SetPosinCanvas(currentCanvas, gameObject,TrackedObject.transform.position, Offset);
                if(currentCanvas.renderMode== RenderMode.WorldSpace)
                    controller.SetRotationinCanvas(currentCanvas,gameObject,TrackedObject.transform.position,FollowCamera);
            }

            
        }

        // Update is called once per frame
        void Update()
        {
            if (SignalInt != null)
            {
                value.text = Mathf.Abs(SignalInt.Value).ToString();
            }

            if (SignalFloat != null)
            {
                value.text = Mathf.Abs(SignalFloat.Value).ToString(Format,CultureInfo.InvariantCulture);
            }
            title.text = Title;
            if (ObjectTracking && TrackedObject != null)
            {
                controller.SetPosinCanvas(currentCanvas, gameObject,TrackedObject.transform.position, Offset);
                if(currentCanvas.renderMode== RenderMode.WorldSpace)
                    controller.SetRotationinCanvas(currentCanvas,gameObject,TrackedObject.transform.position,FollowCamera);
            }
        }

      
       
    }
}