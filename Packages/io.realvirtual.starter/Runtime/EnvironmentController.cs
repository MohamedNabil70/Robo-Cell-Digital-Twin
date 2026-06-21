using System;
using UnityEngine;
using NaughtyAttributes;


namespace realvirtual
{
    [ExecuteAlways]
    public class EnvironmentController : MonoBehaviour
    {
        public enum Mode
        {
            Default,
            Dark,
            White
        }
        
        
        [Header("General")]
        [OnValueChanged("UpdateEnvironment")]
        public Mode mode = Mode.Default;
        
        [OnValueChanged("UpdateEnvironment")]
        public bool advanced = false;
        
        [Header("Floor")]
        [OnValueChanged("UpdateFloor")]
        public int size = 10;
        [OnValueChanged("UpdateFloor")]
        public bool fade = false;

        private void Start()
        {
            if (!Application.isPlaying)
            {
                UpdateEnvironment();
            }
            
            UpdateFloor();
        }

        private void OnValidate()
        {
            UpdateFloor();
        }

        private void UpdateFloor()
        {
            EvironmentSetup env;
            if (mode == Mode.Default)
            {
                env = transform.Find("Default").gameObject.GetComponent<EvironmentSetup>();
            }
            else if (mode == Mode.Dark)
            {
                env = transform.Find("Dark").gameObject.GetComponent<EvironmentSetup>();
            }
            else
            {
                env = transform.Find("White").gameObject.GetComponent<EvironmentSetup>();
            }
            
            env.size = size;
            env.fade = fade;
            
            env.SetupFloor();
        }
        
        
        [Button("Refresh")]
        public void UpdateEnvironment()
        {
            EvironmentSetup defaultEnv = transform.Find("Default").gameObject.GetComponent<EvironmentSetup>();
            EvironmentSetup darkEnv = transform.Find("Dark").gameObject.GetComponent<EvironmentSetup>();
            EvironmentSetup whiteEnv = transform.Find("White").gameObject.GetComponent<EvironmentSetup>();
            
            darkEnv.Deactivate();
            whiteEnv.Deactivate();
            defaultEnv.Deactivate();
            
            
            if (mode == Mode.Default)
            {
                defaultEnv.advanced = advanced;
                defaultEnv.size = size;
                defaultEnv.fade = fade;
                defaultEnv.Activate();
            }
            else if (mode == Mode.Dark)
            {
                darkEnv.advanced = advanced;
                darkEnv.size = size;
                darkEnv.fade = fade;
                darkEnv.Activate();
                
            }
            else if (mode == Mode.White)
            {
                whiteEnv.advanced = advanced;
                whiteEnv.size = size;
                whiteEnv.fade = fade;
                whiteEnv.Activate();
            }
        }
    }

}
