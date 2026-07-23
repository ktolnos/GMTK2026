using System;
using System.Collections;
using UnityEngine;

public class SpriteAnimator: MonoBehaviour
{
    public new Animation animation;
    public bool autoplay = true;
    public bool loop = true;
    public SpriteRenderer spriteRenderer;
    private bool pause;
    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        if (!loop && autoplay)
        {
            PlayOnce();
        }
    }

    private void Update()
    {
        if (animation.frames.Length == 0 || !autoplay || pause || !loop)
        {
            return;
        }
        spriteRenderer.sprite = animation.frames[(int)(Time.time *  animation.fps) % animation.frames.Length];
    }
    
    [Serializable]
    public class Animation
    {
        public Sprite[] frames;
        public float fps = 10f;
        
        public Animation(Sprite[] frames, float fps)
        {
            this.frames = frames;
            this.fps = fps;
        }
    }

    public void PlayOnce()
    {
        PlayOnce(animation);
    }

    public void PlayOnce(Animation anim)
    {
        StartCoroutine(PlayOnceCoroutine(anim));
    }

    private IEnumerator PlayOnceCoroutine(Animation anim)
    {        
        pause = true;
        foreach (var animationFrame in anim.frames)
        {
            spriteRenderer.sprite = animationFrame;
            yield return new WaitForSeconds(1f / anim.fps);
        }
        pause = false;
    }
}