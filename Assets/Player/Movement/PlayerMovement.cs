using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public ThirdPersonCamera playerCamera;
    public float walkSpeed = 4f;
    public float sprintSpeed = 7f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;

    CharacterController controller;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.GetComponent<ThirdPersonCamera>();
    }



    // Online help
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Vector2 input = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
        if (kb != null)
        {
            input.x += (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            input.y += (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        }
        bool jump = (kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        bool sprint = (kb != null && kb.leftShiftKey.isPressed) || (pad != null && pad.leftStickButton.isPressed);
#else
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        bool jump = Input.GetButtonDown("Jump");
        bool sprint = Input.GetKey(KeyCode.LeftShift);
#endif
        if (playerCamera != null)
            transform.rotation = Quaternion.Euler(0f, playerCamera.AimYaw, 0f);

        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 move = transform.forward * input.y + transform.right * input.x;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f;
            if (jump) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        verticalVelocity += gravity * Time.deltaTime;

        controller.Move((move * (sprint ? sprintSpeed : walkSpeed) + Vector3.up * verticalVelocity) * Time.deltaTime);
    }
}
