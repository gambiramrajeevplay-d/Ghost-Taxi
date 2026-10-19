
using UnityEngine;

public class GhostCarEffect : MonoBehaviour
{
    private const int GHOST_COUNT = 4;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Ghosts")]
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private float orbitRadius = 2.5f;
    [SerializeField] private float orbitHeight = 1.5f;
    [SerializeField] private float rotationSpeed = 35f;

    [Header("Orbit Behaviour")]
    [SerializeField] private bool orbitOnlyWhenMoving = false;
    [SerializeField] private float movingSpeedThreshold = 0.5f;
    [SerializeField] private float orbitBlendSpeed = 3f;

    [Header("Ghost Rotation")]
    [SerializeField] private bool faceCenter = false;
    [SerializeField] private Vector3 ghostRotationOffset = Vector3.zero;

    [Header("Ghost Animation")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float swayAmount = 4f;
    [SerializeField] private float swaySpeed = 1.5f;

    [Header("Ghost Audio")]
    [SerializeField] private AudioClip ghostSound;
    [SerializeField] private float soundInterval = 10f;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [SerializeField] private bool playSoundImmediately = true;

    private static GhostCarEffect activeEffect;

    private AudioSource ghostAudioSource;
    private float nextSoundTime;

    private Transform player;
    private Rigidbody playerRigidbody;

    private GameObject[] ghosts;
    private float[] phaseOffsets;
    private Quaternion[] initialRotations;

    private float orbitAngle;
    private float orbitSpeedFactor = 1f;
    private bool effectActive;

    private void Update()
    {
        if (!effectActive || player == null || ghosts == null)
            return;

        UpdateGhostAudio();

        float targetFactor = 1f;

        if (orbitOnlyWhenMoving)
        {
            float speed = playerRigidbody != null
                ? playerRigidbody.velocity.magnitude
                : 0f;

            targetFactor =
                speed > movingSpeedThreshold ? 1f : 0f;
        }

        orbitSpeedFactor = Mathf.MoveTowards(
            orbitSpeedFactor,
            targetFactor,
            orbitBlendSpeed * Time.deltaTime
        );

        orbitAngle +=
            rotationSpeed * orbitSpeedFactor * Time.deltaTime;

        float angleStep = 360f / GHOST_COUNT;

        for (int i = 0; i < GHOST_COUNT; i++)
        {
            if (ghosts[i] == null)
                continue;

            float radians =
                (angleStep * i + orbitAngle) * Mathf.Deg2Rad;

            // Orbit around the car using its local XZ plane.
            Vector3 flatOffset = new Vector3(
                Mathf.Cos(radians) * orbitRadius,
                0f,
                Mathf.Sin(radians) * orbitRadius
            );

            float bob = Mathf.Sin(
                Time.time * floatSpeed + phaseOffsets[i]
            ) * floatHeight;

            Transform ghostTransform = ghosts[i].transform;

            ghostTransform.localPosition =
                flatOffset + Vector3.up * (orbitHeight + bob);

            Quaternion rotation = initialRotations[i];

            if (faceCenter)
            {
                Vector3 direction = -flatOffset;
                direction.y = 0f;

                if (direction.sqrMagnitude > 0.001f)
                {
                    rotation = Quaternion.LookRotation(
                        direction.normalized,
                        Vector3.up
                    ) * Quaternion.Euler(ghostRotationOffset);
                }
            }

            if (enableSway)
            {
                float sway = Mathf.Sin(
                    Time.time * floatSpeed * swaySpeed
                    + phaseOffsets[i]
                ) * swayAmount;

                rotation *= Quaternion.Euler(0f, 0f, sway);
            }

            ghostTransform.localRotation = rotation;
        }
    }

    public void ActivateGhostEffect()
    {
        // Prevent duplicate effects from multiple instances.
        if (activeEffect != null && activeEffect != this)
        {
            if (activeEffect.effectActive)
                return;

            activeEffect = null;
        }

        if (effectActive)
            return;

        if (ghostPrefab == null)
        {
            Debug.LogWarning(
                "GhostCarEffect: Assign the ghost prefab.",
                this
            );
            return;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject == null)
        {
            Debug.LogWarning(
                "GhostCarEffect: Player tag not found.",
                this
            );
            return;
        }

        activeEffect = this;
        player = playerObject.transform;

        playerRigidbody =
            playerObject.GetComponentInParent<Rigidbody>();

        if (playerRigidbody == null)
            playerRigidbody = playerObject.GetComponent<Rigidbody>();

        // Remove ghosts left by a previous activation of this
        // system before creating the new set.
        RemovePreviousGhosts();

        effectActive = true;
        orbitAngle = 0f;
        orbitSpeedFactor = orbitOnlyWhenMoving ? 0f : 1f;

        SpawnGhosts();
        SetupAudioSource();

        nextSoundTime = Time.time +
            (playSoundImmediately
                ? 0f
                : Mathf.Max(0.1f, soundInterval));

        if (playSoundImmediately)
            PlayGhostSound();
    }

    private void SpawnGhosts()
    {
        ghosts = new GameObject[GHOST_COUNT];
        phaseOffsets = new float[GHOST_COUNT];
        initialRotations = new Quaternion[GHOST_COUNT];

        float angleStep = 360f / GHOST_COUNT;

        for (int i = 0; i < GHOST_COUNT; i++)
        {
            // Parent each ghost to the player so it follows
            // the car automatically.
            GameObject ghost = Instantiate(ghostPrefab, player);

            ghost.name = "OrbitGhost_" + i;

            GhostMovement movement =
                ghost.GetComponent<GhostMovement>();

            if (movement != null)
                movement.enabled = false;

            // Disable colliders and physics on the visual ghosts.
            foreach (Collider col in
                     ghost.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            foreach (Rigidbody rb in
                     ghost.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            phaseOffsets[i] =
                Random.Range(0f, Mathf.PI * 2f);

            initialRotations[i] =
                Quaternion.Euler(ghostRotationOffset);

            float radians = angleStep * i * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(radians) * orbitRadius,
                orbitHeight,
                Mathf.Sin(radians) * orbitRadius
            );

            ghost.transform.localPosition = offset;
            ghost.transform.localRotation = initialRotations[i];

            ghosts[i] = ghost;
            ghost.SetActive(true);
        }
    }

    private void RemovePreviousGhosts()
    {
        // Only remove ghosts belonging to this effect.
        // Do not delete unrelated GhostOrbit objects.
        Transform[] children =
            player.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == player)
                continue;

            if (child.name.StartsWith("OrbitGhost_"))
                Destroy(child.gameObject);
        }
    }

