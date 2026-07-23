using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class GM: MonoBehaviour
{
    public static int LoopSeconds = 10;
    public static int LoopFrames = LoopSeconds * 50;
    public static int Step = 0;
    private InputAction loopResetAction;
    private InputAction nextAction;
    private InputAction previousAction;
    public delegate void LoopResetDelegate();
    public static event LoopResetDelegate LoopReset;
    private static int activePlayerIndex;

    public static Player ActivePlayer => Player.players[activePlayerIndex];
    public static bool isPlaying = false;
    
    private void Awake()
    {
        LoopReset = null;
        Step = 0;
    }

    private void OnEnable()
    {
        loopResetAction = InputSystem.actions.FindAction("Reset");
        nextAction = InputSystem.actions.FindAction("Next");
        previousAction = InputSystem.actions.FindAction("Previous");
    }

    private void Start()
    {
        ResetLoop();
    }

    private void FixedUpdate()
    {
        if (isPlaying)
        {
            Step++;
            if (Step >= LoopFrames || loopResetAction.WasReleasedThisFrame())
            {
                ResetLoop();
            }
        }
        
        if (nextAction.WasPerformedThisFrame())
        {
            do
            {
                activePlayerIndex = (activePlayerIndex + 1) % Player.players.Length;
            } while (!ActivePlayer.isUnlocked);
        }
        if (previousAction.WasPerformedThisFrame())
        {
            do
            {
                activePlayerIndex = (activePlayerIndex - 1 + Player.players.Length) % Player.players.Length;
            } while (!ActivePlayer.isUnlocked);
        }
    }
    
    private static void ResetLoop()
    {
        Step = 0;
        LoopReset?.Invoke();
        Debug.Log("Reset Loop");
        isPlaying = false;
        PlayerSelectionUI.I.Show();
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
}