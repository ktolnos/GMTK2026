using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using static UnityEngine.Rendering.DebugUI;

public class AudioManager : MonoBehaviour {

    public static AudioManager I;

    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource bombSource;
    [SerializeField] private AudioSource clockSource;
    [SerializeField] private AudioSource positionalAudioSourcePrefab;
    [SerializeField] private int positionalAudioSourcesPoolSize = 20;

    [Header("UI Sounds")]
    [SerializeField] private AudioContainer uiClickAudio;
    [SerializeField] private AudioContainer uiAltClickAudio;
    [SerializeField] private AudioContainer uiSelectAudio;
    [SerializeField] private AudioContainer uiCloseAudio;
    [SerializeField] private AudioContainer uiErrorAudio;
    [SerializeField] private AudioContainer uiTypingAudio;

    [Header("Footsteps")]
    [SerializeField] private AudioContainer footstepsHeavyMetalWalkAudio;
    [SerializeField] private AudioContainer footstepsHeavyMetalRunAudio;
    [SerializeField] private AudioContainer footstepsLightMetalWalkAudio;
    [SerializeField] private AudioContainer footstepsLightMetalRunAudio;
    [SerializeField] private AudioContainer footstepsStoneWalkAudio;
    [SerializeField] private AudioContainer footstepsStoneRunAudio;

    [Header("Clock Sounds")]
    [SerializeField] private AudioContainer clockTickAudio;
    [SerializeField] private AudioContainer clockTackAudio;

    [Header("Bomb Sounds")]
    [SerializeField] private AudioContainer bombBuildupAudio;
    [SerializeField] private AudioContainer bombExplosionAudio;

    private void Awake() {
        if (I != null) {
            DestroyImmediate(gameObject);
        } else {
            I = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    private int positionalAudioSourceIndex = 0;
    private readonly List<AudioSource> positionalAudioSources = new();

    public void PlayUIClick() {
        uiClickAudio.PlayOneShot(uiSource);
    }

    public void PlayUIAltClick() {
        uiAltClickAudio.PlayOneShot(uiSource);
    }

    public void PlayUISelect() {
        uiSelectAudio.PlayOneShot(uiSource);
    }

    public void PlayUIClose() {
        uiCloseAudio.PlayOneShot(uiSource);
    }

    public void PlayUIError() {
        uiErrorAudio.PlayOneShot(uiSource);
    }

    public void PlayUITyping() {
        uiTypingAudio.PlayOneShot(uiSource);
    }

    public void PlayBombBuildup() {
        bombBuildupAudio.Play(bombSource);
    }

    public void PlayBombExplosion() {
        bombExplosionAudio.Play(bombSource);
    }

    public void PlayClockTick() {
        clockTickAudio.Play(clockSource);
    }

    public void PlayClockTack() {
        clockTackAudio.Play(clockSource);
    }

    public void PlayMusic(AudioContainer audio) {
        audio.Play(musicSource);
    }

    public AudioContainer GetFootstepsAudioContainer(GroundMaterial groundMaterial, bool isHeavy) {
        return groundMaterial switch {
            GroundMaterial.HeavyMetal => isHeavy ? footstepsHeavyMetalWalkAudio : footstepsHeavyMetalRunAudio,
            GroundMaterial.LightMetal => isHeavy ? footstepsLightMetalWalkAudio : footstepsLightMetalRunAudio,
            _ => isHeavy ? footstepsStoneWalkAudio : footstepsStoneRunAudio,
        };
    }

    public void PlayAtPosition(AudioContainer audioContainer, Vector2 position) {
        AudioSource audioSource;
        if (positionalAudioSourceIndex > positionalAudioSources.Count - 1) {
            audioSource = Instantiate(positionalAudioSourcePrefab, transform);
            positionalAudioSources.Add(audioSource);
        } else {
            audioSource = positionalAudioSources[positionalAudioSourceIndex];
        }
        audioSource.transform.position = position;
        audioContainer.Play(audioSource);
        positionalAudioSourceIndex++;
        if (positionalAudioSourceIndex >= positionalAudioSourcesPoolSize) {
            positionalAudioSourceIndex = 0;
        }
    }

    public void ChangeVolume(float value) {
        mixer.SetFloat("MasterVolume", Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f);
    }

    public void OnResetLoop() {
        bombSource.Stop();
    }
}
