// Assets/Code/Player/PlayerRanged.cs
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class PlayerRanged : MonoBehaviour
{
    [Header("Links")]
    public Transform firePoint;                 // откуда летит стрела (нос камеры/рук)
    public Transform aimFrom;                   // направление (камера/риг)
    public GameObject arrowPrefab;

    [Header("Damage/Fire")]
    public int baseDamage = 12;
    public float fireCooldown = 0.35f;

    [Header("Arrow")]
    public float arrowSpeed = 30f;
    public float arrowLife = 5f;
    public LayerMask hitMask = ~0;

    [Header("SFX")]
    public AudioClip shootSfx;
    public float sfxVolume = 0.6f;

    PlayerStats stats;
    AudioSource audioSrc;
    float cd;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        audioSrc = GetComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 1f;
    }

    void Update()
    {
        if (cd > 0f) cd -= Time.deltaTime;

        // Лук должен быть получен
        if (!stats || !stats.hasBow) return;

        // Простое управление: ПКМ/RightTrigger/К
        bool shoot =
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);

        if (shoot && cd <= 0f)
        {
            Fire();
            cd = fireCooldown;
        }
    }

    void Fire()
    {
        if (!arrowPrefab) return;

        Vector3 pos = firePoint ? firePoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 dir = (aimFrom ? aimFrom.forward : transform.forward);
        dir.y = 0f; if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        var go = Instantiate(arrowPrefab, pos, Quaternion.LookRotation(dir, Vector3.up));
        var proj = go.GetComponent<ArrowProjectile>();
        if (proj)
        {
            int dmg = Mathf.RoundToInt(baseDamage * (stats ? stats.rangedDamageMult : 1f));
            proj.Init(dmg, gameObject, dir * arrowSpeed, arrowLife, hitMask);
        }

        if (audioSrc && shootSfx) audioSrc.PlayOneShot(shootSfx, sfxVolume);
    }
}
