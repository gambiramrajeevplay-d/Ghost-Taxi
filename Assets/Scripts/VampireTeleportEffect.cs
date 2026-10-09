using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VampireTeleportEffect : MonoBehaviour
{
    [Header("Car References")]
    [Tooltip("Assign the car's visual mesh/model child, NOT the RCC physics root.")]
    [SerializeField] private Transform visualRoot;

    [Header("Wave Delays")]
    [Tooltip("Delay before each wave starts. Example: 5, 7, 10.")]
    [SerializeField] private float[] delays = { 5f, 7f, 10f };

    [Tooltip("How many seconds back the car teleports.")]
    [SerializeField] private float rewindSeconds = 5f;

    [Header("Squash Effect")]
    [Tooltip("Time to quickly enlarge before the sudden squash/drop.")]
    [SerializeField] private float squashExpandDuration = 0.12f;

    [Tooltip("How much to enlarge before the squash. 1.2 means 120% of original scale.")]
    [SerializeField] private float squashExpandMultiplier = 1.2f;

    [Tooltip("Compress the visual vertically during anticipation.")]
    [SerializeField] private float squashYMultiplier = 0.72f;

    [Tooltip("Stretch the visual horizontally during anticipation.")]
    [SerializeField] private float squashXZMultiplier = 1.12f;

    [Tooltip("Time to drop from the squashed pose down to zero.")]
    [SerializeField] private float shrinkDuration = 0.3f;

    [Tooltip("Time to grow back after teleporting. Controls are already restored during this animation.")]
    [SerializeField] private float growDuration = 0.35f;

    [Tooltip("Small overshoot on the grow-back for a bouncy finish.")]
    [SerializeField] private float growOvershoot = 1.12f;

    [Header("Ground")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRayStartHeight = 5f;
    [SerializeField] private float groundRayDistance = 20f;
    [SerializeField] private float groundClearance = 0.05f;

    [Header("History Recording")]
    [SerializeField] private float recordInterval = 0.02f;

    [Header("Debug")]
    [SerializeField] private bool logCountdown = false;

    /// <summary>Seconds remaining in the current countdown.</summary>
    public float TimeUntilNextTeleport { get; private set; }

    private class CarSnapshot
    {
        public float time;
        public Vector3 position;

        public CarSnapshot(float time, Vector3 position)
        {
            this.time = time;
            this.position = position;
        }
    }

    private readonly List<CarSnapshot> history = new List<CarSnapshot>();

    private RCC_CarControllerV3 playerCar;
    private Rigidbody carRigidbody;

    private Vector3 originalScale;
    private Quaternion originalVisualRotation;

    private bool effectActive;
    private bool isTeleporting;
    private bool isGrowing;
    private bool originalCanControl;
    private float nextRecordTime;
    private Coroutine teleportLoopRoutine;
    private Coroutine growRoutine;

    private void Awake()
    {
        playerCar = GetComponent<RCC_CarControllerV3>();

        if (playerCar == null)
            playerCar = GetComponentInParent<RCC_CarControllerV3>();

        if (playerCar != null)
            carRigidbody = playerCar.GetComponent<Rigidbody>();

        if (visualRoot == null && playerCar != null)
        {
            visualRoot = playerCar.transform;
            Debug.LogWarning(
                "VampireTeleportEffect: Assign Visual Root to a mesh/model child, not the RCC physics root. " +
                "Squashing the physics root can affect the vehicle.",
                this);
        }

        SaveOriginalVisual();
    }

    private void SaveOriginalVisual()
    {
        if (visualRoot == null)
            return;

        originalScale = visualRoot.localScale;
        originalVisualRotation = visualRoot.localRotation;
    }

    private void Update()
    {
        if (!effectActive || isTeleporting || playerCar == null)
            return;

        if (Time.time >= nextRecordTime)
        {
            RecordState();
            nextRecordTime = Time.time + Mathf.Max(0.01f, recordInterval);
        }

        TrimHistory();
    }

    public void ActivateVampireEffect()
    {
        if (effectActive)
            return;

        if (playerCar == null)
        {
            Debug.LogWarning("VampireTeleportEffect: Player car was not found.", this);
            return;
        }

        if (delays == null || delays.Length == 0)
        {
            Debug.LogWarning("VampireTeleportEffect: Add at least one delay.", this);
            return;
        }

        effectActive = true;
        isTeleporting = false;
        isGrowing = false;

        SaveOriginalVisual();
        originalCanControl = playerCar.canControl;

        history.Clear();
        RecordState();
        nextRecordTime = Time.time + Mathf.Max(0.01f, recordInterval);

        teleportLoopRoutine = StartCoroutine(TeleportLoop());
    }

    private IEnumerator TeleportLoop()
    {
        int index = 0;

        while (effectActive)
        {
            float countdown = Mathf.Max(0f, delays[index]);
            float timer = 0f;
            int lastLoggedSecond = 0;

            // Player has control during the countdown.
            while (timer < countdown)
            {
                if (!effectActive)
                    yield break;

                timer += Time.deltaTime;
                TimeUntilNextTeleport = Mathf.Max(0f, countdown - timer);

                if (logCountdown)
                {
                    int second = Mathf.FloorToInt(timer);
                    if (second > lastLoggedSecond)
                    {
                        lastLoggedSecond = second;
                        Debug.Log("Teleport countdown: " + second + " / " + countdown);
                    }
                }

                yield return null;
            }

            if (!effectActive)
                yield break;

            yield return StartCoroutine(DoTeleport());

            if (!effectActive)
                yield break;

            // Controls have been returned by DoTeleport().
            // Start the next delay from the list immediately.
            index = (index + 1) % delays.Length;
        }
    }

    private IEnumerator DoTeleport()
    {
        while (isGrowing && effectActive)
            yield return null;

        if (!effectActive || playerCar == null)
            yield break;

        isTeleporting = true;

        // Choose the rewind destination before stopping history recording.
        CarSnapshot target = GetSnapshotAtTime(Time.time - rewindSeconds);

        // Preserve the car's current rotation: only its position will change.
        Quaternion carRotationBeforeEffect = playerCar.transform.rotation;

        playerCar.canControl = false;
        StopCarPhysics();

        // Squash anticipation: enlarge the visual briefly, then collapse it.
        yield return StartCoroutine(ExpandVisual());

        if (!effectActive)
            yield break;

        yield return StartCoroutine(ShrinkToZero());

        if (!effectActive)
            yield break;

        // Teleport position only. Do not apply any recorded rotation.
        if (target != null)
        {
            Vector3 destination = target.position;

            if (carRigidbody != null)
                carRigidbody.position = destination;
            else
                playerCar.transform.position = destination;

            // Ensure the vehicle's original/current rotation is not changed.
            if (carRigidbody != null)
                carRigidbody.rotation = carRotationBeforeEffect;
            else
                playerCar.transform.rotation = carRotationBeforeEffect;

            Physics.SyncTransforms();
            SnapCarToGround();
            StopCarPhysics();
        }
        else
        {
            Debug.LogWarning(
                "VampireTeleportEffect: No history available to teleport to.",
                this);
        }

        // Reset only the model's local rotation, never spin the car.
        ResetVisualRotation();

        // Begin recording from the new position. Keeping only this fresh
        // segment prevents history from crossing a teleport.
        history.Clear();
        RecordState();
        nextRecordTime = Time.time + Mathf.Max(0.01f, recordInterval);

        // Force controls back on immediately after the teleport effect.
        // This intentionally uses true instead of originalCanControl.
        playerCar.canControl = true;
        isTeleporting = false;

        // The player can drive while the model grows back.
        growRoutine = StartCoroutine(Grow());
    }

    private IEnumerator ExpandVisual()
    {
        if (visualRoot == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, squashExpandDuration));
            yield break;
        }

        float duration = Mathf.Max(0.01f, squashExpandDuration);
        float elapsed = 0f;

        // A quick anticipation squash: slightly wider and shorter, then pop larger.
        Vector3 squashScale = new Vector3(
            originalScale.x * Mathf.Max(1f, squashXZMultiplier),
            originalScale.y * Mathf.Clamp(squashYMultiplier, 0.1f, 1f),
            originalScale.z * Mathf.Max(1f, squashXZMultiplier));

        Vector3 expandedScale = originalScale * Mathf.Max(1f, squashExpandMultiplier);

        while (elapsed < duration)
        {
            if (!effectActive)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Squash quickly in the first half, then pop outward.
            Vector3 scale;
            if (t < 0.45f)
            {
                float squashT = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                scale = Vector3.Lerp(originalScale, squashScale, squashT);
            }
            else
            {
                float popT = Mathf.SmoothStep(0f, 1f, (t - 0.45f) / 0.55f);
                scale = Vector3.Lerp(squashScale, expandedScale, popT);
            }

            visualRoot.localScale = scale;
            // No visual or car rotation is applied.
            visualRoot.localRotation = originalVisualRotation;

            StopCarPhysics();
            yield return null;
        }

        visualRoot.localScale = expandedScale;
        visualRoot.localRotation = originalVisualRotation;
    }

    private IEnumerator ShrinkToZero()
    {
        float duration = Mathf.Max(0.01f, shrinkDuration);

        if (visualRoot == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float elapsed = 0f;
        Vector3 expandedScale = originalScale * Mathf.Max(1f, squashExpandMultiplier);

        while (elapsed < duration)
        {
            if (!effectActive)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Implode quickly toward zero near the end.
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            visualRoot.localScale = Vector3.Lerp(expandedScale, Vector3.zero, eased);
            visualRoot.localRotation = originalVisualRotation;

            StopCarPhysics();
            yield return null;
        }

        visualRoot.localScale = Vector3.zero;
        visualRoot.localRotation = originalVisualRotation;
    }

    private IEnumerator Grow()
    {
        isGrowing = true;

        float duration = Mathf.Max(0.01f, growDuration);

        if (visualRoot != null)
        {
            visualRoot.localRotation = originalVisualRotation;
            float elapsed = 0f;
            Vector3 overshootScale = originalScale * Mathf.Max(1f, growOvershoot);

            while (elapsed < duration)
            {
                if (!effectActive)
                {
                    isGrowing = false;
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Grow from invisible, briefly overshoot, then settle at original scale.
                if (t < 0.72f)
                {
                    float growT = Mathf.SmoothStep(0f, 1f, t / 0.72f);
                    visualRoot.localScale = Vector3.Lerp(Vector3.zero, overshootScale, growT);
                }
                else
                {
                    float settleT = Mathf.SmoothStep(0f, 1f, (t - 0.72f) / 0.28f);
                    visualRoot.localScale = Vector3.Lerp(overshootScale, originalScale, settleT);
                }

                visualRoot.localRotation = originalVisualRotation;
                yield return null;
            }

            ResetVisual();
        }

        isGrowing = false;
        growRoutine = null;
    }

    private void RecordState()
    {
        if (playerCar == null)
            return;

        history.Add(new CarSnapshot(Time.time, playerCar.transform.position));
    }

    private void TrimHistory()
    {
        float oldestAllowed = Time.time - (Mathf.Max(0f, rewindSeconds) + 1f);

        while (history.Count > 2 && history[1].time < oldestAllowed)
            history.RemoveAt(0);
    }

    private CarSnapshot GetSnapshotAtTime(float targetTime)
    {
        if (history.Count == 0)
            return null;

        if (targetTime <= history[0].time)
            return history[0];

        CarSnapshot previous = history[0];

        for (int i = 1; i < history.Count; i++)
        {
            CarSnapshot next = history[i];

            if (next.time >= targetTime)
            {
                float diff = next.time - previous.time;
                if (diff <= 0.0001f)
                    return previous;

                float t = Mathf.Clamp01((targetTime - previous.time) / diff);
                return new CarSnapshot(
                    targetTime,
                    Vector3.Lerp(previous.position, next.position, t));
            }

            previous = next;
        }

        return history[history.Count - 1];
    }

    private void SnapCarToGround()
    {
        if (playerCar == null)
            return;

        Vector3 origin =
            playerCar.transform.position + Vector3.up * groundRayStartHeight;

        if (!Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            groundRayStartHeight + groundRayDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning(
                "VampireTeleportEffect: Ground not found. Check Ground Layer and ray distance.",
                this);
            return;
        }

        Vector3 pos = playerCar.transform.position;
        pos.y = hit.point.y + GetPivotToBottomDistance() + groundClearance;

        if (carRigidbody != null)
            carRigidbody.position = pos;
        else
            playerCar.transform.position = pos;

        Physics.SyncTransforms();
    }

    private float GetPivotToBottomDistance()
    {
        float lowest = float.PositiveInfinity;

        foreach (Collider col in playerCar.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger)
                continue;

            lowest = Mathf.Min(lowest, col.bounds.min.y);
        }

        if (float.IsPositiveInfinity(lowest))
            return 0f;

        return Mathf.Max(0f, playerCar.transform.position.y - lowest);
    }

    private void StopCarPhysics()
    {
        if (carRigidbody == null)
            return;

        carRigidbody.velocity = Vector3.zero;
        carRigidbody.angularVelocity = Vector3.zero;
    }

    private void ResetVisualRotation()
    {
        if (visualRoot != null)
            visualRoot.localRotation = originalVisualRotation;
    }

    private void ResetVisual()
    {
        if (visualRoot == null)
            return;

        visualRoot.localScale = originalScale;
        visualRoot.localRotation = originalVisualRotation;
    }

    private void OnDisable()
    {
        effectActive = false;
        isTeleporting = false;
        isGrowing = false;

        StopAllCoroutines();
        teleportLoopRoutine = null;
        growRoutine = null;

        ResetVisual();

        if (playerCar != null)
            playerCar.canControl = true;
    }
}
