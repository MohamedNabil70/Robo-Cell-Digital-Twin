using UnityEngine.Scripting.APIUpdating;
using TMPro;
using UnityEngine;


namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIMaterialIcon")]
    public class rvUIMaterialIcon : MonoBehaviour
    {
        public TextMeshProUGUI text;
        public string unicode;
        public Color color = Color.white;
    
        void OnValidate()
        {
            SetIcon();
            SetColor();
        }
    
        public void SetIcon()
        {
            if (text != null && !string.IsNullOrEmpty(unicode))
            {
                text.text = "\\u" + unicode;
            }
        }
        
        public void SetColor()
        {
            if (text != null)
            {
                text.color = color;
            }
        }
    
        public void SetIconByUnicode(string unicodeHex)
        {
            unicode = unicodeHex;
            SetIcon();
        }
    }
    
    
}
