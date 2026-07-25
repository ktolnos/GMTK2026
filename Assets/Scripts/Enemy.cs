using System;
using UnityEngine;

public class Enemy : MonoBehaviour
{

    [NonSerialized] public bool isMoving;
    [NonSerialized] public Vector2 direction;

    public float speed = 2f;
    public Transform[] waypoints;
    public bool stayDuringAttack = false;
    public int attackDuration = 0;
    public bool stayStill = false;
    public float attackDistance = 10;
    public GameObject warning;

    [Header("Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private bool isHeavyFootsteps;
    [SerializeField] private float footstepDelaySeconds = .3f;

    private Gun gun;
    private int waypointIndex = 0;
    private Rigidbody2D rb;
    private Player targetPlayer;
    private Health health;
    private float nextFootstepAt = 0f;

    private void Awake()
    {
        gun = GetComponentInChildren<Gun>();
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        if (warning)
        {
            warning.SetActive(false);
        }
    }

    private void FixedUpdate()
    {
        if (!GM.isPlaying || health.stunEnd > GM.Step)
        {
            rb.linearVelocity = Vector2.zero;
            isMoving = false;
            direction = Vector2.zero;
            return;
        }
        var closestDistance = 15f;
        targetPlayer = null;
        foreach (var player in Player.players)
        {
            if (!player)
            {
                continue;
            }
        
            var source = gun.transform.position;
            var target = player.rb.position + player.collider.offset;
            var distance = Vector2.Distance(source, target);
            if (distance < closestDistance)
            {
                var hit = Physics2D.Raycast(source, target - (Vector2) source,
                                    Vector2.Distance(source, target),
                                    LayerMask.GetMask("Default"));
                if (!hit)
                {
                    closestDistance = distance;
                    targetPlayer = player;
                }
            }
            
        }
        
        var seesPlayer = targetPlayer != null;
        if (warning)
        {
            warning.SetActive(seesPlayer);
        }
        if (seesPlayer)
        {
            Vector2 diff = (Vector2)targetPlayer.transform.position + targetPlayer.collider.offset - (Vector2)gun.transform.position;
            if(closestDistance <= attackDistance){
                gun.Shoot(diff.x > 0 ? Vector3.right : Vector3.left);
            }
            if(stayDuringAttack && !stayStill)
            {
                stayStill = true;
            }
            if (Mathf.Abs(diff.y) > 0.02f || Mathf.Abs(diff.x) > 2f)
            {
                Vector2 dir;
                if (Mathf.Abs(diff.y) > 0.1f)
                {
                    dir = diff.y > 0 ? Vector3.up : Vector3.down;
                }
                else
                {
                    dir = diff.x > 0 ? Vector3.right : Vector3.left;
                }
                isMoving = true;
                rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
                direction = dir;
            }
            else
            {
                isMoving = false;
                direction = Vector2.zero;
            }
        }
        else if (waypoints.Length > 0)
        {
            var target = waypoints[waypointIndex].position;
            var direction = (target - transform.position).normalized;
            var step = speed * Time.fixedDeltaTime;
            var requiredStep = (target - transform.position).magnitude;
            if (requiredStep < step)
            {
                rb.MovePosition(target);
                waypointIndex++;
                waypointIndex %= waypoints.Length;
            }
            else
            {
                isMoving = true;
                direction = (Vector2)direction;
                rb.MovePosition(rb.position + (Vector2)direction * step);
            }
        }else{
            isMoving = false;
            direction = Vector2.zero;
        }
        if(GM.Step - gun.lastShotStep > attackDuration && !gun.isAnimating)
        {
            stayStill = false;
        }
        if (footstepAudioSource != null && isMoving && Time.time >= nextFootstepAt) {
            var groundMaterial = Level.I.GetGroundMaterialAt(transform.position);
            var footstepsAudioContainer = GM.AudioManager.GetFootstepsAudioContainer(groundMaterial, isHeavyFootsteps);
            footstepsAudioContainer.PlayOneShot(footstepAudioSource);
            nextFootstepAt = Time.time + footstepDelaySeconds;
        }
        if(stayStill)
        {
            isMoving = false;
            direction = Vector2.zero;
            rb.MovePosition(transform.position);
        }
    }
}