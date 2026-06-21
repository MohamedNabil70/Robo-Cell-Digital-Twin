// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using System.Collections.Generic;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace realvirtual
{
    //! HMI prefab to generate a drop down menu with a list of options
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-dropdown")]
    public class HMI_DropDown : HMI, IPointerEnterHandler, IPointerExitHandler,IPointerClickHandler
{

        public Color ColorElements;//!< Color of the elements
        public Color ColorSelected;//!< Color of the selected element
        [OnValueChanged("Init")]public string Menuetitle;//!< Title of the menu
        [OnValueChanged("Init")]public int TextSize = 14;//!< Text size of the elements
        [OnValueChanged("Init")]public List<string> DropDownElements = new List<string>();//!< List of elements
        [OnValueChanged("Init")] public string StartMode;//<! Start mode of the menu
        
        public PLCInputInt SignalSelectedElement;//!< Signal which displays the selected element
        public PLCInputInt SignalSetElement;//!< Signal which sets the selected element
        public int Duration;//!< Duration how long the menu is visible after the mouse left the menu area
        public bool ActivateMenueOnMouseEnter = false;//!< If true the menu is activated when the mouse enters the menu area
        
        private GameObject MenueObj;
        private GameObject ButtonPrefab;
        private string currentChoice;
        private TextMeshProUGUI title;
        private List<GameObject> buttonlist = new List<GameObject>();
        private TextMeshProUGUI ElementTitle;
        private GameObject element;
        private GameObject elementTitle;
        private HMI_MenueItem ButtonUI;
        private VerticalLayoutGroup OwnLayout;
        private float HightMenue;
        private RectTransform Menueformat;
        private int lastValue;
        private int startInt = 0;


        public new void Awake()
        {
            Init();
            MenueObj.SetActive(false);
        }
        public override void Init()
        {
            
            ButtonPrefab = Global.GetComponentByName<Component>(this.gameObject, "ItemDefault").gameObject;
            MenueObj = Global.GetComponentByName<Component>(this.gameObject, "Menue").gameObject;
            MenueObj.GetComponent<HMI_MouseAreaCtrl>().ConnectedButton = this;
            OwnLayout = MenueObj.GetComponent<VerticalLayoutGroup>();
            HightMenue =( OwnLayout.spacing * DropDownElements.Count )+ OwnLayout.padding.bottom + OwnLayout.padding.top;
            if (DropDownElements.Count == 0)
            {
                Debug.LogError("No menue defined.");
                return;
            }

            title = GetText("Title");
            if (title != null)
            {
                title.text = Menuetitle;
                title.fontSize = TextSize;
                getElements();
                foreach (var obj in buttonlist)
                {
                    DestroyImmediate(obj);
                }

                buttonlist.Clear();

                for (int i = 0; i < DropDownElements.Count; i++)
                {
                    GameObject button = Instantiate(ButtonPrefab, MenueObj.transform);
                    button.name = DropDownElements[i];
                    if (button.name == StartMode)
                        startInt = i;
                    button.SetActive(true);
                    buttonlist.Add(button);
                }

                for (int i = 0; i < buttonlist.Count; i++)
                {
                    ButtonUI = buttonlist[i].GetComponent<HMI_MenueItem>();
                    ButtonUI.bgImg = ButtonUI.gameObject.GetComponent<Image>();
                    ButtonUI.Color = new Color(ColorElements.r, ColorElements.g,
                        ColorElements.b, Color.a);
                    ButtonUI.ColorMouseOver = new Color(ColorMouseOver.r, ColorMouseOver.g, ColorMouseOver.b,
                        ColorMouseOver.a);
                    ButtonUI.currentMenue = this;
                    if (i == startInt)
                    {
                        if (SignalSelectedElement != null)
                            SignalSelectedElement.Value = i + 1;
                    }

                    RectTransform currentButton = buttonlist[i].GetComponent<RectTransform>();
                    HightMenue = HightMenue + currentButton.rect.height;

                    element = Global.GetComponentByName<Component>(buttonlist[i].gameObject, "Text").gameObject;
                    element.SetActive(true);
                    ElementTitle = element.GetComponent<TextMeshProUGUI>();
                    ElementTitle.text = DropDownElements[i];
                    ElementTitle.fontSize = TextSize;
                }
                
                Menueformat = MenueObj.GetComponent<RectTransform>();
                Menueformat.sizeDelta = new Vector2(Menueformat.rect.width, HightMenue);
                var menueformatPosition = Menueformat.localPosition;
                var buttonformat = this.GetComponent<RectTransform>();
                var y = (HightMenue / 2) - (buttonformat.rect.height / 2);
                Menueformat.localPosition = new Vector3(menueformatPosition.x, -y, menueformatPosition.z);
                title = GetText("Title");
                title.text = DropDownElements[startInt];
                title.fontSize=TextSize;
                bgImg = GetImage("MenueBase");
                UpdateColorSetting(buttonlist[startInt].transform.name);
            }
            MenueObj.SetActive(false);
            ButtonPrefab.SetActive(false);
            
            
        }
        private void getElements()
        {
            Transform[] elem = MenueObj.GetComponentsInChildren<Transform>();
            foreach (var obj in elem)
            {
                if (obj.gameObject != MenueObj)
                    buttonlist.Add(obj.gameObject);
            }
        }
       
        private void CloseMenu()
        {
            MenueObj.SetActive(false);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            bgImg.color=new Color(ColorMouseOver.r,ColorMouseOver.g,ColorMouseOver.b,ColorMouseOver.a);
            if (ActivateMenueOnMouseEnter)
            {
                UpdateColorSetting(title.text);
                MenueObj.SetActive(true);
                if(SignalSelectedElement!=null)
                    lastValue = SignalSelectedElement.Value;
            }
            
        }
        public override void CloseExtendedArea(Vector3 pos)
        {
            if (!MenueObj.GetComponent<RectTransform>().rect.Contains(MenueObj.transform.InverseTransformPoint(pos)))
                Invoke("CloseMenu",0.5f);
        }
        
        public void OnPointerExit(PointerEventData eventData)
        {
            bgImg.color=new Color(Color.r,Color.g,Color.b,Color.a);
            if (!MenueObj.GetComponent<RectTransform>().rect.Contains(MenueObj.transform.InverseTransformPoint(eventData.position)))
                Invoke("CloseMenu",0.5f);
        }
    
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!ActivateMenueOnMouseEnter)
            {
                MenueObj.SetActive(true);
                if(SignalSelectedElement!=null)
                    lastValue = SignalSelectedElement.Value;
            }
        }
        public void elementClick(string elementName)
        {
            UpdateColorSetting(elementName);
        }
        private void UpdateColorSetting(string elementName)
        {
            HMI_MenueItem currentItem;
            for (int i = 0; i < buttonlist.Count; i++)
            {
                currentItem = buttonlist[i].GetComponent<HMI_MenueItem>();
                if (buttonlist[i].name == elementName)
                {
                    currentItem.Color = new Color(ColorSelected.r,ColorSelected.g,ColorSelected.b,ColorSelected.a);
                    currentItem.bgImg.color = new Color(ColorSelected.r,ColorSelected.g,ColorSelected.b,ColorSelected.a);
                    title.text = buttonlist[i].name;
                    if (SignalSelectedElement != null)
                        SignalSelectedElement.Value = i + 1;
                }
                else
                {
                    currentItem.Color = new Color(ColorElements.r,ColorElements.g,ColorElements.b,ColorElements.a);
                    currentItem.bgImg.color = new Color(ColorElements.r,ColorElements.g,ColorElements.b,ColorElements.a);
                }
            }
        }
    
    }
}
