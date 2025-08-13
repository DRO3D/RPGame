using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationLerp = 10f;
    public Transform cameraTransform; // Оставь пустым — подцепим MainCamera

    [Header("Dash")]
    public float dashSpeed = 12f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;

    CharacterController cc;
    PlayerInputActions input;
    Vector2 moveInput;
    bool isDashing;
    float lastDashTime;
    Vector3 lastMoveWorldDir = Vector3.forward; // куда бежать, если дэш

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        input = new PlayerInputActions();
    }

    void OnEnable()
    {
        input.Player.Enable();
        input.Player.Move.performed += OnMove;
        input.Player.Move.canceled  += OnMove;
        input.Player.Dash.performed += OnDash;
    }
    void OnDisable()
    {
        input.Player.Dash.performed -= OnDash;
        input.Player.Move.performed -= OnMove;
        input.Player.Move.canceled  -= OnMove;
        input.Player.Disable();
    }

    void Start()
    {
        // если не задана — берём MainCamera
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
    }

    void OnMove(InputAction.CallbackContext ctx) => moveInput = ctx.ReadValue<Vector2>();

    void OnDash(InputAction.CallbackContext ctx)
    {
        if (!isDashing && Time.time >= lastDashTime + dashCooldown && lastMoveWorldDir.sqrMagnitude > 0.001f)
            StartCoroutine(Dash());
    }

    System.Collections.IEnumerator Dash()
    {
        isDashing = true;
        lastDashTime = Time.time;

        float t = 0f;
        while (t < dashDuration)
        {
            cc.Move(lastMoveWorldDir * dashSpeed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
        isDashing = false;
    }

    void Update()
    {
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;

        // плоские оси камеры
        Vector3 camF = Vector3.forward, camR = Vector3.right;
        if (cameraTransform)
        {
            camF = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            camR = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;
        }

        // ввод -> мировое направление
        Vector3 moveDir = camF * moveInput.y + camR * moveInput.x;
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        // запоминаем последнее ненулевое направление (для дэша)
        if (moveDir.sqrMagnitude > 0.0001f) lastMoveWorldDir = moveDir;

        // движение (без физики вниз — добавь по желанию)
        if (!isDashing)
            cc.Move(moveDir * moveSpeed * Time.deltaTime);

        // разворот в сторону движения (или в сторону камеры — если нужно, поменяй на camF)
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerp * Time.deltaTime);
        }
    }
}
