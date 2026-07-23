using System;
using UnityEditor.Media;
using UnityEngine;

public class Health: MonoBehaviour
{
    public float maxHealth = 1f;
    public float currentHealth;
    public bool destroyOnDeath = true;
    public GameObject deathEffect;
    public float deathEffectDuration = 1f;

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
            if (deathEffect != null)
            {
                var effect = Instantiate(deathEffect, transform.position, transform.rotation);
                if (deathEffectDuration > 0f)
                {
                    Destroy(effect, deathEffectDuration);
                }
            } 
            
        }
    }
}