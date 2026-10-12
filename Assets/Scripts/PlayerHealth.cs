using UnityEngine;
using TMPro;

/// <summary>
/// Put this on the player car root (the RCC car object).
/// Tracks HP, shows it in a TextMeshPro text, and fires an event on death.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("UI")]
    [Tooltip("Drag a TextMeshPro - Text (UI) object here.")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string format = "HP: {0}";

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onDeath;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        CurrentHealth = maxHealth;
        UpdateUI();
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        UpdateUI();

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            Debug.Log("Player died.");
            onDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (hpText != null)
            hpText.text = string.Format(format, Mathf.CeilToInt(CurrentHealth));
    }
}