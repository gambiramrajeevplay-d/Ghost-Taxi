using UnityEngine;

public class GhostCarEffect : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    [Header("Orbiting Ghosts")]
    [Tooltip("The ghost PREFAB (project asset) to spawn.")]
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private int ghostCount = 4;
    [SerializeField] private float orbitRadius = 2.5f;
    [SerializeField] private float orbitHeight = 1.5f;
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Orbit Behaviour")]
    [Tooltip("If on, ghosts only orbit while the car is moving.")]
    [SerializeField] private bool orbitOnlyWhenMoving = false;
    [SerializeField] private float movingSpeedThreshold = 0.5f;
    [SerializeField] private float orbitBlendSpeed = 3f;

    [Header("Ghost Rotation")]
    [SerializeField] private bool faceCenter = true;

    [Header("Ghost Animation")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float swayAmount = 6f;
    [SerializeField] private float swaySpeed = 1.5f;

    private Transform player;
    private Rigidbody playerRigidbody;

    private GameObject[] ghosts;
    private float[] phaseOffsets;

    private float orbitAngle;
    private float orbitSpeedFactor = 1f;
    private bool effectActive = false;

    private void Update()
    {
        if (!effectActive || ghosts == null || ghosts.Length == 0)
            return;

        float targetFactor = 1f;

        if (orbitOnlyWhenMoving && playerRigidbody != null)
        {
            targetFactor =
                playerRigidbody.velocity.magnitude > movingSpeedThreshold ? 1f : 0f;
        }

        orbitSpeedFactor = Mathf.MoveTowards(
            orbitSpeedFactor, targetFactor, orbitBlendSpeed * Time.deltaTime);

        orbitAngle += rotationSpeed * orbitSpeedFactor * Time.deltaTime;

        float angleStep = 360f / ghosts.Length;

        for (int i = 0; i < ghosts.Length; i++)
        {
            if (ghosts[i] == null)
                continue;

            float radians = (angleStep * i + orbitAngle) * Mathf.Deg2Rad;

            Vector3 flat = new Vector3(
                Mathf.Cos(radians) * orbitRadius,
                0f,
                Mathf.Sin(radians) * orbitRadius);

            float bob =
                Mathf.Sin(Time.time * floatSpeed + phaseOffsets[i]) * floatHeight;

            Transform g = ghosts[i].transform;
            g.localPosition = flat + Vector3.up * (orbitHeight + bob);

            Quaternion rot = Quaternion.identity;

            if (faceCenter && flat.sqrMagnitude > 0.001f)
                rot = Quaternion.LookRotation(-flat);

            if (enableSway)
            {
                float sway = Mathf.Sin(
                    Time.time * floatSpeed * swaySpeed + phaseOffsets[i]) * swayAmount;

                rot *= Quaternion.Euler(0f, 0f, sway);
            }

            g.localRotation = rot;
        }
    }

    public void ActivateGhostEffect()
    {
        if (effectActive)
            return;

        if (ghostPrefab == null)
        {
            Debug.LogWarning("GhostCarEffect: Ghost Prefab is NOT assigned.", this);
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject == null)
        {
            Debug.LogWarning(
                "GhostCarEffect: No object with tag '" + playerTag + "' found.", this);
            return;
        }

        player = playerObject.transform;
        playerRigidbody = playerObject.GetComponentInParent<Rigidbody>();

        effectActive = true;
        orbitAngle = 0f;
        orbitSpeedFactor = orbitOnlyWhenMoving ? 0f : 1f;

        SpawnGhosts();
    }

    private void SpawnGhosts()
    {
        if (ghostCount <= 0)
            return;

        ghosts = new GameObject[ghostCount];
        phaseOffsets = new float[ghostCount];

        for (int i = 0; i < ghostCount; i++)
        {
            GameObject g = Instantiate(ghostPrefab, player);
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.SetActive(true);

            GhostMovement movement = g.GetComponent<GhostMovement>();
            if (movement != null)
                movement.enabled = false;

            foreach (Collider c in g.GetComponentsInChildren<Collider>())
                c.enabled = false;

            foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;

            ghosts[i] = g;
            phaseOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    /// <summary>
    /// Stops the ghost effect and destroys ALL ghost clones: the ones this script
    /// spawned and any GhostOrbit clones spawned by GhostMovement.
    /// </summary>
    public void DeactivateGhostEffect()
    {
        effectActive = false;

        DestroyGhosts();
        DestroyOrbitGhosts();
    }

    public bool IsActive()
    {
        return effectActive;
    }

    private void DestroyGhosts()
    {
        if (ghosts == null)
            return;

        for (int i = 0; i < ghosts.Length; i++)
        {
            if (ghosts[i] != null)
                Destroy(ghosts[i]);
        }

        ghosts = null;
        phaseOffsets = null;
    }

    // Removes clones created by GhostMovement.SpawnOrbitGhosts (they carry GhostOrbit).
    private void DestroyOrbitGhosts()
    {
        Transform root = player;

        if (root == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject != null)
                root = playerObject.transform;
        }

        if (root == null)
            root = transform.root;

        GhostOrbit[] orbits = root.GetComponentsInChildren<GhostOrbit>(true);

        for (int i = 0; i < orbits.Length; i++)
        {
            if (orbits[i] != null)
                Destroy(orbits[i].gameObject);
        }
    }

    private void OnDestroy()
    {
        DestroyGhosts();
    }
}