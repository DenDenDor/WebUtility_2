using System.Linq;
using UnityEngine;
using WebUtility;

public class SDKAdapterPresenter : IPresenter
{
    private TypeSDK _currentSDKType;

    public void Init()
    {
        LoadSDKSelection();
        InitializeAdapter();
    }
    
    private void LoadSDKSelection()
    {
        TextAsset configFile = Resources.Load<TextAsset>("SDKConfig");
        if (configFile != null)
        {
            string content = configFile.text;
            if (System.Enum.TryParse(content, out TypeSDK loadedSDK))
            {
                _currentSDKType = loadedSDK;
                Debug.Log($"Loaded SDK type from config: {_currentSDKType}");
            }
        }
        else
        {
            Debug.LogWarning("SDK config file not found. Using default SDK type.");
        }
    }
    
    private void InitializeAdapter()
    {
        var adapterName = _currentSDKType + "SDKAdapter";
        var adapters = Resources.LoadAll<AbstractSDKAdapter>("SDKAdapters");

        // _sdkAdapter = adapters.FirstOrDefault(a => a.GetType().Name == adapterName);
        //     
        // if (_sdkAdapter != null)
        // {
        //     _sdkAdapter.Init();
        //     Debug.Log($"Initialized {_sdkAdapter.GetType().Name}");
        // }
        // else
        // {
        //     Debug.LogError($"Adapter {adapterName} not found!");
        // }
    }


    public void Exit()
    {
        
    }
}