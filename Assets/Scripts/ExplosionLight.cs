using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ExplosionLight: MonoBehaviour
{
    private Light2D light2D;
    public float maxIntensity;
    public float fadeOutTime;
    public float fadeInTime;

    IEnumerator Start()
    {
        light2D = GetComponent<Light2D>();
        var startTime = Time.time;
        while (Time.time - startTime < fadeInTime)
        {
            light2D.intensity = Mathf.Lerp(0, maxIntensity, (Time.time - startTime) / fadeInTime);
            yield return null;
        }
        var fadeOutStartTime = Time.time;
        while (Time.time - fadeOutStartTime < fadeOutTime)
        {
            light2D.intensity = Mathf.Lerp(maxIntensity, 0, (Time.time - fadeOutStartTime) / fadeOutTime);
            yield return null;
        }
        light2D.intensity = 0;
    }


}