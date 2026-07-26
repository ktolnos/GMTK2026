using System;
using System.Collections;
using System.IO;
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
    public static float ReferenceDeltaTime = 1f / StepsPerSecond;
    public static int LoopSteps = LoopSeconds * StepsPerSecond;
    public static int Step = 0;
    private InputAction loopResetAction;
    private InputAction nextAction;
    private InputAction previousAction;
    private InputAction fastForwardAction;
    private static int activePlayerIndex;
    public bool skipSave;
    
    public Light2D globalLight;

    public static Player ActivePlayer => Player.players[activePlayerIndex];
    public static bool isPlaying = false;

    public static float lastResetTime = 0;
    
    private void Awake()
    {
        Step = 0;
        isPlaying = false;
        I = this;
        if (lastResetTime > Time.realtimeSinceStartup)
        {
            lastResetTime = -100;
        }
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
    //         Time.fixedDeltaTime = ReferenceDeltaTime / 2f;
    //     }
    //     else
    //     {
    //         Time.timeScale = 1f;
    //         Time.fixedDeltaTime = ReferenceDeltaTime;
    //     }
    // }

    private void FixedUpdate()
    {
        if (isPlaying)
        {
            Step++;
            if (loopResetAction.WasReleasedThisFrame())
            {
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
    
    public static void ResetLoop()
    {
        if (Time.realtimeSinceStartup - lastResetTime < 0.5f)
        {
            Debug.Log("Double reset: time = " + Time.realtimeSinceStartup + " last reset = " + lastResetTime);
            return;
        }
        lastResetTime = Time.realtimeSinceStartup;
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
        AudioManager.I.PlayBombExplosion();
        var startTime = Time.time;
        var animationTime = 1f;
        var animationTailTime = .5f;
        CameraController.I.Shake(animationTime + animationTailTime, intensity: 3f, effectFalloff: false);
        while (Time.time - startTime < animationTime)
        {
            var t = (Time.time - startTime) / animationTime;
            globalLight.intensity = 1 + 80 * t;
            yield return null;
        }
        yield return new WaitForSeconds(animationTailTime);
        ResetLoop();
    }

    public void DeleteSaves()
    {
        var saveFilePath = Application.persistentDataPath + "/";
        if (Directory.Exists(saveFilePath))
        {
            Directory.Delete(saveFilePath, true);
            Directory.CreateDirectory(saveFilePath);
            Debug.Log("Save file deleted.");
        }
        else
        {
            Debug.LogWarning("No save file found to delete.");
        }

        skipSave = true;
        if (Application.isPlaying)
        {
            ResetLoop();
        }
    }
}