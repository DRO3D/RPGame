using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]   // <— добавь это
public class PlayerCombat : MonoBehaviour
{
public PlayerStats stats;
public int swordBonusDamage = 10; 

[Header("Links")]
public Transform aimFrom;


    [Header("Melee Attack")]
    public int damage = 20;
    [Tooltip("Дальность удара по земле (по XZ)")]
    public float range = 5f;
    [Tooltip("Радиус 'толщины' дуги")]
    public float hitRadius = 0.75f;
    [Tooltip("Полу-угол сектора атаки, градусы")]
    [Range(5, 180)] public float arc = 60f;

    [Tooltip("Перезарядка атаки, сек")]
    public float attackCooldown = 0.4f;
    [Tooltip("Короткий толчок вперёд во время удара")]
    public float lungeDistance = 0.6f;
    public float lungeTime = 0.08f;

    [Header("VFX / SFX (optional)")]
    public GameObject swingVfxPrefab;
    public AudioClip swingSfx;
    public AudioClip hitSfx;
    public float sfxVolume = 0.6f;

    // internals
    CharacterController cc;
    InputAction _attackAction;
    bool canAttack = true;


     AudioSource _audio;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        // гарантированно получаем AudioSource (компонент будет добавлен из-за RequireComponent)
        _audio = GetComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;

        if (!stats) stats = GetComponent<PlayerStats>();

        SetupAttackAction();
        
    }

    void OnEnable()
{
    _attackAction?.Enable();
}


    void OnDisable()
{
    _attackAction?.Disable();
}

void SetupAttackAction()
{
    if (_attackAction != null) return;

    _attackAction = new InputAction("Attack", InputActionType.Button);
    _attackAction.AddBinding("<Mouse>/leftButton");
    _attackAction.AddBinding("<Gamepad>/rightTrigger");   // RT
    _attackAction.AddBinding("<Keyboard>/space");         // пробел – как запасной
    _attackAction.performed += ctx => TryAttack();
}

    void Update()
    {
        
    }

    void OnAttackPerformed(InputAction.CallbackContext _)
    {
        TryAttack();
    }

    void TryAttack()
    {
        if (!canAttack) return;
        Debug.Log("ATTACK!");
        StartCoroutine(AttackRoutine());
    }

    public LayerMask hitMask;  // по умолчанию – все слои
public bool debugHits = false;   // для отладки

readonly Collider[] _hits = new Collider[32];
readonly HashSet<object> _hitSet = new();  // чтобы не бить цель дважды

IEnumerator AttackRoutine()
{
    canAttack = false;

    // лёгкий рывок вперёд
    var forward = GetAimForward();
    yield return StartCoroutine(Lunge(forward));

    // VFX/SFX
    if (swingVfxPrefab) Instantiate(swingVfxPrefab, transform.position + Vector3.up, Quaternion.LookRotation(forward, Vector3.up));
    if (swingSfx) _audio.PlayOneShot(swingSfx, sfxVolume);

        Debug.Log("Allert");

        // ---- ХИТ СВИП ----
        _hitSet.Clear();                              // ВАЖНО: чистим на каждый удар
    int count = Physics.OverlapSphereNonAlloc(
        transform.position + Vector3.up * 0.9f,
        range + hitRadius,
        _hits,
        hitMask,
        QueryTriggerInteraction.Collide
    );
        Debug.Log("count: " + count);
    var myRoot = transform.root;                  // чтобы исключить СЕБЯ
    
int CalcMeleeDamage()
{
    int baseDmg = damage;
    float mult = (stats ? stats.meleeDamageMult : 1f);
    int flat = 0;
    if (stats && stats.hasSword) flat += swordBonusDamage + stats.swordFlatBonus;
    return Mathf.Max(0, Mathf.RoundToInt(baseDmg * mult) + flat);
}


        for (int i = 0; i < count; i++)
        {
            var col = _hits[i];
            if (!col) continue;

            // не бьём свои коллайдеры/дочерние
            if (col.transform.root == myRoot) continue;

            // (по желанию) сектор/дистанция
            Vector3 to = col.transform.position - transform.position;
            to.y = 0;
            if (to.magnitude > range + hitRadius) continue;
            if (Vector3.Angle(forward, to.normalized) > arc) continue;

            // 1) Health на самом объекте
            if (col.TryGetComponent<Health>(out var h))
            {
                if (_hitSet.Add(h))
                {
                    h.ApplyDamage(CalcMeleeDamage(), gameObject);                // <- даём УРОН врагу
                    if (hitSfx) _audio.PlayOneShot(hitSfx, sfxVolume);
                    if (col.attachedRigidbody) col.attachedRigidbody.AddForce(forward * 3f, ForceMode.Impulse);
                }
                continue;
            }

            // 1b) Health на родителе
            var hParent = col.GetComponentInParent<Health>();
            if (hParent && _hitSet.Add(hParent))
            {
                hParent.ApplyDamage(CalcMeleeDamage(), gameObject);
                if (hitSfx) _audio.PlayOneShot(hitSfx, sfxVolume);
                continue;
            }

            // 2) Любой IDamageable (если есть другой имплементатор)
            if (col.TryGetComponent<IDamageable>(out var dmg) && _hitSet.Add(dmg))
            {
                dmg.ApplyDamage(CalcMeleeDamage(), gameObject);                   // твой интерфейс с (int, GameObject)
                if (hitSfx) _audio.PlayOneShot(hitSfx, sfxVolume);
            }
            else if (col.GetComponentInParent<IDamageable>() is IDamageable dmgParent && _hitSet.Add(dmgParent))
            {
                dmgParent.ApplyDamage(CalcMeleeDamage(), gameObject);
                if (hitSfx) _audio.PlayOneShot(hitSfx, sfxVolume);
            }

            Debug.Log($"Hit cand: {col.name}, dist={to.magnitude:F2}, angle={Vector3.Angle(forward, to.normalized):F1}");

        }


    
    yield return new WaitForSeconds(attackCooldown);
    canAttack = true;
}


    IEnumerator Lunge(Vector3 forward)
    {
        if (lungeDistance <= 0f || lungeTime <= 0f) yield break;

        float t = 0f;
        while (t < lungeTime)
        {
            // простая ease-out кривая
            float k = 1f - (t / lungeTime);
            float step = (lungeDistance * k) * Time.deltaTime / lungeTime;
            cc.Move(forward * step);
            t += Time.deltaTime;
            yield return null;
        }
    }

    Vector3 GetAimForward()
    {
        var src = aimFrom ? aimFrom : transform;
        Vector3 f = src.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = transform.forward;
        return f.normalized;
    }

    // чтобы видеть зону удара в редакторе
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.2f);
        Vector3 pos = transform.position + Vector3.up * 0.9f;
        Gizmos.DrawWireSphere(pos, range + hitRadius);

        Vector3 f = (aimFrom ? aimFrom.forward : transform.forward);
        f.y = 0f; f.Normalize();
        Quaternion q1 = Quaternion.AngleAxis(+arc, Vector3.up);
        Quaternion q2 = Quaternion.AngleAxis(-arc, Vector3.up);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, q1 * f * range);
        Gizmos.DrawRay(pos, q2 * f * range);
    }
}
