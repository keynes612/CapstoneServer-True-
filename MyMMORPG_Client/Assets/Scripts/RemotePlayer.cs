using UnityEngine;
using Shared;

public class RemotePlayer : MonoBehaviour
{
    public float walkSpeed = 5f;
    public float runSpeed = 9f;

    private Vector3 _targetPos;
    private Vector2 _moveDir;
    private bool _isRunning;
    private SpriteRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    public void UpdateState(PlayerStateInfo info)
    {
        _targetPos = new Vector3(info.X, info.Y, 0f);
        _moveDir = new Vector2(info.DirX, info.DirY);
        _isRunning = info.IsRunning;

        // 화면 밖(원거리) 판정 시 렌더링 비활성화
        if (_renderer != null && Camera.main != null)
        {
            float distSq = (Camera.main.transform.position - _targetPos).sqrMagnitude;
            _renderer.enabled = distSq <= MapConfig.MidDistSq;
        }
    }

    private void Update()
    {
        // 추측 항법(Dead Reckoning) + 위치 보정
        float speed = _isRunning ? runSpeed : walkSpeed;
        _targetPos += (Vector3)(_moveDir * speed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * 15f);
    }
}