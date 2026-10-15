using UnityEngine;
using System.Collections;

public class WitchCarEffect : MonoBehaviour
{
    [Header("Car Visual Model")]
    [Tooltip("Assign the complete car visual hierarchy, including visible wheels.")]
    [SerializeField] private Transform carVisualRoot;

    [Header("Scale Settings")]
    [Tooltip("Minimum size relative to the original size.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float minScaleMultiplier = 0.3f;

    [Tooltip("Maximum size relative to the original size.")]
    [Range(1f, 3f)]
    [SerializeField] private float maxScaleMultiplier = 3f;

    [Header("Timing")]
    [Tooltip("Minimum wait before changing size.")]
    [SerializeField] private float minWaitTime = 5f;

    [Tooltip("Maximum wait before changing size.")]
    [SerializeField] private float maxWaitTime = 10f;

    [Tooltip("Duration of each smooth scale transition.")]
    [SerializeField] private float scaleTransitionDuration = 0.75f;

    [Header("Witch Audio")]
    [Tooltip("Sound played when the witch effect activates and periodically afterward.")]
    [SerializeField] private AudioClip witchSound;

    [Tooltip("Seconds between sound plays.")]
    [SerializeField] private float soundInterval = 10f;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Tooltip("Play the sound immediately when the effect activates.")]
    [SerializeField] private bool playSoundImmediately = true;

    private AudioSource witchAudioSource;
    private float nextSoundTime;

    private Vector3 originalScale;
    private bool effectActive;
    private Coroutine scaleRoutine;

    private void Awake()
    {
        if (carVisualRoot == null)
        {
            Debug.LogError(
                "WitchCarEffect: Assign the complete car visual hierarchy.",
                this
            );

            enabled = false;
            return;
        }

        originalScale = carVisualRoot.localScale;
    }

    // Call this after the witch enters the car.
    public void ActivateWitchEffect()
    {
        if (!enabled || effectActive)
            return;

        effectActive = true;

        SetupAudioSource();

        if (playSoundImmediately)
        {
            PlayWitchSound();
            nextSoundTime = Time.time + Mathf.Max(0.1f, soundInterval);
        }
        else
        {
            nextSoundTime = Time.time + Mathf.Max(0.1f, soundInterval);
        }

        scaleRoutine = StartCoroutine(WitchScaleRoutine());
    }

    private void SetupAudioSource()
    {
        if (witchAudioSource != null)
            return;

        // Create a dedicated audio object attached to the car.
        Transform audioParent = carVisualRoot != null
            ? carVisualRoot
            : transform;

        GameObject audioObject = new GameObject("WitchEffectAudio");
        audioObject.transform.SetParent(audioParent, false);

        witchAudioSource = audioObject.AddComponent<AudioSource>();
        witchAudioSource.playOnAwake = false;
        witchAudioSource.loop = false;
        witchAudioSource.spatialBlend = 0f;
        witchAudioSource.volume = soundVolume;
    }

    private void Update()
    {
        if (!effectActive)
            return;

        if (witchSound == null)
            return;

        if (witchAudioSource == null)
            SetupAudioSource();

        if (Time.time >= nextSoundTime)
        {
            PlayWitchSound();

            nextSoundTime = Time.time +
                Mathf.Max(0.1f, soundInterval);
        }
    }

    private void PlayWitchSound()
    {
        if (witchSound == null)
            return;

        if (witchAudioSource == null)
            SetupAudioSource();

        if (witchAudioSource != null)
        {
            witchAudioSource.PlayOneShot(witchSound, soundVolume);
        }
    }

    private IEnumerator WitchScaleRoutine()
    {
        while (effectActive)
        {
            // Wait before shrinking.
            yield return new WaitForSeconds(
                Random.Range(minWaitTime, maxWaitTime)
            );

            if (!effectActive)
                break;

            yield return StartCoroutine(
                SmoothScale(originalScale * minScaleMultiplier)
            );

            if (!effectActive)
                break;

            // Wait before growing.
            yield return new WaitForSeconds(
                Random.Range(minWaitTime, maxWaitTime)
            );

            if (!effectActive)
                break;

            yield return StartCoroutine(
                SmoothScale(originalScale * maxScaleMultiplier)
            );
        }

        scaleRoutine = null;
    }

    private IEnumerator SmoothScale(Vector3 targetScale)
    {
        Vector3 startScale = carVisualRoot.localScale;

        float duration = Mathf.Max(
            0.01f,
            scaleTransitionDuration
        );

        float timer = 0f;

        while (timer < duration)
        {
            if (!effectActive)
                yield break;

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);
            t = Mathf.SmoothStep(0f, 1f, t);

            carVisualRoot.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                t
            );

            yield return null;
        }

        if (effectActive)
            carVisualRoot.localScale = targetScale;
    }

    public void DeactivateWitchEffect()
    {
        effectActive = false;

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }

        if (carVisualRoot != null)
            carVisualRoot.localScale = originalScale;

        if (witchAudioSource != null)
        {
            witchAudioSource.Stop();
            Destroy(witchAudioSource.gameObject);
            witchAudioSource = null;
        }

        nextSoundTime = 0f;
    }

    private void OnDisable()
    {
        effectActive = false;

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }

        if (carVisualRoot != null)
            carVisualRoot.localScale = originalScale;

        if (witchAudioSource != null)
        {
            witchAudioSource.Stop();
            Destroy(witchAudioSource.gameObject);
            witchAudioSource = null;
        }

        nextSoundTime = 0f;
    }

    private void OnDestroy()
    {
        if (witchAudioSource != null)
        {
            Destroy(witchAudioSource.gameObject);
            witchAudioSource = null;
        }
    }
}