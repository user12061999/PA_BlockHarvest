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

    public void OnClickEndCard()
    {
        Debug.Log("Click end card");
        Luna.Unity.Playable.InstallFullGame();
    }
}
