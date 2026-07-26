using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using System;


[Serializable]
public class DialogueLine{
    public string text;
    public bool playerSide;
}


public class Dialogue : MonoBehaviour
{
    public TextMeshProUGUI textComponent;
    public Image dialogueImage;
    public DialogueLine[] replica;
    public float typingSpeed;
    public Sprite newCharacterSprite;
    public Color newCharacterBackgroundColor = Color.white;
    public GameObject DialogMenu;
    public Image backgroundImage;

    private int index = 0;
    private InputAction interactAction;
    private Player player;
    private bool isOpen = false;
    private bool pendingOpen = false;
    private bool isTyping = false;
    private Sprite playerSprite;
    private Color backgroundColor;

    void Start()
    {
        player = GetComponent<Player>();
        textComponent.text = string.Empty;
        interactAction = InputSystem.actions.FindAction("Interact");   
    }

    void FixedUpdate()
    {
        if (!isOpen) return;
        if (interactAction.WasPressedThisFrame())
        {
            AudioManager.I.PlayUIAltClick();
            if (isTyping)
            {
                StopAllCoroutines();
                textComponent.text = replica[index].text;
                isTyping = false;
                return;
            }
            NextSentence();
        }
    }

    void LateUpdate()
    {
        if (pendingOpen) 
        { 
            isOpen = true; 
            pendingOpen = false; 
        }
    }

    public void StartDialogue(DialogStarter dialogueStarter)
    {
        index = 0;
        isOpen = false;
        pendingOpen = true; 
        StopAllCoroutines();
        textComponent.text = string.Empty;
        DialogMenu.SetActive(true);
        StartCoroutine(TypeSentence(replica[index].text));
        playerSprite = dialogueStarter.playerSprite;
        backgroundColor = dialogueStarter.backgroundColor;
        backgroundImage.color = backgroundColor;
        dialogueImage.sprite = playerSprite;
    }

   void NextSentence()
   {
      if (index < replica.Length - 1)
      {
         index++;
         StopAllCoroutines();
         textComponent.text = string.Empty;
         StartCoroutine(TypeSentence(replica[index].text));
      }
      else
      {
         EndDialogue();
      }
   }

    void EndDialogue(){
        isOpen = false;
        pendingOpen = false;
        isTyping = false;
        GM.isPlaying = true;
        Time.timeScale = 1;
        GM.ResetLoop();
        DialogMenu.SetActive(false);
    }

    IEnumerator TypeSentence(string sentence)
    {
        if (replica[index].playerSide)
        {
            dialogueImage.sprite = playerSprite;
            backgroundImage.color = backgroundColor;
        } else {
            dialogueImage.sprite = newCharacterSprite;
            backgroundImage.color = newCharacterBackgroundColor;
        }
        isTyping = true;
        textComponent.text = sentence;
        textComponent.maxVisibleCharacters = 0;
        for (var i = 0; i < sentence.Length; i++)
        {
            textComponent.maxVisibleCharacters = i;
            AudioManager.I.PlayUITyping();
            yield return new WaitForSecondsRealtime(typingSpeed);
        }
        textComponent.maxVisibleCharacters = sentence.Length;
        isTyping = false;
    }
}
