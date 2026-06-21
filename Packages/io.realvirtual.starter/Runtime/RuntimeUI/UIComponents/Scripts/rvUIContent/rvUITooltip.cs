using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUITooltip")]
    public class rvUITooltip : rvUIContent
    { 
    rvUIText text;
    rvUIRelativePlacement relativePlacement;

    void Awake()
    {
        text = GetComponentInChildren<rvUIText>(true);
        relativePlacement = GetComponentInChildren<rvUIRelativePlacement>();
    }

    public void Init(string text, RectTransform parent, rvUIRelativePlacement.Placement placement)
    {
        this.text.SetText(text);
        relativePlacement.target = parent;
        relativePlacement.PlaceRelativeTo(parent, placement, margin:4);
    }
    
    public override void RefreshLayout()
    {

    }
}
}
