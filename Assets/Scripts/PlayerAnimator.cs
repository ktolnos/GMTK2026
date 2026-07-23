using System;
using DefaultNamespace;
using UnityEngine;

public class PlayerAnimator: MonoBehaviour
{
    private Player player;
    private SpriteAnimator spriteAnimator;

    public PlayerSprites controlled;
    public PlayerSprites uncontrolled;
    
    private void Awake()
    {
        player = GetComponent<Player>();
        spriteAnimator = GetComponent<SpriteAnimator>();
    }

    private void Update()
    {
        var sprites = player.isControlled ? controlled : uncontrolled;
        if (player.isMoving)
        {
            spriteAnimator.animation = sprites.walk;
        }
        else
        {
            spriteAnimator.animation = sprites.idle;
        }

        spriteAnimator.spriteRenderer.flipX = player.direction == Direction.Left;
    }
}