    private void SetupAudioSource()
    {
        if (player == null || ghostAudioSource != null)
            return;

        GameObject audioObject =
            new GameObject("GhostEffectAudio");

        audioObject.transform.SetParent(player, false);

        ghostAudioSource =
            audioObject.AddComponent<AudioSource>();

        ghostAudioSource.playOnAwake = false;
        ghostAudioSource.loop = false;
        ghostAudioSource.spatialBlend = 0f;
        ghostAudioSource.volume = soundVolume;
    }

    private void UpdateGhostAudio()
    {
        if (ghostSound == null || ghostAudioSource == null)
            return;

        ghostAudioSource.volume = soundVolume;

        if (Time.time >= nextSoundTime)
        {
            PlayGhostSound();

            nextSoundTime = Time.time +
                Mathf.Max(0.1f, soundInterval);
        }
    }

    private void PlayGhostSound()
    {
        if (ghostSound == null)
            return;

        if (ghostAudioSource == null)
            SetupAudioSource();

        if (ghostAudioSource != null)
            ghostAudioSource.PlayOneShot(ghostSound, soundVolume);
    }

    public void DeactivateGhostEffect()
    {
        effectActive = false;

        DestroyGhosts();

        if (ghostAudioSource != null)
        {
            ghostAudioSource.Stop();
            Destroy(ghostAudioSource.gameObject);
            ghostAudioSource = null;
        }

        nextSoundTime = 0f;
        playerRigidbody = null;
        player = null;

        if (activeEffect == this)
            activeEffect = null;
    }

    public bool IsActive()
    {
        return effectActive;
    }

    private void DestroyGhosts()
    {
        if (ghosts != null)
        {
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] != null)
                    Destroy(ghosts[i]);
            }
        }

        ghosts = null;
        phaseOffsets = null;
        initialRotations = null;
    }

    private void OnDestroy()
    {
        DestroyGhosts();

        if (ghostAudioSource != null)
            Destroy(ghostAudioSource.gameObject);

        if (activeEffect == this)
            activeEffect = null;
    }
}
