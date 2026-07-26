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
    }
}