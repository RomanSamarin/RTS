using System;
using UnityEngine;

/// <summary>
/// Здоровье здания. Вешается на любое здание.
/// Автоматически попадает в общий список BuildingManager.AllBuildings.
/// </summary>
[DisallowMultipleComponent]
public class BuildingHealth : MonoBehaviour
{
    [Header("Здоровье")]
    [Min(1f)]
    [Tooltip("Максимальное здоровье здания.")]
    public float maxHealth = 500f;

    [Tooltip("Уничтожать здание, когда здоровье опустится до нуля.")]
    public bool destroyOnDeath = true;

    [Header("Текущее состояние")]
    [Tooltip("Текущее здоровье. Видно в инспекторе и меняется в игре.")]
    [SerializeField] private float currentHealth = -1f;

    public float CurrentHealth => currentHealth < 0f ? maxHealth : currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;
    public bool IsDead => CurrentHealth <= 0f;

    /// <summary>Здоровье изменилось (текущее значение).</summary>
    public event Action<BuildingHealth> HealthChanged;

    /// <summary>Здание уничтожено (здоровье дошло до нуля).</summary>
    public event Action<BuildingHealth> Died;
    public event Action<BuildingHealth> Destroyed;

    private void OnDestroy()
    {
        Destroyed?.Invoke(this);
    }

    private void Awake()
    {
        FactionMember.Ensure(gameObject);
        BuildingRoadAccess.Ensure(gameObject);
        if (currentHealth < 0f)
            currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        BuildingManager.Register(this);
    }

    private void OnDisable()
    {
        BuildingManager.Unregister(this);
    }
    public void OnButtonRecovery()
    {

    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);

        if (currentHealth < 0f)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    /// <summary>Наносит урон. Возвращает фактически снятое здоровье.</summary>
    public float TakeDamage(float amount,GameObject attacker=null)
    {
        if(attacker!=null&&!FactionMember.AreEnemies(attacker,gameObject))return 0f;
        if (amount <= 0f || IsDead)
            return 0f;

        float before = CurrentHealth;
        SetHealth(CurrentHealth - amount);

        return before - CurrentHealth;
    }

    /// <summary>Лечит здание. Возвращает фактически восстановленное здоровье.</summary>
    public float Heal(float amount)
    {
        if (amount <= 0f || IsDead)
            return 0f;

        float before = CurrentHealth;
        SetHealth(CurrentHealth + amount);

        return CurrentHealth - before;
    }

    public void SetHealth(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxHealth);

        if (Mathf.Approximately(clamped, CurrentHealth))
            return;

        bool wasAlive = !IsDead;
        currentHealth = clamped;

        HealthChanged?.Invoke(this);

        if (wasAlive && IsDead)
            Die();
    }

    public void RestoreFullHealth()
    {
        SetHealth(maxHealth);
    }

    private void Die()
    {
        Died?.Invoke(this);

        if (destroyOnDeath)
            Destroy(gameObject);
    }
    void Update()
    {
        if(SaveGameManager.IsLoading)return;
    }
}
