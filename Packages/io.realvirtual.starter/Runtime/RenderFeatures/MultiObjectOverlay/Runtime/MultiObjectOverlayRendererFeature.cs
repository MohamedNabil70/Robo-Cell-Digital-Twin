// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace realvirtual.RendererFeatures
{
    public class MultiObjectOverlayRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] public Color color = Color.white;

        public enum Mode
        {
            Default,
            XRay,
            Blink
        }

        [SerializeField] public Mode mode;
        
        
        [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;

        
        private Material overlayMaterial; 


        private MultiObjectOverlayPass _overlayPass;

        private Renderer[] _targetRenderers;


        private Mode _currentMode = Mode.Default;
        [SerializeField] public float blinkSpeed = 1;

        public void SetRenderers(Renderer[] targetRenderers)
        {
            _targetRenderers = targetRenderers;

            if (_overlayPass != null)
                _overlayPass.Renderers = _targetRenderers;
        }

        public override void Create()
        {
            // Pass in constructor variables which don't/shouldn't need to be updated every frame.
            _overlayPass = new MultiObjectOverlayPass();

            _currentMode = mode;
            
            // NEVER use DestroyImmediate in Create() - it can cause crashes during serialization
            // The old material will be garbage collected automatically
            overlayMaterial = null;
            overlayMaterial = GetMaterial();
            
            // Apply the color immediately after creating the material
            if (overlayMaterial != null)
            {
                overlayMaterial.SetColor("_BaseColor", color);
                overlayMaterial.SetFloat("_Speed", blinkSpeed);


            }
        }

        Material GetMaterial()
        {
            switch (_currentMode)
            {
                case Mode.Default:
                    return new Material(Shader.Find("Shader Graphs/OverlayHighlight"));
                case Mode.XRay:
                    return new Material(Shader.Find("Shader Graphs/OverlayHighlightXRay"));
                case Mode.Blink:
                    return new Material(Shader.Find("Shader Graphs/OverlayHighlightBlink"));
                default:
                    return null;
            }
        }

        void CheckSwitchShader()
        {
            if (mode != _currentMode)
            {
                _currentMode = mode;
                // NEVER use DestroyImmediate here - it can cause crashes
                // Simply replace the reference, Unity will handle cleanup
                overlayMaterial = GetMaterial();
                
                // Apply the color to the new material
                if (overlayMaterial != null)
                {
                    overlayMaterial.SetColor("_BaseColor", color);
                    overlayMaterial.SetFloat("_Speed", blinkSpeed);

                }
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_overlayPass == null)
            {
                Create();
            }
            
            if (_overlayPass == null)
                return;

            // Check if material needs to be recreated (can happen after domain reload)
            if (overlayMaterial == null)
            {
                overlayMaterial = GetMaterial();
                if (overlayMaterial != null)
                {
                    overlayMaterial.SetColor("_BaseColor", color);
                    overlayMaterial.SetFloat("_Speed", blinkSpeed);

                }
            }

            if (!overlayMaterial ||
                _targetRenderers == null ||
                _targetRenderers.Length == 0)
            {
                // Don't render the effect if there's nothing to render
                return;
            }

            // Any variables you may want to update every frame should be set here.
            _overlayPass.RenderEvent = renderEvent;

            CheckSwitchShader();
            overlayMaterial.SetColor("_BaseColor", color);
            overlayMaterial.SetFloat("_Speed", blinkSpeed);


            _overlayPass.OverlayMaterial = overlayMaterial;

            _overlayPass.Renderers = _targetRenderers;

            renderer.EnqueuePass(_overlayPass);
        }

     
        
        public void SetColor(Color color)
        {
            this.color = color;
        }

        public void SetMode(Mode mode)
        {
            this.mode = mode;
            CheckSwitchShader();
        }
        
    }
}
