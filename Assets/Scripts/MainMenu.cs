using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private AudioContainer music;
    [SerializeField] private Slider volumeSlider;

    private void Start() {
        AudioManager.I.PlayMusic(music);
        volumeSlider.onValueChanged.RemoveAllListeners();
        volumeSlider.onValueChanged.AddListener(v => AudioManager.I.ChangeVolume(v / 10f));
        volumeSlider.onValueChanged.AddListener(v => AudioManager.I.PlayUISelect());
        AudioManager.I.ChangeVolume(volumeSlider.value / 10f);
    }

    public void StartGame()
    {
        Debug.Log("Starting Game");
        SceneManager.LoadScene("SampleScene");
    }
}
