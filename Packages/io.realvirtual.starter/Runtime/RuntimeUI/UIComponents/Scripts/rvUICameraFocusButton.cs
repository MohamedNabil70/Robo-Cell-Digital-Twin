using UnityEngine.Scripting.APIUpdating;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUICameraFocusButton")]
    public class rvUICameraFocusButton : MonoBehaviour
    {
        
    
        public static rvUICameraFocusButton Instantiate()
        {
            GameObject prefab = UnityEngine.Resources.Load<GameObject>("rvUICameraFocusButton");
            GameObject instance = Instantiate(prefab); 
            return instance.GetComponent<rvUICameraFocusButton>();
        }
        
        
        public UnityEvent OnClick;
    
        void Start()
        {
            // CRITICAL FIX: Do NOT wire up Button.onClick here - it removes listeners added by CameraFollowObject
            // The button setup is now handled externally in CameraFollowObject.CreateStopButton()
            // Only clean up any persistent calls that might be in the prefab itself
            Button button = GetComponentInChildren<Button>();
            if (button != null)
            {
                // Remove only persistent calls from the prefab (if any)
                // but DON'T add any new listeners here - that's the caller's responsibility
                var persistentCallCount = button.onClick.GetPersistentEventCount();
                if (persistentCallCount > 0)
                {
                    // If there are persistent calls, log a warning as they should be removed from prefab
                    Debug.LogWarning($"rvUICameraFocusButton: Button has {persistentCallCount} persistent onClick calls in prefab. These should be removed from the prefab.");
                }
            }
        }
    
    
        public void Delete()
        {
            DestroyImmediate(this.gameObject);
        }
    }
}
