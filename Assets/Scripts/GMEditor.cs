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
            var saveFilePath = Application.persistentDataPath + "/";
            if (Directory.Exists(saveFilePath))
            {
                Directory.Delete(saveFilePath, true);
                Debug.Log("Save file deleted.");
            }
            else
            {
                Debug.LogWarning("No save file found to delete.");
            }
        }
        if (GUILayout.Button("Open Save File Location"))
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
        
}

#endif