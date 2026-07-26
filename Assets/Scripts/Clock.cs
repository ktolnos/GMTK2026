using System;
using TMPro;
using UnityEngine;

public class Clock : MonoBehaviour
{
    public TextMeshProUGUI clockText;
    public bool wholeLoop = true;
    public int maxTime = 15;
    public bool endLoopOnDestroy = false;
    [SerializeField] private float buildupAudioSeconds = 9.5f;

    private bool buildUpAudioStarted = false;

    private void Update()
    {
        int seconds;
        if (wholeLoop)
        {
            seconds = Mathf.RoundToInt((float)(GM.LoopSteps - GM.Step) * GM.LoopSeconds / GM.LoopSteps);
        }
        else
        {
            seconds = maxTime - GM.Step / GM.StepsPerSecond;
        }
        if (!buildUpAudioStarted && endLoopOnDestroy && seconds <= buildupAudioSeconds && buildupAudioSeconds > 0) 
        {
            buildUpAudioStarted = true;
            AudioManager.I.PlayBombBuildup();
        }

        clockText.text = $"0:{seconds:D2}";
    }

    private void OnDestroy()
    {
        if (endLoopOnDestroy && GM.isPlaying && GM.I != null && buildupAudioSeconds > 0)
        {
            GM.I.TriggerFinalExplosion();
        }
    }
}
