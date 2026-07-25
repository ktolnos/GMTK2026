using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class PostProcessingHelper: MonoBehaviour
{
    public Renderer2DData rendererData;
    public Volume mainVolume;
    private ScriptableRendererFeature crt;
    private ChromaticAberration chrom;

    public Image recordingImage;

    private void Start()
    {
        foreach (var rendererDataRendererFeature in rendererData.rendererFeatures)
        {
            if (rendererDataRendererFeature.name == "CRT")
            {
                crt = rendererDataRendererFeature;
            }
        }

        mainVolume.profile.TryGet(out chrom);
    }

    private void Update()
    {
        var postEnabled = !GM.ActivePlayer.isControlled && GM.isPlaying;
        crt.SetActive(postEnabled);
        chrom.active = postEnabled;
        recordingImage.enabled = postEnabled;

        var color = recordingImage.color;
        color.a = (Mathf.Sin(Time.time * Mathf.PI) + 1f) / 2;
        recordingImage.color = color;
    }
        
}