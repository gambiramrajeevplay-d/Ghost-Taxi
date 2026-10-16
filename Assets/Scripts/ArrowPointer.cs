
using UnityEngine;

public class ArrowPointer : MonoBehaviour
{
    [Header("Auto-Found Targets")]
    [SerializeField] private GhostPickupTrigger pickupTrigger;
    [SerializeField] private GhostDropTrigger dropTrigger;

    [Header("Pointer Settings")]
    [SerializeField] private float rotationSpeed = 0f;

    private Transform target;
    private bool targetSwitched;

    private static ArrowPointer instance;

    public static ArrowPointer Instance => instance;

    private void Awake()
    {
        instance = this;
        FindTargets();
    }

    private void Start()
    {
        // Pickup is the default target.
        if (pickupTrigger != null)
        {
            target = pickupTrigger.transform;
        }
        else
        {
            Debug.LogWarning(
                "ArrowPointer: Pickup trigger was not found.", this);
        }
    }

    private void FindTargets()
    {
        if (pickupTrigger == null)
            pickupTrigger = FindObjectOfType<GhostPickupTrigger>(true);

        if (dropTrigger == null)
            dropTrigger = FindObjectOfType<GhostDropTrigger>(true);
    }

    private void Update()
    {
        if (target == null)
            return;

        LookAtTarget();
    }

    public void SwitchToDropTarget()
    {
        if (targetSwitched)
            return;

        FindTargets();

        if (dropTrigger == null)
        {
            Debug.LogWarning(
                "ArrowPointer: Drop trigger was not found.", this);
            return;
        }

        target = dropTrigger.transform;
        targetSwitched = true;
    }

    private void LookAtTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion desiredRotation =
            Quaternion.LookRotation(direction.normalized);

        if (rotationSpeed <= 0f)
        {
            transform.rotation = desiredRotation;
        }
        else
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                rotationSpeed * Time.deltaTime);
        }
    }
}
