using System;
using UnityEngine;

public class CameraController: MonoBehaviour
{
    private void LateUpdate()
    {
        if (GM.ActivePlayer == null)
        {
            return;
        }
        transform.position = new Vector3(
            GM.ActivePlayer.transform.position.x, 
            GM.ActivePlayer.transform.position.y, 
            transform.position.z
        );
    }
}