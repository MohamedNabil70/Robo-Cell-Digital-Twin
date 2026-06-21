// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using NaughtyAttributes;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace realvirtual
{
//! Button which set the assigned tab to active or deactive
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-tab")]
    public class HMI_TabButton : HMI, IPointerExitHandler, IPointerEnterHandler
    {
        public HMI_Tab Tab; //!< The tab to activate or deactivate

        //!< The HMI Controller
        public Color ConnectedTabActive; //!< Color of the button when the tab is active
        [OnValueChanged("Init")] public Sprite ButtonImage;
        public Sprite ButtonImageActive; //!< Image of the button when the tab is active

        //!< Signal to set the button to active
        [Header("PLC IO's")] public PLCOutputBool SignalButtonOn;

        //!< Image of the button
        public PLCOutputBool Color1Signal; //!< Signal to set the color of the button
        public Color Color1; //!< Color of the button when signal 1 is high
        public Sprite Image1; //!< Image of the button when signal 1 is high
        public PLCOutputBool Color2Signal; //!< Signal2 to set the color of the button
        public Color Color2; //!< Color of the button when signal 2 is high
        public Sprite Image2; //!< Image of the button when signal 2 is high
        public PLCOutputBool Color3Signal; //!< Signal3 to set the color of the button
        public Color Color3; //!< Color of the button when signal 3 is high
        public Sprite Image3; //!< Image of the button when signal 3 is high

        private Button _button;
        private Image _buttonImg;
        private bool signalAvailable = false;
        private ColorBlock BGColors;
        private Color StartButtonColor;
        private TextMeshProUGUI counterText;
        private bool _pointerOver = false;

        public new void Awake()
        {
            if (Tab != null)
            {
                Tab.gameObject.SetActive(true);
                Tab.tabbutton = this;

                _button = GetComponentInChildren<Button>();
                _button.onClick.AddListener(Tab.OnClickButton);

                if (!Tab.gameObject.activeSelf)
                {
                    //start awake method in parent tab
                    if (Tab.transform.parent != null)
                        Tab.transform.parent.gameObject.SetActive(true);
                    Tab.gameObject.SetActive(true);
                    Tab.Awake();
                }
            }
            else
            {
                _button = GetComponentInChildren<Button>();
                _button.onClick.AddListener(OnClick);
            }

            if (SignalButtonOn != null)
            {
                signalAvailable = true;
            }

            Init();
        }

        public override void Init()
        {
            _buttonImg = GetImage("Image");
            _buttonImg.sprite = ButtonImage;
            StartButtonColor = _buttonImg.color;
            bgImg = GetImage("Button");
            bgImg.color = new Color(Color.r, Color.g, Color.b, Color.a);
            counterText = GetText("CounterText");
            counterText.text = "";
        }

        public void Start()
        {
            if (Tab != null && Tab.TabActivated)
                SetBGColor(ConnectedTabActive);
        }

        // Update is called once per frame
        void Update()
        {
            if (signalAvailable)
            {
                if (SignalButtonOn.Value)
                {
                    Tab.visibilityCont(true);
                }
            }

            if (Color1Signal != null && Color1Signal.Value)
            {
                if (Image1 != null)
                    _buttonImg.sprite = Image1;

                bgImg.color = new Color(Color1.r, Color1.g, Color1.b, Color.a);
            }
            else if (Color2Signal != null && Color2Signal.Value)
            {
                if (Image2 != null)
                    _buttonImg.sprite = Image2;
                bgImg.color = new Color(Color2.r, Color2.g, Color2.b, Color.a);
            }
            else if (Color3Signal != null && Color3Signal.Value)
            {
                if (Image3 != null)
                    _buttonImg.sprite = Image3;
                bgImg.color = new Color(Color3.r, Color3.g, Color3.b, Color.a);
            }
        }

        public void SetButtonFeatures(Color color, string number)
        {
            _buttonImg.color = new Color(color.r, color.g, color.b, Color.a);
            counterText.color = new Color(color.r, color.g, color.b, Color.a);
            counterText.text = number;
        }

        public void ResetFeatures()
        {
            counterText.text = "";
            _buttonImg.color = StartButtonColor;
        }

        public void SetBGColor(Color color)
        {
            if (color == ColorMouseOver)
                Debug.Log("debug");
            bgImg.color = new Color(color.r, color.g, color.b, Color.a);
#if UNITY_EDITOR
            EditorUtility.SetDirty(bgImg);
#endif
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerOver = false;
            if (Tab != null && Tab.TabActivated)
                SetBGColor(ConnectedTabActive);
            else
                SetBGColor(Color);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_pointerOver)
            {
                SetBGColor(ColorMouseOver);
                _pointerOver = true;
            }
        }

        public void SetImage(bool active)
        {
            if (active)
                _buttonImg.sprite = ButtonImageActive;
            else
                _buttonImg.sprite = ButtonImage;
        }

        public void OnClick()
        {
            SetBGColor(Color);
            if (EventOnValueChanged != null)
            {
                EventOnValueChanged.Invoke(null);
            }
        }
    }
}