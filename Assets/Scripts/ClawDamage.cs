using UnityEngine;

/// <summary>
/// Put this on each claw object that has a Collider (set the collider to "Is Trigger").
/// Deals damage to the player once per attack swing, only while the monster is attacking.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ClawDamage : MonoBehaviour
{
    [SerializeField] private MonsterAI monster;
    [SerializeField] private float damage = 2f;

    private int lastHitAttackId = -1;

    private void Awake()
    {
        if (monster == null)
            monster = GetComponentInParent<MonsterAI>();

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other) { TryHit(other); }
    private void OnTriggerStay(Collider other) { TryHit(other); }

    private void TryHit(Collider other)
    {
        if (monster == null || !monster.DamageActive)
            return;

        // One hit per claw per swing.
        if (lastHitAttackId == monster.AttackId)
            return;

        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null)
            return;

        lastHitAttackId = monster.AttackId;
        health.TakeDamage(damage);
    }
}