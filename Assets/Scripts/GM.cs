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
    public delegate void LoopResetDelegate();
    public static event LoopResetDelegate LoopReset;
    
    private void Awake()
    {
        LoopReset = null;
    }

    private void OnEnable()
    {
        loopResetAction = InputSystem.actions.FindAction("Jump");
        ResetLoop();
    }

    private void FixedUpdate()
    {
        Step++;
        if (Step >= LoopFrames || loopResetAction.WasReleasedThisFrame())
        {
            ResetLoop();
        }
    }
    
    private static void ResetLoop()
    {
        Step = 0;
        LoopReset?.Invoke();
        Debug.Log("Reset Loop");
    }
}