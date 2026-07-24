using System;
using UnityEngine;

public class Turret : MonoBehaviour
{
    public float radius = 5f;
    public LayerMask targetMask;
    public LayerMask obstacleMask;

    private Gun gun;
    public SpriteRenderer spriteRenderer;

    private void Awake()
    {
        gun = GetComponentInChildren<Gun>();
    }

    private void FixedUpdate()
    {
        ShootClosestTargetInRadius((Vector2)gun.transform.position, radius);
    }

    public void ShootClosestTargetInRadius(Vector2 position, float radius)
    {
        Collider2D[] targetsInRadius = targetMask != 0
            ? Physics2D.OverlapCircleAll(position, radius, targetMask)
            : Physics2D.OverlapCircleAll(position, radius);

        Transform closestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (var col in targetsInRadius)
        {
            if (col == null) continue;
            if (col.gameObject == gameObject || col.transform.IsChildOf(transform))
                continue;

            Vector2 targetPos = (Vector2)col.transform.position;
            float distance = Vector2.Distance(position, targetPos);

            LayerMask mask = obstacleMask != 0 ? obstacleMask : LayerMask.GetMask("Default");
            if (!Physics2D.Raycast(position, targetPos - position, distance, mask))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = col.transform;
                }
            }
        }

        if (closestTarget != null)
        {
            Vector2 direction = (Vector2)closestTarget.position - position;

            spriteRenderer.flipX = direction.x < 0;
            gun.Shoot(direction);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
