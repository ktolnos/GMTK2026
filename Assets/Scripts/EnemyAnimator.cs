using System;
using DefaultNamespace;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

public class EnemyAnimator: MonoBehaviour
{
    private Enemy enemy;
    private SpriteAnimator spriteAnimator;

    public PlayerSprites controlled;
    
    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        spriteAnimator = GetComponent<SpriteAnimator>();
    }

    private void Update()
    {
        var sprites = controlled;
        if (enemy.isMoving)
        {
            spriteAnimator.animation = sprites.walk;
        }
        else
        {
            if(spriteAnimator == null){
                Debug.Log("EnemyAnimator: spriteAnimator is null");
            }
            spriteAnimator.animation = sprites.idle;
        }
        if(Mathf.Abs(enemy.direction.x) > 0.1f){
            spriteAnimator.spriteRenderer.flipX = enemy.direction.x < 0;
        }

    }
}