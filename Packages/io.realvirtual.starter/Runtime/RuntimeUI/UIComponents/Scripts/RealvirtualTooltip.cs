using UnityEngine;
using UnityEngine.UI;

namespace realvirtual
{
    public class RealvirtualTooltip : realvirtualBehavior, IUISkinEdit
    {
        public enum Placement
        {
            Above,
            Below,
            Left,
            Right,
            Auto
        }

        private const float DefaultMargin = 8f;

        private Text tooltipText;
        private Image background;
        private RectTransform baseRectTransform;
        private RectTransform bgRectTransform;
        private Canvas tooltipCanvas;
        private CanvasScaler canvasScaler;

        protected new void Awake()
        {
            tooltipText = GetComponentInChildren<Text>();
            background = GetComponentInChildren<Image>();
            baseRectTransform = Global.GetComponentByName<RectTransform>(gameObject, "Base");
            bgRectTransform = background.gameObject.GetComponent<RectTransform>();
            tooltipCanvas = GetComponent<Canvas>();
            canvasScaler = GetComponent<CanvasScaler>();

            // Ensure canvas scale is 1 for proper sizing
            if (canvasScaler != null && canvasScaler.scaleFactor < 1f)
            {
                canvasScaler.scaleFactor = 1f;
            }

            InitGame4Automation();
        }

        public void SetTooltip(string tip)
        {
            tooltipText.text = tip;
            // Force layout rebuild to get accurate size
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bgRectTransform);

            // Update background size based on text
            var textWidth = tooltipText.preferredWidth;
            var textHeight = tooltipText.preferredHeight;
            bgRectTransform.sizeDelta = new Vector2(textWidth + 16, textHeight + 8);
        }

        public void SetPosition(Vector3 pos)
        {
            baseRectTransform.anchoredPosition3D = pos;
        }

        /// <summary>
        /// Positions the tooltip relative to a target UI element with automatic screen boundary detection.
        /// </summary>
        public void PositionRelativeTo(RectTransform targetRect, Placement preferredPlacement = Placement.Auto, float margin = DefaultMargin)
        {
            if (targetRect == null || baseRectTransform == null)
                return;

            // Force layout update to get accurate tooltip size
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bgRectTransform);

            // Get the scale factor from our canvas
            float scaleFactor = GetCanvasScaleFactor();

            // Get target corners in screen space
            Vector3[] targetCorners = new Vector3[4];
            targetRect.GetWorldCorners(targetCorners);

            // Convert target corners to screen coordinates
            Vector2[] screenCorners = new Vector2[4];
            Camera targetCamera = GetCanvasCamera(targetRect);
            for (int i = 0; i < 4; i++)
            {
                screenCorners[i] = WorldToScreen(targetCorners[i], targetCamera);
            }

            // Get tooltip dimensions in screen space (accounting for canvas scale)
            float tooltipWidth = bgRectTransform.rect.width * scaleFactor;
            float tooltipHeight = bgRectTransform.rect.height * scaleFactor;

            // Determine effective placement based on screen space
            Placement effectivePlacement = preferredPlacement == Placement.Auto
                ? DetermineOptimalPlacement(screenCorners)
                : GetSafePlacement(preferredPlacement, screenCorners);

            // Calculate screen position (this returns the bottom-left corner position)
            Vector2 screenPos = CalculateScreenPosition(screenCorners, effectivePlacement, tooltipWidth, tooltipHeight, margin);

            // Clamp to screen bounds
            screenPos = ClampToScreenBounds(screenPos, tooltipWidth, tooltipHeight, margin);

