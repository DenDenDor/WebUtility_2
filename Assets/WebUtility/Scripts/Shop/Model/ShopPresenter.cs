using UnityEngine;
using WebUtility;

public class ShopPresenter : IPresenter, IUpdatable
{
    [Inject] private ShopWindow _shopWindow;
    
    public void Init()
    {
        
    }

    public void Exit()
    {
        
    }

    public void Update()
    {
        UnityEngine.Debug.Log("Updated..." + _shopWindow.name);
    }
}