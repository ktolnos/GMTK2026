using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerSelectionItem: MonoBehaviour, IPointerEnterHandler
{
    public Button button;
    public Image image;
    public TextMeshProUGUI playerName;
    public TextMeshProUGUI playerDescription;
    
    public Player player;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    public void Setup(Player player)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
            {
                AudioManager.I.PlayUIClick();
                StartGameWithPlayer(player);
            }
        );
        this.player = player;
        image.sprite = player.icon;
        playerName.text = player.playerName;
        playerDescription.text = player.description;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GM.SelectPlayer(player);
        AudioManager.I.PlayUISelect();
    }

    public static void StartGameWithPlayer(Player player)
    {
        GM.SelectPlayer(player);
        PlayerSelectionUI.I.Hide();
        GM.StartLoop();
    }

}