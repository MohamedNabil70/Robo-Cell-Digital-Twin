// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using System;
using System.Collections.Generic;
#if CINEMACHINE
using Cinemachine; 
#endif
using NaughtyAttributes;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;


namespace realvirtual
{
    public enum MessageTypes {Message,Warning,Failure}
    //! HMI element to display messages
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-message")]
    public class HMI_Message : BehaviorInterface
    {
        [Header("Settings")]
        [OnValueChanged("Modify")]public MessageTypes MsgType;//!< Type of message
        public bool UseUserDefinedColor;//!< if true, use user defined color of the message background
        [ShowIf("UseUserDefinedColor")] public Color BackgroundColor;//!< user defined color of the message background
        public bool UseUserDefinedIcon;//!< if true, use user defined icon
        [ShowIf("UseUserDefinedIcon")] public Sprite MessageIcon;//!< user defined icon
        [OnValueChanged("Modify")]public string Text;//!< text of the message
        [OnValueChanged("Modify")]public int SizeText;//!< size of the text
        [OnValueChanged("Modify")] public Color FontColor;//!< color of the text and icons
        [OnValueChanged("Modify")]public bool ShowTimeStamp;//!< if true, show time stamp
        [OnValueChanged("Modify")]public bool Modal = false;//!< if true, the message is modal
        public int MessageCheckTime;//!< time in seconds to check if the message is still valid after canceling
        [OnValueChanged("Modify")]public bool CancelButton;//!< if true, show cancel button
        [OnValueChanged("Modify")]public bool AcknowledgeButton;//!< if true, show acknowledge button
        [OnValueChanged("Modify")] public bool ShowButton;//!< if true, show information button
        public bool ForceAcknowledge = false;//!< if true, the message has to be acknowledged by the user
  
        [Header("Event Message")]
#if CINEMACHINE
        public CinemachineVirtualCamera MessageCamera;//!< camera which is activated when the message is shown
#else
        [InfoBox("Please install Cinemachine via the package manager for full HMI functionality")] 
#endif
        public GameObject MessageTriggeredObject; //!< object which is activated when the message is shown
        
        [Header("Event Information")]
#if CINEMACHINE      
        public CinemachineVirtualCamera InformationCamera;//!< camera which is activated when the information button is pressed
        #else
        [Header("To define a camera, please import Cinemachine")]
#endif
        public GameObject InformationTriggeredObject;//!< object which is activated when the information button is pressed


        public bool Highlighting;//!< if true, the defined objects is highlighted
        [ShowIf("Highlighting")] public List<MeshRenderer> HightlightedObjects=new List<MeshRenderer>();//!< list of objects which are highlighted
        [ShowIf("Highlighting")] public Material HighlightMaterial;//!< material for highlighting
        [ShowIf("Highlighting")] public bool FlickerEffectMaterial = false;//!< if true, the highlighting material is flickering
        [ShowIf("FlickerEffectMaterial")] public Material FlickerMaterial;//!< material for flickering
        [ShowIf("FlickerEffectMaterial")] public float FlickerTime = 2f;//!< time for flickering
        
        
        [Header("PLC IO's")]
        public PLCOutputBool SignalMessage;//!< PLC output for message
        public PLCInputBool SignalAcknowledge;//!< PLC input for acknowledge
        public PLCInputBool SignalCancel;//!< PLC input for cancel
        [Foldout("References")]public Image IconReference;
        [Foldout("References")]public TextMeshProUGUI TextReference;
        [Foldout("References")]public Button AcknowledgeButtonReference;
        [Foldout("References")]public Button CancelButtonReference;
        [Foldout("References")]public Button ShowButtonReference;

        [HideInInspector] public DateTime timeStamp = new DateTime(0);
        [HideInInspector] public string currentType;
        [HideInInspector] public HMI_Tab parentTab;
        [HideInInspector] public Image bgImg;
        private bool signalChange = false;
        private GameObject content;
        private Color defaultColor;
        private Sprite defaultMsgIcon;
   
