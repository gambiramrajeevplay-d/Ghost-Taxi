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
    [Tooltip("Add the ghost, witch, or other passenger prefabs here.")]
    [SerializeField]
    private List<PassengerPrefabEntry> passengerPrefabs =
        new List<PassengerPrefabEntry>();

    [Tooltip("Optional fallback spawn point. The script first searches the player car for a matching spawn point.")]
    [SerializeField] private Transform fallbackSpawnPoint;

    [Header("Player Car Spawn Point Search")]
    [Tooltip("Name of the Transform on the player car used to spawn the passenger.")]
    [SerializeField] private string spawnPointName = "PassengerSpawnPoint";

    [Tooltip("Optional tag used to find the passenger spawn point on the player car. Leave empty to skip tag search.")]
    [SerializeField] private string spawnPointTag = "";

    [Header("Passenger Movement")]
    [Tooltip("World-space direction. Default is world -Z.")]
    [SerializeField] private Vector3 movementDirection = new Vector3(0f, 0f, -1f);

    [SerializeField] private float movementDistance = 10f;
    [SerializeField] private float movementSpeed = 2f;

    [Header("Passenger Rotation")]
    [Tooltip("Rotate the passenger so it faces the direction it moves.")]
    [SerializeField] private bool rotateToFaceDirection = true;

    [Tooltip("Keep the passenger upright by ignoring the vertical part of the movement direction.")]
    [SerializeField] private bool keepUpright = true;

    [Tooltip("Extra rotation (Euler angles) applied after facing the direction. Use this if the model's forward axis is not +Z, e.g. (0, 180, 0).")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero;

    [Tooltip("Turn speed in degrees per second. Set to 0 to snap instantly.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Header("Player Control")]
    [SerializeField] private bool stopPlayerDuringDrop = true;
    [SerializeField] private float controlResumeDelay = 0.25f;

    [Header("Passenger Effects")]
    [Tooltip("Disable ghost, witch, vampire and skeleton effects when the player enters this trigger.")]
    [SerializeField] private bool disableAllEffectsOnDrop = true;

    private RCC_CarControllerV3 playerCar;
    private bool dropStarted;

    private void OnTriggerEnter(Collider other)
    {
        if (dropStarted)
            return;

        RCC_CarControllerV3 car =
            other.GetComponentInParent<RCC_CarControllerV3>();

        if (car == null)
            return;

        playerCar = car;
        dropStarted = true;

        // Disable effects FIRST. Some of them (e.g. vampire) set canControl = true
        // when they deactivate, which would undo the control lock below.
        if (disableAllEffectsOnDrop)
            DisableAllPassengerEffects();

        if (stopPlayerDuringDrop)
        {
            playerCar.canControl = false;

            Rigidbody rb = playerCar.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        StartCoroutine(DropPassenger());
    }

    private void DisableAllPassengerEffects()
    {
        if (playerCar == null)
            return;

        // Ghost: removes the orbiting ghost clones.
        GhostCarEffect ghost = playerCar.GetComponentInChildren<GhostCarEffect>(true);
        if (ghost != null)
            ghost.DeactivateGhostEffect();

        // Fallback: remove any orbit ghosts spawned by GhostMovement.
        GhostOrbit[] orbitGhosts = playerCar.GetComponentsInChildren<GhostOrbit>(true);
        for (int i = 0; i < orbitGhosts.Length; i++)
        {
            if (orbitGhosts[i] != null)
                Destroy(orbitGhosts[i].gameObject);
        }

        // Witch: stops scaling and restores the car size.
        WitchCarEffect witch = playerCar.GetComponentInChildren<WitchCarEffect>(true);
        if (witch != null)
            witch.DeactivateWitchEffect();

        // Vampire: stops teleporting and restores the visual.
        VampireTeleportEffect vampire = playerCar.GetComponentInChildren<VampireTeleportEffect>(true);
        if (vampire != null)
            vampire.DeactivateVampireEffect();

        // Skeleton: stops bone spawning and cleans up spawned bones.
        SkeletonCarEffect skeleton = playerCar.GetComponentInChildren<SkeletonCarEffect>(true);
        if (skeleton != null)
            skeleton.DeactivateSkeletonEffect();
    }

    private IEnumerator DropPassenger()
    {
        if (passengerPrefabs == null || passengerPrefabs.Count == 0)
        {
            Debug.LogWarning(
                "GhostDropTrigger: Add at least one passenger prefab to the list.",
                this);

            yield return ResumeControls();
            yield break;
        }

        // Find a ghost first; if no ghost is configured, use the first valid entry.
        GameObject selectedPrefab = FindPrefabContaining("ghost");

        if (selectedPrefab == null)
            selectedPrefab = FindFirstValidPrefab();

        if (selectedPrefab == null)
        {
            Debug.LogWarning(
                "GhostDropTrigger: No valid passenger prefab is assigned.",
                this);

            yield return ResumeControls();
            yield break;
        }

        Transform carSpawnPoint = FindPlayerCarSpawnPoint();
        Vector3 spawnPosition = carSpawnPoint != null
            ? carSpawnPoint.position
            : (fallbackSpawnPoint != null ? fallbackSpawnPoint.position : playerCar.transform.position);

        Quaternion spawnRotation = carSpawnPoint != null
            ? carSpawnPoint.rotation
            : (fallbackSpawnPoint != null ? fallbackSpawnPoint.rotation : playerCar.transform.rotation);

        GameObject spawned = Instantiate(
            selectedPrefab,
            spawnPosition,
            spawnRotation);

        Vector3 direction = movementDirection.sqrMagnitude > 0.0001f
            ? movementDirection.normalized
            : Vector3.back;

        Vector3 target = spawned.transform.position +
                         direction * Mathf.Max(0f, movementDistance);

        float speed = Mathf.Max(0.01f, movementSpeed);

        // Work out the rotation the passenger should end up with.
        Quaternion targetRotation = spawned.transform.rotation;
        bool hasTargetRotation = false;

        if (rotateToFaceDirection)
        {
            Vector3 lookDirection = direction;

            if (keepUpright)
                lookDirection.y = 0f;

            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                targetRotation =
                    Quaternion.LookRotation(lookDirection.normalized, Vector3.up) *
                    Quaternion.Euler(rotationOffset);

                hasTargetRotation = true;

                // Instant snap if no turn speed is set.
                if (rotationSpeed <= 0f)
                    spawned.transform.rotation = targetRotation;
            }
        }

        // Move the spawned passenger away from the car along the configured direction.
        while (spawned != null &&
               Vector3.Distance(spawned.transform.position, target) > 0.05f)
        {
            spawned.transform.position = Vector3.MoveTowards(
                spawned.transform.position,
                target,
                speed * Time.deltaTime);

            if (hasTargetRotation && rotationSpeed > 0f)
            {
                spawned.transform.rotation = Quaternion.RotateTowards(
                    spawned.transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        if (spawned != null)
            Destroy(spawned);

        yield return ResumeControls();
    }

    private GameObject FindPrefabContaining(string searchText)
    {
        foreach (PassengerPrefabEntry entry in passengerPrefabs)
        {
            if (entry == null || entry.prefab == null)
                continue;

            string entryName = string.IsNullOrWhiteSpace(entry.passengerName)
                ? entry.prefab.name
                : entry.passengerName;

            if (entryName.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                entry.prefab.name.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0)
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

    private Transform FindPlayerCarSpawnPoint()
    {
        if (playerCar == null)
            return null;

        if (!string.IsNullOrWhiteSpace(spawnPointName))
        {
            Transform[] children = playerCar.GetComponentsInChildren<Transform>(true);

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
            Transform[] children = playerCar.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child.CompareTag(spawnPointTag))
                    return child;
            }
        }

        return null;
    }

    private IEnumerator ResumeControls()
    {
        if (controlResumeDelay > 0f)
            yield return new WaitForSeconds(controlResumeDelay);

        if (playerCar != null)
            playerCar.canControl = true;
    }

    private void OnDisable()
    {
        if (dropStarted && playerCar != null)
            playerCar.canControl = true;
    }
}