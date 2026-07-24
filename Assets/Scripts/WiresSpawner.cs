using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WiresSpawner: MonoBehaviour
{
    private Player player;
    public TileBase poweredWire;
    public TileBase unpoweredWire;
    
    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void FixedUpdate()
    {
        if (player.lastInteractStep >= GM.Step-1)
        {
            SpawnWire();
        }
    }
    
    private void SpawnWire()
    {
        var playerPos = (Vector2)player.transform.position + player.collider.offset;
        var tm = Level.I.wireTilemap;
        var cell = tm.WorldToCell(playerPos);
        if (tm.HasTile(cell))
        {
            return;
        }
        tm.SetTile(cell, unpoweredWire);
        Level.I.RefreshPowerGrid(cell);
    }
}