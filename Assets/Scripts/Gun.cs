using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System;

public class Gun: MonoBehaviour
{
    public Rigidbody2D bulletPrefab;
    public float stepsBetweenBullets = 2;
    public float bulletSpeed = 20f;
    public bool mokriyUron = false;

    public SpriteAnimator.Animation shootAnimation;
    public SpriteAnimator targetAnimator;

    [NonSerialized] public float lastShotStep = -1000;
    public bool isAnimating;

    public void Shoot(Vector2 direction)
    {
        if (isAnimating || GM.Step < lastShotStep + stepsBetweenBullets)
        {
            return;
        }
        if (shootAnimation != null && shootAnimation.frames.Length > 0) {
            StartCoroutine(ShootAnimated(direction));
        } else {
            ShootImpl(direction);
        }
    }

    private void ShootImpl(Vector2 direction)
    {
        Vector3 pos = transform.position;
        if (mokriyUron)
        {
            pos.z = 5;
        }
        var bullet = Instantiate(bulletPrefab, pos, transform.rotation);

        if (bullet.bodyType == RigidbodyType2D.Dynamic)
        {
            bullet.linearVelocity = direction.normalized * bulletSpeed;
        }
        lastShotStep = GM.Step;
    }


    private IEnumerator ShootAnimated(Vector2 direction) {
        isAnimating = true;
        targetAnimator.pause = true;
        for (int i = 0; i < shootAnimation.frames.Length; i++)
        {
            targetAnimator.spriteRenderer.sprite = shootAnimation.frames[i];
            yield return new WaitForFixedUpdate();
        }
        targetAnimator.pause = false;
        ShootImpl(direction);
        isAnimating = false;
    }
    
}