// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;
#if CINEMACHINE
using Cinemachine;
#endif

#pragma warning disable 0414

namespace realvirtual
{
    //! HMI_Tab is a container for HMI_Elements
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-tab")]
    public class HMI_Tab : realvirtualBehavior 
    {
        
        public bool TabActivated;//!< is the tab currently active
        public bool Modal = false; //!< is the tab modal
        public bool BlockMouseNavigationWhenActive = false;
#if CINEMACHINE
        public CinemachineVirtualCamera Camera;//!< the camera to use for this tab
#endif
        public PLCOutputBool SignalTab;//!< the PLC signal to use for this tab
        public delegate void OnClickDelegate(); //!< delegate for the OnClick event

        public event OnClickDelegate OnClick; //!< event that is fired when the tab is clicked
       
        [HideInInspector]public GameObject cont;
        [HideInInspector]public HMI_TabButton tabbutton;
        private bool noSignal = false;
        private  List<GameObject> list = new List<GameObject>();
        private Object[] allObjects;
        private List<HMI_Message> messages;
        private bool _containsMessage = false;
        private HMI_Message _activeMsg;
       
        private List<HMI_Message> _activeFailure = new List<HMI_Message>();
        private List<HMI_Message> _activeWarning = new List<HMI_Message>();
        private List<HMI_Message> _activeInfo= new List<HMI_Message>();
        private List<HMI_Message> _activeMessages = new List<HMI_Message>();

        private HMI_Controller _controller;
        private bool _isCameraController = false;
        private bool _ownCamera = false;
        private bool MouseNavigationBlockedGenerally = false;

        
        public new void Awake()
        {
            gameObject.SetActive(true);
            cont= Global.GetGameObjectByName("Content",this.gameObject);
            if (cont == null)
            {
                Debug.Log("The necessary GameObject 'Content' is missing in the HMI_Tab '" + gameObject.name + "'.");
            }
            var overlay = Global.GetComponentByName<Component>(this.gameObject, "OverlayContent");
            if (overlay != null)
            {
                overlay.gameObject.SetActive(true);
                Global.SetActiveSubObjects(overlay.gameObject,true);
                List<HMI> elements = Global.GetComponentsAlsoInactive<HMI>(overlay.gameObject);
                foreach (var obj in elements)
                {
                    obj.Init();
                }
            }
            var worldSpace = Global.GetComponentByName<Component>(this.gameObject, "WorldspaceContent");
            if(worldSpace != null)
            {
                worldSpace.gameObject.SetActive(true);
                Global.SetActiveSubObjects(worldSpace.gameObject,true);
                List<HMI> elements = Global.GetComponentsAlsoInactive<HMI>(worldSpace.gameObject);
                foreach (var obj in elements)
                {
                    obj.Init();
                }
            }
            // Check for Cameracontroller
            _controller= FindAnyObjectByType<HMI_Controller>();
            if (_controller != null)
            {
                _isCameraController = true;
                if (_controller.BlockUserMouseNavigation)
                    MouseNavigationBlockedGenerally = true;
            }

            if (SignalTab == null)
            {
                noSignal = true;
            }
            // check for Type HMI_Message in children
            messages = Global.GetComponentsAlsoInactive<HMI_Message>(cont);
            if (messages.Count > 0)
            {
                _containsMessage = true;
                foreach (var msg in messages)
                {
                    msg.Modify();
                   
                    msg.parentTab = this;
                }
            }
#if CINEMACHINE   
            if(Camera!=null)
                _ownCamera = true;
#endif
            
        }
        public void Start()
        {
            if (TabActivated)
            {
                visibilityCont(true);
                if (OnClick != null)
                    OnClick();
                
            }
            else
            {
                visibilityCont(false);
            }
        }

