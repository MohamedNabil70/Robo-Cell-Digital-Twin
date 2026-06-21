using UnityEngine.Scripting.APIUpdating;
using System;
using UnityEngine;


namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIDeactivateOnClick")]
    public class rvUIDeactivateOnClick : MonoBehaviour
    {
        // desctivates the gameobject on mouse up
    
        public bool outsideOnly = true;
        public Action callback;
    
        void Update()
        {
            if(Input.GetMouseButtonUp(0))
            {
                if (outsideOnly)
                {
                    RectTransform rectTransform = GetComponent<RectTransform>();
                    Vector2 localMousePosition = rectTransform.InverseTransformPoint(Input.mousePosition);
                    if (!rectTransform.rect.Contains(localMousePosition))
                    {
                        gameObject.SetActive(false);
                        callback?.Invoke();
                    }
                }
                else
                {
                    gameObject.SetActive(false);
                    callback?.Invoke();
                }
            }
        }
        
    }
}
