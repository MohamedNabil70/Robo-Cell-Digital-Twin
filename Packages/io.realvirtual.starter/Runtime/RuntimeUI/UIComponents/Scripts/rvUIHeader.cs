using UnityEngine.Scripting.APIUpdating;
using realvirtual;
using UnityEngine;


namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIHeader")]
    public class rvUIHeader : MonoBehaviour
    {
        public rvUIMenuButton knob;
        public rvUIText title;
        public rvUIMenuButton closeButton;
        
        
        public bool useKnob = true;
        public bool useTitle = true;
        public bool useCloseButton = true;
    
        void OnValidate()
        {
            Refresh();
        }
    
        public void Refresh()
        {
            
            if (knob != null)
            {
                knob.gameObject.SetActive(useKnob);
            }
            
            if (title != null)
            {
                title.gameObject.SetActive(useTitle);
            }
            
            if (closeButton != null)
            {
                closeButton.gameObject.SetActive(useCloseButton);
            }
            
        }
    
        public void SetTitle(string text)
        {
            this.title.SetText(text);
            
        }
    }
}
