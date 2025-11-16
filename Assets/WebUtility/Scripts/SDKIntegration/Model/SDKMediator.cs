using System;
using System.Linq;
using UnityEngine;
using WebUtility;

public class SDKMediator
{
    [Inject] private AbstractSDKAdapter _sdkAdapter;

    public SaveData GenerateSaveData()
    {
        SaveData defaultSaveData = new SaveData();

        if (_sdkAdapter.TryLoad(out SaveData saveData))
        {
            defaultSaveData = saveData;
        }

        return defaultSaveData;
    }

    public void SaveMusicValue(float value)
    {
        SaveData defaultSaveData = GenerateSaveData();
        defaultSaveData.MusicValue = value;
        _sdkAdapter.Save(defaultSaveData);
    }

    public void SaveSoundValue(float value)
    {
        SaveData defaultSaveData = GenerateSaveData();
        defaultSaveData.SoundValue = value;
        _sdkAdapter.Save(defaultSaveData);
    }

    public void SaveCoins(int value)
    {
        SaveData defaultSaveData = GenerateSaveData();
        defaultSaveData.Coins = value;
        _sdkAdapter.Save(defaultSaveData);
    }

}