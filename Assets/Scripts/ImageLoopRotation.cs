
using UnityEngine;

public class ImageLoopRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 180f;

    private void Update()
    {
        transform.Rotate(
            0f,
            0f,
            -rotationSpeed * Time.deltaTime
        );
    }
}
