using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Computer : MonoBehaviour
{
    public bool isLocked = true;
    public bool isActivated = false;
    
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
    private bool interactRequested;

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
        StartCoroutine(InteractChecker());
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent(out Player player))
        {
            interactRequested |= other.GetComponent<Player>().lastInteractStep >= GM.Step - 10;
            canBeInteractedWith |= unlockableByPlayers.Contains(player.name);
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

    void FixedUpdate()
    {
        overlaySpriteRenderer.color = overlayColor;
        if (light != null)
        {
            light.color = overlayColor;
        }
        if (interactRequested && !isInteracting)
        {
            Interact();
            interactRequested = false;
        }
        if(isInteracting){
            for (int i = lineRenderers.Count - 1; i >= 0; i--)
                {
                    LineRenderer lr = lineRenderers[i];
                    GameObject obj = destroyOnActivate[i];
                    if (obj == null)
                    {
                        if (lr != null) Destroy(lr);
                        lineRenderers.RemoveAt(i);
                        destroyOnActivate.RemoveAt(i);
                        continue;
                    }
                    if (lr != null && obj != null)
                    {
                        lr.SetPosition(0, obj.transform.position);
                        lr.SetPosition(1, transform.position);
                    }
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
        lineRenderers.Clear();
        destroyOnActivate.Clear();
    }


    IEnumerator InteractChecker()
    {
        while (true)
        {
            if (interactRequested && (canBeInteractedWith || !isLocked) && !isInteracting && !isActivated)
            {
                isInteracting = true;
                spriteAnimator.PlayLoop();
                yield return StartCoroutine(Activate());
            }
            yield return new WaitForFixedUpdate();
        }
    }

}
