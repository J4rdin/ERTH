using UnityEngine;

public class CombatantStats : MonoBehaviour
{
    [Header("Identity")]
    public string unitName = "Combatant";
    public bool isPlayerControlled = true;

    [Header("Core Stats")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    [Tooltip("Maximum grid tiles this unit can move per Move action.")]
    public int movementRange = 5;

    public int attackDamage = 15;
    public int attackRange = 1;

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, amount));
        Debug.Log($"[Stats] {unitName} took {amount} damage. HP: {currentHealth}/{maxHealth}");
    }
}