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
    [Tooltip("Total time of the anticipation (squash, then pop bigger).")]
    [SerializeField] private float squashExpandDuration = 0.18f;

    [Tooltip("How much to enlarge before the drop. 1.2 means 120% of original scale.")]
    [SerializeField] private float squashExpandMultiplier = 1.2f;

    [Tooltip("Compress the visual vertically during anticipation.")]
    [SerializeField] private float squashYMultiplier = 0.72f;

    [Tooltip("Stretch the visual horizontally during anticipation.")]
    [SerializeField] private float squashXZMultiplier = 1.12f;

    [Tooltip("Time to drop from the enlarged pose down to zero.")]
    [SerializeField] private float shrinkDuration = 0.35f;

    [Tooltip("Short pause while invisible, right at the teleport moment. Hides any pop-in.")]
    [SerializeField] private float hiddenHoldDuration = 0.06f;

    [Tooltip("Time to grow back after teleporting. Controls are already restored during this animation.")]
    [SerializeField] private float growDuration = 0.45f;

    [Tooltip("Strength of the bouncy overshoot while growing back. 0 = no overshoot, 1.7 = default bounce.")]
    [SerializeField] private float growBounce = 1.4f;

    [Tooltip("Strength of the wind-up on the shrink. 0 = none, 1 = subtle.")]
    [SerializeField] private float shrinkWindUp = 0.8f;

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

    // Never scale to exact zero: it can cause matrix / normal warnings.
    private const float MinScale = 0.0001f;

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

    // Rigidbody state saved while the effect freezes the car.
    private bool carFrozen;
    private bool savedKinematic;
    private RigidbodyInterpolation savedInterpolation;

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

        // Freeze once (kinematic) instead of zeroing velocity every frame.
        // This removes physics jitter fighting the squash animation.
        FreezeCar();

        // Squash anticipation: squash, then pop larger.
        yield return StartCoroutine(ExpandVisual());
        if (!effectActive) yield break;

        // Collapse to nothing.
        yield return StartCoroutine(ShrinkToZero());
        if (!effectActive) yield break;

        // Teleport position only. Rigidbody interpolation is disabled by FreezeCar,
        // so the car doesn't smear between the old and new position.
        if (target != null)
        {
            Vector3 destination = target.position;

            if (carRigidbody != null)
            {
                carRigidbody.position = destination;
                carRigidbody.rotation = carRotationBeforeEffect;
            }

            playerCar.transform.SetPositionAndRotation(destination, carRotationBeforeEffect);

            Physics.SyncTransforms();
            SnapCarToGround();
        }
        else
        {
            Debug.LogWarning(
                "VampireTeleportEffect: No history available to teleport to.",
                this);
        }

        ResetVisualRotation();

        // Tiny invisible pause so the car has settled before it reappears.
        if (hiddenHoldDuration > 0f)
            yield return new WaitForSeconds(hiddenHoldDuration);

        if (!effectActive) yield break;

        // Begin recording from the new position.
        history.Clear();
        RecordState();
        nextRecordTime = Time.time + Mathf.Max(0.01f, recordInterval);

        // Give physics back and return controls.
        UnfreezeCar();
        playerCar.canControl = true;
        isTeleporting = false;

        // The player can drive while the model grows back.
        growRoutine = StartCoroutine(Grow());
    }

    // ------------------------------------------------------------------
    // Animation
    // ------------------------------------------------------------------

    private IEnumerator ExpandVisual()
    {
        float duration = Mathf.Max(0.02f, squashExpandDuration);

        if (visualRoot == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 squashScale = new Vector3(
            originalScale.x * Mathf.Max(1f, squashXZMultiplier),
            originalScale.y * Mathf.Clamp(squashYMultiplier, 0.1f, 1f),
            originalScale.z * Mathf.Max(1f, squashXZMultiplier));

        Vector3 expandedScale = originalScale * Mathf.Max(1f, squashExpandMultiplier);

        // Two continuous phases. Each starts where the previous one ended,
        // so there is no visible hitch between them.
        float squashTime = duration * 0.4f;
        float popTime = duration - squashTime;

        yield return AnimateScale(originalScale, squashScale, squashTime, EaseOutCubic);
        if (!effectActive) yield break;

        yield return AnimateScale(squashScale, expandedScale, popTime, EaseOutBack05);
    }

    private IEnumerator ShrinkToZero()
    {
        float duration = Mathf.Max(0.02f, shrinkDuration);

        if (visualRoot == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 expandedScale = originalScale * Mathf.Max(1f, squashExpandMultiplier);
        Vector3 zeroScale = originalScale * MinScale;

        float windUp = Mathf.Max(0f, shrinkWindUp);

        yield return AnimateScale(
            expandedScale,
            zeroScale,
            duration,
            t => EaseInBack(t, windUp));

        if (visualRoot != null)
            visualRoot.localScale = zeroScale;
    }

    private IEnumerator Grow()
    {
        isGrowing = true;

        float duration = Mathf.Max(0.02f, growDuration);

        if (visualRoot != null)
        {
            visualRoot.localRotation = originalVisualRotation;
            Vector3 zeroScale = originalScale * MinScale;
            float bounce = Mathf.Max(0f, growBounce);

            // One smooth ease-out-back: grows, overshoots slightly, settles.
            yield return AnimateScale(
                zeroScale,
                originalScale,
                duration,
                t => EaseOutBack(t, bounce));

            if (effectActive)
                ResetVisual();
        }

        isGrowing = false;
        growRoutine = null;
    }

    /// <summary>
    /// Smoothly animates the visual scale. The easing function may overshoot 0..1.
    /// Uses Vector3.LerpUnclamped so overshoot is not clipped.
    /// </summary>
    private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
    {
        duration = Mathf.Max(0.0001f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!effectActive || visualRoot == null)
                yield break;

            // Clamp dt so a single frame hitch can't skip the animation.
            elapsed += Mathf.Min(Time.deltaTime, 0.05f);
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 s = Vector3.LerpUnclamped(from, to, ease(t));
            s.x = Mathf.Max(MinScale, s.x);
            s.y = Mathf.Max(MinScale, s.y);
            s.z = Mathf.Max(MinScale, s.z);

            visualRoot.localScale = s;
            visualRoot.localRotation = originalVisualRotation;

            yield return null;
        }

        if (effectActive && visualRoot != null)
        {
            Vector3 end = to;
            end.x = Mathf.Max(MinScale, end.x);
            end.y = Mathf.Max(MinScale, end.y);
            end.z = Mathf.Max(MinScale, end.z);
            visualRoot.localScale = end;
        }
    }

    // ------------------------------------------------------------------
    // Easing functions
    // ------------------------------------------------------------------

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private static float EaseOutBack05(float t)
    {
        return EaseOutBack(t, 0.5f);
    }

    private static float EaseOutBack(float t, float strength)
    {
        float c1 = strength;
        float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private static float EaseInBack(float t, float strength)
    {
        float c1 = strength;
        float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    // ------------------------------------------------------------------
    // History
    // ------------------------------------------------------------------

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

    // ------------------------------------------------------------------
    // Ground / physics helpers
    // ------------------------------------------------------------------

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

    /// <summary>Stops the car and makes it kinematic so nothing pushes it during the effect.</summary>
    private void FreezeCar()
    {
        if (carRigidbody == null || carFrozen)
            return;

        savedKinematic = carRigidbody.isKinematic;
        savedInterpolation = carRigidbody.interpolation;

        // Zero velocity BEFORE going kinematic (setting it afterwards logs warnings).
        if (!carRigidbody.isKinematic)
        {
            carRigidbody.velocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        carRigidbody.interpolation = RigidbodyInterpolation.None;
        carRigidbody.isKinematic = true;
        carFrozen = true;
    }

    /// <summary>Restores the rigidbody to how it was before the effect.</summary>
    private void UnfreezeCar()
    {
        if (carRigidbody == null || !carFrozen)
            return;

        carRigidbody.isKinematic = savedKinematic;
        carRigidbody.interpolation = savedInterpolation;

        if (!carRigidbody.isKinematic)
        {
            carRigidbody.velocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        carFrozen = false;
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

        UnfreezeCar();
        ResetVisual();

        if (playerCar != null)
            playerCar.canControl = true;
    }

    public void DeactivateVampireEffect()
    {
        effectActive = false;
        isTeleporting = false;
        isGrowing = false;

        StopAllCoroutines();

        teleportLoopRoutine = null;
        growRoutine = null;

        UnfreezeCar();
        ResetVisual();

        if (playerCar != null)
            playerCar.canControl = true;

        history.Clear();
        TimeUntilNextTeleport = 0f;
    }
}