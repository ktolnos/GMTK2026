using UnityEngine;

public class Door : MonoBehaviour
{
    public bool isLocked = true;
    public bool isOpen = false;
    public bool playerInteractRequested;

    public SpriteAnimator spriteAnimator;


    private BoxCollider2D boxCollider;
    

    void Start()
    {
        spriteAnimator = GetComponent<SpriteAnimator>();
        boxCollider = GetComponent<BoxCollider2D>();
    }

    void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player"))
        {
            playerInteractRequested = other.GetComponent<Player>().lastInteractStep >= GM.Step - 10;
        }
    }

    public void Interact()
    {
        if (isLocked)
        {
            Debug.Log("Door is locked");
        }
        else
        {
            isOpen = !isOpen;
            Debug.Log("Door is open");
            boxCollider.enabled = isOpen;

        }
    }

    void Update()
    {
        if (playerInteractRequested)
        {
            Interact();
            playerInteractRequested = false;
        }
    }

}
