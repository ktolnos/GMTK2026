using System;
using TMPro;
using UnityEngine;

public class Clock : MonoBehaviour
{
    public TextMeshProUGUI clockText;

    private void Update()
    {
        var seconds = Mathf.RoundToInt((float)(GM.LoopFrames - GM.Step) * GM.LoopSeconds / GM.LoopFrames);
        clockText.text = $"0:{seconds:D2}";
    }
}
