using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Audio Container")]
public class AudioContainer : ScriptableObject {

    [field: SerializeField] public AudioClip[] Clips { get; set; }
    [field: SerializeField, Range(0f, 1f)] public float VolumeScale { get; set; } = 1f;
    [field: SerializeField, Range(0f, .5f)] public float PitchOffset { get; set; } = 0f;

    private Queue<AudioClip> shuffledClips;

    public void PlayOneShot(AudioSource audioSource, float volumePercent = 1f) {
        audioSource.pitch = PitchOffset > 0f ? Random.Range(1f - PitchOffset, 1f + PitchOffset) : 1f;
        audioSource.PlayOneShot(GetNextClip(), VolumeScale * volumePercent);
    }

    public void Play(AudioSource audioSource, float volumePercent = 1f) {
        audioSource.clip = GetNextClip();
        audioSource.pitch = PitchOffset > 0f ? Random.Range(1f - PitchOffset, 1f + PitchOffset) : 1f;
        audioSource.volume = VolumeScale * volumePercent;
        audioSource.Play();
    }

    public AudioClip GetNextClip() {
        shuffledClips ??= new();
        if (shuffledClips.Count == 0) {
            if (Clips == null || Clips.Length == 0) {
                Debug.LogError($"No clips in Audio Container `{name}`.");
            }
            foreach (var clip in Clips.OrderBy(_ => Random.value)) {
                shuffledClips.Enqueue(clip);
            }
        }
        return shuffledClips.Dequeue();
    }
}
