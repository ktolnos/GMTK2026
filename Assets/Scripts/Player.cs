using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public float speed = 5f;
    private InputAction movementAction;
    private InputAction shootAction;
    private InputAction interactAction;
    private Rigidbody2D rb;
    public bool isControlled = true;
    private HistoryEntry[] history;
    public bool interactRequested = false;
    
    private string savePath;
    
    void Start()
    {
        movementAction = InputSystem.actions.FindAction("Move");
        shootAction = InputSystem.actions.FindAction("Attack");
        interactAction = InputSystem.actions.FindAction("Interact");
        
        
        rb = GetComponent<Rigidbody2D>();
        InputSystem.actions.Enable();
        savePath =  Application.persistentDataPath + "/Player" + gameObject.name + ".save";
        GM.LoopReset += LoopReset;
    }

    void LoopStart()
    {
        if (!File.Exists(savePath))
        {
            isControlled = true;
            history = new HistoryEntry[GM.LoopFrames];
        }
        else
        {
            isControlled = false;
            history = Utils.ReadArrayFromFile(savePath);
        }
    }

    void LoopReset()
    {
        Utils.WriteArrayToFile(history, savePath);
    }
    
    void FixedUpdate()
    {
        if (GM.Step == 0 || history == null)
        {
            LoopStart();
        }
        var moveInput = movementAction.ReadValue<Vector2>();
        var moveVelocity = moveInput * speed;

        var shot = shootAction.WasPressedThisFrame();
        var interact =  interactAction.WasPressedThisFrame();
        
        if (isControlled || moveInput != Vector2.zero)
        {
            if (!isControlled)
            {
                isControlled = true;
                for (int i = GM.Step; i < GM.LoopFrames; i++)
                {
                    history[i].isWritten = false;
                }
            }
            var position = rb.position + moveVelocity * Time.fixedDeltaTime;
            
            history[GM.Step] = new HistoryEntry()
            {
                position = position,
                shot = shot,
                interact = interact,
                isWritten = true,
            };
        }
        
        var entry = history[GM.Step];
        if (!entry.isWritten)
        {
            return;
        }
        
        rb.position = entry.position;
        interactRequested = entry.interact;
    }
    
    [Serializable]
    public struct HistoryEntry
    {
        public Vector2 position;
        public Direction direction;
        public bool shot;
        public Vector2 aim;
        public bool interact;
        public bool isWritten;
    }
}
