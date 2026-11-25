using UnityEngine;

// 2D 위에서 보는 추적 카메라.
public class CombatCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 0, -10);
    [Min(1)] public float orthographicSize = 9f;

    void Start()
    {
        // 씬에 배치한 플레이어를 자동으로 따라간다.
        if (target == null)
        {
            CombatPlayerMovement player = FindAnyObjectByType<CombatPlayerMovement>();
            if (player != null) target = player.transform;
        }
        Camera cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = orthographicSize;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        transform.rotation = Quaternion.identity;
    }
}
