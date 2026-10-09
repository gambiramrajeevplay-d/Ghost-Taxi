using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SkeletonCarEffect : MonoBehaviour
{
    [Header("Breakable Skeleton Prefab")]
    [Tooltip("Second prefab containing separate bone GameObjects.")]
    [SerializeField] private GameObject breakableSkeletonPrefab;

    [Header("Collision")]
    [Tooltip("Only collisions with these layers trigger bone spawning.")]
    [SerializeField] private LayerMask obstacleLayers;

    [Tooltip("Minimum relative collision speed required.")]
    [SerializeField] private float minimumImpactSpeed = 3f;

    [Tooltip("Delay between separate collision effects.")]
    [SerializeField] private float hitCooldown = 0.5f;

    [Header("Spawn Position")]
    [SerializeField]
    private Vector3 carSpawnOffset =
        new Vector3(0f, 0.5f, 0f);

    [SerializeField]
    private Vector3 spawnSpread =
        new Vector3(0.8f, 0.4f, 1.2f);

    [Header("Bone Physics")]
    [SerializeField] private float boneMass = 0.2f;
    [SerializeField] private float explosionForce = 4f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float upwardForce = 1f;
    [SerializeField] private float spinForce = 3f;

    [Header("Bone Spawn")]
    [Tooltip("Delay between each bone appearing.")]
    [SerializeField] private float boneSpawnInterval = 0.12f;

    [Tooltip("Optional: add BoxColliders to spawned bones.")]
    [SerializeField] private bool addBoxColliders = true;

    [Header("Cleanup")]
    [SerializeField] private float boneLifetime = 12f;

    private RCC_CarControllerV3 playerCar;

    private bool passengerPickedUp;
    private float nextAllowedHitTime;

    private readonly List<Coroutine> runningEffects =
        new List<Coroutine>();

    private void Awake()
    {
        playerCar = GetComponent<RCC_CarControllerV3>();

        if (playerCar == null)
            playerCar = GetComponentInParent<RCC_CarControllerV3>();

        if (playerCar == null)
        {
            Debug.LogError(
                "SkeletonCarEffect must be attached to the player car.",
                this
            );
        }
    }

    // Call this when the rigged passenger skeleton is picked up.
    public void ActivateSkeletonEffect()
    {
        if (playerCar == null || breakableSkeletonPrefab == null)
        {
            Debug.LogWarning(
                "Assign the player car and breakable skeleton prefab.",
                this
            );
            return;
        }

        passengerPickedUp = true;
        nextAllowedHitTime = 0f;

        Debug.Log(
            "Skeleton passenger picked up. Waiting for obstacle collisions."
        );
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!passengerPickedUp || playerCar == null)
            return;

        if (collision == null || collision.contactCount == 0)
            return;

        // Check the collided object's layer.
        int otherLayer = 1 << collision.gameObject.layer;

        if ((obstacleLayers.value & otherLayer) == 0)
            return;

        // Ignore light contacts.
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minimumImpactSpeed)
            return;

        // Prevent multiple colliders from triggering the effect
        // repeatedly during the same brief impact.
        if (Time.time < nextAllowedHitTime)
            return;

        nextAllowedHitTime = Time.time + hitCooldown;

        Vector3 impactPoint = collision.GetContact(0).point;
        Vector3 impactDirection = -collision.GetContact(0).normal;

        if (impactDirection.sqrMagnitude < 0.01f)
            impactDirection = playerCar.transform.forward;

        impactDirection.Normalize();

        Coroutine effect = StartCoroutine(
            SpawnBonesForHit(impactPoint, impactDirection)
        );

        runningEffects.Add(effect);
    }

    private IEnumerator SpawnBonesForHit(
        Vector3 impactPoint,
        Vector3 impactDirection)
    {
        Vector3 carPosition =
            playerCar.transform.TransformPoint(carSpawnOffset);

        // Spawn a fresh copy of the second skeleton prefab for this hit.
        GameObject skeletonInstance = Instantiate(
            breakableSkeletonPrefab,
            carPosition,
            playerCar.transform.rotation
        );

        // Collect all child transforms before detaching them.
        // No Rigidbody or Collider components are required in the prefab.
        List<Transform> boneObjects = new List<Transform>();

        for (int i = 0; i < skeletonInstance.transform.childCount; i++)
        {
            Transform child = skeletonInstance.transform.GetChild(i);
            CollectBoneObjects(child, boneObjects);
        }

        if (boneObjects.Count == 0)
        {
            Debug.LogWarning(
                "No bone child GameObjects found. Make sure the breakable skeleton prefab has separate child objects.",
                skeletonInstance
            );

            Destroy(skeletonInstance);
            yield break;
        }

        // Hide all discovered bone renderers until each bone is released.
        List<Renderer[]> boneRenderers = new List<Renderer[]>();

        foreach (Transform bone in boneObjects)
        {
            Renderer[] renderers =
                bone.GetComponentsInChildren<Renderer>(true);

            boneRenderers.Add(renderers);

            foreach (Renderer r in renderers)
                r.enabled = false;
        }

        for (int i = 0; i < boneObjects.Count; i++)
        {
            Transform bone = boneObjects[i];

            if (bone == null)
                continue;

            // Preserve the existing bone world transform.
            bone.SetParent(null, true);

            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnSpread.x, spawnSpread.x),
                Random.Range(-spawnSpread.y, spawnSpread.y),
                Random.Range(-spawnSpread.z, spawnSpread.z)
            );

            bone.position = carPosition +
                playerCar.transform.TransformDirection(randomOffset);

            // Add physics at runtime because the prefab has none.
            Rigidbody body = bone.GetComponent<Rigidbody>();

            if (body == null)
                body = bone.gameObject.AddComponent<Rigidbody>();

            body.mass = Mathf.Max(0.01f, boneMass);
            body.useGravity = true;
            body.isKinematic = false;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            if (addBoxColliders &&
                bone.GetComponent<Collider>() == null)
            {
                BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();

                // Fit the collider approximately to the visible geometry.
                Renderer renderer =
                    bone.GetComponentInChildren<Renderer>();

                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;

                    box.center = bone.InverseTransformPoint(
                        bounds.center
                    );

                    Vector3 localSize =
                        bone.InverseTransformVector(bounds.size);

                    box.size = new Vector3(
                        Mathf.Abs(localSize.x),
                        Mathf.Abs(localSize.y),
                        Mathf.Abs(localSize.z)
                    );
                }
            }

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            Renderer[] renderersForBone = boneRenderers[i];

            foreach (Renderer r in renderersForBone)
            {
                if (r != null)
                    r.enabled = true;
            }

            body.AddExplosionForce(
                explosionForce,
                impactPoint,
                Mathf.Max(0.1f, explosionRadius),
                upwardForce,
                ForceMode.Impulse
            );

            Vector3 randomDirection =
                (impactDirection +
                 Random.insideUnitSphere * 0.5f).normalized;

            body.AddForce(
                randomDirection * explosionForce * 0.35f,
                ForceMode.Impulse
            );

            body.AddTorque(
                Random.insideUnitSphere * spinForce,
                ForceMode.Impulse
            );

            if (boneLifetime > 0f)
                Destroy(bone.gameObject, boneLifetime);

            yield return new WaitForSeconds(
                Mathf.Max(0.01f, boneSpawnInterval)
            );
        }

        // The individual bones have been detached from the root.
        if (skeletonInstance != null)
            Destroy(skeletonInstance);
    }

    private void CollectBoneObjects(
        Transform current,
        List<Transform> results)
    {
        if (current == null)
            return;

        // Each child GameObject is treated as one bone.
        results.Add(current);

        for (int i = 0; i < current.childCount; i++)
        {
            CollectBoneObjects(current.GetChild(i), results);
        }
    }

    private void OnDisable()
    {
        passengerPickedUp = false;
        runningEffects.Clear();
    }
}