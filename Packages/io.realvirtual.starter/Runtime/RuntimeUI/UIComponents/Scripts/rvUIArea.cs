using UnityEngine;


namespace realvirtual
{
    public class rvUIArea : rvUIContainer
    {
        public enum Area
        {
            Top,
            Bottom,
            Left,
            Right,
            Center,
            Overlay,
            Tooltip
        }
        
        public Area area;
        public override void RefreshLayout()
        {
        }
    
        public override RectTransform GetContentRoot()
        {
            return gameObject.GetComponent<RectTransform>();
        }
    }
}
