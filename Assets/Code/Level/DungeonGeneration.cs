using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;


public class BSPDungeonGO : MonoBehaviour, ISpawnPointProvider
{

    [Header("Grid / World")]
    public int width = 120;
    public int height = 80;
    public float cellSize = 1f;
    public float cellHeight = 0f;

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public Transform parent;

    [Header("Placement & Scale")]
    public bool fitUnitCube = true;
    public float floorThickness = 0.2f;
    public float wallHeight = 2f;
    public float epsilonLift = 0.01f;

[Header("Player / Spawn")]
public bool spawnPlayerHere = false;    // генеротор сам спавнит/ставит игрока
public Transform player;               // если есть готовый объект игрока
public GameObject playerPrefab;        // иначе — префаб игрока
public float playerYOffset = 0.1f;
public GameObject exitMarkerPrefab;

    [Header("Doors")]
    public bool placeDoors = true;
    public GameObject doorPrefab;         // перетащи префаб двери (Pivot по центру)
    public float doorHeight = 2f;         // высота двери (если fitUnitCube=true, ширина=cellSize)
    public float doorThickness = 0.2f;    // "толщина" дверного куба

    [Header("Rooms (BSP)")]
    public int maxDepth = 4;
    public int minRoomW = 6, minRoomH = 6;
    public int padding = 1;

    [Header("Corridors")]
    public int extraLoops = 2;
    public bool carveWide = true;

    [Header("Random")]
    public int seed = 12345;

    [Header("Enemies")]
    public bool spawnEnemies = true;
    public EnemyEntry[] enemies;     // ← вот этот массив
    [Range(0,1)] public float enemyProbNearStart = 0.05f;
    [Range(0,1)] public float enemyProbFar = 0.25f;
    public int enemyMinSpacing = 4;
    public int wallClearance = 1;

// НОВОЕ: кривая сложности и «размытие» подбора
[Header("Difficulty Curve")]
// t=0 (рядом со стартом) -> d=0 … t=1 (далеко) -> d=1.
// Настройте точки под свою игру.
public AnimationCurve difficultyCurve = new AnimationCurve(
    new Keyframe(0f, 0f, 0f, 2f),   // сначала дольше остаёмся в «лёгком»
    new Keyframe(0.6f, 0.35f),
    new Keyframe(1f, 1f)
);

// чем меньше, тем точнее подбираем врага к целевому d
[Range(0.05f, 0.6f)] public float difficultySigma = 0.25f;


    [Header("Loot")]
    public bool spawnLoot = true;
    public GameObject[] lootPrefabs;
    [Range(0, 1)] public float lootProbNearStart = 0.15f;
    [Range(0, 1)] public float lootProbFar = 0.08f;
    public int lootMinSpacing = 5;

    // internal
    private System.Random rng;
    private bool[,] floor;
    private int[,] roomId;                 // -1: не комната (коридор/стена), >=0: индекс комнаты
    private List<RectInt> rooms;
    private List<Vector2Int> roomCenters;
    private readonly List<GameObject> spawned = new();

    // cached difficulty
    private Vector2Int startCell, exitCell;
    private int[,] distFromStart;
    private int maxReachableDist = 1;

    [Header("NavMap")]

    public NavMeshSurface navMeshSurface;

