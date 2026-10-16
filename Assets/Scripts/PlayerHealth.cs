using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Health Fill")]
    [SerializeField] private Image healthFill;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onDeath;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        CurrentHealth = Mathf.Max(0f, maxHealth);
        IsDead = CurrentHealth <= 0f;

        FindHealthFill();
        UpdateHealthFill();
    }

    private void Update()
    {
        // Supports UI that is spawned after the player.
        if (healthFill == null)
        {
            FindHealthFill();

            if (healthFill != null)
                UpdateHealthFill();
        }
    }

    private void FindHealthFill()
    {
        GameObject healthObject =
            GameObject.FindGameObjectWithTag("HealthFill");

        if (healthObject != null)
            healthFill = healthObject.GetComponent<Image>();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

        UpdateHealthFill();

        Debug.Log(
            $"Player health: {CurrentHealth:0.0}/{maxHealth:0.0}"
        );

        if (CurrentHealth <= 0f)
        {
            IsDead = true;

            if (GameManager.Instance != null)
                GameManager.Instance.LoseGame();

            onDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);

        UpdateHealthFill();
    }

    private void UpdateHealthFill()
    {
        if (healthFill == null)
            return;

        healthFill.fillAmount = maxHealth > 0f
            ? Mathf.Clamp01(CurrentHealth / maxHealth)
            : 0f;
    }
}