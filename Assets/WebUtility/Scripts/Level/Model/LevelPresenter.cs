using UnityEngine;
using UnityEngine.SceneManagement;
using WebUtility;
using WebUtility.Data;

public class LevelPresenter : IPresenter
{
    [Inject] private LevelWindow _levelWindow;

    public void Init()
    {
        _levelWindow.Clicked += OnClicked;
        GameObject go = DataConfigManager.GetData<WeaponData>(WeaponDataType.FireArmBow).Go;
        
        Debug.LogError("GO " + go);

        Object.Instantiate(go, Vector3.zero,  Quaternion.identity);
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