using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PlayerSelectionItem: MonoBehaviour
{
    public Button button;
    public Image image;

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
        image.sprite = player.GetComponent<PlayerAnimator>().controlled.idle.frames[0];
    }
    
}