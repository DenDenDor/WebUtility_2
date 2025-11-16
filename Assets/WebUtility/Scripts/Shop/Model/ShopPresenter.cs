using UnityEngine;
using UnityEngine.SceneManagement;
using WebUtility;

public class ShopPresenter : IPresenter, IUpdatable
{
    [Inject] private readonly ShopWindow _shopWindow;
    private readonly float _maxTime = 5;

    private float _time;


    public void Init()
    {
        
    }

    public void Update()
    {
        Debug.Log("Updated..." + _shopWindow.name);

        _time += Time.deltaTime;

        if (_time > _maxTime)
        {
            _shopWindow.Panel.gameObject.SetActive(false);

            SceneManager.LoadScene("Level");
        }
    }


    public void Exit()
    {
        
    }
}