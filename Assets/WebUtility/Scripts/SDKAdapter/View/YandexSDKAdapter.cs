using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class YandexSDKAdapter : AbstractSDKAdapter
{
    private readonly string _saveKey = "SaveKey";

    public override bool IsPaymentAvailable => true;
    public override bool IsMobile => Application.isMobilePlatform;
    public override bool IsSpecialFlag => true;
    public override string Language => Application.systemLanguage.ToString();
    public override bool IsAuthorized => false;

    public override void Save<T>(T data)
    {
        if (string.IsNullOrEmpty(_saveKey))
        {
            Debug.LogError("Ключ не может быть пустым.");
            return;
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(_saveKey, json);
        PlayerPrefs.Save();
    }

    public override bool TryLoad<T>(out T data)
    {
        if (string.IsNullOrEmpty(_saveKey))
        {
            Debug.LogError("Ключ не может быть пустым.");
            data = default;
            return false;
        }

        string json = PlayerPrefs.GetString(_saveKey, null);
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning($"Данные с ключом '{_saveKey}' не найдены.");
            data = default;
            return false;
        }

        data = JsonUtility.FromJson<T>(json);
        return true;
    }

    public override void SendMetrica(string metrica)
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] Send metrica: {metrica}");
    }

    public override void SendMetrica(string metrica, IDictionary<string, string> eventParams)
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] Send metrica: {metrica} with params");
    }

    public override void UpdateLeaderboard()
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] Update leaderboard");
    }

    public override void ShowFullscreenAd()
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] Show fullscreen ad");
    }

    public override bool GetName(out string nickname)
    {
        nickname = "Player";
        return true;
    }

    public override bool GetLanguage(string language)
    {
        return language == Language;
    }

    public override void Watch(Action onWatch, RewardType rewardType)
    {
        onWatch?.Invoke();
    }

    public override void OnStart()
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] OnStart");
    }

    public override void OnEnd()
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] OnEnd");
    }

    public override void MakeReview()
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] Make review");
    }

    public override string GetPrice(string key)
    {
        return $"10 {key}Coins";
    }

    public override IEnumerator GetUserSprite(Action<Sprite> action)
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.blue;
        }
        texture.SetPixels(colors);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        action(sprite);
        yield return null;
    }

    public override void BuyPayment(Action onBuy, string buyKey)
    {
        onBuy?.Invoke();
    }

    public override void InvokeGRA()
    {
        Debug.Log($"[{nameof(YandexSDKAdapter)}] Invoke GRA");
    }
}
