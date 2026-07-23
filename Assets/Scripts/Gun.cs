using UnityEngine;

public class Gun: MonoBehaviour
{
    public Bullet bulletPrefab;
    public float bps = 2;
    public float bulletSpeed = 20f;

    private float lastShotStep;
    
    public void Shoot(Direction direction)
    {
        var bullet = Instantiate(bulletPrefab, transform.position, transform.rotation);
        bullet.rb.linearVelocity = direction.ToVector2() * bulletSpeed;
    }
    
}