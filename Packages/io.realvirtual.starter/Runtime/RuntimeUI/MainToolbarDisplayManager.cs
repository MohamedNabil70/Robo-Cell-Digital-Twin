// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Manages toolbar panel visibility based on available screen width.
    public class MainToolbarDisplayManager : MonoBehaviour
    {
        public RectTransform MainToolbar;
        public RectTransform LeftPanel;
        public RectTransform CenterPanel;
        public RectTransform RightPanel;

        public float Margin = 10f; //!< Extra margin in pixels to prevent tight fitting

        private bool centerVisible = true;
        private bool rightVisible = true;
        private float cachedLeftWidth;
        private float cachedCenterWidth;
        private float cachedRightWidth;

        void LateUpdate()
        {
            if (MainToolbar == null || LeftPanel == null)
                return;

            float toolbarWidth = MainToolbar.rect.width;

            // Cache widths while panels are active (avoids toggling active state to measure)
            if (LeftPanel.gameObject.activeSelf)
                cachedLeftWidth = LeftPanel.rect.width * LeftPanel.localScale.x;
            if (CenterPanel != null && CenterPanel.gameObject.activeSelf)
                cachedCenterWidth = CenterPanel.rect.width * CenterPanel.localScale.x;
            if (RightPanel != null && RightPanel.gameObject.activeSelf)
                cachedRightWidth = RightPanel.rect.width * RightPanel.localScale.x;

            // Compute occupied horizontal regions based on anchor positions:
            // Left panel (anchor left):    occupies [0 .. leftWidth]
            // Center panel (anchor center): occupies [toolbar/2 - center/2 .. toolbar/2 + center/2]
            // Right panel (anchor right):   occupies [toolbar - rightWidth .. toolbar]
            float halfToolbar = toolbarWidth * 0.5f;
            float centerLeftEdge = halfToolbar - cachedCenterWidth * 0.5f;
            float centerRightEdge = halfToolbar + cachedCenterWidth * 0.5f;
            float rightLeftEdge = toolbarWidth - cachedRightWidth;

            // Center visible if it doesn't overlap left or right panels
            bool showCenter = centerLeftEdge >= cachedLeftWidth + Margin
                           && centerRightEdge + Margin <= rightLeftEdge;

            // Right visible if it doesn't overlap left panel
            bool showRight = rightLeftEdge >= cachedLeftWidth + Margin;

            if (showCenter != centerVisible)
            {
                centerVisible = showCenter;
                if (CenterPanel != null)
                    CenterPanel.gameObject.SetActive(centerVisible);
            }

            if (showRight != rightVisible)
            {
                rightVisible = showRight;
                if (RightPanel != null)
                    RightPanel.gameObject.SetActive(rightVisible);
            }
        }
    }
}
