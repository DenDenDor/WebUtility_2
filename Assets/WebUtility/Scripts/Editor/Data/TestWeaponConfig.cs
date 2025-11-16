using System;
using UnityEngine;

[Serializable]
public class TestWeaponConfig : AbstractData
{
    public string weaponName = "Sword";
    public float damage = 50f;
    public float attackSpeed = 1.5f;
    public int durability = 100;
    public Vector2 size = new Vector2(1f, 2f);
    public bool isRanged = false;
    
    [SerializeField] private float _range = 5f;
    [SerializeField] private Color _weaponColor = Color.gray;
}

