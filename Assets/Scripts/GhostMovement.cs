using System;
using UnityEngine;

public class GhostMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float arriveDistance = 0.3f;
    [SerializeField] private float turnSpeed = 8f;

    [Header("Target")]
    [Tooltip("Offset from the car's pivot where the ghost enters.")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Tooltip("Ghost waits this long after entering the car, then disables.")]
    [SerializeField] private float disableDelay = 1f;

    [Header("Ghost Animation")]
    [SerializeField] private float floatHeight = 0.25f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private bool enableSway = true;
    [SerializeField] private float swayAmount = 6f;
    [SerializeField] private float swaySpeed = 1.5f;

    [Header("Spawn After Disable")]
    [Tooltip("Ghost PREFAB (project asset) spawned as children of the Player.")]
    [SerializeField] private GameObject orbitGhostPrefab;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private int orbitGhostCount = 4;
    [SerializeField] private float orbitRadius = 2.5f;
    [SerializeField] private float orbitHeight = 1.5f;
    [SerializeField] private float orbitSpeed = 90f;
    [SerializeField] private bool faceCenter = true;

    private enum State { Idle, Moving, Waiting }
    private State state = State.Idle;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private float startY;
    private float animationTime;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float waitTimer;

    private Transform target;
    private Action onFinished;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        startY = initialLocalPosition.y;
        animationTime = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        animationTime += Time.deltaTime * floatSpeed;

        float bob = Mathf.Sin(animationTime) * floatHeight;
        float sway = enableSway
            ? Mathf.Sin(animationTime * swaySpeed) * swayAmount
            : 0f;

        if (state == State.Idle)
        {
            Vector3 pos = transform.localPosition;
            pos.y = startY + bob;
            transform.localPosition = pos;
            transform.localRotation =
                initialLocalRotation * Quaternion.Euler(0f, 0f, sway);
            return;
        }

        if (target == null)
        {
            state = State.Idle;
            return;
        }

        Vector3 targetPos = target.position + targetOffset;

        if (state == State.Moving)
        {
            basePosition = Vector3.MoveTowards(
                basePosition, targetPos, moveSpeed * Time.deltaTime);

            Vector3 dir = targetPos - basePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                baseRotation = Quaternion.Slerp(
                    baseRotation,
                    Quaternion.LookRotation(dir),
                    turnSpeed * Time.deltaTime);
            }

            if (Vector3.Distance(basePosition, targetPos) <= arriveDistance)
                EnterCar();
        }
        else // Waiting inside the car
        {
            basePosition = targetPos; // stick to the car
            waitTimer += Time.deltaTime;

            if (waitTimer >= disableDelay)
            {
                Finish();
                return;
            }
        }

        transform.position = basePosition + Vector3.up * bob;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, sway);
    }

    /// <summary>Start flying toward the car. onFinished runs after the wait + disable.</summary>
    public void StartMovingToCar(Transform car, Action finishedCallback)
    {
        if (state != State.Idle || car == null)
            return;

        target = car;
        onFinished = finishedCallback;

        basePosition = transform.position;
        baseRotation = transform.rotation;
        waitTimer = 0f;

        state = State.Moving;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (state != State.Moving || target == null)
            return;

        if (other.transform == target || other.transform.IsChildOf(target))
            EnterCar();
    }

    private void EnterCar()
    {
        if (state != State.Moving)
            return;

        state = State.Waiting;
        waitTimer = 0f;
    }

    private void Finish()
    {
        state = State.Idle;

        // Spawn the orbiting ghosts first, then disable this passenger.
        SpawnOrbitGhosts();

        gameObject.SetActive(false);

        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();
    }

    private void SpawnOrbitGhosts()
    {
        if (orbitGhostPrefab == null)
        {
            Debug.LogWarning("GhostMovement: Orbit Ghost Prefab is NOT assigned.", this);
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject == null)
        {
            Debug.LogWarning(
                "GhostMovement: No object with tag '" + playerTag + "' found.", this);
            return;
        }

        Transform player = playerObject.transform;

        for (int i = 0; i < orbitGhostCount; i++)
        {
            GameObject g = Instantiate(orbitGhostPrefab, player);
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.SetActive(true);

            // The spawned copy must not run the pickup logic.
            GhostMovement movement = g.GetComponent<GhostMovement>();
            if (movement != null)
                movement.enabled = false;

            // Don't let the ghosts affect the car's physics.
            foreach (Collider c in g.GetComponentsInChildren<Collider>())
                c.enabled = false;

            foreach (Rigidbody rb in g.GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;

            GhostOrbit orbit = g.AddComponent<GhostOrbit>();
            orbit.Init(
                i, orbitGhostCount,
                orbitRadius, orbitHeight, orbitSpeed,
                faceCenter,
                floatHeight, floatSpeed,
                enableSway, swayAmount, swaySpeed);
        }
    }
}