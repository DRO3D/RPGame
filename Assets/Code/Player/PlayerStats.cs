// Assets/Code/Player/PlayerStats.cs
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Damage multipliers")]
    [Min(0f)] public float meleeDamageMult = 1f;   // 1.0 = без бонуса
    [Min(0f)] public float rangedDamageMult = 1f;

    [Header("Equipment")]
    public bool hasBow = false;
    public bool hasSword = false;

    [Header("Flat bonuses")]
    public int swordFlatBonus = 0; // фикс. бонус урона от меча
}
