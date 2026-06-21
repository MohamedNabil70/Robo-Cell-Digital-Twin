// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;


namespace realvirtual
{
    //! HMI element to display a marker
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/hmi-components/hmi-marker")]
    public class HMI_Marker : HMI
    {
        [Header("Color Switch")]
        [OnValueChanged("Init")] public Material ColorOn;//!< Material to use when the signal is high
        [OnValueChanged("Init")] public Material ColorOff;//!< Material to use when the signal is low
       
        [OnValueChanged("Init")]public bool ObjectTracking;//!< If true, the marker will be placed at the position of the object
        [ShowIf("ObjectTracking")]
        [OnValueChanged("Init")]public GameObject TrackedObject;//!< Object to track
        [ShowIf("ObjectTracking")]
        public Vector3 Offset;//!< Offset to the tracked object

        [Header("Object Switch")]
        public GameObject EnableGameObjectOnSignalTrue;//!< Object to enable when the signal is high
        public GameObject EnableGameObjectOnSignalFalse;//!< Object to enable when the signal is low
        
        [Header("PLC IO's")] 
        public PLCOutputBool SignalBool;//!< Signal to use for the marker
        
        
        private Material _currentMaterial;
        private List<Material> OriginalMaterial;
        private MeshRenderer[] _meshRenderers;
        private bool _colorswitch;
        private bool _objectswitchtrue;
        private bool _objectswitchfalse;
        private List<GameObject> _switchObjects=new List<GameObject>();
        
        
        // Start is called before the first frame update
        new void Awake()
        {
            Init();
            
        }
        public override void Init()
        {
            if(ColorOn!=null && ColorOff!=null)
                _colorswitch = true;

            if (EnableGameObjectOnSignalTrue != null ) 
            {
                _objectswitchtrue = true;
                _switchObjects.Add(EnableGameObjectOnSignalTrue);
            }
            if( EnableGameObjectOnSignalFalse != null)
            {
                _objectswitchfalse = true;
                _switchObjects.Add(EnableGameObjectOnSignalFalse);
            }
            _meshRenderers = GetComponentsInChildren<MeshRenderer>();
            
            if (ObjectTracking && TrackedObject != null)
            {
                transform.position= TrackedObject.transform.position + Offset;
            }

        }
        // Update is called once per frame
        void Update()
        {
            if (_colorswitch)
            {
                if (SignalBool.Value == true)
                {
                    SetNewMaterial(ColorOn);
                }
                else
                {
                    SetNewMaterial(ColorOff);
                }
            }
            if(_objectswitchtrue && SignalBool.Value == true)
            {
               EnableGameObjectOnSignalTrue.SetActive(true);
               if(_objectswitchfalse)
                   EnableGameObjectOnSignalFalse.SetActive(false);
            }
            if(_objectswitchfalse && SignalBool.Value == false)
            {
                EnableGameObjectOnSignalFalse.SetActive(true);
               
                if(_objectswitchtrue)
                    EnableGameObjectOnSignalTrue.SetActive(false);
            }
        }
        private void SetNewMaterial(Material material)
        {
            foreach (var mesh in _meshRenderers)
            {
                if(!_switchObjects.Contains(mesh.gameObject))
                    mesh.material = material;
            }
            
        }
    }
}
