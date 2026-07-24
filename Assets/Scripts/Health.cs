using System;
using UnityEditor.Media;
using UnityEngine;

public class Health: MonoBehaviour
{
    public float maxHealth = 1f;
    public float currentHealth;
    public bool destroyOnDeath = true;
    public GameObject deathEffect;
    public GameObject stunEffect;
    public float deathEffectDuration = 1f;
    public float stunDuration = 1;
    public float stunEnd = 0;

    public bool triggerTimedExplosionOnDeath;

    public void Awake(){
        currentHealth = maxHealth;
    }


    public void TakeDamage(float damage, string damageType = "physical")
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
            if (triggerTimedExplosionOnDeath)
            {
                GetComponent<ExplodeAtTime>().StartExplosion();
            }
        }
        if (damageType == "mokriyUron" && stunEnd <= GM.Step)
        {
            stunEnd = GM.Step + stunDuration*50;
            if(stunEffect != null)
            {
                var effect = Instantiate(stunEffect, transform.position, transform.rotation);
                Destroy(effect, stunDuration);
            }
        }
    }
}
