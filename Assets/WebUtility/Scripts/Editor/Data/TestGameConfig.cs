using System;
using UnityEngine;

[Serializable]
public class TestGameConfig : AbstractData
{
    public string playerName = "Player";
    public int playerLevel = 1;
    public float playerHealth = 100f;
    public bool isActive = true;
    public Vector3 spawnPosition = Vector3.zero;
    public Color playerColor = Color.white;
    
    [SerializeField] private float _damage = 10f;
    [SerializeField] private int _coins = 0;
}

