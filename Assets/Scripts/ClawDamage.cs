using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ClawDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private MonsterAI monster;
    [SerializeField] private float damage = 0.5f;

    private int lastHitAttackId = -1;

    private void Awake()
    {
        if (monster == null)
            monster = GetComponentInParent<MonsterAI>();

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    private void TryHit(Collider other)
    {
        if (monster == null || !monster.DamageActive)
            return;

        // One hit per claw per attack swing.
        if (lastHitAttackId == monster.AttackId)
            return;

        PlayerHealth health =
            other.GetComponentInParent<PlayerHealth>();

        if (health == null || health.IsDead)
            return;

        lastHitAttackId = monster.AttackId;

        health.TakeDamage(damage);

        Debug.Log(
            $"Monster: {monster.name} | Damage: {damage} | " +
            $"Player HP: {health.CurrentHealth}"
        );
    }
}