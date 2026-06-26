using System.Collections.Generic;
using UnityEngine;

public class CarControlReceiver : MonoBehaviour
{
    [Header("References")]
    public Transform carTransform;
    public Transform carPayloadAnchor;
    public GameObject payloadOnCarObject;
    public GameObject payloadOnCarPrefab;

    [Header("Shelf References")]
    public List<GameObject> shelvingAItems = new List<GameObject>();
    public List<Transform> shelvingBSlots = new List<Transform>();
    public GameObject manufacturedObjectPrefab;

    [Header("Motion")]
    public float moveSpeed = 4f;
    public bool smoothMovement = true;

    [Header("Behavior")]
    public bool hidePayloadWhenNotCarrying = true;
    public bool hideOneShelvingAItemOnCar1Pickup = true;
    public bool placeManufacturedObjectOnCar2Drop = true;

    [Header("Diagnostics")]
    [SerializeField] bool hasTarget;
    [SerializeField] Vector3 targetPosition;
    [SerializeField] string lastStatus = "";
    [SerializeField] bool lastAtPickTarget;
    [SerializeField] bool lastAtDropTarget;
    [SerializeField] bool lastCarrying;
    [SerializeField] int hiddenShelvingACount;
    [SerializeField] int placedShelvingBCount;

    void Awake()
    {
        if (carTransform == null)
        {
            carTransform = transform;
        }

        targetPosition = carTransform.position;
    }

    void Update()
    {
        if (!hasTarget || carTransform == null)
        {
            return;
        }

        if (smoothMovement)
        {
            carTransform.position = Vector3.MoveTowards(
                carTransform.position,
                targetPosition,
                Mathf.Max(0f, moveSpeed) * Time.deltaTime);
        }
        else
        {
            carTransform.position = targetPosition;
        }
    }

    public void Configure(Transform target)
    {
        carTransform = target != null ? target : transform;
        targetPosition = carTransform.position;
    }

    public void ApplyControl(string objectId, CarControlData control)
    {
        if (control == null)
        {
            Debug.LogWarning($"[CarControlReceiver:{name}] Ignored null car control.");
            return;
        }

        if (carTransform == null)
        {
            carTransform = transform;
        }

        Vector3 current = carTransform.position;
        targetPosition = new Vector3(control.currentX, current.y, control.currentZ);
        hasTarget = true;

        bool carryingStarted = !lastCarrying && control.carrying;
        bool pickStarted = !lastAtPickTarget && control.atPickTarget;
        bool dropStarted = !lastAtDropTarget && control.atDropTarget;

        if (control.carrying)
        {
            EnsurePayloadOnCar();
        }
        else if (hidePayloadWhenNotCarrying)
        {
            SetPayloadVisible(false);
        }

        if (IsCar1(objectId) &&
            control.atPickTarget &&
            control.carrying &&
            (carryingStarted || pickStarted))
        {
            EnsurePayloadOnCar();
            HideNextShelvingAItem();
        }

        if (IsCar2(objectId) &&
            control.atDropTarget &&
            control.carrying &&
            (dropStarted || carryingStarted))
        {
            PlaceManufacturedObjectInShelvingB();
        }

        lastStatus = control.status ?? string.Empty;
        lastAtPickTarget = control.atPickTarget;
        lastAtDropTarget = control.atDropTarget;
        lastCarrying = control.carrying;
    }

    void EnsurePayloadOnCar()
    {
        if (payloadOnCarObject == null && payloadOnCarPrefab != null)
        {
            payloadOnCarObject = Instantiate(payloadOnCarPrefab);
        }

        if (payloadOnCarObject == null)
        {
            // TODO: Assign payloadOnCarObject or payloadOnCarPrefab if this car should show carried payloads.
            return;
        }

        if (carPayloadAnchor != null)
        {
            payloadOnCarObject.transform.SetParent(carPayloadAnchor, false);
            payloadOnCarObject.transform.localPosition = Vector3.zero;
            payloadOnCarObject.transform.localRotation = Quaternion.identity;
        }
        else if (carTransform != null)
        {
            payloadOnCarObject.transform.SetParent(carTransform, false);
        }

        SetPayloadVisible(true);
    }

    void SetPayloadVisible(bool visible)
    {
        if (payloadOnCarObject != null)
        {
            payloadOnCarObject.SetActive(visible);
        }
    }

    void HideNextShelvingAItem()
    {
        if (!hideOneShelvingAItemOnCar1Pickup)
        {
            return;
        }

        for (int i = 0; i < shelvingAItems.Count; i++)
        {
            GameObject item = shelvingAItems[i];
            if (item == null || !item.activeSelf)
            {
                continue;
            }

            item.SetActive(false);
            hiddenShelvingACount++;
            return;
        }

        // TODO: Assign shelvingAItems if Car1 pickup should hide raw shelf payloads.
    }

    void PlaceManufacturedObjectInShelvingB()
    {
        if (!placeManufacturedObjectOnCar2Drop)
        {
            return;
        }

        if (placedShelvingBCount >= shelvingBSlots.Count)
        {
            // TODO: Assign shelvingBSlots in order if Car2 should place manufactured objects on Shelving B.
            return;
        }

        Transform slot = shelvingBSlots[placedShelvingBCount];
        if (slot == null)
        {
            placedShelvingBCount++;
            return;
        }

        GameObject objectToPlace = null;
        if (payloadOnCarObject != null && payloadOnCarObject.activeSelf)
        {
            objectToPlace = payloadOnCarObject;
            payloadOnCarObject = null;
        }
        else if (manufacturedObjectPrefab != null)
        {
            objectToPlace = Instantiate(manufacturedObjectPrefab);
        }

        if (objectToPlace == null)
        {
            // TODO: Assign manufacturedObjectPrefab or payloadOnCarObject for Car2 Shelving B placement.
            return;
        }

        objectToPlace.transform.SetParent(slot, false);
        objectToPlace.transform.localPosition = Vector3.zero;
        objectToPlace.transform.localRotation = Quaternion.identity;
        objectToPlace.SetActive(true);
        placedShelvingBCount++;
    }

    static bool IsCar1(string objectId) =>
        string.Equals(objectId, "car1", System.StringComparison.OrdinalIgnoreCase);

    static bool IsCar2(string objectId) =>
        string.Equals(objectId, "car2", System.StringComparison.OrdinalIgnoreCase);
}
