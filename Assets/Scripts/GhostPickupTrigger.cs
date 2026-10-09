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

    [Header("Player")]
    [SerializeField] private bool stopPlayerDuringPickup = true;

    [Header("Behaviour Scripts")]
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

        RCC_CarControllerV3 car =
            other.GetComponentInParent<RCC_CarControllerV3>();

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

        StartPickup();
    }

    private void StartPickup()
    {
        switch (passengerType)
        {
            case PassengerType.Ghost:
                StartGhostPickup();
                break;

            case PassengerType.Witch:
                StartWalkingPassengerPickup();
                break;

            case PassengerType.Vampire:
                StartWalkingPassengerPickup();
                break;

            case PassengerType.Zombie:
                StartWalkingPassengerPickup();
                break;

            case PassengerType.Skeleton:
                StartWalkingPassengerPickup();
                break;
        }
    }

    // Ghost keeps its existing movement behavior.
    private void StartGhostPickup()
    {
        if (passenger == null)
        {
            FinishGhostPickup();
            return;
        }

        if (ghostEffect == null && playerCar != null)
        {
            ghostEffect =
                playerCar.GetComponentInChildren<GhostCarEffect>();
        }

        GhostMovement movement =
            passenger.GetComponent<GhostMovement>();

        if (movement == null)
        {
            HidePassenger();
            FinishGhostPickup();
            return;
        }

        movement.StartMovingToCar(
            playerCar.transform,
            FinishGhostPickup
        );
    }

    // Shared by Witch, Vampire, Zombie and Skeleton.
    private void StartWalkingPassengerPickup()
    {
        if (passenger == null)
        {
            Debug.LogWarning(
                "GhostPickupTrigger: Passenger is not assigned.",
                this
            );

            EnablePlayerControl();
            return;
        }

        passenger.SetActive(true);

        WitchMovement movement =
            passenger.GetComponent<WitchMovement>();

        if (movement == null)
        {
            Debug.LogWarning(
                passenger.name +
                " needs the WitchMovement component.",
                passenger
            );

            HidePassenger();
            ActivatePassengerEffect();
            EnablePlayerControl();
            return;
        }

        movement.StartWalkingToCar(
            playerCar.transform,
            () =>
            {
                HidePassenger();
                ActivatePassengerEffect();
                EnablePlayerControl();
            }
        );
    }

    private void ActivatePassengerEffect()
    {
        switch (passengerType)
        {
            case PassengerType.Witch:
                if (witchEffect == null && playerCar != null)
                {
                    witchEffect =
                        playerCar.GetComponentInChildren<WitchCarEffect>();
                }

                if (witchEffect != null)
                    witchEffect.ActivateWitchEffect();
                break;

            case PassengerType.Vampire:
                if (vampireEffect == null && playerCar != null)
                {
                    vampireEffect =
                        playerCar.GetComponent<VampireTeleportEffect>();
                }

                if (vampireEffect != null)
                    vampireEffect.ActivateVampireEffect();
                break;

            case PassengerType.Zombie:
                // Add your zombie effect here when ready.
                Debug.Log("Zombie passenger picked up.");
                break;

            case PassengerType.Skeleton:
                if (skeletonEffect == null && playerCar != null)
                {
                    skeletonEffect =
                        playerCar.GetComponent<SkeletonCarEffect>();
                }

                if (skeletonEffect != null)
                {
                    // Bones spawn after a qualifying obstacle collision.
                    skeletonEffect.ActivateSkeletonEffect();
                }
                else
                {
                    Debug.LogWarning(
                        "SkeletonCarEffect is missing from the player car.",
                        this
                    );
                }
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
        if (playerCar != null)
            playerCar.canControl = true;
    }
}