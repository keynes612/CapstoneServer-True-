using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 공격과 몬스터 AI(감지·추적·예고 공격)를 함께 처리한다. 2D(X·Y), 앞쪽은 transform.up.
[RequireComponent(typeof(CombatHealth))]
public class CombatPlayerAttack : MonoBehaviour
{
    public enum AttackType { Melee, Ranged }
    [Header("Common")]
    public AttackType attackType;
    [Min(1)] public int damage = 20;
    // 공격 후 다음 공격까지의 시간(초). F키로 전환해도 공격 간격은 공유한다.
    [Min(0.01f)] public float attackInterval = 0.4f;
    public LayerMask hitLayers = ~0;

    [Header("Melee (local X = width, Y = reach)")]
    public Vector2 meleeOffset = new Vector2(0, 1.3f);
    // 근접 판정 상자의 전체 크기. X는 폭, Y는 앞뒤 길이.
    public Vector2 meleeSize = new Vector2(1.8f, 1.8f);

    [Header("Ranged")]
    public CombatProjectile projectilePrefab;
    // 몸 중심을 기준으로 투사체가 생성되는 위치.
    public Vector2 muzzleOffset = new Vector2(0, 0.8f);
    [Min(0.01f)] public float projectileSpeed = 12f;
    [Min(0.01f)] public float projectileRange = 15f;
    [Min(0.01f)] public float projectileDiameter = 0.35f;

    [Header("Attack range display")]
    public Color rangeColor = new Color(1, 0, 0, 0.35f);
    [Min(0.01f)] public float meleeVisualDuration = 0.1f;
    [Min(0.01f)] public float rangedVisualDuration = 0.18f;

    [Header("Monster")]
    // 플레이어를 감지하는 거리이자 노란 원의 반지름.
    [Min(0.01f)] public float detectionRange = 10f;
    [Min(0)] public float chaseSpeed = 2.5f;
    // 근접 몬스터가 접근을 멈추고 공격을 시작하는 거리. 실제 적중은 Melee Offset과 Melee Size로 판정.
    [Min(0.01f)] public float meleeAttackDistance = 1.9f;
    // 몬스터가 공격하기 전에 붉은 범위를 보여주는 시간(초).
    [Min(0.01f)] public float windupDuration = 0.45f;

    const int DisplaySortingOrder = 5;
    const int DetectionSegments = 64;

    CombatHealth health;
    CombatHealth target;
    float nextAttackTime;
    float strikeTime;
    bool windingUp;
    Material rangeMaterial;
    LineRenderer detectionRing;
    Collider2D[] ownColliders;

    void Awake()
    {
        health = GetComponent<CombatHealth>();
        ownColliders = GetComponentsInChildren<Collider2D>();
    }

    void LateUpdate()
    {
        if (health.team == CombatHealth.Team.Monster)
        {
            if (!health.IsDead) UpdateMonster();
            UpdateDetectionRing();
            return;
        }

        UpdatePlayer();
    }

