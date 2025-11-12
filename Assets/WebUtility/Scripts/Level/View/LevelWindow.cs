using System;
using UnityEngine;
using UnityEngine.UI;
using WebUtility;

public class LevelWindow : AbstractWindowUi
{
    [SerializeField] private Button _button;

    public event Action Clicked;
    
    public override void Init()
    {
        _button.onClick.AddListener(() =>
        {
            Clicked?.Invoke();
        });
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Clicked?.Invoke();
        }
    }
}