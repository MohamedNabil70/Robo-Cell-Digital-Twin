using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
    public class rvUIHeightAdjuster : MonoBehaviour
    {
        public float minHeight = 0f; // Minimum height constraint
        public float maxHeight = Mathf.Infinity; // Maximum height constraint
        public bool keepAdjusting = false; // Whether to keep adjusting height every frame
        
        private void Update()
        {
            if (keepAdjusting)
            {
                AdjustHeight();
            }
        }
        
        public void AdjustHeight()
        {
            // Force immediate layout updates to ensure target height is current
            Canvas.ForceUpdateCanvases();
            
            rvUIHeightContributor[] contributors = GetComponentsInChildren<rvUIHeightContributor>();
            if (contributors.Length == 0) return;
            
            float totalHeight = 0f;
            foreach (var contributor in contributors)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contributor.GetComponent<RectTransform>());
                totalHeight += contributor.GetCurrentHeightContribution();
            }
            
          
            RectTransform adjusted = GetComponent<RectTransform>();
            
            // Clamp the total height within the specified min and max constraints
            totalHeight = Mathf.Clamp(totalHeight, minHeight, maxHeight);
            
            
            // Set the adjusted RectTransform's height to match the target
            adjusted.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);
            
            // Force layout rebuild on the adjusted element to propagate changes
            LayoutRebuilder.ForceRebuildLayoutImmediate(adjusted);
        }
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(rvUIHeightAdjuster))]
    public class rvUIHeightAdjusterEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            rvUIHeightAdjuster myScript = (rvUIHeightAdjuster)target;
            if (GUILayout.Button("Adjust"))
            {
                myScript.AdjustHeight();
            }
        }
    }
    #endif
}
