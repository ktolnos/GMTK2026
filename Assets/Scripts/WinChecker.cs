using System;
using TMPro;
using UnityEngine;

public class WinChecker: MonoBehaviour
{
    public GameObject winText;
    public GameObject winVfxPrefab;
    public ExplodeAtTime mainBomb;
    
    private bool hasTriggered = false;

    private void Update()
    {
        if (!hasTriggered && mainBomb.isDefused)
        {
            hasTriggered = true;
            winText.SetActive(true);
            Instantiate(winVfxPrefab, winText.transform.position, Quaternion.identity);
        }
    }
}