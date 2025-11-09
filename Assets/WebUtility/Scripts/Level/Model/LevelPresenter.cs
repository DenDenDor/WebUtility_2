using UnityEngine;
using UnityEngine.SceneManagement;
using WebUtility;

public class LevelPresenter : IPresenter
{
    [Inject] private LevelWindow _levelWindow;

    public void Init()
    {
        _levelWindow.Clicked += OnClicked;
    }

    private void OnClicked()
    {
        Debug.LogError("NEW SCENE... ");
        SceneManager.LoadScene("New Scene");
    }

    public void Exit()
    {
        
    }
}