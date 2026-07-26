using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSelectionUI : MonoBehaviour
{
    public static PlayerSelectionUI I;
    public RectTransform panel;
    public PlayerSelectionItem prefab;
    public RectTransform playersParent;

    private List<PlayerSelectionItem> items = new();
    public InputAction movement;
    public InputAction select;

    private float lastSwitchTime = -100;
    
    private void Awake()
    {
        I = this;
        Hide();
        movement = InputSystem.actions.FindAction("Move");
        select = InputSystem.actions.FindAction("Select");
    }
    
    public void Show()
    {
        var unlockedPlayers = 0;
        Player lastPlayer = null;
        panel.gameObject.SetActive(true);
        foreach (PlayerSelectionItem item in playersParent.GetComponentsInChildren<PlayerSelectionItem>())
        {
            Destroy(item.gameObject);
        }
        foreach (var player in Player.players)
        {
            if (player.isUnlocked)
            {
                PlayerSelectionItem item = Instantiate(prefab, playersParent.transform);
                item.Setup(player);
                unlockedPlayers++;
                lastPlayer = player;
                items.Add(item);
            }
        }

        if (unlockedPlayers == 1)
        {
            PlayerSelectionItem.StartGameWithPlayer(lastPlayer);
        }
    }


    public void FixedUpdate()
    {
        if (!panel.gameObject.activeSelf)
        {
            return;
        }
        var movementValue = movement.ReadValue<Vector2>();
        if (movement.WasPerformedThisFrame())
        {
            if (movementValue.x > 0.1f)
            {
                GM.I.SelectNext();
            }
            else if (movementValue.x < -0.1f)
            {
                GM.I.SelectPrevious();
            }
        }
        
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].player == GM.ActivePlayer)
            {
                items[i].button.Select();
            }
        }

        if (select.WasPerformedThisFrame())
        {
            PlayerSelectionItem.StartGameWithPlayer(GM.ActivePlayer);
        }
    }


    public void Hide()
    {
        panel.gameObject.SetActive(false);
    }
}
