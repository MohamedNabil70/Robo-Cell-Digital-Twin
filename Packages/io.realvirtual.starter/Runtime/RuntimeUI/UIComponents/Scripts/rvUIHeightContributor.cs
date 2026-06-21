using UnityEngine;

namespace realvirtual
{


    public class rvUIHeightContributor : MonoBehaviour
    {
        public float offset = 0f; // Optional offset to add to the calculated height

        public float GetCurrentHeightContribution()
        {

            RectTransform rect = GetComponent<RectTransform>();

            return rect.rect.height + offset;

        }
    }

}
