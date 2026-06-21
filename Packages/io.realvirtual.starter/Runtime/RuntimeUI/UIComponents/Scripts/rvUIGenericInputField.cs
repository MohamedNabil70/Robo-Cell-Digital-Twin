using realvirtual;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


namespace realvirtual
{
    public class rvUIGenericInputField : rvUIContent
    {
        public rvUIIcon icon;
        public rvUIText label;
        public TMP_InputField inputField;
    
        
        public bool showIcon = true;
        public bool showText = true;
        public UnityEvent<string> OnValueChanged = new UnityEvent<string>();
        
        
        public override void RefreshLayout()
        {
            if (icon != null)
            {
                icon.gameObject.SetActive(showIcon);
            }
            
            if (label != null)
            {
                label.gameObject.SetActive(showText);
            }
            
        }
    
        public void OnValidate()
        {
            RefreshLayout();
        }
        
        void Awake(){
            RefreshEvents();
        }
    
        void RefreshEvents()
        {
            inputField.onEndEdit.RemoveAllListeners();
            inputField.onEndEdit.AddListener((value) =>
            {
                OnValueChanged.Invoke(value);
            });
        }
    }
}
