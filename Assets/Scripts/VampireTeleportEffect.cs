using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VampireTeleportEffect : MonoBehaviour
{
    [Header("Car References")]
    [Tooltip("Assign the car's visual mesh/model child, not the RCC physics root.")]
    [SerializeField] private Transform visualRoot;

    [Header("Teleport Timing")]
    [SerializeField] private float countdownSeconds = 10f;
    [SerializeField] private float rewindSeconds = 5f;

    private const int MAX_TELEPORTS = 2;

    [Header("Squash Effect")]
    [SerializeField] private float squashExpandDuration = 0.18f;
    [SerializeField] private float squashExpandMultiplier = 1.2f;
    [SerializeField] private float squashYMultiplier = 0.72f;
    [SerializeField] private float squashXZMultiplier = 1.12f;
    [SerializeField] private float shrinkDuration = 0.35f;
    [SerializeField] private float hiddenHoldDuration = 0.06f;
    [SerializeField] private float growDuration = 0.45f;
    [SerializeField] private float growBounce = 1.4f;
    [SerializeField] private float shrinkWindUp = 0.8f;

    [Header("Ground")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundRayStartHeight = 5f;
    [SerializeField] private float groundRayDistance = 20f;
    [SerializeField] private float groundClearance = 0.05f;

    [Header("History Recording")]
    [SerializeField] private float recordInterval = 0.02f;

    [Header("Vampire Audio")]
    [SerializeField] private AudioClip vampireSound;
    [SerializeField] private float soundInterval = 10f;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [SerializeField] private bool playSoundImmediately = true;

    [Header("Debug")]
    [SerializeField] private bool logCountdown = false;

    public float TimeUntilNextTeleport { get; private set; }

    public int TeleportsCompleted { get; private set; }

    private const float MinScale = 0.0001f;

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

    private readonly List<CarSnapshot> history =
        new List<CarSnapshot>();

    private RCC_CarControllerV3 playerCar;
    private Rigidbody carRigidbody;
    private AudioSource vampireAudioSource;

    private float nextSoundTime;
    private float nextRecordTime;

    private Vector3 originalScale;
    private Quaternion originalVisualRotation;

    private bool effectActive;
    private bool isTeleporting;
    private bool isGrowing;
    private bool carFrozen;
    private bool savedKinematic;
    private RigidbodyInterpolation savedInterpolation;

    private Coroutine teleportLoopRoutine;
    private Coroutine growRoutine;

    private void Awake()
    {
        FindCarReferences();

        if (visualRoot == null && playerCar != null)
        {
            Debug.LogWarning(
                "VampireTeleportEffect: Assign Visual Root to the car's model child.",
                this);

            visualRoot = playerCar.transform;
        }

        SaveOriginalVisual();
    }

    private void FindCarReferences()
    {
        playerCar = GetComponent<RCC_CarControllerV3>();

        if (playerCar == null)
            playerCar = GetComponentInParent<RCC_CarControllerV3>();

        if (playerCar != null)
            carRigidbody = playerCar.GetComponent<Rigidbody>();
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

    // =========================================================
    // ACTIVATE EFFECT
    // =========================================================

    public void ActivateVampireEffect()
    {
        if (effectActive || TeleportsCompleted >= MAX_TELEPORTS)
            return;

        FindCarReferences();

        if (playerCar == null)
        {
            Debug.LogWarning(
                "VampireTeleportEffect: Player car was not found.",
                this);
            return;
        }

        SaveOriginalVisual();

        effectActive = true;
        isTeleporting = false;
        isGrowing = false;
        TeleportsCompleted = 0;
        TimeUntilNextTeleport = Mathf.Max(0f, countdownSeconds);

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

    // =========================================================
    // AUDIO
    // =========================================================

    private void SetupAudioSource()
    {
        if (vampireAudioSource != null || playerCar == null)
            return;

        GameObject audioObject =
            new GameObject("VampireEffectAudio");

        audioObject.transform.SetParent(
            playerCar.transform,
            false);

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
            soundVolume);
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

    // =========================================================
    // TWO TELEPORTS ONLY
    // =========================================================

    private IEnumerator TeleportLoop()
    {
        for (int i = 0; i < MAX_TELEPORTS; i++)
        {
            float countdown = Mathf.Max(0f, countdownSeconds);
            float timer = 0f;

            TimeUntilNextTeleport = countdown;

            while (timer < countdown)
            {
                if (!effectActive)
                {
                    teleportLoopRoutine = null;
                    yield break;
                }

                timer += Time.deltaTime;

                TimeUntilNextTeleport =
                    Mathf.Max(0f, countdown - timer);

                if (logCountdown)
                {
                    Debug.Log(
                        "Vampire teleport " + (i + 1) +
                        "/" + MAX_TELEPORTS +
                        " | Remaining: " +
                        Mathf.CeilToInt(TimeUntilNextTeleport),
                        this);
                }

                yield return null;
            }

            if (!effectActive)
            {
                teleportLoopRoutine = null;
                yield break;
            }

            TimeUntilNextTeleport = 0f;

            yield return StartCoroutine(DoTeleport());

            if (!effectActive)
            {
                teleportLoopRoutine = null;
                yield break;
            }

            while (isGrowing && effectActive)
                yield return null;

            if (!effectActive)
            {
                teleportLoopRoutine = null;
                yield break;
            }

            TeleportsCompleted++;
        }

        // Both teleports are complete.
        FinishEffect();
    }

    // =========================================================
    // TELEPORT
    // =========================================================

    private IEnumerator DoTeleport()
    {
        while (isGrowing && effectActive)
            yield return null;

        if (!effectActive || playerCar == null)
            yield break;

        isTeleporting = true;

        CarSnapshot target =
            GetSnapshotAtTime(Time.time - rewindSeconds);

        Quaternion carRotation =
            playerCar.transform.rotation;

        // Disable controls only during the teleport animation.
        playerCar.canControl = false;

        FreezeCar();

        yield return StartCoroutine(ExpandVisual());

        if (!effectActive)
            yield break;

        yield return StartCoroutine(ShrinkToZero());

        if (!effectActive)
            yield break;

        if (target != null)
        {
            Vector3 destination = target.position;

            if (carRigidbody != null)
            {
                carRigidbody.position = destination;
                carRigidbody.rotation = carRotation;
            }

            playerCar.transform.SetPositionAndRotation(
                destination,
                carRotation);

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

        if (hiddenHoldDuration > 0f)
            yield return new WaitForSeconds(hiddenHoldDuration);

        if (!effectActive)
            yield break;

        // Start a fresh history after teleporting.
        history.Clear();
        RecordState();

        nextRecordTime =
            Time.time + Mathf.Max(0.01f, recordInterval);

        UnfreezeCar();

        isTeleporting = false;

        // Restore driving after EACH teleport.
        RestorePlayerControl();

        growRoutine = StartCoroutine(Grow());
    }

    // =========================================================
    // VISUAL ANIMATION
    // =========================================================

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

        Vector3 expandedScale =
            originalScale * Mathf.Max(1f, squashExpandMultiplier);

        float squashTime = duration * 0.4f;
        float popTime = duration - squashTime;

        yield return AnimateScale(
            originalScale,
            squashScale,
            squashTime,
            EaseOutCubic);

        if (!effectActive)
            yield break;

        yield return AnimateScale(
            squashScale,
            expandedScale,
            popTime,
            EaseOutBack05);
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

        yield return AnimateScale(
            expandedScale,
            zeroScale,
            duration,
            t => EaseInBack(t, Mathf.Max(0f, shrinkWindUp)));

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

            yield return AnimateScale(
                zeroScale,
                originalScale,
                duration,
                t => EaseOutBack(t, Mathf.Max(0f, growBounce)));

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

            Vector3 scale =
                Vector3.LerpUnclamped(from, to, ease(t));

            scale.x = Mathf.Max(MinScale, scale.x);
            scale.y = Mathf.Max(MinScale, scale.y);
            scale.z = Mathf.Max(MinScale, scale.z);

            visualRoot.localScale = scale;
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

    // =========================================================
    // EASING
    // =========================================================

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

    // =========================================================
    // POSITION HISTORY
    // =========================================================

    private void RecordState()
    {
        if (playerCar == null)
            return;

        history.Add(
            new CarSnapshot(
                Time.time,
                playerCar.transform.position));
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
                float difference = next.time - previous.time;

                if (difference <= 0.0001f)
                    return previous;

                float t = Mathf.Clamp01(
                    (targetTime - previous.time) / difference);

                return new CarSnapshot(
                    targetTime,
                    Vector3.Lerp(
                        previous.position,
                        next.position,
                        t));
            }

            previous = next;
        }

        return history[history.Count - 1];
    }

    // =========================================================
    // GROUND AND PHYSICS
    // =========================================================

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
                "VampireTeleportEffect: Ground not found. Check Ground Layer.",
                this);
            return;
        }

        Vector3 position = playerCar.transform.position;

        position.y =
            hit.point.y +
            GetPivotToBottomDistance() +
            groundClearance;

        if (carRigidbody != null)
            carRigidbody.position = position;

        playerCar.transform.position = position;

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
            playerCar.transform.position.y - lowest);
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

    // =========================================================
    // RESTORE CONTROLS AND VISUALS
    // =========================================================

    private void RestorePlayerControl()
    {
        if (playerCar == null)
            return;

        // This effect must not leave the car undrivable.
        // Restore the expected gameplay control state explicitly.
        playerCar.canControl = true;
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

    // =========================================================
    // FINISH AFTER EXACTLY TWO TELEPORTS
    // =========================================================

    private void FinishEffect()
    {
        effectActive = false;
        isTeleporting = false;
        isGrowing = false;

        TimeUntilNextTeleport = 0f;

        UnfreezeCar();
        ResetVisual();

        if (playerCar != null)
        {
            RestorePlayerControl();

            // Make sure the Rigidbody is left in its normal state.
            if (carRigidbody != null)
            {
                carRigidbody.WakeUp();
            }
        }

        StopAndDestroyAudio();

        history.Clear();

        teleportLoopRoutine = null;
        growRoutine = null;

        Debug.Log(
            "VampireTeleportEffect finished. Two teleports completed; controls restored.",
            this);
    }

    // =========================================================
    // DEACTIVATION AND CLEANUP
    // =========================================================

    private void OnDisable()
    {
        bool wasActive = effectActive;

        effectActive = false;
        isTeleporting = false;
        isGrowing = false;

        StopAllCoroutines();

        teleportLoopRoutine = null;
        growRoutine = null;

        UnfreezeCar();
        ResetVisual();
        StopAndDestroyAudio();

        if (wasActive && playerCar != null)
            RestorePlayerControl();

        history.Clear();
        TimeUntilNextTeleport = 0f;
    }

    public void DeactivateVampireEffect()
    {
        bool wasActive = effectActive;

        effectActive = false;
        isTeleporting = false;
        isGrowing = false;

        StopAllCoroutines();

        teleportLoopRoutine = null;
        growRoutine = null;

        UnfreezeCar();
        ResetVisual();
        StopAndDestroyAudio();

        if (wasActive && playerCar != null)
            RestorePlayerControl();

        history.Clear();
        TimeUntilNextTeleport = 0f;
    }

    private void OnDestroy()
    {
        UnfreezeCar();
        StopAndDestroyAudio();
    }
}