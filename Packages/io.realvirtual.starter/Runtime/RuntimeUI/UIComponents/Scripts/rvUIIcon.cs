using UnityEngine.Scripting.APIUpdating;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIIcon")]
    public class rvUIIcon : MonoBehaviour
    {
        public enum Mode
        {
            Sprite,
            MaterialIcon
        }
        
        public Color color = Color.white;
    
        public Mode mode = Mode.Sprite;
    
        public Sprite sprite;
    
        public string icon;
    
        private Image imageComponent;
        private rvUIMaterialIcon materialIcon;
    
        
        void OnValidate()
        {
            Apply();
        }
    
    
        public void Apply()
        {
            if (materialIcon == null)
            {
                materialIcon = GetComponentInChildren<rvUIMaterialIcon>(true);
            }
            
            if (imageComponent == null)
            {
                imageComponent = GetComponentInChildren<Image>(true);
            }
    
            if (mode == Mode.Sprite)
            {
                materialIcon.gameObject.SetActive(false);
                imageComponent.gameObject.SetActive(true);
                
                imageComponent.sprite = sprite;
                imageComponent.color = color;
                
            }
            else if (mode == Mode.MaterialIcon)
            {
                imageComponent.gameObject.SetActive(false);
                materialIcon.gameObject.SetActive(true);
    
                materialIcon.unicode = icon;
                materialIcon.color = color;
                
                materialIcon.SetIcon();
                materialIcon.SetColor();
                
                
            }
        }
    
        public void ApplySprite(Sprite sprite)
        {
            this.sprite = sprite;
            mode = Mode.Sprite;
            Apply();
        }
        
        public void ApplyMaterialIcon(string icon)
        {
            this.icon = icon;
            mode = Mode.MaterialIcon;
            Apply();
        }
        
        
        public void CopyFromOther(rvUIIcon other)
        {
            mode = other.mode;
            color = other.color;
            sprite = other.sprite;
            icon = other.icon;
            Apply();
        }
    }
    
}
