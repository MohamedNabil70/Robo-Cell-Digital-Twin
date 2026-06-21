using UnityEngine;
using UnityEngine.UI;

namespace realvirtual
{
    
    [ExecuteAlways]
    public class rvUIRelativePlacement : MonoBehaviour
    {
        public RectTransform target;
        public float margin;

        public enum Placement
        {
            None,
            Inside,
            Above,
            Below,
            Left,
            Right,
            Horizontal, // choose between left or right depending on screen space
            Vertical, // choose between above or below depending on screen space
            Cursor, // spawn on cursor, considering screen space
            Fixed,
            Auto
        }

        [SerializeField] private Placement placement;
        private Placement lastPlacement;
        private Placement effectivePlacement;

        void Start()
        {
            effectivePlacement = GetEffectivePlacement();
            AdjustPivot();
            lastPlacement = effectivePlacement;
        }

        public void PlaceRelativeTo(RectTransform rect, Placement placement, float margin)
        {
            this.margin = margin;
            this.target = rect;
            this.placement = placement;
        }
        
        public void ClearRelativePlacement()
        {
            target = null;
            
        }
        
        public void RefreshPosition()
        {
            LateUpdate();
        }

        void LateUpdate()
        {
            if (target == null) return;

            // Determine effective placement for Horizontal and Vertical modes
            effectivePlacement = GetEffectivePlacement();

            // Adjust pivot if placement changed
            if (effectivePlacement != lastPlacement)
            {
                AdjustPivot();
                lastPlacement = effectivePlacement;
            }

            RectTransform rt = GetComponent<RectTransform>();

            // Get world corners of target
            // corners[0] = bottom-left
            // corners[1] = top-left
            // corners[2] = top-right
            // corners[3] = bottom-right
            Vector3[] targetCorners = new Vector3[4];
            target.GetWorldCorners(targetCorners);

            Vector3 newPosition = rt.position;

            switch (effectivePlacement)
            {
                case Placement.Above:
                    // Element's bottom-left (pivot) aligns with target's top-left corner
                    newPosition.x = targetCorners[1].x; // target top-left X
                    newPosition.y = targetCorners[1].y + margin; // target top-left Y + margin
                    //Debug.Log($"Above: Target top-left corner ({targetCorners[1].x}, {targetCorners[1].y}), margin={margin}, newPos=({newPosition.x}, {newPosition.y})");
                    break;
                case Placement.Below:
                    // Element's top-left (pivot) aligns with target's bottom-left corner
                    newPosition.x = targetCorners[0].x; // target bottom-left X
                    newPosition.y = targetCorners[0].y - margin; // target bottom-left Y - margin
                    //Debug.Log($"Below: Target bottom-left corner ({targetCorners[0].x}, {targetCorners[0].y}), margin={margin}, newPos=({newPosition.x}, {newPosition.y})");
                    break;
                case Placement.Left:
                    // Element's top-right (pivot) aligns with target's top-left corner
                    newPosition.x = targetCorners[1].x - margin; // target top-left X - margin
                    newPosition.y = targetCorners[1].y; // target top-left Y
                    //Debug.Log($"Left: Target top-left corner ({targetCorners[1].x}, {targetCorners[1].y}), margin={margin}, newPos=({newPosition.x}, {newPosition.y})");
                    break;
                case Placement.Right:
                    // Element's top-left (pivot) aligns with target's top-right corner
                    newPosition.x = targetCorners[2].x + margin; // target top-right X + margin
                    newPosition.y = targetCorners[2].y; // target top-right Y
                    //Debug.Log($"Right: Target top-right corner ({targetCorners[2].x}, {targetCorners[2].y}), margin={margin}, newPos=({newPosition.x}, {newPosition.y})");
                    break;
            }

            rt.position = newPosition;
        }

        Placement GetEffectivePlacement()
        {
            
            Placement parsedPlacement = placement;

            if (parsedPlacement == Placement.Auto)
            {
                Debug.Log("Auto placement detected, determining based on parent layout.");
                rvUIContainer parentContainer = target.GetComponentInParent<rvUIFloatingMenuPanel>();
                HorizontalLayoutGroup layoutGroup = parentContainer.GetContentRoot().GetComponent<HorizontalLayoutGroup>();
                if (layoutGroup != null)
                {
                    Debug.Log("Parent has HorizontalLayoutGroup, using Vertical placement, on gameobject: " + gameObject.name);
                    parsedPlacement = Placement.Vertical;
                }
                else
                {
                    Debug.Log("Parent does not have HorizontalLayoutGroup, using Horizontal placement, on gameobject: " + gameObject.name);
                    parsedPlacement = Placement.Horizontal;
                }
            }
            
            
            
            // For fixed placements, return as-is
            if (parsedPlacement != Placement.Horizontal && parsedPlacement != Placement.Vertical)
                return parsedPlacement;

            // Safety check
            if (target == null)
                return parsedPlacement;

            // Get the canvas for screen space calculations
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return parsedPlacement;

            // Get target center in screen space
            Vector3[] targetCorners = new Vector3[4];
            target.GetWorldCorners(targetCorners);
            Vector3 targetCenter = (targetCorners[0] + targetCorners[2]) / 2f;

            // Convert to viewport position (0-1 range)
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3 viewportPos = cam != null ? cam.WorldToViewportPoint(targetCenter) :
                                                 new Vector3(targetCenter.x / Screen.width, targetCenter.y / Screen.height, 0f);

            if (parsedPlacement == Placement.Horizontal)
            {
                // If target is on left half of screen, place element to the right
                // If target is on right half of screen, place element to the left
                return viewportPos.x < 0.5f ? Placement.Right : Placement.Left;
            }
            else // Vertical
            {
                // If target is on top half of screen, place element below
                // If target is on bottom half of screen, place element above
                return viewportPos.y < 0.5f ? Placement.Above : Placement.Below;
            }
        }

        void AdjustPivot()
        {
            RectTransform rt = GetComponent<RectTransform>();
            switch (effectivePlacement)
            {
                case Placement.Above:
                    rt.pivot = new Vector2(0f, 0f); // bottom-left
                    break;
                case Placement.Below:
                    rt.pivot = new Vector2(0f, 1f); // top-left
                    break;
                case Placement.Left:
                    rt.pivot = new Vector2(1f, 1f); // top-right
                    break;
                case Placement.Right:
                    rt.pivot = new Vector2(0f, 1f); // top-left
                    break;
                case Placement.Horizontal:
                case Placement.Vertical:
                    // These should never reach here as GetEffectivePlacement resolves them
                    break;
            }
        }

        public void SetPlacement(Placement placement1)
        {
           placement = placement1;
        }
    }
}
