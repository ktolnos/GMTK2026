using UnityEngine;
using UnityEngine.Tilemaps;

public class Level: MonoBehaviour
{
    public static Level I;
    
    public Tilemap wallsTilemap;
    public Tilemap floorTilemap;
    
    private void Awake()
    {
        I = this;
    }
}