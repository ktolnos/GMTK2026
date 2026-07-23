using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public static Player[] players = new Player[2];
    public int index;
    public bool isUnlocked;
    public float speed = 5f;
    private InputAction movementAction;
    private InputAction shootAction;
    private InputAction interactAction;
    private Rigidbody2D rb;
    public bool isControlled = true;
    private HistoryEntry[] history;
    public bool interactRequested = false;
    public Direction direction;
    public bool isMoving;
    
    private string savePath;
    private Vector2 startPosition;
    
    void Awake()
    {
        movementAction = InputSystem.actions.FindAction("Move");
        shootAction = InputSystem.actions.FindAction("Attack");
        interactAction = InputSystem.actions.FindAction("Interact");
        players[index] = this;
        
        
        rb = GetComponent<Rigidbody2D>();
        InputSystem.actions.Enable();
        savePath =  Application.persistentDataPath + "/Player" + gameObject.name + ".save";
        GM.LoopReset += LoopReset;
        startPosition = rb.position;
    }

    void LoopStart()
    {
        if (File.Exists(savePath))
        {
            history = Utils.ReadArrayFromFile(savePath);
        }

        if (history == null || history.Length != GM.LoopFrames)
        {
            history = new HistoryEntry[GM.LoopFrames];
        }
        isControlled = false;
        Reset();
    }

    void LoopReset()
    {
        if (history == null)
        {
            return;
        }
        Utils.WriteArrayToFile(history, savePath);
        Reset();
    }
    
    void FixedUpdate()
    {
        if (!GM.isPlaying)
        {
            return;
        }
        if ((GM.Step == 0 || history == null))
        {
            LoopStart();
        }
        var moveInput = movementAction.ReadValue<Vector2>();
        var moveVelocity = moveInput * speed;

        var shot = shootAction.WasPressedThisFrame();
        var interact =  interactAction.WasPressedThisFrame();

        if (GM.ActivePlayer != this)
        {
            isControlled = false;
        }
        
        if (GM.ActivePlayer == this && (isControlled || 
                                        moveInput != Vector2.zero ||
                                        shot ||
                                        interact))
        {
            if (!isControlled)
            {
                isControlled = true;
                for (int i = GM.Step; i < GM.LoopFrames; i++)
                {
                    history[i].isWritten = false;
                }
            }
            
            history[GM.Step] = new HistoryEntry()
            {
                movement =  moveVelocity * Time.fixedDeltaTime,
                shot = shot,
                interact = interact,
                isWritten = true,
            };
        }
        
        ApplyHistory(history[GM.Step]);
    }

    private void ApplyHistory(HistoryEntry entry)
    {
        if (!entry.isWritten)
        {
            return;
        }
        
        rb.position += entry.movement;
        interactRequested = entry.interact;
        direction = entry.direction;
        isMoving = entry.movement != Vector2.zero;
    }

    private void Reset()
    {
        rb.position = startPosition;
    }
    
    [Serializable]
    public struct HistoryEntry
    {
        public Vector2 movement;
        public Direction direction;
        public bool shot;
        public Vector2 aim;
        public bool interact;
        public bool isWritten;
    }
}
