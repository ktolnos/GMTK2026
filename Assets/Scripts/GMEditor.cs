#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[UnityEditor.CustomEditor(typeof(GM))]
public class GMEditor : Editor
{
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GM gm = (GM)target;
        if (GUILayout.Button("Delete Save File"))
        {
           gm.DeleteSaves();
        }
        if (GUILayout.Button("Open Save File Location"))
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
        if (GUILayout.Button("Unlock All [in play mode]"))
        {
            foreach (var player in Player.players)
            {
                player.isUnlocked = true;
            }
        }
    }
        
}

#endif