    void OnValidate()
    {
        width = Mathf.Max(8, width);
        height = Mathf.Max(8, height);
        maxDepth = Mathf.Clamp(maxDepth, 1, 10);
        cellSize = Mathf.Max(0.1f, cellSize);
        minRoomW = Mathf.Max(3, minRoomW);
        minRoomH = Mathf.Max(3, minRoomH);
        padding = Mathf.Max(0, padding);
        floorThickness = Mathf.Max(0.01f, floorThickness);
        wallHeight = Mathf.Max(0.1f, wallHeight);
        doorHeight = Mathf.Max(0.1f, doorHeight);
        doorThickness = Mathf.Max(0.02f, doorThickness);
        enemyMinSpacing = Mathf.Max(0, enemyMinSpacing);
        lootMinSpacing = Mathf.Max(0, lootMinSpacing);
        wallClearance = Mathf.Clamp(wallClearance, 0, 5);
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        if (!floorPrefab || !wallPrefab)
        { Debug.LogWarning("Assign floorPrefab & wallPrefab."); return; }

        // root & cleanup
        var rootName = $"Dungeon_{seed}";
        if (parent == null)
        {
            var go = GameObject.Find(rootName) ?? new GameObject(rootName);
            parent = go.transform;
        }
        else
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.GetChild(i).gameObject);
        }
        foreach (var go in spawned) if (go) DestroyImmediate(go);
        spawned.Clear();

        rng = new System.Random(seed);
        floor = new bool[width, height];
        roomId = new int[width, height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) roomId[x, y] = -1;

        rooms = new List<RectInt>();
        roomCenters = new List<Vector2Int>();

        // 1) BSP
        var root = new Node(new RectInt(0, 0, width, height));
        SplitRecursive(root, maxDepth);

        // 2) Rooms
        int currentRoomIndex = 0;
        foreach (var leaf in root.GetLeaves())
        {
            int maxW = leaf.area.width - padding * 2;
            int maxH = leaf.area.height - padding * 2;
            if (maxW < minRoomW || maxH < minRoomH) continue;

            int rw = rng.Next(minRoomW, maxW + 1);
            int rh = rng.Next(minRoomH, maxH + 1);

            int minX = leaf.area.xMin + padding;
            int maxX = leaf.area.xMax - padding - rw; if (maxX < minX) maxX = minX;
            int minY = leaf.area.yMin + padding;
            int maxY = leaf.area.yMax - padding - rh; if (maxY < minY) maxY = minY;

            int rx = rng.Next(minX, maxX + 1);
            int ry = rng.Next(minY, maxY + 1);

            var room = new RectInt(rx, ry, rw, rh);
            rooms.Add(room);
            FillRect(room, true, currentRoomIndex);

            roomCenters.Add(new Vector2Int(
                Mathf.FloorToInt(room.center.x),
                Mathf.FloorToInt(room.center.y)));

            currentRoomIndex++;
        }

        if (roomCenters.Count < 2)
        { Debug.LogWarning("Not enough rooms; tweak params."); return; }

        // 3) Corridors (MST + loops)
        var edges = CompleteGraph(roomCenters);
        var mst = KruskalMST(roomCenters.Count, edges);
        foreach (var e in mst) CarveCorridor(roomCenters[e.u], roomCenters[e.v]);
        for (int i = 0; i < extraLoops && edges.Count > 0; i++)
        { var e = edges[rng.Next(edges.Count)]; CarveCorridor(roomCenters[e.u], roomCenters[e.v]); }

        // 4) Walls
        var walls = ComputeWalls(floor);

        // 5) Spawn tiles
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (floor[x, y]) SpawnFloor(x, y);
        foreach (var w in walls) SpawnWall(w.x, w.y);

        // 6) Start/Exit & difficulty
        ChooseStartExit(out startCell, out exitCell);
        distFromStart = BFSDistances(startCell, out maxReachableDist);

        // --- Player spawn ---
        var spawnPos = GetPlayerSpawn();
        var spawnRot = Quaternion.LookRotation(GetPlayerForward(), Vector3.up);

        if (spawnPlayerHere)
        {
            if (player != null)
            {
                player.SetPositionAndRotation(spawnPos, spawnRot);
            }
            else if (playerPrefab != null)
            {
                var go = Instantiate(playerPrefab, spawnPos, spawnRot, parent); // или без parent, как удобнее
                go.tag = "Player";
                spawned.Add(go);
                player = go.transform; // чтобы дальше ссылки были
            }
        }
        // 7) Doors
        if (placeDoors && doorPrefab != null)
            PlaceDoors();

        // 8) Enemies / Loot
        if (spawnEnemies && enemies != null && enemies.Length > 0)
        {
            navMeshSurface.BuildNavMesh();
            SpawnEnemies();
        }
        if (spawnLoot && lootPrefabs != null && lootPrefabs.Length > 0)
            SpawnLoot();

        Debug.Log($"Dungeon generated: rooms={rooms.Count}, cells={(width * height)}");
    }

    // ---------- Spawning helpers ----------
    Vector3 GridToWorld(int gx, int gy, float y) => new Vector3(gx * cellSize, y, gy * cellSize);

    public Vector3 GetPlayerSpawn()
    {
        // центр стартовой клетки, чуть над полом
        return GridToWorld(
            startCell.x, startCell.y,
            cellHeight + floorThickness + playerYOffset
        );
    }

    public Vector3 GetPlayerForward()
    {
        // смотрим от старта к выходу; если совпали — вперёд по миру
        var dir = new Vector3(exitCell.x - startCell.x, 0f, exitCell.y - startCell.y);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        return dir.normalized;
    }

    void SpawnFloor(int gx, int gy)
    {
        float y = cellHeight + floorThickness * 0.5f;
        var pos = GridToWorld(gx, gy, y);
        var go = Instantiate(floorPrefab, pos, Quaternion.identity, parent);
        if (fitUnitCube) go.transform.localScale = new Vector3(cellSize, floorThickness, cellSize);
        spawned.Add(go);
    }

    void SpawnWall(int gx, int gy)
    {
        float y = cellHeight + floorThickness + wallHeight * 0.5f + epsilonLift;
        var pos = GridToWorld(gx, gy, y);
        var go = Instantiate(wallPrefab, pos, Quaternion.identity, parent);
        if (fitUnitCube) go.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);
        spawned.Add(go);
    }

    // ---------- Door placement ----------
    void PlaceDoors()
    {
        // дверь ставим на клетке коридора, прилегающей к комнате,
        // где у клетки два противоположных соседа-пола и минимум один сосед — комната, а другой — некомната.
        for (int x = 1; x < width - 1; x++)
            for (int y = 1; y < height - 1; y++)
            {
                if (!floor[x, y]) continue;

                bool isRoomHere = roomId[x, y] >= 0;
                // интересует «коридорная» клетка
                if (isRoomHere) continue;

                int ridN = roomId[x, y + 1];
                int ridS = roomId[x, y - 1];
                int ridE = roomId[x + 1, y];
                int ridW = roomId[x - 1, y];

                bool nsOpen = floor[x, y + 1] && floor[x, y - 1];
                bool ewOpen = floor[x + 1, y] && floor[x - 1, y];

                // вертикальная дверь (проход север-юг)
                bool verticalDoor = nsOpen && !ewOpen && ((ridN >= 0) ^ (ridS >= 0));
                // горизонтальная дверь (проход восток-запад)
                bool horizontalDoor = ewOpen && !nsOpen && ((ridE >= 0) ^ (ridW >= 0));

                if (!(verticalDoor || horizontalDoor)) continue;

                // Спавним дверь
                float yWorld = cellHeight + floorThickness + doorHeight * 0.5f + epsilonLift;
                var pos = GridToWorld(x, y, yWorld);
                var rot = verticalDoor ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

                var door = Instantiate(doorPrefab, pos, rot, parent);
                if (fitUnitCube)
                    door.transform.localScale = new Vector3(doorThickness, doorHeight, cellSize); // по Z вдоль прохода
                spawned.Add(door);
            }
    }

    // ---------- Enemies ----------
    void SpawnEnemies()
{
    var used = new bool[width, height]; // занято врагом

    for (int x = 0; x < width; x++)
    for (int y = 0; y < height; y++)
    {
        if (!IsGoodSpawnCell(x, y)) continue;

        // 0..1 по расстоянию от старта
        float t = Difficulty01(x, y);
        // целевая сложность по кривой
        float d = Mathf.Clamp01(difficultyCurve.Evaluate(t));

        // вероятность появления в этой точке (чем дальше — тем выше)
        float p = Mathf.Lerp(enemyProbNearStart, enemyProbFar, t);
        if (rng.NextDouble() > p) continue;

        if (TooCloseToTaken(used, x, y, enemyMinSpacing)) continue;

        var prefab = ChooseEnemyPrefab(d);
        if (prefab == null) continue;

        float yWorld = cellHeight + floorThickness + epsilonLift;
        if (NavMesh.SamplePosition(GridToWorld(x, y, yWorld), out NavMeshHit hit, 2f, NavMesh.AllAreas))
            { 
                var go = Instantiate(prefab, hit.position, Quaternion.identity, parent);

                var agent = go.GetComponent<NavMeshAgent>();
                if (agent != null) agent.Warp(hit.position);

                spawned.Add(go);
                used[x, y] = true;
            }else
    {
        Debug.LogWarning("Нет валидного NavMesh для спауна врага");
    }
    }
}


    GameObject ChooseEnemyPrefab(float targetDifficulty)
{
    if (enemies == null || enemies.Length == 0) return null;
    double sigma2 = difficultySigma * difficultySigma * 2.0;
    double sum = 0.0;
    double[] weights = new double[enemies.Length];

    for (int i = 0; i < enemies.Length; i++)
    {
        var e = enemies[i];
        if (e == null || e.prefab == null) { weights[i] = 0; continue; }
        double delta = e.difficulty - targetDifficulty;
        double w = e.baseWeight * Math.Exp(-(delta * delta) / sigma2);
        weights[i] = w; sum += w;
    }
    if (sum <= 0.0) // fallback
    {
        int best = -1; float bestDelta = float.PositiveInfinity;
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i]; if (e == null || e.prefab == null) continue;
            float d = Mathf.Abs(e.difficulty - targetDifficulty);
            if (d < bestDelta) { bestDelta = d; best = i; }
        }
        return best >= 0 ? enemies[best].prefab : null;
    }
    double r = rng.NextDouble() * sum;
    for (int i = 0; i < enemies.Length; i++) { r -= weights[i]; if (r <= 0.0) return enemies[i].prefab; }
    return enemies[^1].prefab;
}


    // ---------- Loot ----------
    void SpawnLoot()
    {
        var used = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!IsGoodSpawnCell(x, y)) continue;

                float t = Difficulty01(x, y);
                float p = Mathf.Lerp(lootProbNearStart, lootProbFar, t); // ближе к старту чаще
                if (rng.NextDouble() > p) continue;

                if (TooCloseToTaken(used, x, y, lootMinSpacing)) continue;

                var prefab = lootPrefabs[rng.Next(lootPrefabs.Length)];
                float yWorld = cellHeight + floorThickness + epsilonLift;
                var go = Instantiate(prefab, GridToWorld(x, y, yWorld), Quaternion.identity, parent);
                spawned.Add(go);
                used[x, y] = true;
            }
    }

    bool IsGoodSpawnCell(int x, int y)
    {
        if (!floor[x, y]) return false;
        // избегаем стен по периметру
        int wallCount = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (!InBounds(nx, ny) || !floor[nx, ny]) wallCount++;
            }
        return wallCount <= wallClearance; // чем меньше, тем дальше от стен
    }

    bool TooCloseToTaken(bool[,] used, int x, int y, int spacing)
    {
        for (int dx = -spacing; dx <= spacing; dx++)
            for (int dy = -spacing; dy <= spacing; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (!InBounds(nx, ny)) continue;
                if (Mathf.Abs(dx) + Mathf.Abs(dy) <= spacing && used[nx, ny]) return true;
            }
        return false;
    }

    float Difficulty01(int x, int y)
    {
        int d = distFromStart[x, y];
        if (d < 0) return 1f;
        return Mathf.Clamp01(maxReachableDist > 0 ? (float)d / maxReachableDist : 0f);
        // можно заменить на кривую сложности, если захочешь
    }

    // ---------- BSP ----------
    class Node
    {
        public RectInt area;
        public Node left, right;
        public Node(RectInt a) { area = a; }
        public bool IsLeaf => left == null && right == null;
        public IEnumerable<Node> GetLeaves()
        {
            if (IsLeaf) { yield return this; yield break; }
            foreach (var n in left.GetLeaves()) yield return n;
            foreach (var n in right.GetLeaves()) yield return n;
        }
    }

    void SplitRecursive(Node n, int depth)
    {
        if (depth <= 0 ||
            n.area.width < (minRoomW + padding * 2 + 2) ||
            n.area.height < (minRoomH + padding * 2 + 2)) return;

        bool splitVert = rng.NextDouble() < 0.5;
        if (n.area.width / (float)n.area.height > 1.25f) splitVert = true;
        if (n.area.height / (float)n.area.width > 1.25f) splitVert = false;

        if (splitVert)
        {
            int minCut = 3, maxCut = n.area.width - 3;
            if (maxCut <= minCut) return;
            int cut = rng.Next(minCut, maxCut);
            n.left = new Node(new RectInt(n.area.xMin, n.area.yMin, cut, n.area.height));
            n.right = new Node(new RectInt(n.area.xMin + cut, n.area.yMin, n.area.width - cut, n.area.height));
        }
        else
        {
            int minCut = 3, maxCut = n.area.height - 3;
            if (maxCut <= minCut) return;
            int cut = rng.Next(minCut, maxCut);
            n.left = new Node(new RectInt(n.area.xMin, n.area.yMin, n.area.width, cut));
            n.right = new Node(new RectInt(n.area.xMin, n.area.yMin + cut, n.area.width, n.area.height - cut));
        }
        SplitRecursive(n.left, depth - 1);
        SplitRecursive(n.right, depth - 1);
    }

    // ---------- Rooms & Corridors ----------
    void FillRect(RectInt r, bool val, int id)
    {
        for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
                if (InBounds(x, y)) { floor[x, y] = val; roomId[x, y] = id; }
    }

    void CarveCorridor(Vector2Int a, Vector2Int b)
    {
        bool xFirst = rng.NextDouble() < 0.5;
        if (xFirst) { CarveLineX(a.x, b.x, a.y); CarveLineY(a.y, b.y, b.x); }
        else { CarveLineY(a.y, b.y, a.x); CarveLineX(a.x, b.x, b.y); }
    }

    void CarveLineX(int x0, int x1, int y)
    {
        if (x0 == x1) { CarveCell(x0, y); return; }
        int s = Math.Sign(x1 - x0);
        for (int x = x0; x != x1 + s; x += s) CarveCell(x, y);
    }
    void CarveLineY(int y0, int y1, int x)
    {
        if (y0 == y1) { CarveCell(x, y0); return; }
        int s = Math.Sign(y1 - y0);
        for (int y = y0; y != y1 + s; y += s) CarveCell(x, y);
    }
    void CarveCell(int x, int y)
    {
        if (!InBounds(x, y)) return;
        floor[x, y] = true;             // коридор — не комната
        // roomId не трогаем -> остаётся -1
        if (carveWide)
        {
            if (InBounds(x + 1, y)) floor[x + 1, y] = true;
            if (InBounds(x, y + 1)) floor[x, y + 1] = true;
        }
    }

    // ---------- Graph / MST ----------
    struct Edge { public int u, v; public float w; public Edge(int u, int v, float w) { this.u = u; this.v = v; this.w = w; } }
    List<Edge> CompleteGraph(List<Vector2Int> pts)
    {
        var list = new List<Edge>();
        for (int i = 0; i < pts.Count; i++)
            for (int j = i + 1; j < pts.Count; j++)
                list.Add(new Edge(i, j, Vector2Int.Distance(pts[i], pts[j])));
        list.Sort((a, b) => a.w.CompareTo(b.w));
        return list;
    }
    List<Edge> KruskalMST(int n, List<Edge> edges)
    {
        var ds = new DSU(n);
        var res = new List<Edge>();
        foreach (var e in edges) if (ds.Union(e.u, e.v)) { res.Add(e); if (res.Count == n - 1) break; }
        return res;
    }
    class DSU
    {
        int[] p, r;
        public DSU(int n) { p = new int[n]; r = new int[n]; for (int i = 0; i < n; i++) { p[i] = i; r[i] = 0; } }
        public int Find(int x) { return p[x] == x ? x : (p[x] = Find(p[x])); }
        public bool Union(int a, int b) { a = Find(a); b = Find(b); if (a == b) return false; if (r[a] < r[b]) (a, b) = (b, a); p[b] = a; if (r[a] == r[b]) r[a]++; return true; }
    }

    // ---------- Walls detection ----------
    List<Vector2Int> ComputeWalls(bool[,] grid)
    {
        var walls = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y]) continue;
                bool nearFloor = false;
                for (int dx = -1; dx <= 1 && !nearFloor; dx++)
                    for (int dy = -1; dy <= 1 && !nearFloor; dy++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (InBounds(nx, ny) && grid[nx, ny]) nearFloor = true;
                    }
                if (nearFloor) walls.Add(new Vector2Int(x, y));
            }
        return walls;
    }

    bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    // ---------- Start/Exit + BFS ----------
    void ChooseStartExit(out Vector2Int start, out Vector2Int exit)
    {
        if (exitMarkerPrefab != null)
            {
                var posExit = GridToWorld(exitCell.x, exitCell.y, cellHeight + floorThickness + epsilonLift);
                spawned.Add(Instantiate(exitMarkerPrefab, posExit, Quaternion.identity, parent));
            }
        int best = -1; start = roomCenters[0]; exit = roomCenters[^1];
        foreach (var s in roomCenters)
        {
            var d = BFSDistances(s, out int maxD);
            foreach (var t in roomCenters)
            {
                int dist = d[t.x, t.y];
                if (dist >= 0 && dist > best) { best = dist; start = s; exit = t; }
            }
        }
    }

    int[,] BFSDistances(Vector2Int src, out int maxD)
    {
        var dist = new int[width, height];
        for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) dist[x, y] = -1;
        maxD = 0;
        if (!InBounds(src.x, src.y) || !floor[src.x, src.y]) return dist;

        var q = new Queue<Vector2Int>();
        q.Enqueue(src);
        dist[src.x, src.y] = 0;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            for (int k = 0; k < 4; k++)
            {
                int nx = p.x + dx[k], ny = p.y + dy[k];
                if (!InBounds(nx, ny) || !floor[nx, ny] || dist[nx, ny] != -1) continue;
                dist[nx, ny] = dist[p.x, p.y] + 1;
                if (dist[nx, ny] > maxD) maxD = dist[nx, ny];
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return dist;
    }

    void Start()
    {
        Generate();
    }
}

[Serializable]                // только атрибут сериализации
public class EnemyEntry       // без : MonoBehaviour / : ScriptableObject / : Component
{
    public GameObject prefab;
    [Range(0f,1f)] public float difficulty = 0f;
    public float baseWeight = 1f;
}
