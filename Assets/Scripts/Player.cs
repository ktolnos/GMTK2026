using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public float speed = 5f;
    private InputAction movementAction;
    private Rigidbody2D rb;
    void Start()
    {
        movementAction = InputSystem.actions.FindAction("Move");
        rb = GetComponent<Rigidbody2D>();
        InputSystem.actions.Enable();
    }
    
    void FixedUpdate()
    {
        Vector2 moveInput = movementAction.ReadValue<Vector2>();
        Vector2 moveVelocity = moveInput * speed * Time.fixedDeltaTime;
        rb.position += moveVelocity;
    }
}
