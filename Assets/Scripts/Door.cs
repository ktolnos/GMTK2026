using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Door : MonoBehaviour
{
    public bool isLocked = true;
    public bool isOpen = false;
    public List<string> unlockableByPlayers;

    public SpriteAnimator spriteAnimator; 
    public Light2D light;
    public ShadowCaster2D shadowCaster;
    public GameObject top;
    public GameObject overlay;
    public float openTime = 0.5f;

    public BoxCollider2D triggerCollider;
    public BoxCollider2D wallCollider;
    public Color overlayUnlockableColor = Color.green;
    public Color overlayLockedColor = Color.red;

    [SerializeField] private AudioContainer doorOpenAudio;
    [SerializeField] private AudioSource doorOpenSource;

    private bool canBeUnlocked = false;
    private SpriteRenderer overlaySpriteRenderer;
    private Color overlayColor;
    private bool isOpening = false;
    private bool interactRequested = false;
    public bool needsPower = false;
    private bool hasPower;

    void Start()
    {
        spriteAnimator = GetComponent<SpriteAnimator>();
        overlaySpriteRenderer = overlay.GetComponent<SpriteRenderer>();
        
        StartCoroutine(InteractChecker());
    }
    
    void FixedUpdate()
    {
        if (canBeUnlocked || !isLocked || isOpen)
        {
            overlayColor = overlayUnlockableColor;
        }
        else
        {
            overlayColor = overlayLockedColor;
        }
        if (needsPower) {
            hasPower = Level.I.IsPowered(transform.position) || 
                Level.I.IsPowered(transform.position + Vector3.left) ||
                Level.I.IsPowered(transform.position + Vector3.right);
            if (!hasPower)
            {
                overlayColor = Color.black;
            }
        }

        wallCollider.enabled = !isOpen;
        if (shadowCaster != null)
        {
            shadowCaster.enabled = !isOpen;
        }
        overlaySpriteRenderer.color = overlayColor;
        if (light != null)
        {
            light.color = overlayColor;
        }
        top.SetActive(isOpen);
        canBeUnlocked = isOpen;
        interactRequested = false;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (needsPower && !hasPower) {
            return;
        }
        if (other.TryGetComponent(out Player player))
        {
            interactRequested |= other.GetComponent<Player>().lastInteractStep >= GM.Step - 10;
            canBeUnlocked |= unlockableByPlayers.Contains(player.name);
        }
    }


    IEnumerator Open()
    {
        doorOpenAudio.Play(doorOpenSource);
        yield return new WaitForSeconds(openTime);
        isOpen = true;
        isOpening = false;
    }

    IEnumerator InteractChecker()
    {
        while (true)
        {
            if (interactRequested && (canBeUnlocked || !isLocked) && !isOpening && !isOpen)
            {
                isOpening = true;
                spriteAnimator.PlayOnce();
                yield return StartCoroutine(Open());
            }
            yield return new WaitForFixedUpdate();
        }
    }
}
