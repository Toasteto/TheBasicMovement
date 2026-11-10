using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-100)]
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 pivotOffset = new Vector3(0f, 0.5f, 0f);
    public float shoulderOffset = 0.6f;

    public float mouseSensitivity = 0.15f;
    public float gamepadSensitivity = 180f;
    public bool invertY = false;
    public float minPitch = -20f;
    public float maxPitch = 70f;

    public bool allowFreeLook = true;
    public float returnDuration = 0.5f;
    [Range(0f, 0.5f)] public float returnOvershoot = 0.08f;

    public bool enableSway = true;
    public float swayAmount = 0.025f;
    public float bobAmount = 0.015f;
    public float tiltingCool = 0.3f;
    public float swayFrequency = 1f;

    public float startDistance = 5f;
    public float minimumDistance = 1.5f;
    public float maximumDistance = 10f;
    public float zoomStep = 1f;
    public float zoomSmoothTime = 0.1f;

    public bool avoidObstacles = true;
    public float cameraRadius = 0.3f;
    public LayerMask obstacleLayers = ~0;

    public float AimYaw => aimYaw;

    float aimYaw, pitch = 15f, freeLookYaw, freeLookPitch;
    float returnTimer, returnStartYaw, returnStartPitch;
    bool freeLooking, returning;
    float targetDistance, currentDistance, zoomVelocity;
    float swayPhase, swayWeight;
    Vector3 lastTargetPosition;
    CharacterController targetController;
    PlayerMovement targetMovement;
    readonly RaycastHit[] hits = new RaycastHit[16];

    void Start()
    {
        targetDistance = currentDistance = Mathf.Clamp(startDistance, minimumDistance, maximumDistance);
        if (target != null)
        {
            aimYaw = target.eulerAngles.y;
            lastTargetPosition = target.position;
            targetController = target.GetComponent<CharacterController>();
            targetMovement = target.GetComponent<PlayerMovement>();
        }
        LockCursor(true);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        Vector2 stick = pad != null ? pad.rightStick.ReadValue() : Vector2.zero;
        bool freeLookHeld = (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftShoulder.isPressed);
        float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        int dpad = pad == null ? 0 : (pad.dpad.up.wasPressedThisFrame ? 1 : 0) - (pad.dpad.down.wasPressedThisFrame ? 1 : 0);
#else
        bool escape = Input.GetKeyDown(KeyCode.Escape);
        bool click = Input.GetMouseButtonDown(0);
        Vector2 mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        Vector2 stick = Vector2.zero;
        bool freeLookHeld = Input.GetMouseButton(1);
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        int dpad = 0;
#endif
        if (escape) LockCursor(false);
        else if (click) LockCursor(true);

        Vector2 look = stick * (gamepadSensitivity * Time.deltaTime);
        if (Cursor.lockState == CursorLockMode.Locked) look += mouseDelta * mouseSensitivity;
        float pitchInput = invertY ? look.y : -look.y;
        freeLookHeld &= allowFreeLook;

        if (freeLookHeld)
        {
            returning = false;
            freeLookYaw += look.x;
            freeLookPitch = Mathf.Clamp(freeLookPitch + pitchInput, minPitch - pitch, maxPitch - pitch);
        }
        else
        {
            aimYaw = Mathf.Repeat(aimYaw + look.x, 360f);
            pitch = Mathf.Clamp(pitch + pitchInput, minPitch, maxPitch);

            if (freeLooking)
            {
                returning = true;
                returnTimer = 0f;
                returnStartYaw = Mathf.DeltaAngle(0f, freeLookYaw);
                returnStartPitch = freeLookPitch;
            }
            if (returning)
            {
                returnTimer += Time.deltaTime;
                float t = returnDuration > 0f ? Mathf.Clamp01(returnTimer / returnDuration) : 1f;
                float remaining = t < 1f ? 1f - SpringEase(t) : 0f;
                freeLookYaw = returnStartYaw * remaining;
                freeLookPitch = returnStartPitch * remaining;
                returning = t < 1f;
            }
        }
        freeLooking = freeLookHeld;

        float zoom = (scroll > 0f ? 1f : scroll < 0f ? -1f : 0f) + dpad;
        targetDistance = Mathf.Clamp(targetDistance - zoom * zoomStep, minimumDistance, maximumDistance);
    }

    void LateUpdate()
    {
        if (target == null) return;

        currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref zoomVelocity, zoomSmoothTime);

        Quaternion rot = Quaternion.Euler(Mathf.Clamp(pitch + freeLookPitch, minPitch, maxPitch), aimYaw + freeLookYaw, 0f);
        Vector3 pivot = target.position + pivotOffset;

        Vector3 side = rot * (Vector3.right * Mathf.Sign(shoulderOffset));
        float sideDist = Mathf.Abs(shoulderOffset);
        if (avoidObstacles && sideDist > 0f) sideDist = ClearDistance(pivot, side, sideDist);
        Vector3 shoulder = pivot + side * sideDist;

        Vector3 back = rot * Vector3.back;
        float dist = avoidObstacles ? ClearDistance(shoulder, back, currentDistance) : currentDistance;

        Vector3 sway = Sway(out float roll);
        transform.SetPositionAndRotation(shoulder + back * dist + rot * sway, rot * Quaternion.Euler(0f, 0f, roll));
    }

    Vector3 Sway(out float roll)
    {
        float dt = Time.deltaTime;
        Vector3 moved = target.position - lastTargetPosition;
        lastTargetPosition = target.position;
        moved.y = 0f;

        float walk = targetMovement != null ? Mathf.Max(targetMovement.walkSpeed, 0.01f) : 4f;
        float speed = dt > 0f ? Mathf.Min(moved.magnitude / dt / walk, 2f) : 0f;
        bool grounded = targetController == null || targetController.isGrounded;

        swayWeight = Mathf.MoveTowards(swayWeight, enableSway && grounded ? speed : 0f, 4f * dt);
        swayPhase = Mathf.Repeat(swayPhase + speed * swayFrequency * 2f * Mathf.PI * dt, 2f * Mathf.PI);

        float s = Mathf.Sin(swayPhase);
        roll = -s * tiltingCool * swayWeight;
        return new Vector3(s * swayAmount, Mathf.Sin(swayPhase * 2f) * bobAmount, 0f) * swayWeight;
    }

    float SpringEase(float t)
    {
        float ln = Mathf.Log(Mathf.Clamp(returnOvershoot, 0.0005f, 0.9f));
        float z = -ln / Mathf.Sqrt(Mathf.PI * Mathf.PI + ln * ln);
        float root = Mathf.Sqrt(1f - z * z);
        float wd = 6f / z * root;
        float k = z / root;
        float Spring(float x) => 1f - Mathf.Exp(-6f * x) * (Mathf.Cos(wd * x) + k * Mathf.Sin(wd * x));
        return Spring(t) + (1f - Spring(1f)) * t;
    }

    float ClearDistance(Vector3 origin, Vector3 dir, float max)
    {
        int count = Physics.SphereCastNonAlloc(origin, cameraRadius, dir, hits, max, obstacleLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(target)) max = Mathf.Min(max, hits[i].distance);
        return max;
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
