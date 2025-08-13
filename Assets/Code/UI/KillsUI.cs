// Assets/Code/UI/KillsUI.cs
using TMPro;
using UnityEngine;
using System.Collections;

public class KillsUI : MonoBehaviour
{
    public TextMeshProUGUI label;   // можно не задавать — возьмём с этого объекта
    bool subscribed;

    void Awake()
    {
        if (!label) label = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        StartCoroutine(HookWhenReady());
    }

    void OnDisable()
    {
        if (subscribed && GameController.Instance != null)
            GameController.Instance.OnKillsChanged -= UpdateText;
        subscribed = false;
    }

    IEnumerator HookWhenReady()
    {
        // ждём, пока появится GameController (например, он создаётся в другой сцене или позже)
        while (GameController.Instance == null) yield return null;

        GameController.Instance.OnKillsChanged += UpdateText;
        subscribed = true;
        UpdateText(GameController.Instance.enemyKills);
    }

    void UpdateText(int v)
    {
        if (label) label.text = $"Kills: {v}";
    }
}
