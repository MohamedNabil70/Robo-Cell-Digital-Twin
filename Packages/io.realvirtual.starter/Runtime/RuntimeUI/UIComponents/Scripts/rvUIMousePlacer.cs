using UnityEngine;

namespace realvirtual
{
    public static class rvUIMousePlacer
    {
        
        public static void PlaceRecttransformAtMousePosition(RectTransform rectTransform, Vector2 anchor, float paddingTop = 0f)
        {
            // Get the canvas
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = null;
            
            // Determine the camera based on canvas render mode
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = null; // No camera needed for overlay
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera || canvas.renderMode == RenderMode.WorldSpace)
            {
                uiCamera = canvas.worldCamera;
                if (uiCamera == null)
                    uiCamera = Camera.main;
            }
            
            // Get mouse position
            Vector2 mousePosition = Input.mousePosition;
            
            // Convert mouse position to local position in the parent of the rectTransform
            RectTransform parent = rectTransform.parent as RectTransform;
            if (parent == null)
                parent = canvasRect;
                
            Vector2 localMousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                mousePosition,
                uiCamera,
                out localMousePos
            );
            
            // Get the rect dimensions
            Rect rect = rectTransform.rect;
            
            // Calculate where to position the rect based on anchor
            // anchor (0,0) = mouse at bottom-left corner
            // anchor (1,1) = mouse at top-right corner  
            // anchor (0.5,0.5) = mouse at center
            
            // First, calculate the offset from the rect's pivot to where we want the mouse to be
            Vector2 pivotOffset = new Vector2(
                rect.width * (rectTransform.pivot.x - anchor.x),
                rect.height * (rectTransform.pivot.y - anchor.y)
            );
            
            // Position the rect
            Vector2 targetPosition = localMousePos + pivotOffset;
            
            // Now check boundaries - we need to ensure the rect stays within screen bounds
            // Convert the corners to screen space to check boundaries
            rectTransform.anchoredPosition = targetPosition;
            
            // Get the four corners of the rect in screen space
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            
            // Convert world corners to screen points
            Vector2 minScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 maxScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            
            // Check screen boundaries and adjust
            Vector2 adjustment = Vector2.zero;
            
            
            
            // Check left edge
            if (minScreen.x < 0)
                adjustment.x = -minScreen.x;
            // Check right edge  
            else if (maxScreen.x > Screen.width)
                adjustment.x = Screen.width - maxScreen.x;
                
            // Check bottom edge
            if (minScreen.y < 0)
                adjustment.y = -minScreen.y;
            // Check top edge
            else if (maxScreen.y > Screen.height - paddingTop)
                adjustment.y = Screen.height - maxScreen.y - paddingTop;
            
            // If we need to adjust, convert the adjustment back to local space
            if (adjustment != Vector2.zero)
            {
                // Convert the adjustment from screen space to local space
                Vector2 adjustedScreenPos = mousePosition + adjustment;
                Vector2 adjustedLocalPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    adjustedScreenPos,
                    uiCamera,
                    out adjustedLocalPos
                );
                
                // Recalculate position with adjustment
                targetPosition = adjustedLocalPos + pivotOffset;
            }
            
            // Apply the final position
            rectTransform.anchoredPosition = targetPosition;
        }
        
    }
}
