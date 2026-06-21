// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using UnityEngine;

namespace realvirtual
{
    [AddComponentMenu("realvirtual/Visualization/Lamp")]
    [SelectionBase]
    //! Lamp component for creating visual status indicators in industrial automation simulations.
    //! Drives the lamp's appearance via a MaterialPropertyBlock (emission color and intensity)
    //! without instantiating per-renderer materials, so batching is preserved. Supports flashing,
    //! an optional point light, and PLC signal control via SignalLampOn and SingalLampFlashing.
    //!
    //! The shared material on the MeshRenderer must have emission enabled (e.g. URP Lit /
    //! HDRP Lit / Standard with the _EMISSION keyword active) for emission overrides to take effect.
    public class Lamp : realvirtualBehavior
    {
        [Header("Appearance")]
        [Tooltip("Emission color of the lamp when on")]
        public Color OnColor = Color.red; //!< Emission color used when the lamp is on
        [Tooltip("Emission intensity multiplier applied to OnColor (HDR). Higher values produce stronger bloom.")]
        [Min(0f)] public float Intensity = 2f; //!< HDR emission intensity multiplier for the on state

        [Header("Lamp IO's")]
        [Tooltip("Enable flashing mode for the lamp")]
        public bool Flashing = false; //!< True if lamp should be flashing
        [Tooltip("Flashing period in seconds")]
        public float Period = 1; //!< Lamp flashing period in seconds
        [Tooltip("Current lamp state (on/off)")]
        public bool LampOn = false; //!< Lamp is on if true
        [Tooltip("PLC signal to control lamp on/off state")]
        public PLCOutputBool SignalLampOn;
        [Tooltip("PLC signal to control lamp flashing")]
        public PLCOutputBool SingalLampFlashing;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MeshRenderer _meshrenderer;
        private MaterialPropertyBlock _mpb;
        private Light _lamp;

        private float _timeon;
        private bool _lamponbefore;
        private bool _lampon;
        private bool _signallamponNotNull;
        private bool _signallampflashingNotNull;

        private void InitLight()
        {
            if (_meshrenderer == null) _meshrenderer = GetMeshRenderer();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            ApplyEmission(LampOn);
        }

        private void ApplyEmission(bool on)
        {
            if (_meshrenderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            _meshrenderer.GetPropertyBlock(_mpb);
            Color emission = on ? OnColor.linear * Intensity : Color.black;
            _mpb.SetColor(EmissionColorId, emission);
            _mpb.SetColor(BaseColorId, OnColor);
            _mpb.SetColor(ColorId, OnColor);
            _meshrenderer.SetPropertyBlock(_mpb);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            EnsureLampMaterial();
#endif
            InitLight();
        }

#if UNITY_EDITOR
        private void EnsureLampMaterial()
        {
            if (_meshrenderer == null) _meshrenderer = GetComponentInChildren<MeshRenderer>();
            if (_meshrenderer == null) return;

            var mat = UnityEngine.Resources.Load<Material>("Materials/Lamp");
            if (mat != null && _meshrenderer.sharedMaterial != mat)
                _meshrenderer.sharedMaterial = mat;
        }
#endif

        protected override void OnStartSim()
        {
            _timeon = Time.time;
            _lamponbefore = LampOn;
            _lamp = GetComponentInChildren<Light>();
            _signallamponNotNull = SignalLampOn != null;
            _signallampflashingNotNull = SingalLampFlashing != null;

            InitLight();
            Off();
        }

        //! Turns the lamp on.
        public void On()
        {
            LampOn = true;
            ApplyEmission(true);
            if (_lamp != null)
            {
                _lamp.color = OnColor;
                _lamp.enabled = true;
            }
        }

        //! Turns the lamp off.
        public void Off()
        {
            LampOn = false;
            ApplyEmission(false);
            if (_lamp != null)
                _lamp.enabled = false;
        }

        void Update()
        {
            if (_signallamponNotNull)
                LampOn = SignalLampOn.Value;
            if (_signallampflashingNotNull)
                Flashing = SingalLampFlashing.Value;

            if (Flashing)
            {
                float delta = Time.time - _timeon;
                if (!_lampon && delta > Period)
                    _lampon = true;
                else if (_lampon && delta > Period / 2)
                    _lampon = false;
            }
            else
            {
                _lampon = LampOn;
            }

            if (_lampon && _lampon != _lamponbefore)
            {
                On();
                _timeon = Time.time;
            }

            if (!_lampon && _lampon != _lamponbefore)
                Off();

            _lamponbefore = _lampon;
        }
    }
}
