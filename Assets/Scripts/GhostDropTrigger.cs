using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GhostDropTrigger : MonoBehaviour
{
    [System.Serializable]
    public class PassengerPrefabEntry
    {
        public string passengerName;
        public GameObject prefab;
    }

    [Header("Passenger Prefabs")]
    [SerializeField]
    private List<PassengerPrefabEntry> passengerPrefabs =
        new List<PassengerPrefabEntry>();

    [Tooltip("Name used to select the passenger to drop, for example Witch or Ghost.")]
    [SerializeField] private string passengerNameToDrop = "witch";

    [SerializeField] private Transform fallbackSpawnPoint;

    [Header("Player Car Spawn Point Search")]
    [SerializeField] private string spawnPointName = "PassengerSpawnPoint";
    [SerializeField] private string spawnPointTag = "";

    [Header("Spawn Rotation")]
    [SerializeField] private bool useSpawnPointRotation = false;
    [SerializeField] private Vector3 spawnRotationOffset = Vector3.zero;

    [Header("Passenger Movement")]
    [SerializeField]
    private Vector3 movementDirection =
        new Vector3(0f, 0f, -1f);

    [SerializeField] private float movementDistance = 10f;
    [SerializeField] private float movementSpeed = 2f;

    [Header("Passenger Rotation")]
    [SerializeField] private bool rotateToFaceDirection = true;
    [SerializeField] private bool keepUpright = true;
    [SerializeField] private Vector3 movementRotationOffset = Vector3.zero;
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Player Control")]
    [SerializeField] private bool stopPlayerDuringDrop = true;
    [SerializeField] private float controlResumeDelay = 0.25f;

    [Header("Smooth Stop")]
    [Tooltip("How long the car takes to slow down to a stop after entering the trigger.")]
    [SerializeField] private float stopDuration = 0.9f;
    [Tooltip("The player keeps control for this long, then controls are taken away. Must be less than Stop Duration.")]
    [SerializeField] private float controlLossDelay = 0.35f;

    [Header("Passenger Effects")]
    [SerializeField] private bool disableAllEffectsOnDrop = true;

    [Header("Finish")]
    [SerializeField] private float finishNotificationDelay = 0f;

    private RCC_CarControllerV3 playerCar;
    private Rigidbody playerRigidbody;
    private bool dropStarted;
    private bool originalCanControl;

    private void OnTriggerEnter(Collider other)
    {
        if (dropStarted)
            return;

        RCC_CarControllerV3 car =
            other.GetComponentInParent<RCC_CarControllerV3>();

        if (car == null)
            return;

        playerCar = car;
        playerRigidbody = playerCar.GetComponent<Rigidbody>();
        dropStarted = true;
        originalCanControl = playerCar.canControl;

        if (disableAllEffectsOnDrop)
            DisableAllPassengerEffects();

        StartCoroutine(DropPassenger());
    }

    // =========================================================
    // SMOOTH STOP
    // =========================================================

    private IEnumerator SmoothStopPlayer()
    {
        if (playerCar == null)
            yield break;

        float duration = Mathf.Max(0.01f, stopDuration);
        float controlDelay = Mathf.Clamp(controlLossDelay, 0f, duration);
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

            // Take controls away after a short delay instead of instantly.
            if (!controlsRemoved && elapsed >= controlDelay)
            {
                playerCar.canControl = false;
                controlsRemoved = true;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            float remaining = 1f - Mathf.SmoothStep(0f, 1f, t);
            float ratio = previousRemaining > 0.0001f ? remaining / previousRemaining : 0f;
            previousRemaining = remaining;

            if (playerRigidbody != null)
            {
                // Scale the CURRENT velocity so steering/coasting still looks natural.
                // Vertical speed is untouched so gravity and bumps stay normal.
                Vector3 v = playerRigidbody.velocity;
                playerRigidbody.velocity = new Vector3(v.x * ratio, v.y, v.z * ratio);
                playerRigidbody.angularVelocity *= ratio;
            }
        }

        if (!controlsRemoved)
            playerCar.canControl = false;

        if (playerRigidbody != null)
        {
            Vector3 v = playerRigidbody.velocity;
            playerRigidbody.velocity = new Vector3(0f, v.y, 0f);
            playerRigidbody.angularVelocity = Vector3.zero;
        }
    }

    // =========================================================
    // DISABLE PASSENGER EFFECTS
    // =========================================================

    private void DisableAllPassengerEffects()
    {
        if (playerCar == null)
            return;

        GhostCarEffect ghost =
            playerCar.GetComponentInChildren<GhostCarEffect>(true);

        if (ghost != null)
            ghost.DeactivateGhostEffect();

        GhostOrbit[] orbitGhosts =
            playerCar.GetComponentsInChildren<GhostOrbit>(true);

        foreach (GhostOrbit orbit in orbitGhosts)
        {
            if (orbit != null)
                Destroy(orbit.gameObject);
        }

        WitchCarEffect witch =
            playerCar.GetComponentInChildren<WitchCarEffect>(true);

        if (witch != null)
            witch.DeactivateWitchEffect();

        VampireTeleportEffect vampire =
            playerCar.GetComponentInChildren<VampireTeleportEffect>(true);

        if (vampire != null)
            vampire.DeactivateVampireEffect();

        SkeletonCarEffect skeleton =
            playerCar.GetComponentInChildren<SkeletonCarEffect>(true);

        if (skeleton != null)
            skeleton.DeactivateSkeletonEffect();
    }

    // =========================================================
    // DROP PASSENGER
    // =========================================================

    private IEnumerator DropPassenger()
    {
        // Slow the car down smoothly before the passenger is dropped.
        if (stopPlayerDuringDrop)
            yield return SmoothStopPlayer();

        GameObject prefab = FindPrefabContaining(passengerNameToDrop);

        if (prefab == null)
            prefab = FindFirstValidPrefab();

        if (prefab == null)
        {
            Debug.LogWarning(
                "GhostDropTrigger: No valid passenger prefab assigned.",
                this);

            yield return ResumeControls();
            yield break;
        }

        Transform spawnPoint = FindPlayerCarSpawnPoint();

        Vector3 spawnPosition = spawnPoint != null
            ? spawnPoint.position
            : fallbackSpawnPoint != null
                ? fallbackSpawnPoint.position
                : playerCar.transform.position;

        Quaternion baseRotation = useSpawnPointRotation
            ? (spawnPoint != null
                ? spawnPoint.rotation
                : fallbackSpawnPoint != null
                    ? fallbackSpawnPoint.rotation
                    : Quaternion.identity)
            : Quaternion.identity;

        Quaternion initialRotation =
            baseRotation * Quaternion.Euler(spawnRotationOffset);

        GameObject spawned = Instantiate(
            prefab,
            spawnPosition,
            initialRotation);

        Vector3 direction = movementDirection.sqrMagnitude > 0.0001f
            ? movementDirection.normalized
            : Vector3.back;

        if (keepUpright)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.back;

            direction.Normalize();
        }

        // IMPORTANT:
        // If this prefab has WitchMovement, that script handles
        // movement, walking animation and ground detection.
        WitchMovement witchMovement =
            spawned.GetComponentInChildren<WitchMovement>(true);

        if (witchMovement != null)
        {
            witchMovement.StartWalkingAway(
                direction,
                movementDistance,
                movementSpeed);

            while (spawned != null &&
                   witchMovement != null &&
                   witchMovement.IsMoving)
            {
                yield return null;
            }
        }
        else
        {
            // Original movement for other passenger prefabs.
            yield return MovePassengerNormally(
                spawned,
                direction,
                initialRotation);
        }

        if (spawned != null)
            Destroy(spawned);

        if (finishNotificationDelay > 0f)
            yield return new WaitForSeconds(finishNotificationDelay);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PassengerDropped();
        }
        else
        {
            Debug.LogWarning(
                "GhostDropTrigger: GameManager instance not found.",
                this);
        }

        yield return ResumeControls();
    }

    // =========================================================
    // NORMAL MOVEMENT FOR NON-WITCH PASSENGERS
    // =========================================================

    private IEnumerator MovePassengerNormally(
        GameObject spawned,
        Vector3 direction,
        Quaternion initialRotation)
    {
        if (spawned == null)
            yield break;

        Vector3 targetPosition =
            spawned.transform.position +
            direction * Mathf.Max(0f, movementDistance);

        Quaternion targetRotation = initialRotation;
        bool hasTargetRotation = false;

        if (rotateToFaceDirection &&
            direction.sqrMagnitude > 0.0001f)
        {
            Vector3 lookDirection = direction;

            if (keepUpright)
                lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                targetRotation =
                    Quaternion.LookRotation(
                        lookDirection.normalized,
                        Vector3.up) *
                    Quaternion.Euler(movementRotationOffset);

                hasTargetRotation = true;
            }
        }

        float speed = Mathf.Max(0.01f, movementSpeed);

        while (spawned != null &&
               Vector3.Distance(
                   spawned.transform.position,
                   targetPosition) > 0.05f)
        {
            spawned.transform.position = Vector3.MoveTowards(
                spawned.transform.position,
                targetPosition,
                speed * Time.deltaTime);

            if (hasTargetRotation)
            {
                spawned.transform.rotation =
                    rotationSpeed <= 0f
                        ? targetRotation
                        : Quaternion.RotateTowards(
                            spawned.transform.rotation,
                            targetRotation,
                            rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        if (spawned != null)
        {
            spawned.transform.position = targetPosition;

            if (hasTargetRotation)
                spawned.transform.rotation = targetRotation;
        }
    }

    // =========================================================
    // FIND PASSENGER PREFAB
    // =========================================================

    private GameObject FindPrefabContaining(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return null;

        foreach (PassengerPrefabEntry entry in passengerPrefabs)
        {
            if (entry == null || entry.prefab == null)
                continue;

            string entryName = string.IsNullOrWhiteSpace(entry.passengerName)
                ? entry.prefab.name
                : entry.passengerName;

            if (entryName.IndexOf(
                    searchText,
                    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.prefab.name.IndexOf(
                    searchText,
                    System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return entry.prefab;
            }
        }

        return null;
    }

    private GameObject FindFirstValidPrefab()
    {
        foreach (PassengerPrefabEntry entry in passengerPrefabs)
        {
            if (entry != null && entry.prefab != null)
                return entry.prefab;
        }

        return null;
    }

    // =========================================================
    // FIND SPAWN POINT
    // =========================================================

    private Transform FindPlayerCarSpawnPoint()
    {
        if (playerCar == null)
            return null;

        Transform[] children =
            playerCar.GetComponentsInChildren<Transform>(true);

        if (!string.IsNullOrWhiteSpace(spawnPointName))
        {
            foreach (Transform child in children)
            {
                if (child.name.Equals(
                    spawnPointName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(spawnPointTag))
        {
            foreach (Transform child in children)
            {
                if (child.CompareTag(spawnPointTag))
                    return child;
            }
        }

        return null;
    }

    // =========================================================
    // RESUME PLAYER CONTROL
    // =========================================================

    private IEnumerator ResumeControls()
    {
        if (controlResumeDelay > 0f)
            yield return new WaitForSeconds(controlResumeDelay);

        if (playerCar != null)
            playerCar.canControl = originalCanControl;
    }

    private void OnDisable()
    {
        if (dropStarted && playerCar != null)
            playerCar.canControl = originalCanControl;
    }
}