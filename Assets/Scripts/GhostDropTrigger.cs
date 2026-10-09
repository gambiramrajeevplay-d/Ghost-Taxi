using UnityEngine;
using System.Collections;

public class GhostDropTrigger : MonoBehaviour
{
    [Header("Ghost")]
    [SerializeField] private GameObject ghostPrefab;

    [Tooltip("Where the ghost appears when dropped.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Ghost Movement")]
    [SerializeField] private float ghostMoveSpeed = 2f;

    [SerializeField] private float ghostMoveDistance = 10f;

    [Header("Player Control")]
    [SerializeField] private bool stopPlayerDuringDrop = true;

    [SerializeField] private float controlResumeDelay = 0.25f;

    private RCC_CarControllerV3 playerCar;

    private GhostCarEffect ghostEffect;

    private bool dropStarted = false;

    private void OnTriggerEnter(Collider other)
    {
        if (dropStarted)
            return;

        RCC_CarControllerV3 car =
            other.GetComponentInParent<RCC_CarControllerV3>();

        if (car == null)
            return;

        playerCar = car;

        ghostEffect =
            playerCar.GetComponent<GhostCarEffect>();

        StartDrop();
    }

    private void StartDrop()
    {
        dropStarted = true;

        // Stop player control.
        if (playerCar != null &&
            stopPlayerDuringDrop)
        {
            playerCar.canControl = false;
        }

        // Remove the four ghosts.
        if (ghostEffect != null)
        {
            ghostEffect.DeactivateGhostEffect();
        }

        // Spawn the dropped ghost.
        if (ghostPrefab == null)
        {
            Debug.LogWarning(
                "GhostDropTrigger: Ghost Prefab is not assigned."
            );

            EnablePlayerControl();
            return;
        }

        Vector3 spawnPosition;

        Quaternion spawnRotation;

        if (spawnPoint != null)
        {
            spawnPosition =
                spawnPoint.position;

            spawnRotation =
                spawnPoint.rotation;
        }
        else
        {
            spawnPosition =
                transform.position;

            spawnRotation =
                Quaternion.identity;
        }

        GameObject spawnedGhost =
            Instantiate(
                ghostPrefab,
                spawnPosition,
                spawnRotation
            );

        StartCoroutine(
            MoveGhostAway(spawnedGhost)
        );
    }

    private IEnumerator MoveGhostAway(
        GameObject spawnedGhost)
    {
        if (spawnedGhost == null)
        {
            EnablePlayerControl();
            yield break;
        }

        // Move in the spawn point's forward direction.
        Vector3 direction;

        if (spawnPoint != null)
        {
            direction =
                spawnPoint.forward;
        }
        else
        {
            direction =
                transform.forward;
        }

        direction.Normalize();

        Vector3 targetPosition =
            spawnedGhost.transform.position +
            direction * ghostMoveDistance;

        while (spawnedGhost != null)
        {
            spawnedGhost.transform.position =
                Vector3.MoveTowards(
                    spawnedGhost.transform.position,
                    targetPosition,
                    ghostMoveSpeed *
                    Time.deltaTime
                );

            if (
                Vector3.Distance(
                    spawnedGhost.transform.position,
                    targetPosition
                ) <= 0.05f
            )
            {
                break;
            }

            yield return null;
        }

        // Remove dropped ghost.
        if (spawnedGhost != null)
        {
            Destroy(spawnedGhost);
        }

        // Small delay.
        if (controlResumeDelay > 0f)
        {
            yield return new WaitForSeconds(
                controlResumeDelay
            );
        }

        EnablePlayerControl();
    }

    private void EnablePlayerControl()
    {
        if (playerCar != null)
        {
            playerCar.canControl = true;
        }
    }

    private void OnDisable()
    {
        if (dropStarted &&
            playerCar != null)
        {
            playerCar.canControl = true;
        }
    }
}