using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerSelectionItem: MonoBehaviour
{
    public Button button;
    public Image image;
    public TextMeshProUGUI playerName;
    public TextMeshProUGUI playerDescription;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    public void Setup(Player player)
    {
        button.onClick.AddListener(() =>
            {
                GM.SelectPlayer(player);
                PlayerSelectionUI.I.Hide();
                GM.StartLoop();
            }
        );
        image.sprite = player.icon;
        playerName.text = player.playerName;
        playerDescription.text = player.description;
    }
}