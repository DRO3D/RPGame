//Проверка работы InputSystem
using UnityEngine;
using UnityEngine.InputSystem; // важно

public class InputBootstrap : MonoBehaviour
{
    private void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            Debug.LogWarning("Keyboard not detected by the new Input System");
        else
            Debug.Log("Keyboard OK");

        if (Gamepad.current != null)
            Debug.Log($"Gamepad: {Gamepad.current.displayName}");

        foreach (var d in InputSystem.devices)
            Debug.Log($"Device: {d.displayName} ({d.layout})");
#else
        Debug.LogError("New Input System is not enabled (Project Settings → Player → Active Input Handling).");
#endif
    }
}