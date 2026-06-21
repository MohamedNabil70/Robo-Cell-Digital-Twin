using UnityEngine.Scripting.APIUpdating;
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

//! Positions UI panels at screen corners and edges with configurable margins.
//! Sets RectTransform anchors and pivots to place panels at predefined screen positions.
//! Useful for placing toolbars, status panels, and other UI elements at fixed screen locations.

namespace realvirtual
{
    [ExecuteAlways]
    [MovedFrom(false, sourceClassName: "rvUIPanelPlacer")]
    public class rvUIPanelPlacer : MonoBehaviour
    {
        public enum Position
        {
            TopLeft,      //!< Top-left corner of the screen
            TopCenter,    //!< Top center of the screen
            TopRight,     //!< Top-right corner of the screen
            MiddleLeft,   //!< Middle-left edge of the screen
            MiddleCenter, //!< Center of the screen
            MiddleRight,  //!< Middle-right edge of the screen
            BottomLeft,   //!< Bottom-left corner of the screen
            BottomCenter, //!< Bottom center of the screen
            BottomRight   //!< Bottom-right corner of the screen
        }
    
        public Position panelPosition = Position.BottomCenter; //!< Position where the panel should be placed
        public float margin = 4; //!< Margin in pixels from the screen edges
    
        void Start()
        {
            // Apply initial placement
            Place(panelPosition);
        }
    
        /// <summary>
        /// Places the panel at the specified screen position with margin offset.
        /// Sets anchors, pivot, and position to align the panel correctly.
        /// </summary>
        /// <param name="position">The screen position to place the panel at</param>
        public void Place(Position position)
        {
            this.panelPosition = position;
            RectTransform rt = GetComponent<RectTransform>();
    
            if (rt == null)
            {
                return;
            }
    
            // Set anchors and pivot based on position
            switch (position)
            {
                case Position.TopLeft:
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.anchoredPosition = new Vector2(margin, -margin);
                    break;
    
                case Position.TopCenter:
                    rt.anchorMin = new Vector2(0.5f, 1);
                    rt.anchorMax = new Vector2(0.5f, 1);
                    rt.pivot = new Vector2(0.5f, 1);
                    rt.anchoredPosition = new Vector2(0, -margin);
                    break;
    
                case Position.TopRight:
                    rt.anchorMin = new Vector2(1, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-margin, -margin);
                    break;
    
                case Position.MiddleLeft:
                    rt.anchorMin = new Vector2(0, 0.5f);
                    rt.anchorMax = new Vector2(0, 0.5f);
                    rt.pivot = new Vector2(0, 0.5f);
                    rt.anchoredPosition = new Vector2(margin, 0);
                    break;
    
                case Position.MiddleCenter:
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    break;
    
                case Position.MiddleRight:
                    rt.anchorMin = new Vector2(1, 0.5f);
                    rt.anchorMax = new Vector2(1, 0.5f);
                    rt.pivot = new Vector2(1, 0.5f);
                    rt.anchoredPosition = new Vector2(-margin, 0);
                    break;
    
                case Position.BottomLeft:
                    rt.anchorMin = new Vector2(0, 0);
                    rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.anchoredPosition = new Vector2(margin, margin);
                    break;
    
                case Position.BottomCenter:
                    rt.anchorMin = new Vector2(0.5f, 0);
                    rt.anchorMax = new Vector2(0.5f, 0);
                    rt.pivot = new Vector2(0.5f, 0);
                    rt.anchoredPosition = new Vector2(0, margin);
                    break;
    
                case Position.BottomRight:
                    rt.anchorMin = new Vector2(1, 0);
                    rt.anchorMax = new Vector2(1, 0);
                    rt.pivot = new Vector2(1, 0);
                    rt.anchoredPosition = new Vector2(-margin, margin);
                    break;
            }
        }
    
        /// <summary>
        /// Applies the current panel position. Call this after changing panelPosition or margin at runtime.
        /// </summary>
        [NaughtyAttributes.Button("Apply Position")]
        public void ApplyPosition()
        {
            Place(panelPosition);
        }
    }
}
