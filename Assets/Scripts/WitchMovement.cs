using UnityEngine;
using System.Collections;

public class WitchMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Ground")]
    [Tooltip("Ground layer used to keep the witch on the ground.")]
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private float groundRayHeight = 2f;
    [SerializeField] private float groundRayDistance = 5f;
    [SerializeField] private float groundOffset = 0f;

    [Header("Animation")]
    [SerializeField] private string walkingParameter = "isWalking";
    [SerializeField] private Animator animator;

    private Transform playerCar;

    private bool walkingToCar;
    private bool pickedUp;

    private System.Action pickupComplete;

    private int walkingParameterHash;
    private bool hasWalkingParameter;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError(
                "WitchMovement: No Animator found.",
                this
            );

            return;
        }

        walkingParameterHash =
            Animator.StringToHash(walkingParameter);

        hasWalkingParameter =
            HasAnimatorParameter(
                animator,
                walkingParameterHash
            );

        if (!hasWalkingParameter)
        {
            Debug.LogError(
                "WitchMovement: Animator does not contain Bool parameter '" +
                walkingParameter +
                "'.",
                animator
            );
        }
    }

    private void Update()
    {
        if (!walkingToCar || pickedUp)
            return;

        WalkTowardsCar();
    }

    // =========================================================
    // START WALKING
    // =========================================================

    public void StartWalkingToCar(
        Transform car,
        System.Action onComplete)
    {
        if (car == null)
        {
            Debug.LogWarning(
                "WitchMovement: Player car is null."
            );

            return;
        }

        playerCar = car;
        pickupComplete = onComplete;

        pickedUp = false;
        walkingToCar = true;

        SetWalking(true);
    }

    // =========================================================
    // WALK TOWARDS CAR
    // =========================================================

    private void WalkTowardsCar()
    {
        if (playerCar == null)
        {
            StopWalking();
            return;
        }

        Vector3 targetPosition = playerCar.position;

        // Keep witch at current Y.
        targetPosition.y = transform.position.y;

        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        // Rotate toward car.
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
        }

        // Move toward car.
        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

        // Keep witch on ground.
        KeepOnGround();

        SetWalking(true);
    }

    // =========================================================
    // WITCH COLLIDES WITH CAR
    // =========================================================

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp)
            return;

        RCC_CarControllerV3 car =
            other.GetComponentInParent<RCC_CarControllerV3>();

        if (car == null)
            return;

        // Only accept the car we were walking toward.
        if (playerCar != null &&
            car.transform != playerCar)
        {
            return;
        }

        pickedUp = true;
        walkingToCar = false;

        SetWalking(false);

        Debug.Log("Witch entered player car.");

        StartCoroutine(PickupWitch(car));
    }

    // =========================================================
    // PICKUP
    // =========================================================

    private IEnumerator PickupWitch(
        RCC_CarControllerV3 car)
    {
        // Small delay if you want the witch to visually enter
        // the car before disappearing.
        yield return new WaitForSeconds(0.05f);

        // Stop movement.
        StopWalking();

        // Tell GhostPickupTrigger that pickup is complete.
        pickupComplete?.Invoke();

        pickupComplete = null;

        // Disable witch.
        gameObject.SetActive(false);
    }

    // =========================================================
    // STOP
    // =========================================================

    private void StopWalking()
    {
        walkingToCar = false;

        SetWalking(false);
    }

    // =========================================================
    // GROUND
    // =========================================================

    private void KeepOnGround()
    {
        Vector3 rayOrigin =
            transform.position +
            Vector3.up * groundRayHeight;

        RaycastHit hit;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out hit,
            groundRayDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            Vector3 position =
                transform.position;

            position.y =
                hit.point.y + groundOffset;

            transform.position =
                position;
        }
    }

    // =========================================================
    // ANIMATION
    // =========================================================

    private void SetWalking(bool value)
    {
        if (animator == null)
            return;

        if (!hasWalkingParameter)
            return;

        animator.SetBool(
            walkingParameterHash,
            value
        );
    }

    // =========================================================
    // CHECK ANIMATOR PARAMETER
    // =========================================================

    private bool HasAnimatorParameter(
        Animator targetAnimator,
        int parameterHash)
    {
        AnimatorControllerParameter[] parameters =
            targetAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == parameterHash)
            {
                if (parameters[i].type ==
                    AnimatorControllerParameterType.Bool)
                {
                    return true;
                }

                Debug.LogError(
                    "WitchMovement: '" +
                    walkingParameter +
                    "' is not a Bool parameter.",
                    targetAnimator
                );

                return false;
            }
        }

        return false;
    }

    // =========================================================
    // DISABLE
    // =========================================================

    private void OnDisable()
    {
        if (animator != null &&
            hasWalkingParameter)
        {
            animator.SetBool(
                walkingParameterHash,
                false
            );
        }
    }

    // =========================================================
    // GIZMO
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Vector3 rayOrigin =
            transform.position +
            Vector3.up * groundRayHeight;

        Gizmos.DrawLine(
            rayOrigin,
            rayOrigin +
            Vector3.down * groundRayDistance
        );
    }
}