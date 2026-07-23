using System;
using DefaultNamespace;
using UnityEngine;

public class PlayerAnimator: MonoBehaviour
{
    private Player player;
    private SpriteAnimator spriteAnimator;

    public PlayerSprites controlled;
    
    private void Awake()
    {
        player = GetComponent<Player>();
        spriteAnimator = GetComponent<SpriteAnimator>();
    }

    private void Update()
    {
        var sprites = controlled;
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