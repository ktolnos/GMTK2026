using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public Rigidbody2D rb;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }
}