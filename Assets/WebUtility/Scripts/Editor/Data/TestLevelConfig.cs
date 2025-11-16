using System;
using UnityEngine;

[Serializable]
public class TestLevelConfig : AbstractData
{
    public string levelName = "Level 1";
    public int levelNumber = 1;
    public float difficulty = 1.0f;
    public Vector3 startPosition = Vector3.zero;
    public bool isUnlocked = true;
    
    [SerializeField] private int _maxEnemies = 10;
    [SerializeField] private float _timeLimit = 300f;
}

