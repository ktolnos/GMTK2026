using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour {

    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource uiSource;

    [Header("UI Sounds")]
    [SerializeField] private AudioContainer uiClickAudio;
    [SerializeField] private AudioContainer uiAltClickAudio;
    [SerializeField] private AudioContainer uiSelectAudio;
    [SerializeField] private AudioContainer uiCloseAudio;
    [SerializeField] private AudioContainer uiErrorAudio;

    [Header("Footsteps")]
    [SerializeField] private AudioContainer footstepsHeavyMetalWalkAudio;
    [SerializeField] private AudioContainer footstepsHeavyMetalRunAudio;
    [SerializeField] private AudioContainer footstepsLightMetalWalkAudio;
    [SerializeField] private AudioContainer footstepsLightMetalRunAudio;
    [SerializeField] private AudioContainer footstepsStoneWalkAudio;
    [SerializeField] private AudioContainer footstepsStoneRunAudio;

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
}
