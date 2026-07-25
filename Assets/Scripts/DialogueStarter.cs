using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class DialogStarter : MonoBehaviour
{

    public Sprite playerSprite;
    public Color textColor = Color.white;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out Player player))
        {
            if (!player.isUnlocked)
            {
                GM.isPlaying = false;
                Dialogue dialogue = player.GetComponent<Dialogue>();
                dialogue.StartDialogue(this);
                player.isUnlocked = true;
            }
        }
    }
}
