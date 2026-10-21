using UnityEngine;

public class GhostOrbit : MonoBehaviour
{
    private const int GHOST_COUNT = 4;

    private bool initialized;
    private int index;

    private float radius;
    private float height;
    private float speed;

    private bool faceCenter;
    private float floatHeight;
    private float floatSpeed;

    private bool enableSway;
    private float swayAmount;
    private float swaySpeed;
    private float phase;

    private Transform orbitCenter;
    private float currentAngle;

    public void Init(
        int index,
        int count,
        float radius,
        float height,
        float speed,
        bool faceCenter,
        float floatHeight,
        float floatSpeed,
        bool enableSway,
        float swayAmount,
        float swaySpeed)
    {
        this.index = Mathf.Clamp(index, 0, GHOST_COUNT - 1);

        this.radius = radius;
        this.height = height;
        this.speed = speed;

        this.faceCenter = faceCenter;
        this.floatHeight = floatHeight;
        this.floatSpeed = floatSpeed;

        this.enableSway = enableSway;
        this.swayAmount = swayAmount;
        this.swaySpeed = swaySpeed;

        orbitCenter = transform.parent;

        currentAngle = (360f / GHOST_COUNT) * this.index;
        phase = Random.Range(0f, Mathf.PI * 2f);

        initialized = orbitCenter != null;

        if (!initialized)
        {
            Debug.LogWarning(
                "GhostOrbit: No parent transform found.",
                this);
        }
    }

    private void Update()
    {
        if (!initialized || orbitCenter == null)
            return;

        // Rotate around the world Y-axis.
        currentAngle += speed * Time.deltaTime;

        float radians = currentAngle * Mathf.Deg2Rad;

        Vector3 flatOffset = new Vector3(
            Mathf.Cos(radians) * radius,
            0f,
            Mathf.Sin(radians) * radius);

        float bob =
            Mathf.Sin(Time.time * floatSpeed + phase) *
            floatHeight;

        // Keep the orbit horizontal, regardless of car tilt.
        Vector3 targetPosition =
            orbitCenter.position +
            flatOffset +
            Vector3.up * (height + bob);

        transform.position = targetPosition;

        // Calculate the ghost's upright rotation.
        Quaternion targetRotation =
            Quaternion.Euler(0f, currentAngle + 90f, 0f);

        if (faceCenter)
        {
            Vector3 direction =
                orbitCenter.position - targetPosition;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up);
            }
        }

        // Add gentle rocking without changing the orbit axis.
        if (enableSway)
        {
            float sway =
                Mathf.Sin(
                    Time.time * floatSpeed * swaySpeed + phase) *
                swayAmount;

            targetRotation *= Quaternion.Euler(0f, 0f, sway);
        }

        transform.rotation = targetRotation;
    }

    private void OnDisable()
    {
        initialized = false;
    }
}