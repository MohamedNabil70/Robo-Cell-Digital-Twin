// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace realvirtual
{
    //! Adjust the position of the UI element to the position of the object in the scene
    public class HMI_ContentPosition : MonoBehaviour
    {
        private RectTransform rectTransform;
        // Start is called before the first frame update
        void Start()
        {

            rectTransform = this.GetComponent<RectTransform>();
            var distancetop = rectTransform.rect.height / 4;
            var layoutgroup = this.GetComponent<VerticalLayoutGroup>();
            if(layoutgroup != null)
                layoutgroup.padding.top = (int)distancetop;
            #if UNITY_EDITOR
            EditorUtility.SetDirty( this.gameObject);
            #endif
        }

    }
}