        private List<ObjectSelection> selections = new List<ObjectSelection>();
        private bool highlighted = false;
        
        private bool CamNavigation;
        private bool _msgCamera;
        
        public void Modify()
        {
            content=Global.GetGameObjectByName("ContArea",gameObject);
            content.SetActive(true);
            Global.SetActiveSubObjects(content,true);
            bgImg = content.GetComponent<Image>();
            if (UseUserDefinedColor)
            {
                bgImg.color=BackgroundColor;
            }
            else
            {
                getDefaultColor(currentType);
                bgImg.color = defaultColor;
            }
            if (UseUserDefinedIcon)
            {
                IconReference.sprite = MessageIcon;
                IconReference.color = FontColor;
            }
            else
            {
                getDefaultSprite(currentType);
                IconReference.sprite = defaultMsgIcon;
                IconReference.color = FontColor;
            }
            if (Highlighting && HightlightedObjects.Count == 0)
            {
                #if UNITY_EDITOR
                EditorUtility.DisplayDialog("Error:", "Pleas define a game objcet which will be highlighted.", "OK", "");
                #endif
                return;
            }

            if (TextReference != null)
            {
                TextReference.text = Text;
                TextReference.fontSize = SizeText;
                TextReference.color = FontColor;
            }
#if CINEMACHINE
            ShowButtonReference.onClick.RemoveAllListeners();
            ShowButtonReference.onClick.AddListener(ClickShowButtonReference);
#endif
            AcknowledgeButtonReference.onClick.RemoveAllListeners();
            AcknowledgeButtonReference.onClick.AddListener(ClickQuitButtonReference);
            CancelButtonReference.onClick.RemoveAllListeners();
            CancelButtonReference.onClick.AddListener(ClickCancelButtonReference);
            
            setButtonState(ShowButtonReference,ShowButton);
            setButtonState(AcknowledgeButtonReference,AcknowledgeButton);
            setButtonState(CancelButtonReference,CancelButton);
#if CINEMACHINE  
            if(InformationCamera != null )
                CamNavigation =true;
            else
                CamNavigation = false;
            
            if(MessageCamera != null )
                _msgCamera =true;
            else
                _msgCamera = false;
   #endif         
           
#if UNITY_EDITOR
            switch (MsgType)
            {
                case MessageTypes.Message:
                {
                    currentType = "Message";
                    break;
                }
                case MessageTypes.Warning:
                {
                    currentType = "Warning";
                    break;
                }
                case MessageTypes.Failure:
                {
                    currentType = "Failure";
                    break;
                }
            }
#endif
            if(Application.isPlaying)
                content.SetActive(false);
        }
#if CINEMACHINE
        public void ClickShowButtonReference()
        {
            Debug.Log("start move");
            if (CamNavigation)
            {
                parentTab.ChangeToTargetCamera(InformationCamera);
            }
        }
#endif  
        public void ClickQuitButtonReference()
        {
            
            if(SignalAcknowledge != null)
                SignalAcknowledge.SetValue(true);
        
            ShowMessage(false);
            
            parentTab.OnClickAcknowledge();
#if CINEMACHINE
            if (CamNavigation)
            {
                parentTab.ReturnToNavigationCamera();
            }
#endif

            timeStamp = new DateTime(0);

        }
        public void ClickCancelButtonReference()
        {
            parentTab.OnClickCancel();
            if(SignalCancel != null)
                SignalCancel.SetValue(true);
            if(MessageCheckTime>0 && SignalCancel != null)
                Invoke("CheckSignal",MessageCheckTime);
        }

