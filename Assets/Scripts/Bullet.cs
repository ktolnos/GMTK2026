using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Rigidbody2D rb;
    public float damage = 1;
    public bool destroyOnCollision = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (destroyOnCollision) Destroy(gameObject);
        if (collision.transform.gameObject.TryGetComponent(out Health health))
        {
            health.TakeDamage(damage); 
        }
    }
}