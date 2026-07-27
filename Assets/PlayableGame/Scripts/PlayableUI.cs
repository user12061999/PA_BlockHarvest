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

    private void Awake()
    {
        if (objectiveText == null) objectiveText = FindText("ObjectiveText");
        if (timerText == null) timerText = FindText("TimerText");

        if (ctaPanel == null)
        {
            var panel = transform.Find("CtaPanel");
            if (panel != null) ctaPanel = panel.gameObject;
        }
    }

    public void ShowGameplay(float sessionSeconds)
    {
        SetHarvestCounts(0, 0, 0, 0);
        if (timerText != null) timerText.text = Mathf.CeilToInt(sessionSeconds).ToString();
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

    private Text FindText(string childName)
    {
        var child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private void SetRemainingMoves(int remainingPlacements)
    {
        if (remainingMovesLabel != null)
        {
            remainingMovesLabel.text = remainingPlacements.ToString();
        }
    }
}
