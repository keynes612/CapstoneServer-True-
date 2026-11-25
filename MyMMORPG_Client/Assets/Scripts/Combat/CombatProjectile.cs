using System;
using UnityEngine;

// 원형 캐스트로 빠른 투사체가 대상을 통과하지 않게 한다. 진행 방향은 transform.up.
public class CombatProjectile : MonoBehaviour
{
    CombatHealth.Team ownerTeam;
    int damage;
    float speed;
    float remainingRange;
    float radius;
    LayerMask hitLayers;

    public void Launch(CombatHealth.Team team, int amount, float moveSpeed, float range,
        float diameter, LayerMask layers)
    {
        ownerTeam = team;
        damage = amount;
        speed = Mathf.Max(0.01f, moveSpeed);
        remainingRange = Mathf.Max(0.01f, range);
        radius = Mathf.Max(0.01f, diameter) / 2;
        hitLayers = layers;
        transform.localScale = Vector3.one * radius * 2;
    }

    void Update()
    {
        float distance = Mathf.Min(speed * Time.deltaTime, remainingRange);
        // 대상 안에서 시작한 경우도 처리한다.
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, radius, hitLayers))
            if (TryHit(hit)) return;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, radius,
            transform.up, distance, hitLayers);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit2D hit in hits)
            if (TryHit(hit.collider)) return;

        transform.position += transform.up * distance;
        remainingRange -= distance;
        if (remainingRange <= 0) Destroy(gameObject);
    }

    bool TryHit(Collider2D hit)
    {
        if (hit == null || hit.isTrigger) return false;
        CombatHealth target = hit.GetComponentInParent<CombatHealth>();
        if (target != null && target.team == ownerTeam) return false;
        if (target != null) target.TakeDamage(damage);
        Destroy(gameObject);
        return true;
    }
}
