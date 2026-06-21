// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using NaughtyAttributes;
using TMPro;
using UnityEngine;

using UnityEngine.UI;

namespace realvirtual
{
    //! HMI text element
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-text")]
    public class HMI_Text : HMI
    {
        [OnValueChanged("Init")][Multiline]public string Text;//!< Text to display
        [OnValueChanged("Init")]public int TextFontSize;//!< Font size of the text
        public PLCInputBool SignalActivateInformation;
        public bool FollowCamera = true;//!< If true, the text will always face the camera (only relevant in render mode "Worldspace")
        [OnValueChanged("Init")]public bool ObjectTracking;//!< If true, the text placed at the position of the defined object
        [ShowIf("ObjectTracking")]
        [OnValueChanged("Init")]public GameObject TrackedObject;//!< Object to track
        [ShowIf("ObjectTracking")] [OnValueChanged("Init")]public Vector3 Offset;//!< Offset to the tracked object
        
        
        private TextMeshProUGUI info;
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

            info = GetText("Value");
            info.text= Text;
            info.fontSize = TextFontSize;
            if (ObjectTracking && TrackedObject != null)
            {
                controller.SetPosinCanvas(currentCanvas,gameObject, TrackedObject.transform.position, Offset);
                if(currentCanvas.renderMode== RenderMode.WorldSpace)
                    controller.SetRotationinCanvas(currentCanvas,gameObject, TrackedObject.transform.position,FollowCamera);
            }
            bgImg = GetImage("TextDisplay");
            bgImg.color = Color;
        }

        // Update is called once per frame
        void Update()
        {
            if (ObjectTracking && TrackedObject != null)
            {
                controller.SetPosinCanvas(currentCanvas,gameObject, TrackedObject.transform.position, Offset);
                if(currentCanvas.renderMode== RenderMode.WorldSpace)
                    controller.SetRotationinCanvas(currentCanvas,gameObject, TrackedObject.transform.position,FollowCamera);
            }
            else
            {
                if(FollowCamera)
                   controller.SetRotationinCanvas(currentCanvas,gameObject, gameObject.transform.position,FollowCamera); 
            }
            
        
        }
    }
}