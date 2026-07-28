using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private BlockManager blockManager;
    [SerializeField] private HarvestManager harvestManager;
    [SerializeField] private PlayableUI playableUI;
    [SerializeField] private float sessionSeconds = 20f;

    public bool IsRunning { get; private set; }

    private void Awake()
    {
        var root = transform.parent != null ? transform.parent : transform;
        if (boardManager == null) boardManager = root.GetComponentInChildren<BoardManager>(true);
        if (blockManager == null) blockManager = root.GetComponentInChildren<BlockManager>(true);
        if (harvestManager == null) harvestManager = root.GetComponentInChildren<HarvestManager>(true);
        if (playableUI == null) playableUI = root.GetComponentInChildren<PlayableUI>(true);
    }

    private void Start()
    {
        StartPrototype();
    }

    public void StartPrototype()
    {
        IsRunning = true;
        if (boardManager != null) boardManager.ResetBoard();
        if (blockManager != null) blockManager.PrepareStartingBlocks();
        if (harvestManager != null) harvestManager.ResetObjectives();
        if (playableUI != null) playableUI.ShowGameplay(sessionSeconds);
    }

    public void CompletePrototype()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        if (LunaManager.ins != null)
        {
            LunaManager.ins.ShowWinCard();
        }
        else if (playableUI != null)
        {
            playableUI.ShowCta();
        }
    }
}
