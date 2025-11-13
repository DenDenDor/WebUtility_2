using UnityEngine;
using UnityEngine.SceneManagement;
using WebUtility;

public class LevelPresenter : IPresenter
{
    [Inject] private LevelWindow _levelWindow;

    public void Init()
    {
        _levelWindow.Clicked += OnClicked;
        
        GameObject go = DataConfigManager.GetData<WeaponData>("390a5133-85d0-4158-8ef8-bd4b7bc9b3ee").Go;

        Debug.LogError("GO " + go);
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