    // 플레이어 입력과 조준
    void UpdatePlayer()
    {
        if (health.IsDead) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            attackType = attackType == AttackType.Melee ? AttackType.Ranged : AttackType.Melee;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Time.time < nextAttackTime) return;
        if (!AimAtMouse(mouse)) return;
        ShowRange(attackType == AttackType.Melee ? meleeVisualDuration : rangedVisualDuration);
        Strike();
    }

    bool AimAtMouse(Mouse mouse)
    {
        Camera camera = Camera.main;
        bool ranged = attackType == AttackType.Ranged;
        if (camera == null || (ranged && projectilePrefab == null)) return false;

        Vector2 screen = mouse.position.ReadValue();
        Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
            transform.position.z - camera.transform.position.z));
        Vector2 aim = (Vector2)(world - transform.position);
        // 원거리는 총구 안쪽 클릭을 건너뛰고, 근접은 방향을 정할 수 없는 몸 중심 클릭만 건너뛴다.
        float minimumDistance = ranged ? Mathf.Max(0.001f, Mathf.Abs(muzzleOffset.y)) : 0.001f;
        if (aim.sqrMagnitude <= minimumDistance * minimumDistance) return false;

        Face(aim);
        return true;
    }

    void Face(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void OnGUI()
    {
        if (health == null || health.team != CombatHealth.Team.Player) return;
        GUI.Box(new Rect(12, 12, 200, 28), $"{attackType}  |  F: Switch Attack");
    }

    // 몬스터 감지, 추적, 공격 예고
    void UpdateMonster()
    {
        // 예고가 시작되면 방향을 고정해 표시한 방향으로 공격한다.
        if (windingUp)
        {
            if (Time.time < strikeTime) return;
            windingUp = false;
            Strike();
            return;
        }

        if (target == null)
        {
            var player = FindAnyObjectByType<CombatPlayerMovement>();
            if (player == null) return;
            target = player.GetComponent<CombatHealth>();
        }
        if (target.IsDead) return;

        Vector2 direction = target.transform.position - transform.position;
        float distance = direction.magnitude;
        if (distance > detectionRange || distance < 0.001f) return;
        Face(direction);

        if (attackType == AttackType.Melee && distance > meleeAttackDistance)
        {
            float step = Mathf.Min(chaseSpeed * Time.deltaTime, distance - meleeAttackDistance);
            if (step > 0 && !Blocked(direction.normalized, step))
                transform.position += (Vector3)(direction.normalized * step);
            return;
        }

        if (Time.time < nextAttackTime ||
            (attackType == AttackType.Ranged && (projectilePrefab == null ||
             distance > projectileRange + muzzleOffset.y))) return;

        windingUp = true;
        strikeTime = Time.time + windupDuration;
        ShowRange(windupDuration);
    }

    // 몬스터 이동 경로에 다른 충돌체가 있는지 확인한다. 자기 충돌체와 트리거는 무시한다.
    bool Blocked(Vector2 direction, float step)
    {
        Vector2 size = (Vector2)transform.lossyScale * 0.9f;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(transform.position, size,
            transform.eulerAngles.z, direction, step, hitLayers);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || IsOwn(hit.collider)) continue;
            return true;
        }
        return false;
    }

    bool IsOwn(Collider2D collider)
    {
        foreach (Collider2D own in ownColliders)
            if (own == collider) return true;
        return false;
    }

    // 플레이어와 몬스터가 공유하는 공격 판정
    void Strike()
    {
        nextAttackTime = Time.time + Mathf.Max(0.01f, attackInterval);
        if (attackType == AttackType.Melee) MeleeAttack();
        else if (projectilePrefab != null)
        {
            CombatProjectile shot = Instantiate(projectilePrefab, WorldOffset(muzzleOffset), transform.rotation);
            shot.Launch(health.team, damage, projectileSpeed, projectileRange, projectileDiameter, hitLayers);
        }
    }

    Vector3 WorldOffset(Vector2 offset) => transform.position + transform.rotation * (Vector3)offset;

    void MeleeAttack()
    {
        Vector2 size = PositiveSize(meleeSize);
        Collider2D[] hits = Physics2D.OverlapBoxAll(WorldOffset(meleeOffset), size,
            transform.eulerAngles.z, hitLayers);
        var damaged = new HashSet<CombatHealth>();
        foreach (Collider2D hit in hits)
        {
            if (hit.isTrigger) continue;
            CombatHealth victim = hit.GetComponentInParent<CombatHealth>();
            if (victim != null && victim.team != health.team && damaged.Add(victim))
                victim.TakeDamage(damage);
        }
    }

    static Vector2 PositiveSize(Vector2 value) => new Vector2(
        Mathf.Max(0.01f, value.x), Mathf.Max(0.01f, value.y));

    // 게임 화면의 범위 표시
    void UpdateDetectionRing()
    {
        if (health.IsDead)
        {
            if (detectionRing != null) detectionRing.enabled = false;
            return;
        }

        if (detectionRing == null)
        {
            Material material = GetRangeMaterial();
            if (material == null) return;

            var ring = new GameObject("Monster Detection Range");
            ring.transform.SetParent(transform, false);
            detectionRing = ring.AddComponent<LineRenderer>();
            detectionRing.sharedMaterial = material;
            detectionRing.useWorldSpace = true;
            detectionRing.loop = true;
            detectionRing.positionCount = DetectionSegments;
            detectionRing.startWidth = detectionRing.endWidth = 0.07f;
            detectionRing.startColor = detectionRing.endColor = new Color(1, 0.8f, 0.15f, 0.7f);
            detectionRing.sortingOrder = DisplaySortingOrder;
            detectionRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Vector3 center = transform.position;
        // 노란 원의 반지름은 몬스터 감지에 쓰는 detectionRange와 같다.
        for (int i = 0; i < DetectionSegments; i++)
        {
            float angle = i * 2 * Mathf.PI / DetectionSegments;
            detectionRing.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * detectionRange);
        }
    }

    Material GetRangeMaterial()
    {
        if (rangeMaterial != null) return rangeMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) rangeMaterial = new Material(shader);
        return rangeMaterial;
    }

    void ShowRange(float duration)
    {
        if (GetRangeMaterial() == null) return;

        // 붉은 영역은 근접 판정 상자 또는 투사체의 폭과 사거리를 표시한다.
        bool melee = attackType == AttackType.Melee;
        Vector2 size = PositiveSize(meleeSize);
        Vector3 forward = transform.up;
        Vector3 start = melee ? WorldOffset(meleeOffset) - forward * (size.y / 2)
                              : WorldOffset(muzzleOffset);
        Vector3 end = start + forward * (melee ? size.y : Mathf.Max(0.01f, projectileRange));

        var indicator = new GameObject("Attack Range");
        var line = indicator.AddComponent<LineRenderer>();
        line.sharedMaterial = rangeMaterial;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = line.endWidth = melee ? size.x : Mathf.Max(0.01f, projectileDiameter);
        line.startColor = line.endColor = rangeColor;
        line.numCapVertices = melee ? 0 : 8;
        line.sortingOrder = DisplaySortingOrder;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(indicator, Mathf.Max(0.01f, duration));
    }

    void OnDestroy()
    {
        if (rangeMaterial != null) Destroy(rangeMaterial);
    }

    void OnDrawGizmosSelected()
    {
        if (attackType == AttackType.Melee)
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(meleeOffset, PositiveSize(meleeSize));
        }
        else
        {
            Gizmos.color = Color.cyan;
            Vector3 start = WorldOffset(muzzleOffset);
            Gizmos.DrawWireSphere(start, projectileDiameter / 2);
            Gizmos.DrawLine(start, start + transform.up * projectileRange);
        }
    }
}
