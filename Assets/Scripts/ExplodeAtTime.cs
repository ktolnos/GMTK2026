using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

public class ExplodeAtTime : MonoBehaviour
{
    public GameObject explosionPrefab;
    public float time;
    public float shakeTime;
    public float selfDestroyTime = -1;
    public float explosionDestroyTime = 1f;
    private bool isExploding = false;
    public bool endOfLoop;
    public float randomOffset;
    public bool explodeWalls;
    public float wallsExplosionRadius;

    private void Start()
    {
        if (endOfLoop)
        {
            time = GM.LoopSeconds;
        }

        time -= Random.value * randomOffset;
    }

    public void FixedUpdate()
    {
        var currentTime = (float)GM.Step * GM.LoopSeconds / GM.LoopSteps;
        if (currentTime > time)
        {
            StartExplosion();
        }
    }

    public void StartExplosion()
    {
        StartCoroutine(Explode());
    }

    private IEnumerator Explode()
    {
        if (isExploding) yield break;
        isExploding = true;
        if (explosionPrefab)
        {
            var explosion = Instantiate(explosionPrefab, transform.position, transform.rotation);
            if (explosionDestroyTime > 0)
            {
                Destroy(explosion, explosionDestroyTime);
            }
        }
        
        if (shakeTime > 0)
        {
            CameraController.I.Shake(shakeTime);
        }
        
        if (selfDestroyTime > 0)
        {
           yield return new WaitForSeconds(selfDestroyTime);
        }
        Destroy(gameObject, selfDestroyTime);

        if (explodeWalls)
        {
            var walls = Level.I.wallsTilemap;

            var explosionPosition = transform.position;
            var centerCell = walls.WorldToCell(explosionPosition);
            var cellRadius = Mathf.CeilToInt(wallsExplosionRadius);
            var radiusSquared = wallsExplosionRadius * wallsExplosionRadius;

            for (var y = centerCell.y - cellRadius; y <= centerCell.y + cellRadius; y++)
            {
                for (var x = centerCell.x - cellRadius; x <= centerCell.x + cellRadius; x++)
                {
                    var cell = new Vector3Int(x, y, centerCell.z);
                    if (!walls.HasTile(cell)) continue;

                    var cellCenter = walls.GetCellCenterWorld(cell);
                    if ((cellCenter - explosionPosition).sqrMagnitude > radiusSquared) continue;

                    walls.SetTile(cell, null);
                }
            }

            var objects = Physics2D.OverlapCircleAll(explosionPosition, wallsExplosionRadius);
            foreach (var collider2D in objects)
            {
                if (collider2D.TryGetComponent<Health>(out var health))
                {
                    health.TakeDamage(100f);
                }
                else if (!collider2D.TryGetComponent(out Tilemap tm))
                {
                    Destroy(collider2D.gameObject);
                }
            }
        }
    }
}