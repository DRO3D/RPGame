using UnityEngine;

[RequireComponent(typeof(HealthMock))] // замените на свой компонент здоровья
public class PlayerSaveAgent : MonoBehaviour
{
    HealthMock _hp;

    void Awake()
    {
        _hp = GetComponent<HealthMock>();
        SaveManager.OnCollectData += OnCollect;
        SaveManager.OnApplyData += OnApply;
    }

    void OnDestroy()
    {
        SaveManager.OnCollectData -= OnCollect;
        SaveManager.OnApplyData  -= OnApply;
    }

    void OnCollect(SaveData data)
    {
        data.player = PlayerDTO.From(transform, _hp.Value);
    }

    void OnApply(SaveData data)
    {
        if (data.player == null) return;
        transform.position = data.player.ToVector3();
        _hp.Value = data.player.health;
    }
}

// ВРЕМЕННЫЙ мок здоровья — замените на свой компонент.
public class HealthMock : MonoBehaviour
{
    public float Value = 100f;
}
