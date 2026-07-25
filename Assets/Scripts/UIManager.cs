using UnityEngine;

public class UIManager: MonoBehaviour
{
    public static UIManager I;
    public RectTransform resetText;
    
    private void Awake()
    {
        I = this;
        resetText.gameObject.SetActive(false);
    }

    public void ShowResetText()
    {
        resetText.gameObject.SetActive(true);
    }
}