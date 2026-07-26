using System.IO;
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

    public void DeleteSaves()
    {
        var saveFilePath = Application.persistentDataPath + "/";
        if (Directory.Exists(saveFilePath))
        {
            Directory.Delete(saveFilePath, true);
            Directory.CreateDirectory(saveFilePath);
            Debug.Log("Save file deleted.");
        }
        else
        {
            Debug.LogWarning("No save file found to delete.");
        }
        PlayerPrefs.DeleteAll();
        GM.currentDifficulty = GM.Difficulty.Normal;
        SceneManager.LoadScene("Menu");
    }
}
