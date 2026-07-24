using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

public class Level: MonoBehaviour
{
    public static Level I;
    
    public Tilemap wallsTilemap;
    public Tilemap floorTilemap;
    public Tilemap wireTilemap;

    public TileBase unpoweredWire;
    public TileBase poweredWire;
    
    private void Awake()
    {
        I = this;
    }

    public bool IsPowered(Vector2 pos)
    {
        return wireTilemap.GetTile(wireTilemap.WorldToCell(pos)) == poweredWire;
    }
    
    public void RefreshPowerGrid(Vector3Int changePosition)
    {
        var tm = wireTilemap;
        var directions = new[]
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right,
        };

        var frontier = new Queue<Vector3Int>();
        var visited = new HashSet<Vector3Int>();
        var startCell = changePosition;
        frontier.Enqueue(startCell);
        visited.Add(startCell);

        var affectedCells = new HashSet<Vector3Int>();
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var currentTile = tm.GetTile(current);
            if (currentTile != null && (ReferenceEquals(currentTile, poweredWire) || ReferenceEquals(currentTile, unpoweredWire)))
            {
                affectedCells.Add(current);
            }

            foreach (var direction in directions)
            {
                var neighbor = current + direction;
                if (visited.Contains(neighbor))
                {
                    continue;
                }

                var neighborTile = tm.GetTile(neighbor);
                if (neighborTile == null)
                {
                    continue;
                }

                if (!ReferenceEquals(neighborTile, poweredWire) && !ReferenceEquals(neighborTile, unpoweredWire))
                {
                    continue;
                }

                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
            }
        }

        foreach (var affectedCell in affectedCells)
        {
            tm.SetTile(affectedCell, unpoweredWire);
        }

        foreach (var powerSource in PowerSource.all)
        {
            var sourceCell = tm.WorldToCell(powerSource.transform.position);
            var sourceFrontier = new Queue<Vector3Int>();
            var sourceVisited = new HashSet<Vector3Int>();
            sourceFrontier.Enqueue(sourceCell);
            sourceVisited.Add(sourceCell);

            while (sourceFrontier.Count > 0)
            {
                var current = sourceFrontier.Dequeue();
                if (affectedCells.Contains(current))
                {
                    tm.SetTile(current, poweredWire);
                }

                foreach (var direction in directions)
                {
                    var neighbor = current + direction;
                    if (sourceVisited.Contains(neighbor))
                    {
                        continue;
                    }

                    var neighborTile = tm.GetTile(neighbor);
                    if (neighborTile == null)
                    {
                        continue;
                    }

                    if (!ReferenceEquals(neighborTile, poweredWire) && !ReferenceEquals(neighborTile, unpoweredWire))
                    {
                        continue;
                    }

                    sourceVisited.Add(neighbor);
                    sourceFrontier.Enqueue(neighbor);
                }
            }
        }
    }
}