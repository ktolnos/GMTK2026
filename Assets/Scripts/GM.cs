using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class GM: MonoBehaviour
{
    public static int LoopSeconds = 30;
    public static int LoopFrames = LoopSeconds * 50;
    public static int Step = 0;
    private InputAction loopResetAction;
    private InputAction nextAction;
    private InputAction previousAction;
    private static int activePlayerIndex;
    
    public Light2D globalLight;

    public static Player ActivePlayer => Player.players[activePlayerIndex];
    public static bool isPlaying = false;
    
    private void Awake()
    {
        Step = 0;
        isPlaying = false;
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
    }

    private void FixedUpdate()
    {
        if (isPlaying)
        {
            Step++;
            if (Step >= LoopFrames || loopResetAction.WasReleasedThisFrame())
            {
                isPlaying = false;
                StartCoroutine(FinalExplosion());
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
        var animationTime = 0.5f;
        while (Time.time - startTime < animationTime)
        {
            var t = (Time.time - startTime) / animationTime;
            globalLight.intensity = 1 + 10 * t;
            yield return null;
        }
        ResetLoop();
    }
}