// Assets/Code/Core/BootConfig.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Boot Config", fileName = "BootConfig")]
public class BootConfig : ScriptableObject
{
    [Header("Scenes")]
    public string gameplaySceneName = "Game";      // имя основной сцены

    [Header("Prefabs")]
    public GameObject gameControllerPrefab;        // префаб с GameController
    public GameObject playerPrefab;                // префаб игрока (c Health)

    [Header("Spawn")]
    public bool useSceneSpawnPoint = true;         // искать SpawnPoint в сцене
    public Vector3 fallbackSpawn = new Vector3(0, 1, 0);
    public Vector3 fallbackForward = Vector3.forward;

    [Header("Misc")]
    public bool lockAndHideCursor = true;
}
