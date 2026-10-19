
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SkeletonCarEffect : MonoBehaviour
{
    [Header("Breakable Skeleton Prefab")]
    [Tooltip("Prefab containing separate bone GameObjects.")]
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
    private Vector3 carSpawnOffset = new Vector3(0f, 0.5f, 0f);

    [SerializeField]
    private Vector3 spawnSpread = new Vector3(0.8f, 0.4f, 1.2f);

    [Header("Bone Physics")]
    [SerializeField] private float boneMass = 0.2f;
    [SerializeField] private float explosionForce = 4f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float upwardForce = 1f;
    [SerializeField] private float spinForce = 3f;

    [Header("Bone Spawn")]
    [Tooltip("Delay between each bone appearing.")]
    [SerializeField] private float boneSpawnInterval = 0.12f;

    [Tooltip("Add BoxColliders to bones without colliders.")]
    [SerializeField] private bool addBoxColliders = true;

    [Header("Cleanup")]
    [SerializeField] private float boneLifetime = 12f;

    [Header("Skeleton Audio")]
    [SerializeField] private AudioClip skeletonSound;
    [SerializeField] private float soundInterval = 10f;

    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [SerializeField] private bool playSoundImmediately = true;

    [Header("Level Fail")]
    [Tooltip("Trigger level fail after the bones have spawned.")]
    [SerializeField] private bool triggerLevelFail = true;

    private AudioSource skeletonAudioSource;
    private RCC_CarControllerV3 playerCar;

    private bool passengerPickedUp;
    private bool failTriggered;
    private float nextAllowedHitTime;
    private float nextSoundTime;

    private readonly List<GameObject> spawnedObjects =
        new List<GameObject>();

    private void Awake()
    {
        playerCar = GetComponent<RCC_CarControllerV3>();

        if (playerCar == null)
            playerCar = GetComponentInParent<RCC_CarControllerV3>();

        if (playerCar == null)
        {
            Debug.LogError(
                "SkeletonCarEffect must be attached to the player car.",
                this);
        }
    }

    // Call this when the skeleton passenger is picked up.
    public void ActivateSkeletonEffect()
    {
        if (playerCar == null || breakableSkeletonPrefab == null)
        {
            Debug.LogWarning(
                "Assign the player car and breakable skeleton prefab.",
                this);
            return;
        }

        if (passengerPickedUp || failTriggered)
            return;

        passengerPickedUp = true;
        nextAllowedHitTime = 0f;

        SetupAudioSource();

        if (playSoundImmediately)
            PlaySkeletonSound();

        nextSoundTime = Time.time +
            Mathf.Max(0.1f, soundInterval);

        Debug.Log(
            "Skeleton passenger picked up. Waiting for obstacle collision.");
    }

    private void SetupAudioSource()
    {
        if (skeletonAudioSource != null || playerCar == null)
            return;

        GameObject audioObject = new GameObject("SkeletonEffectAudio");
        audioObject.transform.SetParent(playerCar.transform, false);

        skeletonAudioSource = audioObject.AddComponent<AudioSource>();
        skeletonAudioSource.playOnAwake = false;
        skeletonAudioSource.loop = false;
        skeletonAudioSource.spatialBlend = 0f;
        skeletonAudioSource.volume = soundVolume;
    }

    private void Update()
    {
        if (!passengerPickedUp || skeletonSound == null)
            return;

        if (skeletonAudioSource == null)
            SetupAudioSource();

        if (Time.time >= nextSoundTime)
        {
            PlaySkeletonSound();

            nextSoundTime = Time.time +
                Mathf.Max(0.1f, soundInterval);
        }
    }

    private void PlaySkeletonSound()
    {
        if (skeletonSound == null)
            return;

        if (skeletonAudioSource == null)
            SetupAudioSource();

        if (skeletonAudioSource != null)
        {
            skeletonAudioSource.volume = soundVolume;
            skeletonAudioSource.PlayOneShot(skeletonSound, soundVolume);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!passengerPickedUp || playerCar == null || failTriggered)
            return;

        if (collision == null || collision.contactCount == 0)
            return;

        int otherLayer = 1 << collision.gameObject.layer;

        if ((obstacleLayers.value & otherLayer) == 0)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minimumImpactSpeed)
            return;

        if (Time.time < nextAllowedHitTime)
            return;

        nextAllowedHitTime = Time.time + hitCooldown;

        // Prevent additional collisions from starting duplicate effects.
        failTriggered = true;
        passengerPickedUp = false;

        Vector3 impactPoint = collision.GetContact(0).point;
        Vector3 impactDirection = -collision.GetContact(0).normal;

        if (impactDirection.sqrMagnitude < 0.01f)
            impactDirection = playerCar.transform.forward;

        impactDirection.Normalize();

        StartCoroutine(
            SpawnBonesForHit(impactPoint, impactDirection));
    }

    private IEnumerator SpawnBonesForHit(
        Vector3 impactPoint,
        Vector3 impactDirection)
    {
        Vector3 carPosition =
            playerCar.transform.TransformPoint(carSpawnOffset);

        GameObject skeletonInstance = Instantiate(
            breakableSkeletonPrefab,
            carPosition,
            playerCar.transform.rotation);

        spawnedObjects.Add(skeletonInstance);

        List<Transform> boneObjects = new List<Transform>();

        for (int i = 0; i < skeletonInstance.transform.childCount; i++)
        {
            CollectBoneObjects(
                skeletonInstance.transform.GetChild(i),
                boneObjects);
        }

        if (boneObjects.Count == 0)
        {
            Debug.LogWarning(
                "No bone child GameObjects found in the skeleton prefab.",
                skeletonInstance);

            Destroy(skeletonInstance);
            spawnedObjects.Remove(skeletonInstance);

            TriggerLevelFail();
            yield break;
        }

        List<Renderer[]> boneRenderers = new List<Renderer[]>();

        foreach (Transform bone in boneObjects)
        {
            Renderer[] renderers =
                bone.GetComponentsInChildren<Renderer>(true);

            boneRenderers.Add(renderers);

            foreach (Renderer renderer in renderers)
                renderer.enabled = false;
        }

        for (int i = 0; i < boneObjects.Count; i++)
        {
            Transform bone = boneObjects[i];

            if (bone == null)
                continue;

            bone.SetParent(null, true);
            spawnedObjects.Add(bone.gameObject);

            Vector3 randomOffset = new Vector3(
                Random.Range(-spawnSpread.x, spawnSpread.x),
                Random.Range(-spawnSpread.y, spawnSpread.y),
                Random.Range(-spawnSpread.z, spawnSpread.z));

            bone.position = carPosition +
                playerCar.transform.TransformDirection(randomOffset);

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
                BoxCollider box =
                    bone.gameObject.AddComponent<BoxCollider>();

                Renderer renderer = bone.GetComponentInChildren<Renderer>();

                if (renderer != null)
                {
                    Bounds bounds = renderer.bounds;

                    box.center = bone.InverseTransformPoint(
                        bounds.center);

                    Vector3 localSize =
                        bone.InverseTransformVector(bounds.size);

                    box.size = new Vector3(
                        Mathf.Abs(localSize.x),
                        Mathf.Abs(localSize.y),
                        Mathf.Abs(localSize.z));
                }
            }

            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            foreach (Renderer renderer in boneRenderers[i])
            {
                if (renderer != null)
                    renderer.enabled = true;
            }

            body.AddExplosionForce(
                explosionForce,
                impactPoint,
                Mathf.Max(0.1f, explosionRadius),
                upwardForce,
                ForceMode.Impulse);

            Vector3 randomDirection =
                (impactDirection +
                 Random.insideUnitSphere * 0.5f).normalized;

            body.AddForce(
                randomDirection * explosionForce * 0.35f,
                ForceMode.Impulse);

            body.AddTorque(
                Random.insideUnitSphere * spinForce,
                ForceMode.Impulse);

            if (boneLifetime > 0f)
                Destroy(bone.gameObject, boneLifetime);

            yield return new WaitForSeconds(
                Mathf.Max(0.01f, boneSpawnInterval));
        }

        if (skeletonInstance != null)
        {
            spawnedObjects.Remove(skeletonInstance);
            Destroy(skeletonInstance);
        }

        // All bones have now spawned. Start the level-fail sequence.
        TriggerLevelFail();
    }

    private void TriggerLevelFail()
    {
        if (!triggerLevelFail)
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoseGame();
        }
        else
        {
            Debug.LogWarning(
                "SkeletonCarEffect: GameManager instance was not found.",
                this);
        }
    }

    private void CollectBoneObjects(
        Transform current,
        List<Transform> results)
    {
        if (current == null)
            return;

        results.Add(current);

        for (int i = 0; i < current.childCount; i++)
        {
            CollectBoneObjects(current.GetChild(i), results);
        }
    }

    private void DestroySpawnedObjects()
    {
        foreach (GameObject spawnedObject in spawnedObjects)
        {
            if (spawnedObject != null)
                Destroy(spawnedObject);
        }

        spawnedObjects.Clear();
    }

    public void DeactivateSkeletonEffect()
    {
        passengerPickedUp = false;

        StopAllCoroutines();
        DestroySpawnedObjects();
        StopAndDestroyAudio();

        nextSoundTime = 0f;
    }

    private void OnDisable()
    {
        passengerPickedUp = false;

        StopAllCoroutines();
        DestroySpawnedObjects();
        StopAndDestroyAudio();
    }

    private void StopAndDestroyAudio()
    {
        if (skeletonAudioSource == null)
            return;

        skeletonAudioSource.Stop();
        Destroy(skeletonAudioSource.gameObject);
        skeletonAudioSource = null;
    }

    private void OnDestroy()
    {
        StopAndDestroyAudio();
    }
}
