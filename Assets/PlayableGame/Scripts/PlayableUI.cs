using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class PlayableUI : MonoBehaviour
{
    [SerializeField] private Text objectiveText;
    [SerializeField] private Text timerText;
    [SerializeField] private TextMeshProUGUI remainingMovesLabel;
    [Header("Full Board Bonus")]
    [SerializeField] private RectTransform fullBoardBonusPanel;
    [SerializeField] private RectTransform fullBoardBonusClock;
    [SerializeField] private Image fullBoardBonusClockImage;
    [SerializeField] private Sprite fullBoardBonusClockSprite;
    [SerializeField] private float fullBoardBonusVisibleSeconds = 2f;
    [SerializeField] private float fullBoardBonusScaleSeconds = 0.2f;
    [SerializeField] private float fullBoardBonusMoveSeconds = 0.45f;
    [Header("Bonus Impact / Shake")]
    [SerializeField] private AudioClip clockImpactSound;
    [SerializeField] private float shakeDuration = 0.25f;
    [SerializeField] private float shakeMagnitude = 8f;
    [SerializeField] private float punchScaleAmount = 1.3f;
    [SerializeField] private GameObject ctaPanel;

    private BlockManager blockManager;
    private CanvasGroup fullBoardBonusCanvasGroup;
    private Coroutine fullBoardBonusRoutine;
    private Coroutine shakeRoutine;

    public bool IsTutorialVisible => false;

    private void Awake()
    {
        if (objectiveText == null) objectiveText = FindText("ObjectiveText");
        if (timerText == null) timerText = FindText("TimerText");
        if (blockManager == null) blockManager = FindObjectOfType<BlockManager>();
        HideLegacyTutorialPanel();

        if (ctaPanel == null)
        {
            var panel = transform.Find("CtaPanel");
            if (panel != null) ctaPanel = panel.gameObject;
        }

        if (fullBoardBonusPanel == null)
        {
            var panel = transform.Find("FullBoardBonusPanel");
            fullBoardBonusPanel = panel != null ? panel.GetComponent<RectTransform>() : null;
        }

        if (fullBoardBonusPanel != null)
        {
            fullBoardBonusPanel.gameObject.SetActive(false);
        }
    }

    public void ShowGameplay(float sessionSeconds)
    {
        if (timerText != null) timerText.text = Mathf.CeilToInt(sessionSeconds).ToString();
        HideLegacyTutorialPanel();
        ShowPlacementTutorial();
        if (ctaPanel != null) ctaPanel.SetActive(false);
    }

    public void SetHarvestCounts(int wheat, int meat, int flower, int fish)
    {
        if (objectiveText != null)
        {
            objectiveText.text = "Wheat " + wheat + "  Meat " + meat + "  Flower " + flower + "  Fish " + fish;
        }
    }

    public void SetHarvestCounts(int wheat, int wheatGoal, int meat, int meatGoal, int flower, int flowerGoal, int fish, int fishGoal, int remainingPlacements)
    {
        if (objectiveText != null)
        {
            objectiveText.text =
                "Wheat " + wheat + "/" + wheatGoal
                + "  Meat " + meat + "/" + meatGoal
                + "  Flower " + flower + "/" + flowerGoal
                + "  Fish " + fish + "/" + fishGoal
                + "  Moves " + remainingPlacements;
        }

        SetRemainingMoves(remainingPlacements);
    }

    public void PlayFullBoardMoveBonus(int amount, Action onComplete)
    {
        if (!Application.isPlaying || amount <= 0)
        {
            if (onComplete != null)
            {
                onComplete();
            }

            return;
        }

        EnsureFullBoardBonusViews();
        if (fullBoardBonusPanel == null || fullBoardBonusClock == null || remainingMovesLabel == null)
        {
            if (onComplete != null)
            {
                onComplete();
            }

            return;
        }

        if (fullBoardBonusRoutine != null)
        {
            StopCoroutine(fullBoardBonusRoutine);
        }

        fullBoardBonusRoutine = StartCoroutine(PlayFullBoardMoveBonusRoutine(onComplete));
    }

    public void SetHarvestCounts(List<string> resourceGoals, int remainingPlacements)
    {
        if (objectiveText != null)
        {
            objectiveText.text = resourceGoals != null && resourceGoals.Count > 0
                ? string.Join("  ", resourceGoals) + "  Moves " + remainingPlacements
                : "Moves " + remainingPlacements;
        }

        SetRemainingMoves(remainingPlacements);
    }

    public void ShowCta()
    {
        if (ctaPanel != null) ctaPanel.SetActive(true);
    }

    public void HideTutorial()
    {
    }

    public void DismissTutorialAndShowPlacementTutorial()
    {
        HideTutorial();
        ShowPlacementTutorial();
    }

    private void ShowPlacementTutorial()
    {
        if (blockManager == null)
        {
            blockManager = FindObjectOfType<BlockManager>();
        }

        if (blockManager != null)
        {
            blockManager.ShowPlacementTutorial();
        }
    }

    private Text FindText(string childName)
    {
        var child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private void EnsureFullBoardBonusViews()
    {
        if (fullBoardBonusPanel == null)
        {
            var existingPanel = transform.Find("FullBoardBonusPanel");
            fullBoardBonusPanel = existingPanel != null ? existingPanel.GetComponent<RectTransform>() : null;
        }

        if (fullBoardBonusPanel == null)
        {
            fullBoardBonusPanel = CreateFullBoardBonusPanel();
        }

        if (fullBoardBonusCanvasGroup == null && fullBoardBonusPanel != null)
        {
            fullBoardBonusCanvasGroup = fullBoardBonusPanel.GetComponent<CanvasGroup>();
            if (fullBoardBonusCanvasGroup == null)
            {
                fullBoardBonusCanvasGroup = fullBoardBonusPanel.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (fullBoardBonusClock == null && fullBoardBonusPanel != null)
        {
            var clock = fullBoardBonusPanel.Find("Clock");
            fullBoardBonusClock = clock != null ? clock.GetComponent<RectTransform>() : null;
            if (fullBoardBonusClock == null)
            {
                fullBoardBonusClock = CreateFullBoardBonusClock(fullBoardBonusPanel);
            }
        }

        if (fullBoardBonusClockImage == null && fullBoardBonusClock != null)
        {
            fullBoardBonusClockImage = fullBoardBonusClock.GetComponent<Image>();
        }

        if (fullBoardBonusClockImage != null && fullBoardBonusClockSprite != null)
        {
            fullBoardBonusClockImage.sprite = fullBoardBonusClockSprite;
            fullBoardBonusClockImage.color = Color.white;
        }
    }

    private RectTransform CreateFullBoardBonusPanel()
    {
        var panelObject = new GameObject("FullBoardBonusPanel", typeof(RectTransform));
        panelObject.transform.SetParent(transform, false);

        var panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(260f, 120f);

        var background = panelObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);

        fullBoardBonusCanvasGroup = panelObject.AddComponent<CanvasGroup>();

        fullBoardBonusClock = CreateFullBoardBonusClock(panel);
        panelObject.SetActive(false);
        return panel;
    }

    private RectTransform CreateFullBoardBonusClock(RectTransform parent)
    {
        var clockObject = new GameObject("Clock", typeof(RectTransform));
        clockObject.transform.SetParent(parent, false);

        var clock = clockObject.GetComponent<RectTransform>();
        clock.anchorMin = new Vector2(0.5f, 0.5f);
        clock.anchorMax = new Vector2(0.5f, 0.5f);
        clock.pivot = new Vector2(0.5f, 0.5f);
        clock.anchoredPosition = Vector2.zero;
        clock.sizeDelta = new Vector2(72f, 72f);

        fullBoardBonusClockImage = clockObject.AddComponent<Image>();
        fullBoardBonusClockImage.sprite = fullBoardBonusClockSprite;
        fullBoardBonusClockImage.color = Color.white;
        fullBoardBonusClockImage.raycastTarget = false;
        return clock;
    }

    private IEnumerator PlayFullBoardMoveBonusRoutine(Action onComplete)
    {
        var panelStartScale = fullBoardBonusPanel.localScale;
        if (panelStartScale.sqrMagnitude <= 0.0001f)
        {
            panelStartScale = Vector3.one;
        }

        fullBoardBonusPanel.gameObject.SetActive(true);
        fullBoardBonusPanel.SetAsLastSibling();
        fullBoardBonusPanel.localScale = Vector3.zero;

        if (fullBoardBonusCanvasGroup != null)
        {
            fullBoardBonusCanvasGroup.alpha = 1f;
        }

        var clockStartLocalPosition = fullBoardBonusClock.localPosition;
        var clockStartScale = fullBoardBonusClock.localScale;
        fullBoardBonusClock.localPosition = clockStartLocalPosition;
        fullBoardBonusClock.localScale = clockStartScale;

        var scaleSeconds = Mathf.Max(0.01f, fullBoardBonusScaleSeconds);
        for (var elapsed = 0f; elapsed < scaleSeconds; elapsed += Time.deltaTime)
        {
            fullBoardBonusPanel.localScale = Vector3.Lerp(Vector3.zero, panelStartScale, Mathf.SmoothStep(0f, 1f, elapsed / scaleSeconds));
            yield return null;
        }

        fullBoardBonusPanel.localScale = panelStartScale;
        yield return new WaitForSeconds(Mathf.Max(0f, fullBoardBonusVisibleSeconds - scaleSeconds));

        var from = fullBoardBonusClock.position;
        var to = remainingMovesLabel.rectTransform.position;
        var moveSeconds = Mathf.Max(0.01f, fullBoardBonusMoveSeconds);
        for (var elapsed = 0f; elapsed < moveSeconds; elapsed += Time.deltaTime)
        {
            var t = Mathf.SmoothStep(0f, 1f, elapsed / moveSeconds);
            fullBoardBonusClock.position = Vector3.Lerp(from, to, t);
            if (fullBoardBonusCanvasGroup != null)
            {
                fullBoardBonusCanvasGroup.alpha = Mathf.Lerp(1f, 0.65f, t);
            }

            yield return null;
        }

        fullBoardBonusClock.position = to;

        // Âm thanh và rung lắc khi đồng hồ chạm đích
        PlayClockImpactSound();
        PlayTargetShake();

        fullBoardBonusClock.localPosition = clockStartLocalPosition;
        fullBoardBonusClock.localScale = clockStartScale;
        fullBoardBonusPanel.localScale = panelStartScale;
        if (fullBoardBonusCanvasGroup != null)
        {
            fullBoardBonusCanvasGroup.alpha = 0f;
        }

        fullBoardBonusPanel.gameObject.SetActive(false);
        fullBoardBonusRoutine = null;

        if (onComplete != null)
        {
            onComplete();
        }
    }

    private void PlayClockImpactSound()
    {
        if (AudioManager.ins == null)
        {
            return;
        }

        if (clockImpactSound != null)
        {
            AudioManager.ins.PlaySound(clockImpactSound);
            return;
        }

        AudioManager.ins.PlayGoalHit();
    }

    private void PlayTargetShake()
    {
        if (remainingMovesLabel == null)
        {
            return;
        }

        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
        }

        shakeRoutine = StartCoroutine(ShakeTargetRoutine(remainingMovesLabel.rectTransform));
    }

    private IEnumerator ShakeTargetRoutine(RectTransform target)
    {
        var originalPos = target.anchoredPosition;
        var originalScale = target.localScale;
        var duration = Mathf.Max(0.01f, shakeDuration);

        for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            var t = elapsed / duration;
            var decay = 1f - t;

            // Rung vị trí ngẫu nhiên
            var offsetX = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude * decay;
            var offsetY = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude * decay;
            target.anchoredPosition = originalPos + new Vector2(offsetX, offsetY);

            // Punch scale (phóng to nhẹ rồi thu lại)
            var scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * (punchScaleAmount - 1f);
            target.localScale = originalScale * scaleMultiplier;

            yield return null;
        }

        target.anchoredPosition = originalPos;
        target.localScale = originalScale;
        shakeRoutine = null;
    }

    private void HideLegacyTutorialPanel()
    {
        var panel = transform.Find("TutorialPanel");
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private void SetRemainingMoves(int remainingPlacements)
    {
        if (remainingMovesLabel != null)
        {
            remainingMovesLabel.text = remainingPlacements.ToString();
        }
    }
}