using UnityEngine;
using System.Collections;

public class WitchMovement : MonoBehaviour
{
    private enum MovementMode
    {
        None,
        TowardCar,
        AwayFromCar
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRayHeight = 2f;
    [SerializeField] private float groundRayDistance = 5f;
    [SerializeField] private float groundOffset = 0f;

    [Header("Animation")]
    [SerializeField] private string walkingParameter = "isWalking";
    [SerializeField] private Animator animator;

    private Transform playerCar;
    private System.Action pickupComplete;
    private MovementMode movementMode = MovementMode.None;

    private Vector3 destination;
    private Vector3 walkDirection;

    private bool pickedUp;
    private bool hasWalkingParameter;
    private int walkingParameterHash;

    public bool IsMoving =>
        movementMode != MovementMode.None && !pickedUp;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        walkingParameterHash =
            Animator.StringToHash(walkingParameter);

        hasWalkingParameter = false;

        if (animator == null)
        {
            Debug.LogError(
                "WitchMovement: No Animator found.",
                this);
            return;
        }

        foreach (AnimatorControllerParameter parameter
                 in animator.parameters)
        {
            if (parameter.nameHash == walkingParameterHash &&
                parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasWalkingParameter = true;
                break;
            }
        }

        if (!hasWalkingParameter)
        {
            Debug.LogError(
                "WitchMovement: Animator needs a Bool parameter named '" +
                walkingParameter + "'.",
                animator);
        }

        if (groundLayer.value == 0)
        {
            Debug.LogWarning(
                "WitchMovement: Assign the ground layer in the Inspector.",
                this);
        }
    }

    private void Update()
    {
        if (pickedUp)
            return;

        switch (movementMode)
        {
            case MovementMode.TowardCar:
                WalkTowardsCar();
                break;

            case MovementMode.AwayFromCar:
                WalkAway();
                break;
        }
    }

    // =====================================================
    // PICKUP: WALK TOWARDS THE CAR
    // =====================================================

    public void StartWalkingToCar(
        Transform car,
        System.Action onComplete)
    {
        if (car == null)
        {
            Debug.LogWarning(
                "WitchMovement: Player car is null.",
                this);
            return;
        }

        playerCar = car;

        // IMPORTANT: Save the callback so player controls
        // can be restored when pickup finishes.
        pickupComplete = onComplete;

        pickedUp = false;
        movementMode = MovementMode.TowardCar;

        SetWalking(true);
    }

    private void WalkTowardsCar()
    {
        if (playerCar == null)
        {
            StopMovement();
            return;
        }

        Vector3 target = playerCar.position;
        target.y = transform.position.y;

        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            RotateTowards(direction);

            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                moveSpeed * Time.deltaTime);
        }

        KeepOnGround();
        SetWalking(true);
    }

    // =====================================================
    // DROP: WALK AWAY FROM THE CAR
    // =====================================================

    public void StartWalkingAway(
        Vector3 direction,
        float distance,
        float speed)
    {
        StopAllCoroutines();

        playerCar = null;
        pickupComplete = null;
        pickedUp = false;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = Vector3.back;

        walkDirection = direction.normalized;
        moveSpeed = Mathf.Max(0.01f, speed);

        KeepOnGround();

        destination = transform.position +
                      walkDirection * Mathf.Max(0f, distance);

        movementMode = MovementMode.AwayFromCar;

        SetWalking(distance > 0f);
    }

    private void WalkAway()
    {
        Vector3 remaining = destination - transform.position;
        remaining.y = 0f;

        if (remaining.sqrMagnitude <= 0.01f)
        {
            StopMovement();
            return;
        }

        RotateTowards(walkDirection);

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime);

        KeepOnGround();
        SetWalking(true);
    }

    // =====================================================
    // ROTATION
    // =====================================================

    private void RotateTowards(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            direction.normalized,
            Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    // =====================================================
    // GROUND DETECTION
    // =====================================================

    private void KeepOnGround()
    {
        Vector3 rayOrigin =
            transform.position + Vector3.up * groundRayHeight;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            groundRayDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            Vector3 position = transform.position;
            position.y = hit.point.y + groundOffset;
            transform.position = position;
        }
    }

    // =====================================================
    // PICKUP COLLISION
    // =====================================================

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp ||
            movementMode != MovementMode.TowardCar)
            return;

        RCC_CarControllerV3 car =
            other.GetComponentInParent<RCC_CarControllerV3>();

        if (car == null)
            return;

        if (playerCar != null && car.transform != playerCar)
            return;

        pickedUp = true;
        movementMode = MovementMode.None;

        SetWalking(false);

        StartCoroutine(FinishPickup());
    }

    private IEnumerator FinishPickup()
    {
        yield return new WaitForSeconds(0.05f);

        StopMovement();

        // Notify GhostPickupTrigger BEFORE disabling the witch.
        System.Action callback = pickupComplete;
        pickupComplete = null;

        if (callback != null)
            callback.Invoke();

        gameObject.SetActive(false);
    }

    // =====================================================
    // STOP / ANIMATION
    // =====================================================

    private void StopMovement()
    {
        movementMode = MovementMode.None;
        SetWalking(false);
    }

    private void SetWalking(bool walking)
    {
        if (animator == null || !hasWalkingParameter)
            return;

        animator.SetBool(walkingParameterHash, walking);
    }

    private void OnDisable()
    {
        if (animator != null && hasWalkingParameter)
            animator.SetBool(walkingParameterHash, false);
    }

    // =====================================================
    // GROUND RAY VISUALIZATION
    // =====================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3 origin =
            transform.position + Vector3.up * groundRayHeight;

        Gizmos.DrawLine(
            origin,
            origin + Vector3.down * groundRayDistance);
    }
}