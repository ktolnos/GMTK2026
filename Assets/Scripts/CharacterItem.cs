using System;
using UnityEngine;
using UnityEngine.UI;

public class CharacterItem : MonoBehaviour
{
    public Image icon;
    private Player player;

    public void Bind(Player player)
    {
        icon.sprite = player.icon;
        this.player = player;
    }

    public void Update()
    {
        var alpha = player == GM.ActivePlayer ? 1f : 0.3f;
        var color = new Color(1f, 1f, 1f, alpha);
        icon.color = color;
    }
}