            // The baseRectTransform is a child of the root canvas
            // Its anchor is at (0,0) and pivot is at (0,0), meaning anchoredPosition
            // represents the bottom-left corner position relative to the canvas bottom-left
            baseRectTransform.anchoredPosition = screenPos / scaleFactor;
        }

        /// <summary>
        /// Positions the tooltip relative to a target using world corners (legacy compatibility).
        /// </summary>
        public void PositionRelativeTo(Vector3[] targetCorners, UI.Tooltipposition position, float margin = DefaultMargin)
        {
            if (targetCorners == null || targetCorners.Length < 4 || baseRectTransform == null)
                return;

            Placement placement = ConvertLegacyPosition(position);

            // Force layout update
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bgRectTransform);

            float scaleFactor = GetCanvasScaleFactor();

            // Convert world corners to screen coordinates
            Vector2[] screenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                // Assume the corners are already in screen space for legacy interface
                screenCorners[i] = new Vector2(targetCorners[i].x, targetCorners[i].y);
            }

            float tooltipWidth = bgRectTransform.rect.width * scaleFactor;
            float tooltipHeight = bgRectTransform.rect.height * scaleFactor;

            Placement effectivePlacement = GetSafePlacement(placement, screenCorners);

            Vector2 screenPos = CalculateScreenPosition(screenCorners, effectivePlacement, tooltipWidth, tooltipHeight, margin);
            screenPos = ClampToScreenBounds(screenPos, tooltipWidth, tooltipHeight, margin);

            baseRectTransform.anchoredPosition = screenPos / scaleFactor;
        }

        private float GetCanvasScaleFactor()
        {
            if (canvasScaler != null)
                return canvasScaler.scaleFactor;
            if (tooltipCanvas != null)
                return tooltipCanvas.scaleFactor;
            return 1f;
        }

        private Camera GetCanvasCamera(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    return null;
                return canvas.worldCamera;
            }
            return null;
        }

        private Vector2 WorldToScreen(Vector3 worldPoint, Camera camera)
        {
            if (camera != null)
                return camera.WorldToScreenPoint(worldPoint);
            return new Vector2(worldPoint.x, worldPoint.y);
        }

        private Placement DetermineOptimalPlacement(Vector2[] screenCorners)
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Calculate target center position
            float targetCenterX = (screenCorners[0].x + screenCorners[2].x) / 2f;
            float targetCenterY = (screenCorners[0].y + screenCorners[1].y) / 2f;

            // Determine which half of screen the target is in
            bool isInTopHalf = targetCenterY > screenHeight / 2f;
            bool isInBottomHalf = targetCenterY < screenHeight / 2f;
            bool isInRightHalf = targetCenterX > screenWidth / 2f;
            bool isInLeftHalf = targetCenterX < screenWidth / 2f;

            // Check how close to edges (use 15% threshold for edge detection)
            bool isNearTopEdge = targetCenterY > screenHeight * 0.85f;
            bool isNearBottomEdge = targetCenterY < screenHeight * 0.15f;
            bool isNearRightEdge = targetCenterX > screenWidth * 0.85f;
            bool isNearLeftEdge = targetCenterX < screenWidth * 0.15f;

            // Priority rules:
            // 1. Near top edge → place Below
            // 2. Near bottom edge → place Above
            // 3. Near right edge (but not near top/bottom) → place Left
            // 4. Near left edge (but not near top/bottom) → place Right
            // 5. Default: top half → Below, bottom half → Above

            if (isNearTopEdge)
            {
                return Placement.Below;
            }
            else if (isNearBottomEdge)
            {
                return Placement.Above;
            }
            else if (isNearRightEdge)
            {
                return Placement.Left;
            }
            else if (isNearLeftEdge)
            {
                return Placement.Right;
            }
            else
            {
                // Default: use screen half for vertical placement
                return isInTopHalf ? Placement.Below : Placement.Above;
            }
        }

        private Placement GetSafePlacement(Placement preferred, Vector2[] screenCorners)
        {
            // For explicit placement requests, use the standard screen-half logic as fallback
            // This ensures tooltip always appears on the opposite side of the target from screen edge
            float screenHeight = Screen.height;
            float targetCenterY = (screenCorners[0].y + screenCorners[1].y) / 2f;
            bool isInTopHalf = targetCenterY > screenHeight / 2f;

            switch (preferred)
            {
                case Placement.Above:
                case Placement.Below:
                    // Use screen position to determine vertical placement
                    return isInTopHalf ? Placement.Below : Placement.Above;

                case Placement.Left:
                case Placement.Right:
                    // For horizontal preference, check screen position
                    float screenWidth = Screen.width;
                    float targetCenterX = (screenCorners[0].x + screenCorners[2].x) / 2f;
                    bool isInRightHalf = targetCenterX > screenWidth / 2f;
                    return isInRightHalf ? Placement.Left : Placement.Right;

                default:
                    return isInTopHalf ? Placement.Below : Placement.Above;
            }
        }

        private Vector2 CalculateScreenPosition(Vector2[] screenCorners, Placement placement, float tooltipWidth, float tooltipHeight, float margin)
        {
            Vector2 position = Vector2.zero;

            // Calculate target bounds
            float targetLeft = screenCorners[0].x;
            float targetRight = screenCorners[2].x;
            float targetBottom = screenCorners[0].y;
            float targetTop = screenCorners[1].y;
            float targetCenterX = (targetLeft + targetRight) / 2f;
            float targetCenterY = (targetBottom + targetTop) / 2f;

            switch (placement)
            {
                case Placement.Above:
                case Placement.Below:
                    // For vertical placements, try to center horizontally but stay within screen bounds
                    float idealX = targetCenterX - tooltipWidth / 2f;

                    // Check if centering would push tooltip off-screen
                    if (idealX < margin)
                    {
                        // Align to left edge of screen with margin
                        position.x = margin;
                    }
                    else if (idealX + tooltipWidth > Screen.width - margin)
                    {
                        // Align tooltip's right edge to screen's right edge with margin
                        position.x = Screen.width - margin - tooltipWidth;
                    }
                    else
                    {
                        position.x = idealX;
                    }

                    if (placement == Placement.Above)
                    {
                        position.y = targetTop + margin;
                    }
                    else // Below
                    {
                        position.y = targetBottom - margin - tooltipHeight;
                    }
                    break;

                case Placement.Left:
                case Placement.Right:
                    // For horizontal placements, try to center vertically but stay within screen bounds
                    float idealY = targetCenterY - tooltipHeight / 2f;

                    // Check if centering would push tooltip off-screen
                    if (idealY < margin)
                    {
                        position.y = margin;
                    }
                    else if (idealY + tooltipHeight > Screen.height - margin)
                    {
                        position.y = Screen.height - margin - tooltipHeight;
                    }
                    else
                    {
                        position.y = idealY;
                    }

                    if (placement == Placement.Left)
                    {
                        position.x = targetLeft - margin - tooltipWidth;
                    }
                    else // Right
                    {
                        position.x = targetRight + margin;
                    }
                    break;
            }

            return position;
        }

        private Vector2 ClampToScreenBounds(Vector2 screenPos, float tooltipWidth, float tooltipHeight, float margin)
        {
            // Clamp X to keep tooltip within screen
            if (screenPos.x < margin)
                screenPos.x = margin;
            else if (screenPos.x + tooltipWidth > Screen.width - margin)
                screenPos.x = Screen.width - margin - tooltipWidth;

            // Clamp Y to keep tooltip within screen
            if (screenPos.y < margin)
                screenPos.y = margin;
            else if (screenPos.y + tooltipHeight > Screen.height - margin)
                screenPos.y = Screen.height - margin - tooltipHeight;

            return screenPos;
        }

        private Placement ConvertLegacyPosition(UI.Tooltipposition position)
        {
            switch (position)
            {
                case UI.Tooltipposition.Above: return Placement.Above;
                case UI.Tooltipposition.Under: return Placement.Below;
                case UI.Tooltipposition.Left: return Placement.Left;
                case UI.Tooltipposition.Right: return Placement.Right;
                default: return Placement.Below;
            }
        }

        public float GetHeight()
        {
            return bgRectTransform.rect.height;
        }

        public float GetWidth()
        {
            return bgRectTransform.rect.width;
        }

        // Legacy methods for backwards compatibility
        public float getHeight() => GetHeight();
        public float getWidth() => GetWidth();

        public void UpdateUISkinParameter(RealvirtualUISkin skin)
        {
            var bgcolor = skin.TooltipBackgroundColor;
            var textcolor = skin.TooltipFontColor;
            tooltipText.font = skin.TooltipFont;
            tooltipText.color = new Color(textcolor.r, textcolor.g, textcolor.b, textcolor.a);
            background.color = new Color(bgcolor.r, bgcolor.g, bgcolor.b, bgcolor.a);
        }
    }
}
