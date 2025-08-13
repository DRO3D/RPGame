// Assets/Code/Core/Bootstrap.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;   // <- для v3

[DefaultExecutionOrder(-1000)]
public class Bootstrap : MonoBehaviour
{

// Найти/создать CameraRig (Cinemachine Camera v3), повесить на игрока и проставить TrackingTarget
[SerializeField] GameObject cameraRigPrefab;                // задай в инспекторе (префаб с Cinemachine Camera)
[SerializeField] Vector3 rigLocalOffset = new(0, 2.5f, -5); // оффсет относительно игрока
[SerializeField] Vector3 rigLocalEuler  = Vector3.zero;
    [SerializeField] string firstSceneName = "Dungeon";

    [Header("Optional Prefabs (fallbacks)")]
    [SerializeField] GameObject gameControllerPrefab;  // можно не задавать, если GC уже в сцене/другом ДДОЛ
    [SerializeField] GameObject playerPrefab;          // заспавним, если в сцене нет игрока
    // в классе Bootstrap
    [SerializeField] Vector3 fallbackCamOffset = new Vector3(0, 2.5f, -5f);


    static Bootstrap _instance;

    void Awake()
    {
        if (_instance && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    IEnumerator Start()
    {
        // 1) Загрузка из автосейва (если есть)
        if (SaveManager.Load(out var save) && !string.IsNullOrEmpty(save.sceneName))
        {
            if (!CanLoad(save.sceneName))
            {
                Debug.LogError($"[Bootstrap] Scene '{save.sceneName}' is not in Build Settings. Falling back to '{firstSceneName}'.");
            }
            else
            {
                yield return SceneManager.LoadSceneAsync(save.sceneName, LoadSceneMode.Single);
                // ждём кадр, чтобы все Awake/Start отработали
                yield return null;
                SaveManager.ApplyLoadedData(save);
                yield return PostSceneInit();
                yield break;
            }
        }

        // 2) Нет сейва — грузим стартовую
        if (!CanLoad(firstSceneName))
        {
            Debug.LogError($"[Bootstrap] Scene '{firstSceneName}' is not in Build Settings.");
            yield break;
        }
        yield return SceneManager.LoadSceneAsync(firstSceneName, LoadSceneMode.Single);
        yield return null;                 // дать сцене проинициализироваться
        yield return PostSceneInit();      // страховочный пост-инит
    }

    // --- после загрузки сцены, безопасный инит
    // ---- после загрузки сцены
    // ---- после загрузки сцены
    IEnumerator PostSceneInit()
    {
        // 0) дать сцене стартануть
        yield return null;

        // 1) GameController
        if (GameController.Instance == null && gameControllerPrefab != null)
        {
            var gc = Instantiate(gameControllerPrefab);
            gc.name = "[GameController]";
            DontDestroyOnLoad(gc);
            yield return null;
            Debug.Log("[Bootstrap] GameController created.");
        }

        // 2) Подождём генератор (до 2 сек)
        BSPDungeonGO provider = null;
        float t = 0f;
        while ((provider = FindObjectOfType<BSPDungeonGO>()) == null && t < 2f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Debug.Log($"[Bootstrap] Provider: {(provider ? "found" : "not found")}");

        // 3) Игрок
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player)
        {
            if (!playerPrefab)
            {
                Debug.LogError("[Bootstrap] playerPrefab NOT assigned — cannot spawn player.");
            }
            else
            {
                var (pos, fwd) = ResolveSpawn();         // гарантированно вернёт точку
                if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
                {
                    Debug.LogWarning("[Bootstrap] ResolveSpawn returned NaN. Using fallback (0,1,0).");
                    pos = new Vector3(0, 1, 0); fwd = Vector3.forward;
                }

                player = Instantiate(playerPrefab, pos, Quaternion.LookRotation(fwd, Vector3.up));
                player.tag = "Player";
                player.name = "[Player]";

                // гарантия, что атака есть и включена
                var combat = player.GetComponent<PlayerCombat>();
                if (combat == null) combat = player.AddComponent<PlayerCombat>();
                combat.enabled = true;

                // (опционально) если нужно — выставим дефолтный hitMask
                if (combat.hitMask == 0) combat.hitMask = ~0; // Everything, на время отладки


                Debug.Log($"[Bootstrap] Spawn Player at {pos}, dir {fwd}.");
            }
        }
        else
        {
            Debug.Log("[Bootstrap] Player already in scene.");
        }

        // 4) Связь с контроллером
    if (GameController.Instance && player)
    {
        var hp = player.GetComponent<Health>();
        if (hp) GameController.Instance.BindPlayer(hp);  
    }
        // 5) Камера: гарантируем brain и подвесим риг к игроку
        var mainCam = EnsureMainCamera();          
        AttachCameraRig(player.transform);  

    }
    
// Создать/найти основную камеру и убедиться, что на ней есть CinemachineBrain
    Camera EnsureMainCamera()
{
    var cam = Camera.main;
    if (!cam)
    {
        // берём любую активную камеру
        foreach (var c in FindObjectsOfType<Camera>(true))
            if (c.enabled) { cam = c; break; }
        if (!cam)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            cam.tag = "MainCamera";
        }
    }
    if (cam.GetComponent<CinemachineBrain>() == null)
        cam.gameObject.AddComponent<CinemachineBrain>();
    return cam;
}


void AttachCameraRig(Transform target)
{
    if (!target) return;

    // 1) найдём/создадим риг с CinemachineCamera
    CinemachineCamera cam3 =
#if UNITY_2023_1_OR_NEWER
        FindFirstObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
#else
        FindObjectOfType<CinemachineCamera>(true);
#endif
    GameObject rigGO = cam3 ? cam3.gameObject : (cameraRigPrefab ? Instantiate(cameraRigPrefab) : null);

    if (!rigGO)
    {
        // фолбэк — прицепим обычную MainCamera к игроку
        var mc = EnsureMainCamera();
        mc.transform.SetParent(target, false);
        mc.transform.localPosition = rigLocalOffset;
        mc.transform.localRotation = Quaternion.Euler(rigLocalEuler);
        Debug.LogWarning("[Bootstrap] No CameraRig, parented MainCamera to player.");
        return;
    }

    // 2) повесим риг на игрока
    var t = rigGO.transform;
    t.SetParent(target, false);
    t.localPosition = rigLocalOffset;
    t.localRotation = Quaternion.Euler(rigLocalEuler);

    // 3)CameraOrbit
    var orbit = rigGO.GetComponent<CameraOrbit>();
    if (orbit)
    {
        // если поле называется иначе — поправь имя здесь
        var f = orbit.GetType().GetField("Target") ?? orbit.GetType().GetField("target");
        if (f != null) f.SetValue(orbit, target);
        var p = orbit.GetType().GetProperty("Target");
        if (p != null && p.CanWrite) p.SetValue(orbit, target);
    }

    // 4) попытаемся выставить таргеты в CM v3 (если получится — отлично)
    cam3 ??= rigGO.GetComponent<CinemachineCamera>();
    bool cmTargetsSet = false;
    if (cam3)
        cmTargetsSet = TrySetCm3Targets(cam3, target, target); // Follow=LookAt=player

    if (!cmTargetsSet)
    {
        // 5) если не удалось — переключаемся на Manual:
        ForceManualMode(cam3);
        Debug.Log("[Bootstrap] CM3 manual mode: camera driven by rig transform (Orbit).");
    }

    cam3.enabled = true;
}

/// попытка выставить TrackingTarget / LookAtTarget в CM v3
static bool TrySetCm3Targets(CinemachineCamera cam3, Transform follow, Transform lookAt)
{
    var tProp = typeof(CinemachineCamera).GetProperty("Target");
    if (tProp == null) return false;

    var settings = tProp.GetValue(cam3);           // boxed struct
    if (settings == null) return false;

    var sType   = settings.GetType();
    var trackP  = sType.GetProperty("TrackingTarget");
    var lookAtP = sType.GetProperty("LookAtTarget");

    bool ok = false;
    if (trackP != null) { trackP.SetValue(settings, follow); ok = true; }
    if (lookAtP != null) { lookAtP.SetValue(settings, lookAt); ok = true; }

    if (ok) tProp.SetValue(cam3, settings);        // записать обратно в камеру
    return ok;
}

/// убрать компоненты, требующие Tracking Target, чтобы камера работала «по трансформу»
static void ForceManualMode(CinemachineCamera cam3)
{
    if (!cam3) return;

    // удаляем все Position/Rotation контроллеры, если есть
    var comps = cam3.GetComponents<Component>();
    foreach (var c in comps)
    {
        var type = c.GetType().FullName; // имена в CM3 начинаются с "Unity.Cinemachine.CinemachinePosition"
        if (type != null && (type.Contains("CinemachinePosition") || type.Contains("CinemachineRotation")))
        {
            Object.Destroy(c);
        }
    }
}



// Привязать риг/виртуальную камеру к игроку (v3/v2) или прицепить обычную камеру
void AttachCamera(Transform target)
{
    if (!target) return;

#if UNITY_2023_1_OR_NEWER
    var vcam3 = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>(FindObjectsInactive.Include);
#else
    var vcam3 = Object.FindObjectOfType<Cinemachine.CinemachineCamera>(true);
#endif
    if (vcam3)
    {
        // v3
        vcam3.Follow  = target;
        vcam3.LookAt  = target;
        // у некоторых шаблонов поле называется TrackingTarget — выставим и его на всякий
        var p = vcam3.GetType().GetProperty("TrackingTarget");
        if (p != null && p.CanWrite) p.SetValue(vcam3, target);
        return;
    }

    // v2
#if UNITY_2023_1_OR_NEWER
    var vcam2 = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineVirtualCamera>(FindObjectsInactive.Include);
#else
    var vcam2 = Object.FindObjectOfType<Cinemachine.CinemachineVirtualCamera>(true);
#endif
    if (vcam2)
    {
        vcam2.Follow = target;
        vcam2.LookAt = target;
        return;
    }

    // Фолбэк: прицепим обычную main-камеру к игроку
    var mainCam = Camera.main;
    if (mainCam)
    {
        Vector3 offset = new Vector3(0, 2.5f, -5f);
        mainCam.transform.SetParent(target);
        mainCam.transform.localPosition = offset;
        mainCam.transform.localRotation =
            Quaternion.LookRotation(-offset.normalized, Vector3.up);
    }
}

// универсальный поиск одного объекта (учитывая неактивные)
static T FindOne<T>() where T : UnityEngine.Object
{
#if UNITY_2023_1_OR_NEWER
    return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
    return Object.FindObjectOfType<T>(true);
#endif
}

// аккуратно выставляем свойство/поле (v2/v3 могут отличаться)
static void TrySetPropertyOrField(object obj, string name, object value)
{
    var t = obj.GetType();
    var p = t.GetProperty(name);
    if (p != null && p.CanWrite) { p.SetValue(obj, value); return; }
    var f = t.GetField(name);
    if (f != null) { f.SetValue(obj, value); }
}

    (Vector3 pos, Vector3 forward) ResolveSpawn()
{
    // 1) BSPDungeonGO (через интерфейс, если есть)
    var provider = FindObjectOfType<BSPDungeonGO>();
    if (provider != null)
    {
        try
        {
            var pos = provider.GetPlayerSpawn();
            var fwd = provider.GetPlayerForward();
            Debug.Log($"[Bootstrap] Spawn from provider: {pos}");
            return (pos, fwd);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Bootstrap] Provider spawn failed: {e.Message}");
        }
    }

    // 2) SpawnPoint в сцене (если буду использовать)
    var sp = FindObjectOfType<SpawnPoint>();
    if (sp)
    {
        Debug.Log($"[Bootstrap] Spawn from SpawnPoint: {sp.transform.position}");
        return (sp.transform.position, sp.transform.forward);
    }

    // 3) Последний шанс
    Debug.LogWarning("[Bootstrap] No provider/SpawnPoint — fallback (0,1,0).");
    return (new Vector3(0, 1, 0), Vector3.forward);
}


    bool CanLoad(string sceneName) => Application.CanStreamedLevelBeLoaded(sceneName);

    // Публичный рестарт текущей сцены
    public void Restart()
    {
        StartCoroutine(RestartRoutine());
    }

    IEnumerator RestartRoutine()
    {
        Time.timeScale = 1f;
        var active = SceneManager.GetActiveScene().name;
        yield return SceneManager.LoadSceneAsync(active, LoadSceneMode.Single);
        yield return null;
        yield return PostSceneInit();
    }
}
