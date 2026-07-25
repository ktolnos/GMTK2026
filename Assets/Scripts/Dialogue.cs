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
   public Color newCharacterTextColor = Color.white;
   public GameObject DialogMenu;


   private int index = 0;
   private InputAction interactAction;
   private Player player;
   private bool isOpen = false;
   private bool pendingOpen = false;
   private bool isTyping = false;
   private Sprite playerSprite;
   private Color playerTextColor;

   void Start()
   {
    player = GetComponent<Player>();
    textComponent.text = string.Empty;
    interactAction = InputSystem.actions.FindAction("Interact");   
   }

   void FixedUpdate()
   {
    if (!isOpen || isTyping) return;
    if(interactAction.WasPressedThisFrame())
    {
      NextSentence();
    }
   }

   void LateUpdate()
   {
    if (pendingOpen) { isOpen = true; pendingOpen = false; }
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
      playerTextColor = dialogueStarter.textColor;
      textComponent.color = playerTextColor;
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
    DialogMenu.SetActive(false);
   }

   IEnumerator TypeSentence(string sentence)
   {
        if (replica[index].playerSide)
        {
            dialogueImage.sprite = playerSprite;
            textComponent.color = playerTextColor;
        }else{
            dialogueImage.sprite = newCharacterSprite;
            textComponent.color = newCharacterTextColor;
        }
        isTyping = true;
        foreach (char letter in sentence.ToCharArray())
        {
            textComponent.text += letter;
            yield return new WaitForSecondsRealtime(typingSpeed);
        }
        isTyping = false;
   }
   
}

