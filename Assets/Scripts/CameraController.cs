using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class CameraController: MonoBehaviour
{
    public static CameraController I;
    public Camera mainCamera;

    private float shakeEndTime = -100;
    
    private void Awake()
    {
        I = this;
    }
    
    private void LateUpdate()
    {
        if (GM.ActivePlayer == null)
        {
            return;
        }
        transform.position = new Vector3(
            GM.ActivePlayer.transform.position.x, 
            GM.ActivePlayer.transform.position.y, 
            transform.position.z
        );
        if (Time.time < shakeEndTime)
        {
            mainCamera.transform.localPosition = Random.insideUnitCircle * 0.1f;
        }
        else
        {
            mainCamera.transform.localPosition = Vector3.zero;
        }
    }
    
    public void Shake(float duration)
    {
        shakeEndTime = Time.time + duration;
    }
}