        public void OnClickButton()
        {
            if (OnClick != null)
                OnClick();
            
            if (cont.activeSelf )
            {
                visibilityCont(false);
                TabActivated = false;
                if(_activeMsg != null)
                    _activeMsg.ShowMessage(false);
#if CINEMACHINE
                _controller.ResetCamera();
#endif
            }
            else
            {
                TabActivated = true;
                if (_containsMessage)
                {
                    visibilityCont(true);
                    hideOtherTabs();
                    _activeMsg = getCurrentMessage();
                    if (_activeMsg != null)
                    {
                        _activeMsg.ShowMessage(true);
                    }
                    
                }
                else
                {
                    visibilityCont(true);
                    hideOtherTabs();
                }
#if CINEMACHINE
                if(_ownCamera)

                    _controller.SetCamera(Camera);
#endif
            }
        }
        public void visibilityCont(bool vis)
        {
            if(cont==null)
                cont= Global.GetGameObjectByName("Content",this.gameObject);
            cont.SetActive(vis);
            if(tabbutton!=null)
                if(vis)
                    tabbutton.SetBGColor(tabbutton.ConnectedTabActive);
                else
                {
                    tabbutton.SetBGColor(tabbutton.Color);
                }
            if(BlockMouseNavigationWhenActive && vis)
                _controller.SetMouseNavigsationStatus(true);
            else
            {
                if(!MouseNavigationBlockedGenerally)
                    _controller.SetMouseNavigsationStatus(false);
            }

        }
        public void Update()
        {
            if (!noSignal)
            {
                if (SignalTab.Value)
                {
                    visibilityCont(true);
                    hideOtherTabs();
                    if (_containsMessage)
                    {
                        _activeMsg = getCurrentMessage();
                        if(_activeMsg != null)
                            _activeMsg.ShowMessage(true);
                    }
#if CINEMACHINE
                    if(_ownCamera)

                        _controller.SetCamera(Camera);
#endif
                }
                else
                {
                    visibilityCont(false);
                }
            }
            else
            {
                if (_containsMessage)
                {
                    _activeMsg = getCurrentMessage();
                    if (_activeMsg == null)
                    {
                        
                    }
                    else
                    {
                        
                        if (_activeMsg != null)
                        {
                            if (!TabActivated && _activeMsg.Modal)
                            {
                                visibilityCont(true);
                                hideOtherTabs();
                            }
                            if(TabActivated || _activeMsg.ForceAcknowledge)
                                _activeMsg.ShowMessage(true);
                        }
                    }
                }
                
            }
            
        }
        public void OnClickAcknowledge()
        {
            _activeMsg = null;
        }
        public void OnClickCancel()
        {
            int pos = 0;
            // get position of activeMsg in list activemessages
            if (_activeMsg != null)
            {
                pos = _activeMessages.IndexOf(_activeMsg);
                _activeMsg.ShowMessage(false);
            }

            if (_activeMessages.Count == 1)
            {
                
            }
            else
            {
                if (_activeMessages.Count - 1 >= pos + 1)
                    _activeMsg = _activeMessages[pos + 1];
                else
                {
                    _activeMsg = _activeMessages[0];
                    _activeMsg.ShowMessage(true);
                }
            }

        }
        public void ModalMessageActivated(HMI_Message msg)
        {
            visibilityCont(true);
            hideOtherTabs();
            msg.ShowMessage(true);
        }
#if CINEMACHINE
        public void ChangeToTargetCamera(CinemachineVirtualCamera msgcam)
        {
            if (_isCameraController)
            {
                _controller.SetCamera(msgcam);
            }
        }
        
        public void ReturnToNavigationCamera()
        {
            _controller.ResetCamera();
        }
#endif
        #region private Methods
        
        private HMI_Message getCurrentMessage()
        {
            messages.Sort((x, y) => x.timeStamp.CompareTo(y.timeStamp));
            _activeFailure.Clear();
            _activeWarning.Clear();
            _activeInfo.Clear();
            // add all messages of type failure to list
            foreach (var msg in messages)
            {
                if (msg.SignalMessage.Value && (msg.SignalAcknowledge==null || msg.SignalAcknowledge.Value==false) && (msg.SignalCancel== null || msg.SignalCancel.Value==false))
                {
                    if (msg.currentType == "Failure")
                    {
                        if (!_activeFailure.Contains(msg))
                            _activeFailure.Add(msg);
                    }
                    if (msg.currentType == "Warning")
                    {
                        if (!_activeWarning.Contains(msg) ) 
                            _activeWarning.Add(msg);
                    }
                    if (msg.currentType == "Message" )
                    {
                        if (!_activeInfo.Contains(msg))
                            _activeInfo.Add(msg);
                    }
                    msg.ShowMessage(false);
                }
                
            }
            // combine all lists to one
            _activeMessages.Clear();
            _activeMessages.AddRange(_activeFailure);
            _activeMessages.AddRange(_activeWarning);
            _activeMessages.AddRange(_activeInfo);
            if (_activeMessages.Count > 0)
            {
                string number = "";
                switch (_activeMessages[0].currentType)
                {
                    case "Failure":
                        number = _activeFailure.Count.ToString();
                        break;
                    case "Warning":
                        number = _activeWarning.Count.ToString();
                        break;
                    case "Message":
                        number = _activeInfo.Count.ToString();
                        break;
                }
                tabbutton.SetButtonFeatures(_activeMessages[0].bgImg.color,number);
                
                return _activeMessages[0];
            }
            else
            {
                tabbutton.ResetFeatures();
                return null;
            }
        }

        private void hideOtherTabs()
        {
            if (Modal)
            {
                allObjects = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(HMI_Tab));

                var groupcomps = UnityEngine.Resources.FindObjectsOfTypeAll(typeof(HMI_Tab));

                foreach (var comp in allObjects)
                {
                    var gr = (HMI_Tab)comp;
                    #if UNITY_EDITOR
                    if (EditorUtility.IsPersistent(gr.transform.root.gameObject))
                        continue;
                    #endif
                    if (gr.gameObject != this.gameObject)
                    {
                        gr.visibilityCont(false);
                        gr.TabActivated = false;
                        if(gr.tabbutton!=null)
                            gr.tabbutton.SetBGColor(gr.tabbutton.Color);
                    }

                }
            }

        }
        #endregion
    }
}
