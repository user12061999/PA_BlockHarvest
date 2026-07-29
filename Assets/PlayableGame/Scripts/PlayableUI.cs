using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PlayableUI : MonoBehaviour
{
    [SerializeField] private Text objectiveText;
    [SerializeField] private Text timerText;
    [SerializeField] private TextMeshProUGUI remainingMovesLabel;
    [SerializeField] private bool dismissTutorialOnScreenClick = true;
    [SerializeField] private float tutorialDismissInputDelay = 0.15f;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TextMeshProUGUI tutorialMovesLabel;
    [SerializeField] private TextMeshProUGUI tutorialGoalLabel;
    [SerializeField] private GameObject ctaPanel;

    private BlockManager blockManager;
    private Button tutorialDismissButton;
    private bool waitingForTutorialDismiss;
    private float tutorialCanDismissAt;

    public bool IsTutorialVisible => waitingForTutorialDismiss && tutorialPanel != null && tutorialPanel.activeInHierarchy;

    private void Awake()
    {
        if (objectiveText == null) objectiveText = FindText("ObjectiveText");
        if (timerText == null) timerText = FindText("TimerText");
        if (blockManager == null) blockManager = FindObjectOfType<BlockManager>();
        if (tutorialMovesLabel == null) tutorialMovesLabel = FindTmpText("TutorialMovesLabel");
        if (tutorialGoalLabel == null) tutorialGoalLabel = FindTmpText("TutorialGoalLabel");

        if (tutorialPanel == null)
        {
            var panel = transform.Find("TutorialPanel");
            if (panel != null) tutorialPanel = panel.gameObject;
        }

        BindTutorialDismissButton();

        if (ctaPanel == null)
        {
            var panel = transform.Find("CtaPanel");
            if (panel != null) ctaPanel = panel.gameObject;
        }
    }

    private void Update()
    {
        if (!waitingForTutorialDismiss || !dismissTutorialOnScreenClick || Time.unscaledTime < tutorialCanDismissAt)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) || HasTouchBegan())
        {
            DismissTutorialAndShowPlacementTutorial();
        }
    }

    public void ShowGameplay(float sessionSeconds)
    {
        if (timerText != null) timerText.text = Mathf.CeilToInt(sessionSeconds).ToString();
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
            BindTutorialDismissButton();
            waitingForTutorialDismiss = true;
            tutorialCanDismissAt = Time.unscaledTime + tutorialDismissInputDelay;
        }
        else
        {
            ShowPlacementTutorial();
        }

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
        SetTutorialCounts(wheat, wheatGoal, meat, meatGoal, flower, flowerGoal, fish, fishGoal, remainingPlacements);
    }

    public void ShowCta()
    {
        if (ctaPanel != null) ctaPanel.SetActive(true);
    }

    public void HideTutorial()
    {
        waitingForTutorialDismiss = false;
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
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

    private bool HasTouchBegan()
    {
        for (var i = 0; i < Input.touchCount; i++)
        {
            if (Input.GetTouch(i).phase == TouchPhase.Began)
            {
                return true;
            }
        }

        return false;
    }

    private void BindTutorialDismissButton()
    {
        if (tutorialPanel == null || !dismissTutorialOnScreenClick)
        {
            return;
        }

        if (tutorialDismissButton == null)
        {
            tutorialDismissButton = tutorialPanel.GetComponent<Button>();
        }

        var panelGraphic = tutorialPanel.GetComponent<Graphic>();
        if (panelGraphic == null)
        {
            var image = tutorialPanel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            panelGraphic = image;
        }

        panelGraphic.raycastTarget = true;

        if (tutorialDismissButton == null)
        {
            tutorialDismissButton = tutorialPanel.AddComponent<Button>();
        }

        tutorialDismissButton.targetGraphic = panelGraphic;
        tutorialDismissButton.interactable = true;
        tutorialDismissButton.onClick.RemoveListener(DismissTutorialAndShowPlacementTutorial);
        tutorialDismissButton.onClick.AddListener(DismissTutorialAndShowPlacementTutorial);
    }

    private Text FindText(string childName)
    {
        var child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private TextMeshProUGUI FindTmpText(string childName)
    {
        var child = transform.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private void SetRemainingMoves(int remainingPlacements)
    {
        if (remainingMovesLabel != null)
        {
            remainingMovesLabel.text = remainingPlacements.ToString();
        }
    }

    private void SetTutorialCounts(int wheat, int wheatGoal, int meat, int meatGoal, int flower, int flowerGoal, int fish, int fishGoal, int remainingPlacements)
    {
        if (tutorialMovesLabel != null)
        {
            tutorialMovesLabel.text = remainingPlacements.ToString();
        }

        if (tutorialGoalLabel != null)
        {
            tutorialGoalLabel.text = (
                Mathf.Max(0, wheatGoal - wheat)
                + Mathf.Max(0, meatGoal - meat)
                + Mathf.Max(0, flowerGoal - flower)
                + Mathf.Max(0, fishGoal - fish)).ToString();
        }
    }
}
