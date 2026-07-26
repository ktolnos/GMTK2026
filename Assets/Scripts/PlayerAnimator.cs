using System;
using DefaultNamespace;
using UnityEngine;

public class PlayerAnimator: MonoBehaviour
{
    private Player player;
    private SpriteAnimator spriteAnimator;

    public PlayerSprites controlled;

    [Header("Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private bool isHeavyFootsteps;
    [SerializeField] private float footstepDelaySeconds = .3f;

    private float nextFootstepAt = 0f;

    private void Awake()
    {
        player = GetComponent<Player>();
        spriteAnimator = GetComponent<SpriteAnimator>();
    }

    private void Update()
    {
        var sprites = controlled;
        if (player.isMoving && GM.isPlaying)
        {
            spriteAnimator.animation = sprites.walk;
            if (Time.time >= nextFootstepAt) {
                var groundMaterial = Level.I.GetGroundMaterialAt(player.transform.position);
                var footstepsAudioContainer = AudioManager.I.GetFootstepsAudioContainer(groundMaterial, isHeavyFootsteps);
                footstepsAudioContainer.PlayOneShot(footstepAudioSource);
                nextFootstepAt = Time.time + footstepDelaySeconds;
            }
        }
        else
        {
            spriteAnimator.animation = sprites.idle;
        }
        player.transform.localScale = new Vector3(player.direction == Direction.Left ? -1 : 1, 1, 1);
    }
}