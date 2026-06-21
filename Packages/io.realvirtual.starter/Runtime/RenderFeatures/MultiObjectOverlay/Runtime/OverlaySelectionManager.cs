// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using NaughtyAttributes;

namespace realvirtual.RendererFeatures
{
    [ExecuteInEditMode]
    public class OverlaySelectionManager : AbstractSelectionManager
    {
        [SerializeField] private MultiObjectOverlayRendererFeature overlayRendererFeature;
        [SerializeField] private List<Renderer> selectedRenderers = new List<Renderer>();
        [SerializeField] public MultiObjectOverlayRendererFeature.Mode mode;
        [SerializeField] public Color color = Color.yellow;
        
        
        [ShowIf("IsBlinkMode")]
        [SerializeField] public float blinkSpeed = 10;
        
        private bool needsDelayedInit = false;
        
        [ContextMenu("Reassign Renderers Now")]
        private void OnValidate()
        {
            #if UNITY_EDITOR
            // CRITICAL: Never perform renderer feature operations in OnValidate during serialization
            // This can cause Unity to crash during domain reload or scene loading
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && 
                !UnityEditor.EditorApplication.isCompiling &&
                !UnityEditor.EditorApplication.isUpdating)
            {
                // Only set the flag, never apply settings directly in OnValidate
                needsDelayedInit = true;
            }
            #endif
        }

        bool IsBlinkMode()
        {
            return mode == MultiObjectOverlayRendererFeature.Mode.Blink;
        }

        void Awake()
        {
            needsDelayedInit = true;
        }

        void OnEnable()
        {
            needsDelayedInit = true;
        }
        
        void Start()
        {
            if (needsDelayedInit)
            {
                ApplySettingsToRendererFeature();
                needsDelayedInit = false;
            }
        }
        
        void Update()
        {
            if (needsDelayedInit)
            {
                ApplySettingsToRendererFeature();
                needsDelayedInit = false;
            }
            
            #if UNITY_EDITOR
            // In editor, periodically check if we need to update (safer than delayCall)
            if (!Application.isPlaying && needsDelayedInit)
            {
                ApplySettingsToRendererFeature();
                needsDelayedInit = false;
            }
            #endif
        }

        void ApplySettingsToRendererFeature()
        {
            // Extra safety checks to prevent crashes
            if (overlayRendererFeature == null)
                return;
            
            #if UNITY_EDITOR
            // Additional safety check for editor - don't apply during unsafe times
            if (!Application.isPlaying && 
                (UnityEditor.EditorApplication.isCompiling || 
                 UnityEditor.EditorApplication.isUpdating))
            {
                return;
            }
            #endif
                
            try
            {
                // Only apply settings if we have renderers selected or in edit mode
                // This prevents empty managers from overriding active ones
                if (selectedRenderers != null && (selectedRenderers.Count > 0 || !Application.isPlaying))
                {
                    // Store values in the renderer feature's serialized fields
                    overlayRendererFeature.color = color;
                    overlayRendererFeature.mode = mode;
                    overlayRendererFeature.blinkSpeed = blinkSpeed;
                    
                    // Then apply them through the setters
                    overlayRendererFeature.SetMode(mode);
                    overlayRendererFeature.SetColor(color);
                }
                
                // Always update the renderer list
                if (selectedRenderers != null)
                {
                    overlayRendererFeature.SetRenderers(selectedRenderers.ToArray());
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error applying settings to renderer feature: {e.Message}", this);
            }
        }

        public override void Select(Renderer renderer)
        {
            if (selectedRenderers.Contains(renderer))
            {
                return;
            }

            selectedRenderers.Add(renderer);
            
            // Apply our settings when we become active
            if (overlayRendererFeature != null)
            {
                overlayRendererFeature.color = color;
                overlayRendererFeature.mode = mode;
                overlayRendererFeature.blinkSpeed = blinkSpeed;
                overlayRendererFeature.SetMode(mode);
                overlayRendererFeature.SetColor(color);
                overlayRendererFeature.SetRenderers(selectedRenderers.ToArray());
            }
        }

        public override void Deselect(Renderer renderer)
        {
            if (!selectedRenderers.Contains(renderer))
            {
                return;
            }

            selectedRenderers.Remove(renderer);
            overlayRendererFeature.SetRenderers(selectedRenderers.ToArray());
        }

        public override void DeselectAll()
        {
            selectedRenderers.Clear();
            overlayRendererFeature.SetRenderers(selectedRenderers.ToArray());
        }

    }
}