using UnityEngine;

public class Gun: MonoBehaviour
{
    public Rigidbody2D bulletPrefab;
    public float stepsBetweenBullets = 2;
    public float bulletSpeed = 20f;
    public bool mokriyUron = false;

    private float lastShotStep = -100;

    public void Shoot(Vector2 direction)
    {
        if (GM.Step < lastShotStep + stepsBetweenBullets)
        {
            return;
        }
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
    
}