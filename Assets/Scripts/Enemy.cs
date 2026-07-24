using System;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private Gun gun;
    public Transform[] waypoints;
    public float speed = 2f;
    private int waypointIndex = 0;
    private Rigidbody2D rb;
    private Player targetPlayer;
    
    private void Awake()
    {
        gun = GetComponentInChildren<Gun>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (!GM.isPlaying)
        {
            return;
        }
        var closestDistance = 10f;
        if (GM.Step % 10 == 0)
        {
            targetPlayer = null;
            foreach (var player in Player.players)
            {
                if (!player)
                {
                    continue;
                }
            
                var source = gun.transform.position;
                var target = player.rb.position + player.collider.offset;
                var distance = Vector2.Distance(source, target);
                if (distance < closestDistance)
                {
                    var hit = Physics2D.Raycast(source, target - (Vector2) source,
                                        Vector2.Distance(source, target),
                                        LayerMask.GetMask("Default"));
                    if (!hit)
                    {
                        closestDistance = distance;
                        targetPlayer = player;
                    }
                }
                
            }
        }
        

        if (targetPlayer != null)
        {
            Vector2 diff = targetPlayer.transform.position - gun.transform.position;
            gun.Shoot(diff.x > 0 ? Vector3.right : Vector3.left);
            if (Mathf.Abs(diff.y) > 0.1f || Mathf.Abs(diff.x) > 2f)
            {
                Vector2 dir;
                if (Mathf.Abs(diff.y) > 0.1f)
                {
                    dir = diff.y > 0 ? Vector3.up : Vector3.down;
                }
                else
                {
                    dir = diff.x > 0 ? Vector3.right : Vector3.left;
                }
                rb.position += dir * speed * Time.fixedDeltaTime;
            }
            
        }
        else if (waypoints.Length > 0)
        {
            var target = waypoints[waypointIndex].position;
            var direction = (target - transform.position).normalized;
            var step = speed * Time.fixedDeltaTime;
            var requiredStep = (target - transform.position).magnitude;
            if (requiredStep < step)
            {
                rb.position = target;
                waypointIndex++;
                waypointIndex %= waypoints.Length;
            }
            else
            {
                rb.position += (Vector2)direction * step;
            }
        }
    }
}