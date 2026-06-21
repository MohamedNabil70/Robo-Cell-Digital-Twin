using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIMenuSpacing")]
    public class rvUIMenuSpacing : rvUIContent
    {
    
    
    public float spacing = 10f;
    


    public override void RefreshLayout()
    {
        SetSpacing(spacing);
    }

    
    
    public void SetSpacing(float spacing)
    {
        this.spacing = spacing;
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(spacing, spacing);
        }
    }

}
}
