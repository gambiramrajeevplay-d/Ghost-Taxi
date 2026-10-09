using UnityEngine;

public class GhostOrbit : MonoBehaviour
{
    // Shared by all ghosts so they stay evenly spaced.
    private static float sharedAngle;
    private static int lastFrame = -1;

    private bool initialized = false;

    private int index;
    private int count = 1;
    private float radius, height, speed;
    private bool faceCenter;
    private float floatHeight, floatSpeed;
    private bool enableSway;
    private float swayAmount, swaySpeed;
    private float phase;

    public void Init(int index, int count,
                     float radius, float height, float speed,
                     bool faceCenter,
                     float floatHeight, float floatSpeed,
                     bool enableSway, float swayAmount, float swaySpeed)
    {
        this.index = index;
        this.count = Mathf.Max(1, count);
        this.radius = radius;
        this.height = height;
        this.speed = speed;
        this.faceCenter = faceCenter;
        this.floatHeight = floatHeight;
        this.floatSpeed = floatSpeed;
        this.enableSway = enableSway;
        this.swayAmount = swayAmount;
        this.swaySpeed = swaySpeed;

        phase = Random.Range(0f, Mathf.PI * 2f);

        if (index == 0)
            sharedAngle = 0f;

        initialized = true;
    }

    private void Update()
    {
        // Ignore any copy that was never initialized (prevents NaN positions).
        if (!initialized)
            return;

        // Advance the shared angle once per frame.
        if (lastFrame != Time.frameCount)
        {
            lastFrame = Time.frameCount;
            sharedAngle += speed * Time.deltaTime;
        }

        float angle = (360f / count) * index + sharedAngle;
        float rad = angle * Mathf.Deg2Rad;

        Vector3 flat = new Vector3(
            Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

        float bob = Mathf.Sin(Time.time * floatSpeed + phase) * floatHeight;

        // Local values, so the ghost moves with the player car.
        transform.localPosition = flat + Vector3.up * (height + bob);

        Quaternion rot = Quaternion.identity;

        if (faceCenter && flat.sqrMagnitude > 0.001f)
            rot = Quaternion.LookRotation(-flat);

        if (enableSway)
        {
            float sway = Mathf.Sin(Time.time * floatSpeed * swaySpeed + phase) * swayAmount;
            rot *= Quaternion.Euler(0f, 0f, sway);
        }

        transform.localRotation = rot;
    }
}