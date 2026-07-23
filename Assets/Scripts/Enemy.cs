using System;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    private Gun gun;
    
    private void Awake()
    {
        gun = GetComponentInChildren<Gun>();
    }

    private void FixedUpdate()
    {
        var seesPlayer = false;
        Player closestPlayer = null;
        var closestDistance = float.MaxValue;
        foreach (var player in Player.players)
        {
            if (!Physics2D.Raycast(gun.transform.position, 
                    player.transform.position - gun.transform.position,
                    Vector2.Distance(player.transform.position, transform.position),
                    LayerMask.GetMask("Default"))
               )
            {
                seesPlayer = true;
                var distance = Vector2.Distance(player.transform.position, gun.transform.position);
                if (distance < closestDistance)
                {
                    closestPlayer = player;
                    closestDistance = distance;
                }
            }
        }

        if (seesPlayer)
        {
            gun.Shoot(closestPlayer.transform.position - gun.transform.position);
        }
    }
}