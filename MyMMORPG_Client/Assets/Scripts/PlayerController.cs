using UnityEngine;
using UnityEngine.InputSystem;
using Shared;

public class PlayerController : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runSpeed = 9f;

    private float _sendTimer = 0f;
    private const float SendInterval = 0.05f; // 20Hz로 서버에 내 위치 보고

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 1. 방향키 및 Z키(달리기) 입력 (New Input System)
        float h = 0f;
        float v = 0f;
        if (keyboard.leftArrowKey.isPressed) h -= 1f;
        if (keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.downArrowKey.isPressed) v -= 1f;
        if (keyboard.upArrowKey.isPressed) v += 1f;

        Vector2 dir = new Vector2(h, v).normalized;
        bool isRunning = keyboard.zKey.isPressed;

        // 2. 클라이언트 측 즉시 이동 (반응성 확보)
        float speed = isRunning ? runSpeed : walkSpeed;
        transform.position += (Vector3)(dir * speed * Time.deltaTime);

        // 카메라가 플레이어를 따라다니도록 설정
        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(transform.position.x, transform.position.y, -10f);
        }

        // 3. C키 상호작용 (포탈 이동 요청)
        if (keyboard.cKey.wasPressedThisFrame)
        {
            NetworkManager.Instance.SendPacket(new C_InteractPacket());
        }

        // 4. 주기적으로 서버에 내 위치 전송
        _sendTimer += Time.deltaTime;
        if (_sendTimer >= SendInterval)
        {
            _sendTimer = 0f;
            NetworkManager.Instance.SendPacket(new C_MovePacket
            {
                X = transform.position.x,
                Y = transform.position.y,
                DirX = dir.x,
                DirY = dir.y,
                IsRunning = isRunning
            });
        }
    }
}