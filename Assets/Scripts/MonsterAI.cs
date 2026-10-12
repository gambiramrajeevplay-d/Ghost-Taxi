using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Monster brain (Animator-parameter version).
/// Idle while the player is out of range. Runs at the player inside detection range,
/// attacks inside attack range, and dies after a set time (death anim, then destroyed).
/// Put this on the monster root (the object with the Animator).
/// The target is found with the "Player" tag.
/// </summary>
[RequireComponent(typeof(Animator))]
public class MonsterAI : MonoBehaviour
{
    private enum State { Idle, Chase, Attack, Dead }

    [Header("Target")]
    [Tooltip("Leave empty: the object tagged 'Player' is found automatically.")]
    [SerializeField] private Transform target;

    [Header("Ranges")]
    [SerializeField] private float detectionRange = 25f;
    [SerializeField] private float attackRange = 4f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 9f;
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("Extra Y rotation if the model doesn't face forward (try 90, -90 or 180).")]
    [SerializeField] private float rotationOffset = 0f;

    [Header("Attack")]
    [Tooltip("Time between damaging swings while the player stays in attack range. Match it to the attack clip length.")]
    [SerializeField] private float attackCooldown = 1.2f;
    [Tooltip("How long the claws can deal damage after each swing starts.")]
    [SerializeField] private float damageWindow = 0.8f;

    [Header("Lifetime")]
    [Tooltip("Seconds before the death animation plays.")]
    [SerializeField] private float lifeTime = 10f;
    [Tooltip("True = the countdown starts when the monster first sees the player. False = starts at spawn.")]
    [SerializeField] private bool startTimerOnDetection = true;
    [Tooltip("Seconds after death starts before the monster is destroyed.")]
    [SerializeField] private float destroyDelay = 3f;

    [Header("Animator Parameters (names must match the Animator window)")]
    [Tooltip("Bool: true while running.")]
    [SerializeField] private string runParam = "IsRunning";
    [Tooltip("Bool: true while attacking.")]
    [SerializeField] private string attackParam = "IsAttacking";
    [Tooltip("Trigger: fires the death animation.")]
    [SerializeField] private string dieParam = "Die";

    /// <summary>True while the claws are allowed to hurt the player.</summary>
    public bool DamageActive => state == State.Attack && Time.time < damageUntil;

    /// <summary>Increases with every new swing. Claws use it to hit once per swing.</summary>
    public int AttackId { get; private set; }

    public bool IsDead => state == State.Dead;

    /// <summary>True while the monster is running at or attacking the player.</summary>
    public bool IsChasing => state == State.Chase || state == State.Attack;

    /// <summary>All active monsters. The camera uses this to frame chasing monsters.</summary>
    public static readonly List<MonsterAI> All = new List<MonsterAI>();

    private Animator animator;
    private int runHash, attackHash, dieHash;
    private State state = State.Idle;
    private bool timerRunning;
    private float lifeTimer;
    private float damageUntil;
    private float nextSwingTime;
    private float nextFindTime;

    private void OnEnable()
    {
        if (!All.Contains(this))
            All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();

        runHash = Animator.StringToHash(runParam);
        attackHash = Animator.StringToHash(attackParam);
        dieHash = Animator.StringToHash(dieParam);

        lifeTimer = lifeTime;
        timerRunning = !startTimerOnDetection;

        ValidateParameter(runParam, AnimatorControllerParameterType.Bool);
        ValidateParameter(attackParam, AnimatorControllerParameterType.Bool);
        ValidateParameter(dieParam, AnimatorControllerParameterType.Trigger);
    }

    private void Start()
    {
        SetState(State.Idle, true);
    }

    private void Update()
    {
        if (state == State.Dead)
            return;

        // Find the player by tag (retry twice a second until found).
        if (target == null)
        {
            if (Time.time >= nextFindTime)
            {
                nextFindTime = Time.time + 0.5f;
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }

            if (target == null)
            {
                SetState(State.Idle);
                return;
            }
        }

        // Lifetime countdown -> death.
        if (timerRunning)
        {
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f)
            {
                Die();
                return;
            }
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // Player out of range: idle.
        if (dist > detectionRange)
        {
            SetState(State.Idle);
            return;
        }

        // First time the player is in range: start the lifetime countdown.
        if (startTimerOnDetection && !timerRunning)
            timerRunning = true;

        FaceDirection(toTarget);

        if (dist <= attackRange)
        {
            SetState(State.Attack);

            // Start a new damaging swing every cooldown while the player is close.
            if (Time.time >= nextSwingTime)
            {
                AttackId++;
                damageUntil = Time.time + damageWindow;
                nextSwingTime = Time.time + attackCooldown;
            }
        }
        else
        {
            SetState(State.Chase);
            transform.position += toTarget.normalized * (moveSpeed * Time.deltaTime);
        }
    }

    private void SetState(State newState, bool force = false)
    {
        if (state == newState && !force) return;
        state = newState;

        switch (newState)
        {
            case State.Idle:
                animator.SetBool(runHash, false);
                animator.SetBool(attackHash, false);
                break;

            case State.Chase:
                animator.SetBool(attackHash, false);
                animator.SetBool(runHash, true);
                break;

            case State.Attack:
                animator.SetBool(runHash, false);
                animator.SetBool(attackHash, true);
                // The first swing lands right away.
                nextSwingTime = 0f;
                break;
        }
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up)
                          * Quaternion.Euler(0f, rotationOffset, 0f);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void Die()
    {
        if (state == State.Dead) return;
        state = State.Dead;

        // Claws stop hurting the player.
        foreach (ClawDamage claw in GetComponentsInChildren<ClawDamage>())
            claw.enabled = false;

        animator.SetBool(runHash, false);
        animator.SetBool(attackHash, false);
        animator.SetTrigger(dieHash);

        Destroy(gameObject, destroyDelay);
    }

    private void ValidateParameter(string paramName, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == paramName && p.type == type)
                return;
        }

        Debug.LogError(
            "MonsterAI: Animator is missing " + type + " parameter '" + paramName + "'.", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}