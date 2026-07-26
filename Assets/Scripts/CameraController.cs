using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class CameraController: MonoBehaviour
{
    public static CameraController I;
    public Camera mainCamera;

    private float shakeSecondsLeft = 0f;
    private float shakeDuration;
    private Vector3 cameraPosSmoothDampVel;
    
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
        var targetPos = new Vector3(
            GM.ActivePlayer.transform.position.x,
            GM.ActivePlayer.transform.position.y,
            transform.position.z
        );
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref cameraPosSmoothDampVel, .1f);

        if (shakeSecondsLeft > 0f)
        {
            var shakeIntensity = shakeSecondsLeft / shakeDuration;
            mainCamera.transform.localPosition = 0.2f * shakeIntensity * Random.insideUnitCircle;
            shakeSecondsLeft -= Time.deltaTime;
        }
        else
        {
            mainCamera.transform.localPosition = Vector3.zero;
        }
    }
    
    public void Shake(float duration)
    {
        shakeSecondsLeft = duration;
        shakeDuration = duration;
    }
}