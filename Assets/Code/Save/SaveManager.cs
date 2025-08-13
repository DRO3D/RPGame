using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class SaveData
{
    public int version = 1;
    public string sceneName;
    public PlayerDTO player;
}

[Serializable]
public class PlayerDTO
{
    public float x, y, z;
    public float health;

    public static PlayerDTO From(Transform t, float health)
        => new PlayerDTO { x = t.position.x, y = t.position.y, z = t.position.z, health = health };

    public Vector3 ToVector3() => new Vector3(x, y, z);
}

public static class SaveManager
{
    public static event Action<SaveData> OnCollectData; // слушатели добавляют свои данные в сейв
    public static event Action<SaveData> OnApplyData;   // слушателям передаём загруженные данные

    static readonly string SavePath = Path.Combine(Application.persistentDataPath, "save.json");

    // горячие клавиши для теста
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void _BootstrapHotkeys()
    {
        var go = new GameObject("[SaveManagerHotkeys]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SaveHotkeys>();
    }

    public static bool Load(out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(SavePath)) return false;
            var json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<SaveData>(json);
            return data != null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Load error: {e}");
            return false;
        }
    }

    public static void ApplyLoadedData(SaveData data)
    {
        try { OnApplyData?.Invoke(data); }
        catch (Exception e) { Debug.LogError($"[Save] Apply error: {e}"); }
    }

    public static void Save()
    {
        try
        {
            var data = new SaveData
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            };
            OnCollectData?.Invoke(data); // слушатели дополняют (игрок, инвентарь и т.д.)

            var json = JsonUtility.ToJson(data, prettyPrint: true);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, json);
            Debug.Log($"[Save] Saved: {SavePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Save] Save error: {e}");
        }
    }
}

// маленький компонент для F5/F9
class SaveHotkeys : MonoBehaviour
{
    void Update()
    {
        // F5
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
        {
            SaveManager.Save();
        }

        // F9
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            if (SaveManager.Load(out var data))
            {
                // если сцена другая, грузим её
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != currentScene)
                {
                    StartCoroutine(LoadThenApply(data));
                }
                else
                {
                    SaveManager.ApplyLoadedData(data);
                }
            }
            else
            {
                Debug.Log("[Save] No save file.");
            }
        }
    }

    IEnumerator LoadThenApply(SaveData data)
    {
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(data.sceneName);
        SaveManager.ApplyLoadedData(data);
    }
}