using UnityEngine;
using UnityEngine.SceneManagement;
using WebUtility;

public class ShopPresenter : IPresenter, IUpdatable
{
    [Inject] private ShopWindow _shopWindow;

    private float _time;
    
    public void Init()
    {
        
    }

    public void Exit()
    {
        
    }

    public void Update()
    {
        UnityEngine.Debug.Log("Updated..." + _shopWindow.name);

        _time += Time.deltaTime;

        if (_time > 3)
        {
            _shopWindow.Panel.gameObject.SetActive(false);

            if (_time > 5)
            {
                SceneManager.LoadScene("Level");
                _time = 0;
            }
        }
    }
}