using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;

namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUITooltipGenerator")]
    public class rvUITooltipGenerator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IRuntimeWindowStyle
    {
    public string text;
    public float delay = 0.5f;


    private Coroutine showRoutine;
    private rvUITooltip instance;
    public rvUIRelativePlacement.Placement placement = rvUIRelativePlacement.Placement.Auto;
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        showRoutine = StartCoroutine(ShowTooltipAfterDelay());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("tooltip Pointer Exit");
        Deactivate();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Deactivate();
    }

    void Deactivate()
    {
        if(showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
        
        if(instance != null){
            Destroy(instance.gameObject);
            instance = null;
        }
    }
    
    IEnumerator ShowTooltipAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        // Show tooltip logic here
        
        instance = RuntimeUIBuilder.Instance.AddTooltip(text, transform as RectTransform, placement);
    }

    public void OnWindowStyleChanged(rvUIMenuWindow.Style newStyle)
    {
        switch (newStyle)
        {
            case rvUIMenuWindow.Style.Window:
                placement = rvUIRelativePlacement.Placement.Right;
                break;
            case rvUIMenuWindow.Style.Vertical:
                placement = rvUIRelativePlacement.Placement.Horizontal;
                break;
            case rvUIMenuWindow.Style.Horizontal:
                placement = rvUIRelativePlacement.Placement.Vertical;
                break;
                
        }
    }
}
}
