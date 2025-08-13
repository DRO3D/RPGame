// Assets/Code/Controllers/CameraOrbit.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class CameraOrbit : MonoBehaviour
{
    public Transform target;            // кого камера обходит (обычно игрок)
    public Vector3 offset = new Vector3(0, 12, -12); // базовый отступ
    public float sensitivity = 0.1f;    // чувствительность мыши
    public float minPitch = 20f;        // минимальный угол наклона
    public float maxPitch = 70f;        // максимальный угол наклона
    public bool invertY = true; 

    private Vector3 currentEuler;
    private PlayerInputActions input;

    void Awake()
    {
        input = new PlayerInputActions();
        input.Player.Enable();
    }

    void Start()
    {
        // начальный угол камеры совпадает с её текущим Transform
        currentEuler = transform.eulerAngles;
        // чтобы мышь не уходила за окно
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Vector2 delta = input.Player.Look.ReadValue<Vector2>() * sensitivity;

        float yFactor = invertY ? 1f : -1f;
        currentEuler.y += delta.x;
        currentEuler.x = Mathf.Clamp(currentEuler.x + delta.y * yFactor,
                                     minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(currentEuler.x, currentEuler.y, 0f);
        transform.position = target.position + rot * offset;
        transform.LookAt(target.position);
    }
}
