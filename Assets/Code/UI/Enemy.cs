// Assets/Code/AI/EnemyAI.cs
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, Return }

    [Header("Links")]
    public Transform player;                 // перетащи Player
    public LayerMask obstacleMask = ~0;      // что мешает линии зрения (стены и т.п.)

    [Header("Perception")]
    public float sightRange = 12f;           // дальность зрения
    [Range(1f, 180f)] public float sightAngle = 70f; // половина угла (конус)
    public float hearRange = 4f;             // “слух”: если игрок близко — увидим даже вне конуса
    public float memoryTime = 2.0f;          // сколько секунд “помним” последнюю позицию цели

    [Header("Combat")]
    public float attackRange = 1.75f;
    public float attackCooldown = 1.0f;
    public int damage = 10;

    [Header("Patrol")]
    public Transform[] waypoints;            // точки патруля
    public float waypointTolerance = 0.4f;
    public bool loopPatrol = true;

    [Header("Debug")]
    public bool drawGizmos = true;

    NavMeshAgent agent;
    State state;
    int wpIndex;
    float attackCdLeft;
    float forgetTimer;
    Vector3 lastSeenPos;
    Vector3 spawnPos;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        spawnPos = transform.position;
    }

    void OnEnable()
    {
        state = waypoints != null && waypoints.Length > 0 ? State.Patrol : State.Chase; // без путевых точек — сразу охотник
        GoToNextWaypoint();
    }

    void Update()
    {
        if (attackCdLeft > 0f) attackCdLeft -= Time.deltaTime;
        SetPlayer();
        TickPerception();

        switch (state)
        {
            case State.Patrol:  TickPatrol();  break;
            case State.Chase:   TickChase();   break;
            case State.Attack:  TickAttack();  break;
            case State.Return:  TickReturn();  break;
        }
    }

    // -------- Perception --------
    void SetPlayer()
    {
        if (player) return;

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go)
        {
            player = go.transform;
            // Debug.Log($"[EnemyAI] Found player: {player.name}");
        }
        // иначе тихо ждём — Bootstrap/генератор может заспавнить позже
    }

    void TickPerception()
    {
        if (!player) return;

        bool canSee = CanSeePlayer();
        bool canHear = Vector3.Distance(transform.position, player.position) <= hearRange;

        if (canSee || canHear)
        {
            lastSeenPos = player.position;
            forgetTimer = memoryTime;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= attackRange)
                state = State.Attack;
            else
                state = State.Chase;
        }
        else
        {
            if (forgetTimer > 0f)
            {
                forgetTimer -= Time.deltaTime;
                // продолжаем идти к последней известной позиции
            }
            else
            {
                if (state == State.Chase || state == State.Attack)
                    state = waypoints != null && waypoints.Length > 0 ? State.Return : State.Patrol;
            }
        }
    }

    bool CanSeePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;
        if (dist > sightRange) return false;

        Vector3 dir = toPlayer.normalized;
        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > sightAngle) return false;

        // Линия зрения
        if (Physics.Raycast(transform.position + Vector3.up * 1.2f, dir, out var hit, dist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // попали во что-то — проверим, это ли игрок
            if (!hit.transform.IsChildOf(player)) return false;
        }
        return true;
    }

    // -------- States --------
    void TickPatrol()
    {
        if (waypoints == null || waypoints.Length == 0) { state = State.Chase; return; }
        if (!agent.hasPath) agent.SetDestination(waypoints[wpIndex].position);

        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
            GoToNextWaypoint();
    }

    void TickChase()
    {
        // если игрок в памяти — бежим туда; иначе просто стоим/возвращаемся
        if (forgetTimer > 0f) agent.SetDestination(lastSeenPos);

        // плавно поворачиваемся к цели
        FaceTarget(lastSeenPos);
    }

    void TickAttack()
    {
        if (!player) { state = State.Patrol; return; }

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > attackRange * 1.1f) { state = State.Chase; return; }

        agent.ResetPath();
        FaceTarget(player.position);

        if (attackCdLeft <= 0f)
        {
            attackCdLeft = attackCooldown;
            DoAttack();
        }
    }

    void TickReturn()
    {
        // возвращаемся к ближайшей точке патруля/старту
        Vector3 target = ClosestPatrolPointOrSpawn();
        if (!agent.hasPath) agent.SetDestination(target);
        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            state = State.Patrol;
            GoToNextWaypoint(true); // начни с ближайшей актуальной
        }
    }

    // -------- Actions --------
    void DoAttack()
{
    if (!player) return;

    // можно сразу попытаться взять Health — он реализует IDamageable
    if (player.TryGetComponent<IDamageable>(out var dmg))
    {
        // ВАЖНО: source — это атакующий, т.е. этот Enemy
        dmg.ApplyDamage(damage, gameObject);
        attackCdLeft = attackCooldown;
    }
}

    void GoToNextWaypoint(bool snapToClosest = false)
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (snapToClosest)
        {
            int best = 0;
            float bestD = float.PositiveInfinity;
            for (int i = 0; i < waypoints.Length; i++)
            {
                float d = (transform.position - waypoints[i].position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            wpIndex = best;
        }

        agent.SetDestination(waypoints[wpIndex].position);
        wpIndex++;
        if (wpIndex >= waypoints.Length) wpIndex = loopPatrol ? 0 : waypoints.Length - 1;
    }

    Vector3 ClosestPatrolPointOrSpawn()
    {
        if (waypoints == null || waypoints.Length == 0) return spawnPos;
        int best = 0; float bestD = float.PositiveInfinity;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float d = (transform.position - waypoints[i].position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return waypoints[best].position;
    }

    void FaceTarget(Vector3 worldPos)
    {
        Vector3 dir = (worldPos - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 8f);
    }

    // -------- Gizmos --------
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(0,1,0,0.2f);
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = new Color(1,0.5f,0,0.2f);
        Gizmos.DrawWireSphere(transform.position, hearRange);
        // конус зрения
        Vector3 f = transform.forward;
        Quaternion q1 = Quaternion.AngleAxis(+sightAngle, Vector3.up);
        Quaternion q2 = Quaternion.AngleAxis(-sightAngle, Vector3.up);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, (q1 * f) * sightRange);
        Gizmos.DrawRay(transform.position, (q2 * f) * sightRange);
    }
}
