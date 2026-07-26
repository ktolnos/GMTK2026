using TMPro;
using UnityEngine;

public class Clock : MonoBehaviour
{
    public TextMeshProUGUI clockText;
    public bool wholeLoop = true;
    public int maxTime = 15;
    public bool endLoopOnDestroy = false;
    [SerializeField] private int buildupAudioSeconds = 10;
    [SerializeField] private bool tickTackSounds = false;

    private bool buildUpAudioStarted = false;
    private int prevSeconds = -1;

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
        if (seconds == prevSeconds) {
            return;
        }
        if (!buildUpAudioStarted && endLoopOnDestroy && seconds <= buildupAudioSeconds && buildupAudioSeconds > 0) 
        {
            buildUpAudioStarted = true;
            AudioManager.I.PlayBombBuildup();
        }
        if (tickTackSounds && GM.isPlaying) {
            if (seconds % 2 == 0) {
                AudioManager.I.PlayClockTick();
            } else {
                AudioManager.I.PlayClockTack();
            }
        }

        clockText.text = $"0:{seconds:D2}";
        prevSeconds = seconds;
    }

    private void OnDestroy()
    {
        if (endLoopOnDestroy && GM.isPlaying && GM.I != null && buildupAudioSeconds > 0)
        {
            GM.I.TriggerFinalExplosion();
        }
    }
}
