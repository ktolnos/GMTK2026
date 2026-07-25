using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class GM: MonoBehaviour
{
    public static GM I;
    public static int LoopSeconds = 59;
    public static int StepsPerSecond = 50;
    public static int LoopSteps = LoopSeconds * StepsPerSecond;
    public static int Step = 0;
    private InputAction loopResetAction;
    private InputAction nextAction;
    private InputAction previousAction;
    private InputAction fastForwardAction;
    private static int activePlayerIndex;
    
    public Light2D globalLight;

    public static Player ActivePlayer => Player.players[activePlayerIndex];
    public static bool isPlaying = false;
    
    private void Awake()
    {
        Step = 0;
        isPlaying = false;
        I = this;
    }
    
    private void Start()
    {
        PlayerSelectionUI.I.Show();
    }

    private void OnEnable()
    {
        loopResetAction = InputSystem.actions.FindAction("Reset");
        nextAction = InputSystem.actions.FindAction("Next");
        previousAction = InputSystem.actions.FindAction("Previous");
        fastForwardAction =  InputSystem.actions.FindAction("Sprint");
    }

    // private void Update()
    // {
    //     if (fastForwardAction.IsPressed())
    //     {
    //         Time.timeScale = 2f;
    //         Time.fixedDeltaTime = 0.01f;
    //     }
    //     else
    //     {
    //         Time.timeScale = 1f;
    //         Time.fixedDeltaTime = 0.02f;
    //     }
    // }

    private void FixedUpdate()
    {
        if (isPlaying)
        {
            Step++;
            if (loopResetAction.WasReleasedThisFrame())
            {
                isPlaying = false;
                ResetLoop();
            }
        }
        
        if (nextAction.WasPerformedThisFrame())
        {
            do
            {
                activePlayerIndex = (activePlayerIndex + 1) % Player.players.Length;
            } while (ActivePlayer == null || !ActivePlayer.isUnlocked);
        }
        if (previousAction.WasPerformedThisFrame())
        {
            do
            {
                activePlayerIndex = (activePlayerIndex - 1 + Player.players.Length) % Player.players.Length;
            } while (ActivePlayer == null || !ActivePlayer.isUnlocked);
        }
    }

    public void TriggerFinalExplosion()
    {
        isPlaying = false;
        StartCoroutine(FinalExplosion());
    }
    
    private static void ResetLoop()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void StartLoop()
    {
        isPlaying = true;
    }

    public static void SelectPlayer(Player player)
    {
        for (int i = 0; i < Player.players.Length; i++)
        {
            if (Player.players[i] == player)
            {
                activePlayerIndex = i;
                break;
            }
        }
    }

    private IEnumerator FinalExplosion()
    {
        var startTime = Time.time;
        var animationTime = 1f;
        CameraController.I.Shake(animationTime);
        while (Time.time - startTime < animationTime)
        {
            var t = (Time.time - startTime) / animationTime;
            globalLight.intensity = 1 + 50 * t;
            yield return null;
        }
        ResetLoop();
    }
}