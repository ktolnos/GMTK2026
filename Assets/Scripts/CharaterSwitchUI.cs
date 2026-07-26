using System;
using UnityEngine;

public class CharaterSwitchUI : MonoBehaviour
{
    public CharacterItem prefab;
    public RectTransform panel;
    public RectTransform container;

    public void Start()
    {
        var numberOfPlayersUnlocked = 0;
        foreach (var player in Player.players)
        {
            if (player.isUnlocked)
            {
                var item = Instantiate(prefab, container);
                item.Bind(player);
                numberOfPlayersUnlocked++;
            }
        }
        panel.gameObject.SetActive(numberOfPlayersUnlocked > 1);
    }
}
