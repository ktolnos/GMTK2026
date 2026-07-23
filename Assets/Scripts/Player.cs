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
    public int lastInteractStep = -100;
    public Direction direction;
    public bool isMoving;
    public int lastShotStep = -100;

    private string savePath;

    private Gun gun;
    public CircleCollider2D collider;
    
    void Awake()
    {
        movementAction = InputSystem.actions.FindAction("Move");
        shootAction = InputSystem.actions.FindAction("Attack");
        interactAction = InputSystem.actions.FindAction("Interact");
        players[index] = this;
        gun = GetComponentInChildren<Gun>();
        
        rb = GetComponent<Rigidbody2D>();
        InputSystem.actions.Enable();
        savePath =  Application.persistentDataPath + "/Player" + gameObject.name + ".save";
        collider = GetComponent<CircleCollider2D>();
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

        var shot = GM.ActivePlayer == this && shootAction.IsPressed();
        var interact = GM.ActivePlayer == this && interactAction.IsPressed();

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
                lastShotStep = shot ? GM.Step : GM.Step > 0 ? history[GM.Step-1].lastShotStep : -100,
                lastInteractStep = interact ? GM.Step : GM.Step > 0 ? history[GM.Step-1].lastInteractStep : -100,
                isWritten = true,
            };
        }
        
        ApplyHistory(history[GM.Step]);
    }

    private void ApplyHistory(HistoryEntry entry)
    {
        if (!entry.isWritten)
        {
            isMoving = false;

            return;
        }
        
        rb.position += entry.movement;
        lastInteractStep = entry.lastInteractStep;
        isMoving = entry.movement != Vector2.zero;
        lastShotStep = entry.lastShotStep;

        if (entry.movement != Vector2.zero)
        {
            if (Mathf.Abs(entry.movement.x) > Mathf.Abs(entry.movement.y))
            {
                if (entry.movement.x > 0)
                {
                    direction = Direction.Right;
                }
                else
                {
                    direction = Direction.Left;
                }
            }
            else
            {
                if (entry.movement.y > 0) 
                {
                    direction = Direction.Up;
                }
                else
                {
                    direction = Direction.Down;
                }
            }
        }
        
        
        if (lastShotStep == GM.Step)
        {
            gun.Shoot(direction.ToVector2());
        }
    }

    private void OnDestroy()
    {
        if (history == null)
        {
            return;
        }
        Utils.WriteArrayToFile(history, savePath);
    }

    [Serializable]
    public struct HistoryEntry
    {
        public Vector2 movement;
        public int lastShotStep;
        public Vector2 aim;
        public int lastInteractStep;
        public bool isWritten;
    }
}
