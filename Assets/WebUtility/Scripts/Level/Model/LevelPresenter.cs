using UnityEngine;
using UnityEngine.SceneManagement;
using WebUtility;
using WebUtility.Data;

public class LevelPresenter : IPresenter
{
    [Inject] private LevelWindow _levelWindow;
    [Inject] private SDKMediator _sdkMediator;

    public void Init()
    {
        _levelWindow.Clicked += OnClicked;
        
        var weaponConfig = DataConfigManager.GetData<WeaponData>(WeaponDataType.FroozenSword7);
        
        Object.Instantiate(weaponConfig.Go, Vector3.zero, Quaternion.identity);
    }

    private void OnClicked()
    {
        Debug.LogError("NEW SCENE... " + _sdkMediator.GenerateSaveData().Coins);
        
        _sdkMediator.SaveCoins(Random.Range(0, 10));
        SceneManager.LoadScene("New Scene");
    }

    public void Exit()
    {
        
    }
}