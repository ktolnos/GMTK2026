using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private AudioContainer music;

    private void Start() {
        AudioManager.I.PlayMusic(music);
    }

    public void StartGame()
    {
        Debug.Log("Starting Game");
        SceneManager.LoadScene("SampleScene");
    }
}
