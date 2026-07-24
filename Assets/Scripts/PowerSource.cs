using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerSource: MonoBehaviour
{
    public static HashSet<PowerSource> all = new();

    private void Start()
    {
        all.Add(this);
        Level.I.RefreshPowerGrid(Level.I.wireTilemap.WorldToCell(transform.position));
    }

    private void OnDestroy()
    {
        all.Remove(this);
        Level.I.RefreshPowerGrid(Level.I.wireTilemap.WorldToCell(transform.position));
    }
}