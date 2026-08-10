using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class PlayableUI : MonoBehaviour
{
    [SerializeField] private Text objectiveText;
    [SerializeField] private Text timerText;
    [SerializeField] private TextMeshProUGUI remainingMovesLabel;
    [SerializeField] private GameObject ctaPanel;

    private BlockManager blockManager;

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
