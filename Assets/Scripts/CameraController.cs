using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class CameraController: MonoBehaviour
{
    public static CameraController I;
    public Camera mainCamera;

    [SerializeField] private Vector2 shakeDistanceMinMax = new(6f, 18f);

    private float shakeDuration;
    private float shakeIntensity;
    private bool shakeEffectFalloff;
    private float shakeSecondsLeft = 0f;
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
            var falloffIntensity = shakeEffectFalloff ? (shakeSecondsLeft / shakeDuration) : 1f;
            mainCamera.transform.localPosition = .3f * shakeIntensity * falloffIntensity * Random.insideUnitCircle;
            shakeSecondsLeft -= Time.deltaTime;
        }
        else
        {
            mainCamera.transform.localPosition = Vector3.zero;
        }
    }
    
    public void Shake(float duration, Vector2? position = null, float intensity = 1f, bool effectFalloff = true)
    {
        shakeDuration = duration;
        if (position.HasValue) {
            var distanceToCamera = Vector2.Distance(transform.position, position.Value);
            intensity *= Mathf.Clamp01((shakeDistanceMinMax.y - distanceToCamera) / (shakeDistanceMinMax.y - shakeDistanceMinMax.x));
        }
        shakeIntensity = intensity;
        shakeEffectFalloff = effectFalloff;
        shakeSecondsLeft = duration;
    }
}