        public void ShowMessage(bool state)
        {
            content.SetActive(state);
            if (state)
            {
                if (Highlighting)
                {
                    foreach (var mesh in HightlightedObjects)
                    {
                        if (mesh.gameObject.GetComponent<ObjectSelection>() == null)
                        {
                            var obj = mesh.gameObject.AddComponent<ObjectSelection>();
                            obj.SetNewMaterial(HighlightMaterial);
                            selections.Add(obj);
                            if(FlickerEffectMaterial)
                                Invoke("Flicker",FlickerTime);
                        }
                    }
                    highlighted = true;
                }
#if CINEMACHINE
                if(_msgCamera)
                    parentTab.ChangeToTargetCamera(MessageCamera);
#endif
                if(timeStamp == new DateTime(0))
                    timeStamp = DateTime.Now;
               
                if (ShowTimeStamp)
                {
                    TextReference.text = "[" +timeStamp.ToString("HH:mm:ss")+"]: " + Text;
                }
            }
            else
            {
                if (Highlighting)
                {
                    foreach (var sel in selections)
                    {
                        sel.ResetMaterial();
                    }
                    selections.Clear();
                    highlighted = false;
                }
#if CINEMACHINE
                if(_msgCamera)
                    parentTab.ReturnToNavigationCamera();
#endif
            }
        }
        // Update is called once per frame
        void Update()
        {
            if (SignalMessage.Value && !signalChange)
            {
                if (Modal)
                {
                    parentTab.ModalMessageActivated(this);
                }
                signalChange = true;
                timeStamp = DateTime.Now;
                if (ShowTimeStamp)
                {
                    TextReference.text = "[" +timeStamp.ToString("HH:mm:ss")+"]: " + Text;
                }
            }
            else
            {
                if(SignalMessage.Value==false && signalChange)
                {
                    signalChange = false;
                    if (!ForceAcknowledge)
                    {
                        ShowMessage(false);
                    }
                }
            }
        
        }
        private void setButtonState(Button button, bool state)
        {
            if (state)
            {
                button.gameObject.SetActive(true);
            }
            else
            {
                button.gameObject.SetActive(false);
            }
            button.image.color=FontColor;
        }
       

        private void getDefaultColor(string msgType)
        {
            switch (msgType)
            {
                case "Message":
                    defaultColor = new Color(116, 116, 116, 1f);
                    break;
                case "Warning":
                    defaultColor = new Color(251, 232, 0, 1f);
                    break;
                
                case "Failure":
                    defaultColor = new Color(255, 0, 0, 1f);
                    break;
            }
        }
        private void getDefaultSprite(string msgType)
        {
            switch (msgType)
            {
                case "Message":
                    defaultMsgIcon = UnityEngine.Resources.Load<Sprite>("Icons/info");
                    break;

                case "Warning":
                    defaultMsgIcon = UnityEngine.Resources.Load<Sprite>("Icons/warning");
                    break;

                case "Failure":
                    defaultMsgIcon = UnityEngine.Resources.Load<Sprite>("Icons/failure");
                    break;

                default:
                    Debug.LogWarning("Unknown msgType: " + msgType);
                    break;
            }
        }
        private void CheckSignal()
        {
            SignalCancel.SetValue(false);
        }

        private void Flicker()
        {
            if (SignalMessage.Value)
            {
                if (highlighted)
                {
                    foreach (var obj in selections)
                    {
                        FlickMaterial(obj, FlickerMaterial);
                    }
                    highlighted = false;
                }
                else
                {
                    foreach (var obj in selections)
                    {
                        FlickMaterial(obj,HighlightMaterial);
                    }
                    highlighted = true;
                }
                Invoke("Flicker",FlickerTime);
            }
        }

        private void FlickMaterial(ObjectSelection obj, Material material)
        {
            
            var meshrenderer =obj.GetComponentInChildren<MeshRenderer>();
            Material[] sharedMaterialsCopy = meshrenderer.materials;

            for (int i = 0; i < meshrenderer.materials.Length; i++)
            {
                sharedMaterialsCopy[i] = material;
            }

            meshrenderer.materials = sharedMaterialsCopy;
        }
    }
}
