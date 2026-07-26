using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public static Player[] players = new Player[4];
    public int index;
    public bool isUnlocked
    {
        get => saveState.unlocked;
        set => saveState.unlocked = value;
    }
    public float speed = 5f;
    private InputAction movementAction;
    private InputAction shootAction;
    private InputAction interactAction;
    public Rigidbody2D rb;
    public bool isControlled = true;
    private List<HistoryEntry> history;
    public int lastInteractStep = -100;
    public Direction direction;
    public bool isMoving;
    public bool isAttacking;
    public int lastShotStep = -100;
    [NonSerialized] public Gun gun;
    public string playerName;
    [TextArea] public string description;
    public Sprite icon;

    private string historySavePath;
    private string stateSavePath;
    private SaveState saveState;


    public CircleCollider2D collider;

    private Health health;
    private float wasControlledStep = -100;
    private int closedDoorCollisionStep = -100;
    private bool isSynced = true;
    
    void Awake()
    {
        movementAction = InputSystem.actions.FindAction("Move");
        shootAction = InputSystem.actions.FindAction("Attack");
        interactAction = InputSystem.actions.FindAction("Interact");
        players[index] = this;
        gun = GetComponentInChildren<Gun>();
        health = GetComponent<Health>();
        
        rb = GetComponent<Rigidbody2D>();
        InputSystem.actions.Enable();
        var baseSavePath = Application.persistentDataPath + "/Player" + gameObject.name;
        historySavePath =  baseSavePath + ".save";
        stateSavePath = baseSavePath + ".state";
        collider = GetComponent<CircleCollider2D>();
        
        if (File.Exists(historySavePath))
        {
            history = Utils.ReadArrayFromFile(historySavePath).ToList();
        }

        if (history == null || history.Count < GM.LoopSteps)
        {
            history = new HistoryEntry[GM.LoopSteps].ToList();
        }
        isControlled = false;
        if (File.Exists(stateSavePath))
        {
            saveState = JsonUtility.FromJson<SaveState>(File.ReadAllText(stateSavePath));
        }
        else
        {
            saveState = new SaveState()
            {
                unlocked = index == 0
            };
        }
    }

    private void Start()
    {
        isSynced = true;
    }

    void FixedUpdate()
    {
        if (!GM.isPlaying)
        {
            return;
        }
        var moveInput = movementAction.ReadValue<Vector2>();
        var moveVelocity = moveInput * speed;

        var shot = GM.ActivePlayer == this && shootAction.IsPressed();
        var interact = GM.ActivePlayer == this && interactAction.IsPressed();

        if (GM.ActivePlayer != this)
        {
            isControlled = false;
        }

        while (history.Count < GM.Step + 1)
        {
            history.Add(new HistoryEntry());
        }
        if (GM.ActivePlayer == this && (isControlled || 
                                        moveInput != Vector2.zero ||
                                        shot ||
                                        interact))
        {
            if (!isControlled)
            {
                isControlled = true;
                for (int i = GM.Step; i < GM.LoopSteps; i++)
                {
                    history[i] = new HistoryEntry();
                }
            }
            history[GM.Step] = new HistoryEntry()
            {
                movement =  moveVelocity * GM.ReferenceDeltaTime,
                position = rb.position,
                lastShotStep = shot ? GM.Step : GM.Step > 0 ? history[GM.Step-1].lastShotStep : -100,
                lastInteractStep = interact ? GM.Step : GM.Step > 0 ? history[GM.Step-1].lastInteractStep : -100,
                lastClosedDoorCollisionStep = closedDoorCollisionStep,
                isWritten = true,
            };
        }

        if (isControlled)
        {
            wasControlledStep = GM.Step;
        }
        var pos = transform.position;
        pos.z = -pos.y*0.0001f;
        transform.position = pos;
        ApplyHistory(history[GM.Step]);
    }

    private void ApplyHistory(HistoryEntry entry)
    {
        if (!entry.isWritten)
        {
            isMoving = false;

            return;
        }

        var contactedClosedDoorsPotentialDesync =
            GM.Step - closedDoorCollisionStep < 2 &&
            entry.lastClosedDoorCollisionStep != closedDoorCollisionStep;
        if (isSynced && !contactedClosedDoorsPotentialDesync)
        {
            rb.MovePosition(entry.position);
        }
        else
        {
            if (Vector2.Distance(rb.position, entry.position) > 0.12f)
            {
                isSynced = false;
                Debug.Log("Desync detected for player " + gameObject.name + " at step " + GM.Step);
            }
            else
            {
                rb.MovePosition(entry.position);
                isSynced = true;
            }
        }

        rb.MovePosition(rb.position + entry.movement);
        
        lastInteractStep = entry.lastInteractStep;
        isMoving = entry.movement != Vector2.zero;
        lastShotStep = entry.lastShotStep;

        if (entry.movement.x != 0f)
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
        
        
        if (lastShotStep == GM.Step)
        {
            gun.Shoot(direction.ToVector2());
        }
    }

    private async void OnDestroy()
    {
        if (history == null || GM.I.skipSave)
        {
            return;
        }
        Utils.WriteArrayToFile(history.ToArray(), historySavePath);
        File.WriteAllText(stateSavePath, JsonUtility.ToJson(saveState));
        if (health.currentHealth <= 0)
        {
            UIManager.I.ShowResetText();
            if (GM.Step - wasControlledStep < 5)
            {
                var deathAnimTime = 1000;
                GM.lastResetTime = Time.realtimeSinceStartup + deathAnimTime / 1000f;
                await Task.Delay(deathAnimTime);
                GM.lastResetTime = 0;
                GM.ResetLoop();
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.TryGetComponent(out Door door) && !door.isOpen)
        {
            closedDoorCollisionStep = GM.Step;
        }
        if (other.gameObject.TryGetComponent(out Player otherPlayer))
        {
            otherPlayer.saveState.unlocked = true;
        }
    }
    
    private void OnCollisionStay2D(Collision2D other)
    {
        if (other.gameObject.TryGetComponent(out Door door) && !door.isOpen)
        {
            closedDoorCollisionStep = GM.Step;
        }
    }

    [Serializable]
    public struct HistoryEntry
    {
        public Vector2 movement;
        public Vector2 position;
        public int lastShotStep;
        public Vector2 aim;
        public int lastInteractStep;
        public bool isWritten;
        public int lastClosedDoorCollisionStep;
    }
    
    public struct SaveState
    {
        public bool unlocked;
    }
}
