using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{
    public bool isLocked = true;
    public bool isOpen = false;
    public bool playerInteractRequested;
    public List<string> unlockableByPlayers;

    public SpriteAnimator spriteAnimator; 
    public GameObject top;
    public GameObject overlay;
    public float openTime = 0.5f;

    public BoxCollider2D triggerCollider;
    public BoxCollider2D wallCollider;
    public Color overlayUnlockableColor = Color.green;
    public Color overlayLockedColor = Color.red;

    private bool canBeUnlocked = false;
    private SpriteRenderer overlaySpriteRenderer;
    private Color overlayColor;
    private bool isOpening = false;

    void Start()
    {
        spriteAnimator = GetComponent<SpriteAnimator>();
        overlaySpriteRenderer = overlay.GetComponent<SpriteRenderer>();
        
        if (isLocked)
        {
            overlayColor = overlayLockedColor;
        }
        else
        {
            overlayColor = overlayUnlockableColor;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent(out Player player))
        {
            playerInteractRequested = other.GetComponent<Player>().lastInteractStep >= GM.Step - 10;
            canBeUnlocked = unlockableByPlayers.Contains(player.name);
            if (canBeUnlocked || !isLocked || isOpen)
            {
                overlayColor = overlayUnlockableColor;
            }
            else
            {
                overlayColor = overlayLockedColor;
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out Player player))
        {
            if (unlockableByPlayers.Contains(player.name))
            {
                canBeUnlocked = false;
                if (!isLocked || isOpen)
                {
                    overlayColor = overlayUnlockableColor;
                }
                else
                {
                    overlayColor = overlayLockedColor;
                }
            }
        }
    }

    public void Interact()
    {
        if((canBeUnlocked || !isLocked) && !isOpening)
        {
            isOpening = true;
            StartCoroutine(Open());
            spriteAnimator.PlayOnce();
        }
        else
        {
            Debug.Log("Door is locked");
        }
    }

    void Update()
    {
        wallCollider.enabled = !isOpen;
        overlaySpriteRenderer.color = overlayColor;
        top.SetActive(isOpen);
        if (playerInteractRequested && !isOpen)
        {
            Interact();
            playerInteractRequested = false;
        }
    }


    IEnumerator Open()
    {
        yield return new WaitForSeconds(openTime);
        isOpen = true;
        isOpening = false;
    }
}
