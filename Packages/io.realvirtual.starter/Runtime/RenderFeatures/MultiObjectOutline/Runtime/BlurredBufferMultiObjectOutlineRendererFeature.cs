// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  


using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace realvirtual.RendererFeatures
{
    public class BlurredBufferMultiObjectOutlineRendererFeature : ScriptableRendererFeature
    {
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");

        [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;
        private Material dilationMaterial;
        private Material outlineMaterial;
        [SerializeField] public int spread = 15;

        private BlurredBufferMultiObjectOutlinePass _outlinePass;

        [SerializeField] public Color color = Color.white;

        private Renderer[] _targetRenderers;

        public void SetRenderers(Renderer[] targetRenderers)
        {
            _targetRenderers = targetRenderers;

            if (_outlinePass != null)
                _outlinePass.Renderers = _targetRenderers;
        }

        public override void Create()
        {
            name = "Multi-Object Outliner";

            // Pass in constructor variables which don't/shouldn't need to be updated every frame.
            _outlinePass = new BlurredBufferMultiObjectOutlinePass();

            // NEVER use DestroyImmediate in Create() - it can cause crashes during serialization
            // Simply replace the references, Unity will handle cleanup
            dilationMaterial = null;
            outlineMaterial = null;
            
            dilationMaterial = new Material(Shader.Find("RendererFeatures/Dilation"));
            outlineMaterial = new Material(Shader.Find("RendererFeatures/OutlineColorAndStencil"));
            
            // Apply the serialized values immediately after creating materials
            if (dilationMaterial != null)
            {
                dilationMaterial.SetInteger("_Spread", spread);
            }
            if (outlineMaterial != null)
            {
                outlineMaterial.SetColor("_BaseColor", color);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_outlinePass == null)
            {
                Create();
            }
            
            if (_outlinePass == null)
                return;

            // Check if materials need to be recreated (can happen after domain reload)
            if (dilationMaterial == null)
            {
                dilationMaterial = new Material(Shader.Find("RendererFeatures/Dilation"));
                if (dilationMaterial != null)
                {
                    dilationMaterial.SetInteger("_Spread", spread);
                }
            }
            
            if (outlineMaterial == null)
            {
                outlineMaterial = new Material(Shader.Find("RendererFeatures/OutlineColorAndStencil"));
                if (outlineMaterial != null)
                {
                    outlineMaterial.SetColor("_BaseColor", color);
                }
            }

            if (!dilationMaterial ||
                !outlineMaterial ||
                _targetRenderers == null ||
                _targetRenderers.Length == 0)
            {
                // Don't render the effect if there's nothing to render
                return;
            }

            // Any variables you may want to update every frame should be set here.
            _outlinePass.RenderEvent = renderEvent;
            _outlinePass.DilationMaterial = dilationMaterial;
            dilationMaterial.SetInteger("_Spread", spread);
            outlineMaterial.SetColor("_BaseColor", color);
            _outlinePass.OutlineMaterial = outlineMaterial;
            _outlinePass.Renderers = _targetRenderers;

            renderer.EnqueuePass(_outlinePass);
        }

        public void SetColor(Color color)
        {
            this.color = color;
        }
        
        public void SetSpread(int newSpread)
        {
            this.spread = newSpread;
        }
    }
}

