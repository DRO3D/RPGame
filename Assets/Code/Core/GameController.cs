// Assets/Code/Core/GameController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1000)]
public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("Refs")]
    public Health playerHealth;
    public string playerTag = "Player";
    public string enemyTag = "Enemy";

    [Header("Match State")]
    public int enemyKills = 0;

    public System.Action<int> OnKillsChanged;
    public System.Action OnPlayerDied;
    public System.Action OnMatchReset;

    readonly HashSet<Health> trackedEnemies = new();

    bool isPlayerDead = false;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

       
    }

    void OnDestroy()
    {
        if (playerHealth) playerHealth.OnDied -= HandlePlayerDied;
        foreach (var e in trackedEnemies) if (e) e.OnDied -= HandleEnemyDied;
        if (Instance == this) Instance = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;

    }

    void Start()
    {
        TryBindPlayer();
        RegisterExistingEnemies();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isPlayerDead = false;
        Time.timeScale = 1f;

        trackedEnemies.Clear();
        TryBindPlayer();
        RegisterExistingEnemies();

        enemyKills = 0;
        OnKillsChanged?.Invoke(enemyKills);
        OnMatchReset?.Invoke();
    }

    void TryBindPlayer()
    {
        if (!playerHealth)
        {
            var playerGo = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGo) playerHealth = playerGo.GetComponent<Health>();
        }
        if (playerHealth)
        {
            playerHealth.OnDied -= HandlePlayerDied; // на всякий случай
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    // НОВОЕ: публичная привязка игрока с подпиской
    public void BindPlayer(Health h)
    {
        if (playerHealth) playerHealth.OnDied -= HandlePlayerDied;
        playerHealth = h;
        if (playerHealth) playerHealth.OnDied += HandlePlayerDied;
    }

    // ---- Регистрация врагов ----
    public void AutoRegister(Health h)
    {
        if (!h || h.IsDead) return;

        if (h == playerHealth || h.teamTag == playerTag || h.CompareTag(playerTag))
        {
            if (!playerHealth) BindPlayer(h); // <<< теперь через BindPlayer
            return;
        }

        if (h.teamTag == enemyTag || h.CompareTag(enemyTag))
            RegisterEnemy(h);
    }

    public void RegisterEnemy(Health enemy)
    {
        if (!enemy || enemy.IsDead || trackedEnemies.Contains(enemy)) return;
        trackedEnemies.Add(enemy);
        enemy.OnDied += HandleEnemyDied;
    }

    void RegisterExistingEnemies()
    {
        var enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        foreach (var go in enemies)
            if (go.TryGetComponent(out Health h)) RegisterEnemy(h);
    }

    // ---- Колбэки смертей ----
    void HandleEnemyDied(Health victim, GameObject killer)
    {
        if (trackedEnemies.Remove(victim))
        {
            enemyKills++;
            OnKillsChanged?.Invoke(enemyKills);
        }
    }

    void HandlePlayerDied(Health _, GameObject killer)
    {
        OnPlayerDied?.Invoke();
        isPlayerDead = true;
        Time.timeScale = 0f;
        Debug.Log("Player died.");

    }

    // ---- Перезапуск ----
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
    }
    
}
