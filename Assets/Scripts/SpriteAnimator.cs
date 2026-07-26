using System;
using System.Collections;
using UnityEngine;

public class SpriteAnimator: MonoBehaviour
{
    public new Animation animation;
    public bool autoplay = true;
    public bool loop = true;
    public SpriteRenderer spriteRenderer;
    [NonSerialized] public bool pause;

    private Animation lastAnimation;
    private float animationStartTime;
    private float elapsed = 0;

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
        if(lastAnimation != animation)
        {
            animationStartTime = Time.time;
        }
        elapsed = Time.time - animationStartTime;
        spriteRenderer.sprite = animation.frames[(int)(elapsed * animation.fps) % animation.frames.Length];        
        lastAnimation = animation;
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
    public void PlayLoop()
    {
        PlayLoop(animation);
    }

    public void PlayLoop(Animation anim)
    {
        StartCoroutine(PlayLoopCoroutine(anim));
    }

    private IEnumerator PlayLoopCoroutine(Animation anim)
    {
        while (loop)
        {
            foreach (var animationFrame in anim.frames)
            {
                spriteRenderer.sprite = animationFrame;
                yield return new WaitForSeconds(1f / anim.fps);
            }
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