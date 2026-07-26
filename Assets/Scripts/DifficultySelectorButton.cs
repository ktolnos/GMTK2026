using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DifficultySelectorButton: MonoBehaviour
{
    public static List<DifficultySelectorButton> all = new();
    public GM.Difficulty difficulty;
    private Button button;
    
    private void Awake()
    {
        all.Add(this);
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        var difficulty = PlayerPrefs.GetString("difficulty");
        if (difficulty == "Easy")
        {
            GM.currentDifficulty = GM.Difficulty.Easy;
        } 
        else if (difficulty == "Medium")
        {
            GM.currentDifficulty = GM.Difficulty.Normal;
        } else if (difficulty == "Hard") 
        {
            GM.currentDifficulty = GM.Difficulty.Hard;
        }
    }

    private void Update()
    {
        if (difficulty == GM.currentDifficulty)
        {
            button.Select();
        }
    }

    private void OnClick()
    {
        GM.currentDifficulty = difficulty;
        PlayerPrefs.SetString("difficulty", GM.currentDifficulty.ToString());
    }
}