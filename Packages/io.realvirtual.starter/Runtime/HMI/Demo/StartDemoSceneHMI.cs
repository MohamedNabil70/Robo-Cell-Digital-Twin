using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
#if CINEMACHINE
using Cinemachine;
#endif
using UnityEngine;
#pragma warning disable 0414

namespace realvirtual
{
    public class StartDemoSceneHMI : MonoBehaviour
    {
        public bool StartDemoScene = true;
        public HMI_Switch StartCanConveyor;
        public HMI_Switch StartHandling;
        public HMI_Tab ControlTab;
        
        [Header("Parameter DropDownMenue")]
        public PLCInputInt ValueDropDown;
#if CINEMACHINE
        public List<CinemachineVirtualCamera> CameraViews;
        #else
        [InfoBox("Cinemachine is not installed. Please install it to use this feature.")]
#endif

        private int lastView = 1;
        private HMI_Controller _controller;
        // Start is called before the first frame update
        void Start()
        {
            _controller = GetComponent<HMI_Controller>();
            if (StartDemoScene)
            {
                ControlTab.TabActivated = true;
                ControlTab.visibilityCont(true);
               // StartCanConveyor.ToggleChanged(true);
                StartHandling.ToggleChanged(true);

            }
        }

        public void Update()
        {
#if CINEMACHINE
            if (ValueDropDown.Value != lastView)
            {
                _controller.SetCamera(CameraViews[ValueDropDown.Value-1]);
                lastView = ValueDropDown.Value;
            }
#endif
        }
    }
}
