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
    [Tooltip("Optional. If empty, the closest matching passenger in the scene (even inactive) is used.")]
    [SerializeField] private GameObject passenger;

    [Header("Player")]
    [SerializeField] private bool stopPlayerDuringPickup = true;

    [Header("Behaviour Scripts (optional, auto-found on the player car)")]
    [SerializeField] private GhostCarEffect ghostEffect;
    [SerializeField] private WitchCarEffect witchEffect;
    [SerializeField] private VampireTeleportEffect vampireEffect;
    [SerializeField] private SkeletonCarEffect skeletonEffect;

    private RCC_CarControllerV3 playerCar;
    private Rigidbody playerRigidbody;
    private bool pickupStarted;

    private void OnTriggerEnter(Collider other)
    {
        if (pickupStarted)
            return;

        RCC_CarControllerV3 car = other.GetComponentInParent<RCC_CarControllerV3>();
        if (car == null)
            return;

        playerCar = car;
        playerRigidbody = playerCar.GetComponent<Rigidbody>();
        pickupStarted = true;

        if (stopPlayerDuringPickup)
            playerCar.canControl = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.velocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }

        ResolveReferences();
        StartPickup();
    }

    // ---------------------------------------------------------------
    // Reference lookup (player is spawned at runtime)
    // ---------------------------------------------------------------

    private void ResolveReferences()
    {
        switch (passengerType)
        {
            case PassengerType.Ghost:
                ghostEffect = FindEffect(ghostEffect);
                if (passenger == null)
                    passenger = FindClosestPassenger<GhostMovement>();
                break;

            case PassengerType.Witch:
                witchEffect = FindEffect(witchEffect);
                if (passenger == null)
                    passenger = FindClosestPassenger<WitchMovement>();
                break;

            case PassengerType.Vampire:
                vampireEffect = FindEffect(vampireEffect);
                if (passenger == null)
                    passenger = FindClosestPassenger<WitchMovement>();
                break;

            case PassengerType.Skeleton:
                skeletonEffect = FindEffect(skeletonEffect);
                if (passenger == null)
                    passenger = FindClosestPassenger<WitchMovement>();
                break;

            case PassengerType.Zombie:
                if (passenger == null)
                    passenger = FindClosestPassenger<WitchMovement>();
                break;
        }
    }

    /// <summary>
    /// Finds an effect: player car children (inactive included) -> player car parents
    /// -> Inspector reference -> anywhere in the scene (inactive included).
    /// Then makes sure it is active and enabled so coroutines can run.
    /// </summary>
    private T FindEffect<T>(T assigned) where T : MonoBehaviour
    {
        T found = null;

        if (playerCar != null)
        {
            found = playerCar.GetComponentInChildren<T>(true);

            if (found == null)
                found = playerCar.GetComponentInParent<T>(true);
        }

        if (found == null && assigned != null)
            found = assigned;

        if (found == null)
            found = FindObjectOfType<T>(true);

        if (found != null)
            EnsureActive(found);

        return found;
    }

    private void EnsureActive(MonoBehaviour behaviour)
    {
        // Activate the object and any inactive parents (up to the car root).
        Transform t = behaviour.transform;
        Transform stopAt = playerCar != null ? playerCar.transform.parent : null;

        while (t != null && t != stopAt)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);

            t = t.parent;
        }

        if (!behaviour.enabled)
            behaviour.enabled = true;
    }

    /// <summary>Closest passenger of type T in the scene, inactive objects included.</summary>
    private GameObject FindClosestPassenger<T>() where T : MonoBehaviour
    {
        T[] all = FindObjectsOfType<T>(true);
        GameObject best = null;
        float bestDistance = float.MaxValue;

        foreach (T candidate in all)
        {
            if (candidate == null)
                continue;

            // Skip clones that live on the player (e.g. orbiting ghosts).
            if (playerCar != null && candidate.transform.IsChildOf(playerCar.transform))
                continue;

            float d = (candidate.transform.position - transform.position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate.gameObject;
            }
        }

        if (best == null)
            Debug.LogWarning("GhostPickupTrigger: No " + typeof(T).Name + " found in the scene.", this);

        return best;
    }

    // ---------------------------------------------------------------
    // Pickup flow
    // ---------------------------------------------------------------

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

        GhostMovement movement = passenger.GetComponent<GhostMovement>();
        if (movement == null)
        {
            HidePassenger();
            FinishGhostPickup();
            return;
        }

        movement.StartMovingToCar(playerCar.transform, FinishGhostPickup);
    }

    private void StartWalkingPassengerPickup()
    {
        if (passenger == null)
        {
            Debug.LogWarning("GhostPickupTrigger: Passenger is not assigned or found.", this);
            ActivatePassengerEffect();
            EnablePlayerControl();
            return;
        }

        passenger.SetActive(true);

        WitchMovement movement = passenger.GetComponent<WitchMovement>();
        if (movement == null)
        {
            Debug.LogWarning(passenger.name + " needs the WitchMovement component.", passenger);
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
                else
                    Debug.LogWarning("WitchCarEffect not found on the player car.", this);
                break;

            case PassengerType.Vampire:
                if (vampireEffect != null)
                    vampireEffect.ActivateVampireEffect();
                else
                    Debug.LogWarning("VampireTeleportEffect not found on the player car.", this);
                break;

            case PassengerType.Zombie:
                Debug.Log("Zombie passenger picked up.");
                break;

            case PassengerType.Skeleton:
                if (skeletonEffect != null)
                    skeletonEffect.ActivateSkeletonEffect();
                else
                    Debug.LogWarning("SkeletonCarEffect not found on the player car.", this);
                break;
        }
    }

    private void FinishGhostPickup()
    {
        HidePassenger();

        if (ghostEffect != null)
            ghostEffect.ActivateGhostEffect();
        else
            Debug.LogWarning("GhostCarEffect not found on the player car.", this);

        EnablePlayerControl();
    }

    private void HidePassenger()
    {
        if (passenger != null)
            passenger.SetActive(false);
    }

    private void EnablePlayerControl()
    {
        if (playerCar != null)
            playerCar.canControl = true;
    }
}