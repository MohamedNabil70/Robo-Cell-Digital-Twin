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
    [Tooltip("When false, this receiver only records MQTT car state and does not spawn, hide, or move payload visuals.")]
    public bool allowLocalPayloadVisuals = true;
    public Vector3 payloadOnCarScale = new Vector3(4f, 4f, 4f);

    [Header("Diagnostics")]
    [SerializeField] bool hasTarget;
    [SerializeField] Vector3 targetPosition;
    [SerializeField] float currentX;
    [SerializeField] float currentY;
    [SerializeField] float currentZ;
    [SerializeField] float goalZ;
    [SerializeField] float fixedGoalX;
    [SerializeField] float fixedGoalY;
    [SerializeField] string lastStatus = "";
    [SerializeField] bool lastAtPickTarget;
    [SerializeField] bool lastAtDropTarget;
    [SerializeField] bool lastCarrying;
    [SerializeField] int hiddenShelvingACount;
    [SerializeField] int placedShelvingBCount;
    [SerializeField] bool warnedMissingPayloadPrefab;

    readonly List<GameObject> managedPayloadObjects = new List<GameObject>();

    void Awake()
    {
        if (carTransform == null)
        {
            carTransform = transform;
        }

        targetPosition = carTransform.position;
        fixedGoalX = targetPosition.x;
        fixedGoalY = targetPosition.y;
        goalZ = targetPosition.z;
        UpdateRuntimePositionDiagnostics();
    }

    void Update()
    {
        UpdateRuntimePositionDiagnostics();

        if (!hasTarget || carTransform == null)
        {
            return;
        }

        targetPosition.x = fixedGoalX;
        targetPosition.y = fixedGoalY;

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

        UpdateRuntimePositionDiagnostics();
    }

    public void Configure(Transform target)
    {
        carTransform = target != null ? target : transform;
        if (!hasTarget)
        {
            targetPosition = carTransform.position;
            fixedGoalX = targetPosition.x;
            fixedGoalY = targetPosition.y;
            goalZ = targetPosition.z;
        }

        UpdateRuntimePositionDiagnostics();
    }

    public void SetLocalPayloadVisualsAllowed(bool allowed)
    {
        allowLocalPayloadVisuals = allowed;
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
        if (control.hasTargetZ)
        {
            Vector3 target = current;
            fixedGoalX = current.x;
            fixedGoalY = current.y;
            target.x = fixedGoalX;
            target.y = fixedGoalY;
            target.z = control.targetZ;
            targetPosition = target;
            hasTarget = true;
            goalZ = targetPosition.z;
        }

        bool carryingStarted = control.hasCarrying && !lastCarrying && control.carrying;
        bool pickStarted = control.hasAtPickTarget && !lastAtPickTarget && control.atPickTarget;
        bool dropStarted = control.hasAtDropTarget && !lastAtDropTarget && control.atDropTarget;
        bool carryingNow = control.hasCarrying ? control.carrying : lastCarrying;

        if (allowLocalPayloadVisuals)
        {
            if (control.hasCarrying && control.carrying)
            {
                EnsurePayloadOnCar();
            }
            else if (control.hasCarrying && hidePayloadWhenNotCarrying)
            {
                SetPayloadVisible(false);
            }

            if (IsCar1(objectId) &&
                control.hasAtPickTarget &&
                control.atPickTarget &&
                carryingNow &&
                (carryingStarted || pickStarted))
            {
                EnsurePayloadOnCar();
                HideNextShelvingAItem();
            }

            if (IsCar2(objectId) &&
                control.hasAtDropTarget &&
                control.atDropTarget &&
                carryingNow &&
                (dropStarted || carryingStarted))
            {
                PlaceManufacturedObjectInShelvingB();
            }
        }

        lastStatus = control.status ?? string.Empty;
        if (control.hasAtPickTarget)
        {
            lastAtPickTarget = control.atPickTarget;
        }

        if (control.hasAtDropTarget)
        {
            lastAtDropTarget = control.atDropTarget;
        }

        if (control.hasCarrying)
        {
            lastCarrying = control.carrying;
        }

        UpdateRuntimePositionDiagnostics();
    }

    void EnsurePayloadOnCar()
    {
        if (payloadOnCarObject == null)
        {
            payloadOnCarObject = FindManagedPayloadOnCar();
        }

        if (payloadOnCarObject == null && payloadOnCarPrefab != null)
        {
            payloadOnCarObject = Instantiate(payloadOnCarPrefab);
            payloadOnCarObject.name = $"{name}_PayloadOnCar";
            managedPayloadObjects.Add(payloadOnCarObject);
        }

        if (payloadOnCarObject == null)
        {
            if (!warnedMissingPayloadPrefab)
            {
                Debug.LogWarning($"[CarControlReceiver:{name}] carrying=true but payloadOnCarPrefab is not assigned.");
                warnedMissingPayloadPrefab = true;
            }

            return;
        }

        if (carPayloadAnchor != null)
        {
            payloadOnCarObject.transform.SetParent(carPayloadAnchor, false);
            payloadOnCarObject.transform.localPosition = Vector3.zero;
            payloadOnCarObject.transform.localRotation = Quaternion.identity;
            payloadOnCarObject.transform.localScale = payloadOnCarScale;
        }
        else if (carTransform != null)
        {
            payloadOnCarObject.transform.SetParent(carTransform, false);
            payloadOnCarObject.transform.localPosition = Vector3.zero;
            payloadOnCarObject.transform.localRotation = Quaternion.identity;
            payloadOnCarObject.transform.localScale = payloadOnCarScale;
        }

        MakePayloadVisualFollowCar(payloadOnCarObject);
        SetPayloadVisible(true);
    }

    void SetPayloadVisible(bool visible)
    {
        if (payloadOnCarObject != null)
        {
            payloadOnCarObject.SetActive(visible);
        }

        if (!visible)
        {
            HideManagedPayloadChildren();
        }
    }

    GameObject FindManagedPayloadOnCar()
    {
        for (int i = 0; i < managedPayloadObjects.Count; i++)
        {
            GameObject payload = managedPayloadObjects[i];
            if (payload != null && IsPayloadAttachedToCar(payload.transform))
            {
                return payload;
            }
        }

        return null;
    }

    void HideManagedPayloadChildren()
    {
        for (int i = 0; i < managedPayloadObjects.Count; i++)
        {
            GameObject payload = managedPayloadObjects[i];
            if (payload != null && IsPayloadAttachedToCar(payload.transform))
            {
                payload.SetActive(false);
            }
        }
    }

    bool IsPayloadAttachedToCar(Transform payload)
    {
        if (payload == null)
        {
            return false;
        }

        return (carPayloadAnchor != null && payload.IsChildOf(carPayloadAnchor)) ||
               (carTransform != null && payload.IsChildOf(carTransform));
    }

    static void MakePayloadVisualFollowCar(GameObject payload)
    {
        if (payload == null)
        {
            return;
        }

        Rigidbody[] bodies = payload.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
            {
                continue;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
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

    void UpdateRuntimePositionDiagnostics()
    {
        if (carTransform == null)
        {
            return;
        }

        Vector3 position = carTransform.position;
        currentX = position.x;
        currentY = position.y;
        currentZ = position.z;
        goalZ = targetPosition.z;
    }

    static bool IsCar1(string objectId) =>
        string.Equals(objectId, "car1", System.StringComparison.OrdinalIgnoreCase);

    static bool IsCar2(string objectId) =>
        string.Equals(objectId, "car2", System.StringComparison.OrdinalIgnoreCase);
}
