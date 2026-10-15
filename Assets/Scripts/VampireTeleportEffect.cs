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

    [Tooltip("How much to enlarge before the drop.")]
    [SerializeField] private float squashExpandMultiplier = 1.2f;

    [Tooltip("Compress the visual vertically during anticipation.")]
    [SerializeField] private float squashYMultiplier = 0.72f;

    [Tooltip("Stretch the visual horizontally during anticipation.")]
    [SerializeField] private float squashXZMultiplier = 1.12f;

    [Tooltip("Time to drop from the enlarged pose down to zero.")]
    [SerializeField] private float shrinkDuration = 0.35f;

    [Tooltip("Short pause while invisible at the teleport moment.")]
    [SerializeField] private float hiddenHoldDuration = 0.06f;

    [Tooltip("Time to grow back after teleporting.")]
    [SerializeField] private float growDuration = 0.45f;

    [Tooltip("Strength of the bouncy overshoot while growing back.")]
    [SerializeField] private float growBounce = 1.4f;

    [Tooltip("Strength of the wind-up on the shrink.")]
    [SerializeField] private float shrinkWindUp = 0.8f;

    [Header("Ground")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRayStartHeight = 5f;
    [SerializeField] private float groundRayDistance = 20f;
    [SerializeField] private float groundClearance = 0.05f;

    [Header("History Recording")]
    [SerializeField] private float recordInterval = 0.02f;

    [Header("Vampire Audio")]
    [Tooltip("Sound played when the vampire passenger is picked up.")]
    [SerializeField] private AudioClip vampireSound;

    [Tooltip("Seconds between sound plays.")]
    [SerializeField] private float soundInterval = 10f;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Tooltip("Play the sound immediately when the effect activates.")]
    [SerializeField] private bool playSoundImmediately = true;

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

    private const float MinScale = 0.0001f;

    private readonly List<CarSnapshot> history =
        new List<CarSnapshot>();

    private RCC_CarControllerV3 playerCar;
    private Rigidbody carRigidbody;

    private AudioSource vampireAudioSource;
    private float nextSoundTime;

    private Vector3 originalScale;
    private Quaternion originalVisualRotation;

    private bool effectActive;
    private bool isTeleporting;
    private bool isGrowing;
    private bool originalCanControl;
    private float nextRecordTime;

    private Coroutine teleportLoopRoutine;
    private Coroutine growRoutine;

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
                this
            );
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
        if (!effectActive)
            return;

        // Audio continues during countdowns and teleport animations.
        UpdateVampireAudio();

        if (isTeleporting || playerCar == null)
            return;

        if (Time.time >= nextRecordTime)
        {
            RecordState();
            nextRecordTime =
                Time.time + Mathf.Max(0.01f, recordInterval);
        }

        TrimHistory();
    }

    // Call this when the vampire enters the car.
    public void ActivateVampireEffect()
    {
        if (effectActive)
            return;

        if (playerCar == null)
        {
            Debug.LogWarning(
                "VampireTeleportEffect: Player car was not found.",
                this
            );
            return;
        }

        if (delays == null || delays.Length == 0)
        {
            Debug.LogWarning(
                "VampireTeleportEffect: Add at least one delay.",
                this
            );
            return;
        }

        effectActive = true;
        isTeleporting = false;
        isGrowing = false;

        SaveOriginalVisual();
        originalCanControl = playerCar.canControl;

        // Set up the audio when the passenger is picked up.
        SetupAudioSource();

        if (playSoundImmediately)
            PlayVampireSound();

        nextSoundTime =
            Time.time + Mathf.Max(0.1f, soundInterval);

        history.Clear();
        RecordState();

        nextRecordTime =
            Time.time + Mathf.Max(0.01f, recordInterval);

        teleportLoopRoutine = StartCoroutine(TeleportLoop());
    }

    // ------------------------------------------------------------
    // Audio
    // ------------------------------------------------------------

    private void SetupAudioSource()
    {
        if (vampireAudioSource != null || playerCar == null)
            return;

        GameObject audioObject =
            new GameObject("VampireEffectAudio");

        audioObject.transform.SetParent(
            playerCar.transform,
            false
        );

        vampireAudioSource =
            audioObject.AddComponent<AudioSource>();

        vampireAudioSource.playOnAwake = false;
        vampireAudioSource.loop = false;
        vampireAudioSource.spatialBlend = 0f;
        vampireAudioSource.volume = soundVolume;
    }

    private void UpdateVampireAudio()
    {
        if (vampireSound == null)
            return;

        if (Time.time >= nextSoundTime)
        {
            PlayVampireSound();

            nextSoundTime =
                Time.time + Mathf.Max(0.1f, soundInterval);
        }
    }

    private void PlayVampireSound()
    {
        if (vampireSound == null)
            return;

        if (vampireAudioSource == null)
            SetupAudioSource();

        if (vampireAudioSource == null)
            return;

        vampireAudioSource.volume = soundVolume;

        vampireAudioSource.PlayOneShot(
            vampireSound,
            soundVolume
        );
    }

    private void StopAndDestroyAudio()
    {
        if (vampireAudioSource == null)
            return;

        vampireAudioSource.Stop();
        Destroy(vampireAudioSource.gameObject);
        vampireAudioSource = null;

        nextSoundTime = 0f;
    }

    // ------------------------------------------------------------
    // Teleport countdown
    // ------------------------------------------------------------

    private IEnumerator TeleportLoop()
    {
        int index = 0;

        while (effectActive)
        {
            float countdown = Mathf.Max(0f, delays[index]);
            float timer = 0f;
            int lastLoggedSecond = 0;

            while (timer < countdown)
            {
                if (!effectActive)
                    yield break;

                timer += Time.deltaTime;

                TimeUntilNextTeleport =
                    Mathf.Max(0f, countdown - timer);

                if (logCountdown)
                {
                    int second = Mathf.FloorToInt(timer);

                    if (second > lastLoggedSecond)
                    {
                        lastLoggedSecond = second;

                        Debug.Log(
                            "Teleport countdown: " +
                            second + " / " + countdown
                        );
                    }
                }

                yield return null;
            }

            if (!effectActive)
                yield break;

            yield return StartCoroutine(DoTeleport());

            if (!effectActive)
                yield break;

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

        CarSnapshot target =
            GetSnapshotAtTime(Time.time - rewindSeconds);

        Quaternion carRotationBeforeEffect =
            playerCar.transform.rotation;

        playerCar.canControl = false;

        FreezeCar();

        yield return StartCoroutine(ExpandVisual());
        if (!effectActive) yield break;

        yield return StartCoroutine(ShrinkToZero());
        if (!effectActive) yield break;

        if (target != null)
        {
            Vector3 destination = target.position;

            if (carRigidbody != null)
            {
                carRigidbody.position = destination;
                carRigidbody.rotation = carRotationBeforeEffect;
            }

            playerCar.transform.SetPositionAndRotation(
                destination,
                carRotationBeforeEffect
            );

            Physics.SyncTransforms();
            SnapCarToGround();
        }
        else
        {
            Debug.LogWarning(
                "VampireTeleportEffect: No history available to teleport to.",
                this
            );
        }

        ResetVisualRotation();

        if (hiddenHoldDuration > 0f)
            yield return new WaitForSeconds(hiddenHoldDuration);

        if (!effectActive)
            yield break;

        history.Clear();
        RecordState();

        nextRecordTime =
            Time.time + Mathf.Max(0.01f, recordInterval);

        UnfreezeCar();

        // Preserve the original behavior: controls return after teleport.
        playerCar.canControl = true;
        isTeleporting = false;

        growRoutine = StartCoroutine(Grow());
    }

    // ------------------------------------------------------------
    // Visual animation
    // ------------------------------------------------------------

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
            originalScale.y * Mathf.Clamp(
                squashYMultiplier, 0.1f, 1f),
            originalScale.z * Mathf.Max(1f, squashXZMultiplier)
        );

        Vector3 expandedScale =
            originalScale * Mathf.Max(1f, squashExpandMultiplier);

        float squashTime = duration * 0.4f;
        float popTime = duration - squashTime;

        yield return AnimateScale(
            originalScale,
            squashScale,
            squashTime,
            EaseOutCubic
        );

        if (!effectActive) yield break;

        yield return AnimateScale(
            squashScale,
            expandedScale,
            popTime,
            EaseOutBack05
        );
    }

    private IEnumerator ShrinkToZero()
    {
        float duration = Mathf.Max(0.02f, shrinkDuration);

        if (visualRoot == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Vector3 expandedScale =
            originalScale * Mathf.Max(1f, squashExpandMultiplier);

        Vector3 zeroScale = originalScale * MinScale;
        float windUp = Mathf.Max(0f, shrinkWindUp);

        yield return AnimateScale(
            expandedScale,
            zeroScale,
            duration,
            t => EaseInBack(t, windUp)
        );

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

            yield return AnimateScale(
                zeroScale,
                originalScale,
                duration,
                t => EaseOutBack(t, bounce)
            );

            if (effectActive)
                ResetVisual();
        }

        isGrowing = false;
        growRoutine = null;
    }

    private IEnumerator AnimateScale(
        Vector3 from,
        Vector3 to,
        float duration,
        System.Func<float, float> ease)
    {
        duration = Mathf.Max(0.0001f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!effectActive || visualRoot == null)
                yield break;

            elapsed += Mathf.Min(Time.deltaTime, 0.05f);

            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 s = Vector3.LerpUnclamped(
                from,
                to,
                ease(t)
            );

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

    // ------------------------------------------------------------
    // Easing functions
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // History
    // ------------------------------------------------------------

    private void RecordState()
    {
        if (playerCar == null)
            return;

        history.Add(
            new CarSnapshot(
                Time.time,
                playerCar.transform.position
            )
        );
    }

    private void TrimHistory()
    {
        float oldestAllowed =
            Time.time - (Mathf.Max(0f, rewindSeconds) + 1f);

        while (history.Count > 2 &&
               history[1].time < oldestAllowed)
        {
            history.RemoveAt(0);
        }
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

                float t = Mathf.Clamp01(
                    (targetTime - previous.time) / diff
                );

                return new CarSnapshot(
                    targetTime,
                    Vector3.Lerp(
                        previous.position,
                        next.position,
                        t
                    )
                );
            }

            previous = next;
        }

        return history[history.Count - 1];
    }

    // ------------------------------------------------------------
    // Ground and physics helpers
    // ------------------------------------------------------------

    private void SnapCarToGround()
    {
        if (playerCar == null)
            return;

        Vector3 origin =
            playerCar.transform.position +
            Vector3.up * groundRayStartHeight;

        if (!Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            groundRayStartHeight + groundRayDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore))
        {
            Debug.LogWarning(
                "VampireTeleportEffect: Ground not found. " +
                "Check Ground Layer and ray distance.",
                this
            );

            return;
        }

        Vector3 pos = playerCar.transform.position;

        pos.y = hit.point.y +
            GetPivotToBottomDistance() +
            groundClearance;

        if (carRigidbody != null)
            carRigidbody.position = pos;

        playerCar.transform.position = pos;

        Physics.SyncTransforms();
    }

    private float GetPivotToBottomDistance()
    {
        float lowest = float.PositiveInfinity;

        foreach (Collider col in
                 playerCar.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger)
                continue;

            lowest = Mathf.Min(lowest, col.bounds.min.y);
        }

        if (float.IsPositiveInfinity(lowest))
            return 0f;

        return Mathf.Max(
            0f,
            playerCar.transform.position.y - lowest
        );
    }

    private void FreezeCar()
    {
        if (carRigidbody == null || carFrozen)
            return;

        savedKinematic = carRigidbody.isKinematic;
        savedInterpolation = carRigidbody.interpolation;

        if (!carRigidbody.isKinematic)
        {
            carRigidbody.velocity = Vector3.zero;
            carRigidbody.angularVelocity = Vector3.zero;
        }

        carRigidbody.interpolation = RigidbodyInterpolation.None;
        carRigidbody.isKinematic = true;

        carFrozen = true;
    }

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

    // ------------------------------------------------------------
    // Deactivation and cleanup
    // ------------------------------------------------------------

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
        StopAndDestroyAudio();

        if (playerCar != null)
            playerCar.canControl = originalCanControl;
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
        StopAndDestroyAudio();

        if (playerCar != null)
            playerCar.canControl = originalCanControl;

        history.Clear();
        TimeUntilNextTeleport = 0f;
    }

    private void OnDestroy()
    {
        StopAndDestroyAudio();
    }
}