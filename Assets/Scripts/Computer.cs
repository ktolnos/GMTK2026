using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Computer : MonoBehaviour
{
    public bool isLocked = true;
    public bool isActivated = false;
    public bool playerInteractRequested;
    public List<string> unlockableByPlayers;
    public List<GameObject> destroyOnActivate;

    public SpriteAnimator spriteAnimator; 
    public Light2D light;
    public GameObject overlay;
    public float interactionTime = 0.5f;

    public BoxCollider2D triggerCollider;
    public Color overlayUnlockableColor = Color.green;
    public Color overlayLockedColor = Color.red;
    public LineRenderer lineRenderer;

    private bool canBeInteractedWith = false;
    private SpriteRenderer overlaySpriteRenderer;
    private Color overlayColor;
    private bool isInteracting = false;
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();

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
            canBeInteractedWith = unlockableByPlayers.Contains(player.name);
            if (canBeInteractedWith || !isLocked || isActivated)
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
                canBeInteractedWith = false;
                if (!isLocked || isActivated)
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
        if((canBeInteractedWith || !isLocked) && !isInteracting)
        {
            isInteracting = true;
            StartCoroutine(Activate());
            spriteAnimator.PlayLoop();
        }
        else
        {
            Debug.Log("Computer is locked");
        }
    }

    void Update()
    {
        overlaySpriteRenderer.color = overlayColor;
        if (light != null)
        {
            light.color = overlayColor;
        }
        if (playerInteractRequested && !isInteracting)
        {
            Interact();
            playerInteractRequested = false;
        }
        if(isInteracting){
            for (int i = 0; i < lineRenderers.Count; i++)
            {
                lineRenderers[i].SetPosition(0, destroyOnActivate[i].transform.position);
            }
        }
    }


    IEnumerator Activate()
    {
        foreach (var i in destroyOnActivate)
        {
            LineRenderer lr = Instantiate(lineRenderer, i.transform.position, i.transform.rotation);
            lr.SetPosition(0, i.transform.position);
            lr.SetPosition(1, transform.position);
            lineRenderers.Add(lr);
            Destroy(lr, interactionTime);   
        }
        yield return new WaitForSeconds(interactionTime);
        isActivated = true;
        isInteracting = false;
        foreach (var i in destroyOnActivate)
        {
            i.GetComponent<Health>().TakeDamage(1000);
        }
    }
}
