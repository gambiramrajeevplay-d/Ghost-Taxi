
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
        scaleRoutine = StartCoroutine(WitchScaleRoutine());
    }

    private IEnumerator WitchScaleRoutine()
    {
        while (effectActive)
        {
            // Wait 5-10 seconds before shrinking.
            yield return new WaitForSeconds(
                Random.Range(minWaitTime, maxWaitTime)
            );

            if (!effectActive)
                break;

            // Shrink to 30% of the original size.
            yield return StartCoroutine(
                SmoothScale(originalScale * minScaleMultiplier)
            );

            // Wait 5-10 seconds before growing.
            yield return new WaitForSeconds(
                Random.Range(minWaitTime, maxWaitTime)
            );

            if (!effectActive)
                break;

            // Grow to 3 times the original size.
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

        carVisualRoot.localScale = targetScale;
    }

    private void OnDisable()
    {
        effectActive = false;

        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }

        // Restore the original visual size when disabled.
        if (carVisualRoot != null)
            carVisualRoot.localScale = originalScale;
    }
}