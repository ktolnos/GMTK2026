using UnityEngine;

public class Gun: MonoBehaviour
{
    public Rigidbody2D bulletPrefab;
    public float stepsBetweenBullets = 2;
    public float bulletSpeed = 20f;

    private float lastShotStep = -100;
    
    public void Shoot(Vector2 direction)
    {
        if (GM.Step < lastShotStep + stepsBetweenBullets)
        {
            return;
        }
        var bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
        if (bullet.bodyType == RigidbodyType2D.Dynamic)
        {
            bullet.linearVelocity = direction.normalized * bulletSpeed;
        }
        lastShotStep = GM.Step;
    }
    
}