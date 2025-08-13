// Assets/Code/Weapons/ArrowProjectile.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ArrowProjectile : MonoBehaviour
{
    int damage;
    GameObject source;
    float life;
    Rigidbody rb;
    LayerMask mask;

    public GameObject hitVfx;

    public void Init(int dmg, GameObject src, Vector3 velocity, float lifeSeconds, LayerMask hitMask)
    {
        damage = dmg;
        source = src;
        life = lifeSeconds;
        mask = hitMask;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var col = GetComponent<Collider>();
        col.isTrigger = true; // удобнее триггером

        rb.linearVelocity = velocity;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other || other.transform.root == (source ? source.transform.root : null)) return;

        // Фильтрация по маске
        if (((1 << other.gameObject.layer) & mask.value) == 0) return;

        // Наносим урон
        if (other.TryGetComponent<Health>(out var h))
        {
            h.ApplyDamage(damage, source);
            HitFXAndDie(); return;
        }
        if (other.GetComponentInParent<Health>() is Health hp)
        {
            hp.ApplyDamage(damage, source);
            HitFXAndDie(); return;
        }
        if (other.TryGetComponent<IDamageable>(out var dmg))
        {
            dmg.ApplyDamage(damage, source);
            HitFXAndDie(); return;
        }
        if (other.GetComponentInParent<IDamageable>() is IDamageable dmgParent)
        {
            dmgParent.ApplyDamage(damage, source);
            HitFXAndDie(); return;
        }

        // если попали в стену — просто исчезаем
        HitFXAndDie();
    }

    void HitFXAndDie()
    {
        if (hitVfx) Instantiate(hitVfx, transform.position, transform.rotation);
        Destroy(gameObject);
    }
}
