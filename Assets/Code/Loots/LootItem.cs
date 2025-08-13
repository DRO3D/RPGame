// Assets/Code/Loots/LootItem.cs
using UnityEngine;

public class LootItem : MonoBehaviour
{
    public enum Kind { MeleeUp, RangedUp, Bow, Sword }

    [Header("Kind")]
    public Kind kind;

    [Header("Values")]
    [Tooltip("Для MeleeUp/RangedUp: прибавка к множителю урона, например 0.25 = +25%")]
    public float multiplierAdd = 0.25f;
    [Tooltip("Для Sword: фикс. прибавка урона")]
    public int swordFlatAdd = 10;

    [Header("FX")]
    public AudioClip pickupSfx;
    public GameObject pickupVfx;
    public float sfxVolume = 0.9f;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var stats = other.GetComponent<PlayerStats>() ?? other.GetComponentInParent<PlayerStats>();
        if (!stats) return;

        switch (kind)
        {
            case Kind.MeleeUp:
                stats.meleeDamageMult += Mathf.Max(0f, multiplierAdd);
                break;

            case Kind.RangedUp:
                stats.rangedDamageMult += Mathf.Max(0f, multiplierAdd);
                break;

            case Kind.Bow:
                stats.hasBow = true;
                // включим PlayerRanged, если есть
                var pr = other.GetComponent<PlayerRanged>() ?? other.GetComponentInParent<PlayerRanged>();
                if (pr) pr.enabled = true;
                break;

            case Kind.Sword:
                stats.hasSword = true;
                stats.swordFlatBonus += Mathf.Max(0, swordFlatAdd);
                break;
        }

        if (pickupVfx) Instantiate(pickupVfx, transform.position, Quaternion.identity);
        if (pickupSfx)
        {
            var a = other.GetComponent<AudioSource>() ?? other.gameObject.AddComponent<AudioSource>();
            a.PlayOneShot(pickupSfx, sfxVolume);
        }
        Destroy(gameObject);
    }
}
