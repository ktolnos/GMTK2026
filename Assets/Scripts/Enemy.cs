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
        var closestDistance = float.MaxValue;
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
                var diff = (Vector2)source - target;
                if (Mathf.Abs(diff.x) < 1.5f && Mathf.Abs(diff.y) < 5f && distance < closestDistance)
                {
                    closestDistance = distance;
                    targetPlayer = player;
                }
            }
        }
        

        if (targetPlayer != null)
        {
            gun.Shoot(targetPlayer.transform.position.x - gun.transform.position.x > 0 ? Vector3.right : Vector3.left);
            var dir = Vector2.zero;
            // if (Mathf.Abs(closestPlayer.transform.position.y - gun.transform.position.y) > 0.1f)
            // {
            //     dir = closestPlayer.transform.position.y - gun.transform.position.y > 0 ? Vector3.up : Vector3.down;
            // }
            // else
            // {
            //     dir = closestPlayer.transform.position.x - gun.transform.position.x > 0 ? Vector3.right : Vector3.left;
            // }
            // rb.position += (Vector2)dir * speed * Time.fixedDeltaTime;
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