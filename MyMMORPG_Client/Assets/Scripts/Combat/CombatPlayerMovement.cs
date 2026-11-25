using UnityEngine;
using UnityEngine.InputSystem;

// 2D(X·Y 평면) 이동. 캐릭터의 앞쪽은 transform.up 방향이다.
[RequireComponent(typeof(Rigidbody2D), typeof(CombatHealth))]
public class CombatPlayerMovement : MonoBehaviour
{
    [Min(0)] public float moveSpeed = 5f;
    [Min(0)] public float turnSpeed = 720f;
    Rigidbody2D body;
    CombatHealth health;
    Vector2 direction;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<CombatHealth>();
        body.gravityScale = 0;
        body.freezeRotation = true;
    }

    void Update()
    {
        Keyboard keys = Keyboard.current;
        Vector2 input = Vector2.zero;
        if (!health.IsDead && keys != null)
        {
            input.x = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0)
                    - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
            input.y = (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0)
                    - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0);
        }
        direction = Vector2.ClampMagnitude(input, 1);
        if (direction.sqrMagnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, turnSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void FixedUpdate()
    {
        body.linearVelocity = direction * moveSpeed;
    }
}
