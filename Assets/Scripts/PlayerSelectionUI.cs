using UnityEngine;

public class PlayerSelectionUI : MonoBehaviour
{
    public static PlayerSelectionUI I;
    public RectTransform panel;
    public PlayerSelectionItem prefab;
    
    private void Awake()
    {
        I = this;
        Hide();
    }
    
    public void Show()
    {
        panel.gameObject.SetActive(true);
        foreach (PlayerSelectionItem item in panel.GetComponentsInChildren<PlayerSelectionItem>())
        {
            Destroy(item.gameObject);
        }
        foreach (var player in Player.players)
        {
            if (player.isUnlocked)
            {
                PlayerSelectionItem item = Instantiate(prefab, panel.transform);
                item.Setup(player);
            }
        }
    }
    public void Hide()
    {
        panel.gameObject.SetActive(false);
    }
}
