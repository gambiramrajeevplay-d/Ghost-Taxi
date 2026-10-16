
using System.Collections;
using UnityEngine;

public class GhostPickupTrigger : MonoBehaviour
{
    public enum PassengerType
    {
        Ghost,
        Witch,
        Vampire,
        Zombie,
        Skeleton
    }

    [Header("Passenger")]
    [SerializeField] private PassengerType passengerType;
    [SerializeField] private GameObject passenger;

    public PassengerType CurrentPassengerType => passengerType;

    [Header("Arrow")]
    [SerializeField] private GameObject arrow;

    [Header("Player")]
    [SerializeField] private bool stopPlayerDuringPickup = true;

    [Header("Drop Trigger")]
    [SerializeField] private GameObject dropTriggerObject;

    [Header("Smooth Stop")]
    [SerializeField] private float stopDuration = 0.9f;
    [SerializeField] private float controlLossDelay = 0.35f;

    [Header("Behaviour Scripts")]
    [SerializeField] private GhostCarEffect ghostEffect;
    [SerializeField] private WitchCarEffect witchEffect;
    [SerializeField] private VampireTeleportEffect vampireEffect;
    [SerializeField] private SkeletonCarEffect skeletonEffect;

    private RCC_CarControllerV3 playerCar;
    private Rigidbody playerRigidbody;
    private bool pickupStarted;

    private void Awake()
    {
        if (dropTriggerObject != null)
            dropTriggerObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (pickupStarted)
            return;

        RCC_CarControllerV3 car =
            other.GetComponentInParent<RCC_CarControllerV3>();

        if (car == null)
            return;

        playerCar = car;
        playerRigidbody = playerCar.GetComponent<Rigidbody>();
        pickupStarted = true;

        if (arrow != null)
            arrow.SetActive(false);

        ResolveReferences();
        StartCoroutine(PickupSequence());
    }

    private IEnumerator PickupSequence()
    {
        if (stopPlayerDuringPickup)
            yield return SmoothStopPlayer();

        StartPickup();
    }

