using UnityEngine;

public class Gun: MonoBehaviour
{
    public Bullet bulletPrefab;
    public float stepsBetweenBullets = 2;
    public float bulletSpeed = 20f;

    private float lastShotStep;
    
    public void Shoot(Vector2 direction)
    {
        if (GM.Step < lastShotStep)
        {
            lastShotStep = -100;
        }

        if (GM.Step < lastShotStep + stepsBetweenBullets)
        {
            return;
        }
        var bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
        bullet.rb.linearVelocity = direction.normalized * bulletSpeed;
        lastShotStep = GM.Step;
    }
    
}