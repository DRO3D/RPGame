using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    public int maxHP = 100;
    public string teamTag = "Neutral";

    public event Action<Health> OnDamaged;
    public event Action<Health, GameObject> OnDied;

    int hp;

    void Awake() => hp = maxHP;

    // ЕДИНСТВЕННЫЙ канонический метод с источником урона
    public void ApplyDamage(int amount, GameObject source)
{
    if (hp <= 0) return;

    int before = hp;
    hp = Mathf.Max(0, hp - Mathf.Max(0, amount));

    OnDamaged?.Invoke(this);

    if (hp <= 0)
    {
        hp = 0;
        OnDied?.Invoke(this, source);
        Destroy(gameObject);
    }
}

    // ЯВНАЯ реализация интерфейса — без источника
    void IDamageable.ApplyDamage(int amount, GameObject target)
    {
        Debug.Log($"{name} hit {amount} -> {hp}");
        ApplyDamage(amount, null);
    }

    public int CurrentHP => hp;
    public bool IsDead => hp <= 0;

    void OnEnable()
    {
        GameController.Instance?.AutoRegister(this);
    }
}
