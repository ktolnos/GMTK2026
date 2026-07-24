using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Rigidbody2D rb;
    public float damage = 1;
    public bool destroyOnCollision = true;
    public string damageType = "physical";
    public int activeTime = 5000;
    public bool destroyAfterDeactivate = false;

    private int activeStep = -100;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        activeStep = GM.Step;
    }

    private void FixedUpdate()
    {
        if (destroyAfterDeactivate && GM.Step > activeStep + activeTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if(GM.Step > activeStep + activeTime)
        {
            return;
        }
        if (destroyOnCollision) Destroy(gameObject);
        if (collision.transform.gameObject.TryGetComponent(out Health health))
        {
            health.TakeDamage(damage, damageType);
        }
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if(GM.Step > activeStep + activeTime)
        {
            if(destroyAfterDeactivate) Destroy(gameObject);
            return;
        }
        if(rb.GetComponent<Collider2D>().isTrigger == false)
        {
            return;
        }
        if (other.gameObject.TryGetComponent(out Health health))
        {
            health.TakeDamage(damage, damageType);
        }
        if (destroyOnCollision) Destroy(gameObject);
    }
}