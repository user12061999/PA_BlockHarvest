using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class LunaManager : MonoBehaviour
{
    public static LunaManager ins;
    [LunaPlaygroundField("Time")] public int timeEndCreative = 30;
    [LunaPlaygroundAsset("Music")] public AudioClip bgMusic;

    [LunaPlaygroundField("Light Intensity")] public float lightIntensity = 1f;
    [LunaPlaygroundField("Light Color")] public Color colorLight = Color.white;
    [Header("Resource Spawn Weights")]
    [LunaPlaygroundField("Grass Empty Weight")] public int grassEmptyWeight = 1;
    [LunaPlaygroundField("Grass Flower Weight")] public int grassFlowerWeight = 1;
    [LunaPlaygroundField("Grass Boar Weight")] public int grassBoarWeight = 1;
    [LunaPlaygroundField("Grass Baby Boar Weight")] public int grassBabyBoarWeight = 1;
    [LunaPlaygroundField("Grass Bear Weight")] public int grassBearWeight = 1;
    [LunaPlaygroundField("Dirt Empty Weight")] public int dirtEmptyWeight = 1;
    [LunaPlaygroundField("Dirt Wheat Weight")] public int dirtWheatWeight = 1;
    [LunaPlaygroundField("Water Empty Weight")] public int waterEmptyWeight = 1;
    [LunaPlaygroundField("Water Fish Weight")] public int waterFishWeight = 1;
    public Light directionalLight;
    public bool isCretivePause;

    private void Awake()
    {
        ins = this;
    }

    public Button[] lstBtnInstall;
    public GameObject EndCard, LoseCard;
    public GameObject WinCard;

    void Start()
    {
        directionalLight.intensity = lightIntensity;
        directionalLight.color = colorLight;

        Luna.Unity.LifeCycle.OnPause += PauseGameplay;
        Luna.Unity.LifeCycle.OnResume += ResumeGameplay;
        foreach (var VARIABLE in lstBtnInstall)
        {
            VARIABLE.onClick.AddListener(OnClickEndCard);
        }

        if (EndCard != null) EndCard.SetActive(false);
        if (LoseCard != null) LoseCard.SetActive(false);
        if (WinCard != null) WinCard.SetActive(false);
        Invoke(nameof(ShowEndCard), timeEndCreative);
    }

    public void SetTexture(RawImage raw, Texture tex)
    {
        var fitter = raw.GetComponent<AspectRatioFitter>();

        raw.texture = tex;

        if (tex != null)
        {
            fitter.aspectRatio = (float)tex.width / tex.height;
        }
    }

    public void PauseGameplay()
    {
        Debug.Log("Pause game");
        Time.timeScale = 0;
    }

    public void ResumeGameplay()
    {
        Debug.Log("Load game");
        Time.timeScale = 1;
    }

    public void ShowEndCard()
    {
        if (isCretivePause) return;
        isCretivePause = true;
        if (AudioManager.ins != null) AudioManager.ins.PlayMusicLose();
        if (EndCard != null) EndCard.SetActive(true);
        Debug.Log("Show end card");
        Luna.Unity.LifeCycle.GameEnded();
    }

    public void ShowLoseCard()
    {
        if (isCretivePause) return;
        isCretivePause = true;
        if (AudioManager.ins != null) AudioManager.ins.PlayMusicLose();
        if (LoseCard != null)
        {
            LoseCard.SetActive(true);
        }
        else if (EndCard != null)
        {
            EndCard.SetActive(true);
        }

        Debug.Log("ShowLoseCard");
        Luna.Unity.LifeCycle.GameEnded();
    }

    public void ShowWinCard()
    {
        if (isCretivePause) return;
        isCretivePause = true;
        if (AudioManager.ins != null) AudioManager.ins.PlayMusicWin();
        if (WinCard != null)
        {
            WinCard.SetActive(true);
        }
        else if (EndCard != null)
        {
            EndCard.SetActive(true);
        }

        Debug.Log("Show win card");
        Luna.Unity.LifeCycle.GameEnded();
    }

    public void showwincard()
    {
        ShowWinCard();
    }

    public void showlosecard()
    {
        ShowLoseCard();
    }

    public TileType RandomResourceForTile(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Grass:
                return PickWeighted(
                    TileType.Empty, grassEmptyWeight,
                    TileType.Flower, grassFlowerWeight,
                    TileType.Boar, grassBoarWeight,
                    TileType.BabyBoar, grassBabyBoarWeight,
                    TileType.Bear, grassBearWeight);
            case TileType.Dirt:
                return PickWeighted(TileType.Empty, dirtEmptyWeight, TileType.Wheat, dirtWheatWeight);
            case TileType.Water:
                return PickWeighted(TileType.Empty, waterEmptyWeight, TileType.Fish, waterFishWeight);
            default:
                return TileType.Empty;
        }
    }

    private TileType PickWeighted(TileType a, int aw, TileType b, int bw)
    {
        aw = Mathf.Max(0, aw);
        bw = Mathf.Max(0, bw);
        var total = aw + bw;
        if (total <= 0) return TileType.Empty;
        return Random.Range(0, total) < aw ? a : b;
    }

    private TileType PickWeighted(TileType a, int aw, TileType b, int bw, TileType c, int cw, TileType d, int dw, TileType e, int ew)
    {
        aw = Mathf.Max(0, aw);
        bw = Mathf.Max(0, bw);
        cw = Mathf.Max(0, cw);
        dw = Mathf.Max(0, dw);
        ew = Mathf.Max(0, ew);
        var total = aw + bw + cw + dw + ew;
        if (total <= 0) return TileType.Empty;
        var roll = Random.Range(0, total);
        if (roll < aw) return a;
        roll -= aw;
        if (roll < bw) return b;
        roll -= bw;
        if (roll < cw) return c;
        roll -= cw;
        if (roll < dw) return d;
        return ew > 0 ? e : TileType.Empty;
    }

    public void OnClickEndCard()
    {
        Debug.Log("Click end card");
        Luna.Unity.Playable.InstallFullGame();
    }
}