    private IEnumerator SmoothStopPlayer()
    {
        if (playerCar == null)
            yield break;

        float duration = Mathf.Max(0.01f, stopDuration);
        float controlDelay = Mathf.Clamp(
            controlLossDelay, 0f, duration);

        bool controlsRemoved = false;
        float elapsed = 0f;
        float previousRemaining = 1f;

        if (controlDelay <= 0f)
        {
            playerCar.canControl = false;
            controlsRemoved = true;
        }

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();

            if (playerCar == null)
                yield break;

            elapsed += Time.fixedDeltaTime;

            if (!controlsRemoved && elapsed >= controlDelay)
            {
                playerCar.canControl = false;
                controlsRemoved = true;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            float remaining = 1f - Mathf.SmoothStep(0f, 1f, t);

            float ratio = previousRemaining > 0.0001f
                ? remaining / previousRemaining
                : 0f;

            previousRemaining = remaining;

            if (playerRigidbody != null)
            {
                Vector3 velocity = playerRigidbody.velocity;

                playerRigidbody.velocity = new Vector3(
                    velocity.x * ratio,
                    velocity.y,
                    velocity.z * ratio);

                playerRigidbody.angularVelocity *= ratio;
            }
        }

        if (!controlsRemoved)
            playerCar.canControl = false;

        if (playerRigidbody != null)
        {
            Vector3 velocity = playerRigidbody.velocity;

            playerRigidbody.velocity = new Vector3(
                0f, velocity.y, 0f);

            playerRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void ResolveReferences()
    {
        switch (passengerType)
        {
            case PassengerType.Ghost:
                ghostEffect = FindEffect(ghostEffect);

                if (passenger == null)
                {
                    GhostMovement ghost = FindClosestPassenger<GhostMovement>();
                    if (ghost != null)
                        passenger = ghost.gameObject;
                }
                break;

            case PassengerType.Witch:
                witchEffect = FindEffect(witchEffect);

                if (passenger == null)
                {
                    WitchMovement witch = FindClosestPassenger<WitchMovement>();
                    if (witch != null)
                        passenger = witch.gameObject;
                }
                break;

            case PassengerType.Vampire:
                vampireEffect = FindEffect(vampireEffect);

                if (passenger == null)
                {
                    WitchMovement vampire = FindClosestPassenger<WitchMovement>();
                    if (vampire != null)
                        passenger = vampire.gameObject;
                }
                break;

            case PassengerType.Skeleton:
                skeletonEffect = FindEffect(skeletonEffect);

                if (passenger == null)
                {
                    WitchMovement skeleton = FindClosestPassenger<WitchMovement>();
                    if (skeleton != null)
                        passenger = skeleton.gameObject;
                }
                break;

            case PassengerType.Zombie:
                if (passenger == null)
                {
                    WitchMovement zombie = FindClosestPassenger<WitchMovement>();
                    if (zombie != null)
                        passenger = zombie.gameObject;
                }
                break;
        }
    } 

    private T FindEffect<T>(T assigned) where T : MonoBehaviour
    {
        T found = null;

        if (playerCar != null)
        {
            found = playerCar.GetComponentInChildren<T>(true);

            if (found == null)
                found = playerCar.GetComponentInParent<T>(true);
        }

        if (found == null)
            found = assigned;

        if (found == null)
            found = FindObjectOfType<T>(true);

        if (found != null)
        {
            Transform current = found.transform;
            Transform stopAt = playerCar != null
                ? playerCar.transform.parent
                : null;

            while (current != null && current != stopAt)
            {
                if (!current.gameObject.activeSelf)
                    current.gameObject.SetActive(true);

                current = current.parent;
            }

            found.enabled = true;
        }

        return found;
    }

    private T FindClosestPassenger<T>() where T : MonoBehaviour
    {
        T[] all = FindObjectsOfType<T>(true);

        GameObject best = null;
        float bestDistance = float.MaxValue;

        foreach (T candidate in all)
        {
            if (candidate == null)
                continue;

            if (playerCar != null &&
                candidate.transform.IsChildOf(playerCar.transform))
                continue;

            float distance =
                (candidate.transform.position - transform.position)
                .sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate.gameObject;
            }
        }

        if (best == null)
        {
            Debug.LogWarning(
                "GhostPickupTrigger: No " +
                typeof(T).Name + " found.", this);
        }

        return best != null ? best.GetComponent<T>() : null;
    }

    private void StartPickup()
    {
        switch (passengerType)
        {
            case PassengerType.Ghost:
                StartGhostPickup();
                break;

            case PassengerType.Witch:
            case PassengerType.Vampire:
            case PassengerType.Zombie:
            case PassengerType.Skeleton:
                StartWalkingPassengerPickup();
                break;
        }
    }

    private void StartGhostPickup()
    {
        if (passenger == null)
        {
            FinishGhostPickup();
            return;
        }

        passenger.SetActive(true);

        GhostMovement movement =
            passenger.GetComponent<GhostMovement>();

        if (movement == null)
        {
            HidePassenger();
            FinishGhostPickup();
            return;
        }

        movement.StartMovingToCar(
            playerCar.transform, FinishGhostPickup);
    }

    private void StartWalkingPassengerPickup()
    {
        if (passenger == null)
        {
            ActivatePassengerEffect();
            EnablePlayerControl();
            return;
        }

        passenger.SetActive(true);

        WitchMovement movement =
            passenger.GetComponent<WitchMovement>();

        if (movement == null)
        {
            HidePassenger();
            ActivatePassengerEffect();
            EnablePlayerControl();
            return;
        }

        movement.StartWalkingToCar(playerCar.transform, () =>
        {
            HidePassenger();
            ActivatePassengerEffect();
            EnablePlayerControl();
        });
    }

    private void ActivatePassengerEffect()
    {
        switch (passengerType)
        {
            case PassengerType.Witch:
                if (witchEffect != null)
                    witchEffect.ActivateWitchEffect();
                break;

            case PassengerType.Vampire:
                if (vampireEffect != null)
                    vampireEffect.ActivateVampireEffect();
                break;

            case PassengerType.Skeleton:
                if (skeletonEffect != null)
                    skeletonEffect.ActivateSkeletonEffect();
                break;

            case PassengerType.Zombie:
                Debug.Log("Zombie passenger picked up.");
                break;
        }
    }

    private void FinishGhostPickup()
    {
        HidePassenger();

        if (ghostEffect != null)
            ghostEffect.ActivateGhostEffect();

        EnablePlayerControl();
    }

    private void HidePassenger()
    {
        if (passenger != null)
            passenger.SetActive(false);
    }

    private void EnablePlayerControl()
    {
        if (ArrowPointer.Instance != null)
            ArrowPointer.Instance.SwitchToDropTarget();

        if (dropTriggerObject != null)
            dropTriggerObject.SetActive(true);

        if (playerCar != null)
            playerCar.canControl = true;
    }
}
