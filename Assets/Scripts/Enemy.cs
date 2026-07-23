using System;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private Gun gun;
    public Transform[] waypoints;
    public float speed = 2f;
    private int waypointIndex = 0;
    private Rigidbody2D rb;
    
    private void Awake()
    {
        gun = GetComponentInChildren<Gun>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        var seesPlayer = false;
        Player closestPlayer = null;
        var closestDistance = float.MaxValue;
        foreach (var player in Player.players)
        {
            if (!player)
            {
                continue;
            }
            var source = gun.transform.position;
            var target = player.transform.position + (Vector3)player.collider.offset;
            var hit = Physics2D.Raycast(source, target - source,
                Vector2.Distance(source, target),
                LayerMask.GetMask("Default"));
            if (!hit)
            {
                seesPlayer = true;
                var distance = Vector2.Distance(player.transform.position, gun.transform.position);
                if (distance < closestDistance)
                {
                    closestPlayer = player;
                    closestDistance = distance;
                }
            }
            else
            {
                Debug.Log(hit.transform.gameObject.name + " for player " + player.name);
                Debug.DrawLine(source, target, Color.red, 1f);
            }
        }

        if (seesPlayer)
        {
            gun.Shoot(closestPlayer.transform.position - gun.transform.position);
        }
        else if (waypoints.Length > 0)
        {
            var target = waypoints[waypointIndex].position;
            var direction = (target - transform.position).normalized;
            rb.position += (Vector2)direction * speed * Time.fixedDeltaTime;

            if (Vector2.Distance(transform.position, target) < 0.1f)
            {
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
            }
        }
    }
}