using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class HarvestManager : MonoBehaviour
{
    private enum ResourceGoalKind
    {
        Wheat,
        Meat,
        Flower,
        Fish
    }

    [Serializable]
    private sealed class ResourceGoalSlot
    {
        public ResourceGoalKind kind;
        public GameObject prefab;
    }

    private sealed class RuntimeResourceGoal
    {
        public TileType resourceType;
        public ResourceGoalKind kind;
        public int amount;
        public int remainingAmount;
        public GameObject prefab;
        public ResourceGoalView view;
    }

    private struct TruckPickupStop
    {
        public ResourceGoalView view;
        public Vector3 stopPosition;
        public float progress;
    }

    [SerializeField] private List<LevelConfig.ResourceGoal> defaultResourceGoals = new List<LevelConfig.ResourceGoal>
    {
        new LevelConfig.ResourceGoal { resourceType = TileType.Wheat, amount = 3 },
        new LevelConfig.ResourceGoal { resourceType = TileType.Boar, amount = 12 },
        new LevelConfig.ResourceGoal { resourceType = TileType.Flower, amount = 8 },
        new LevelConfig.ResourceGoal { resourceType = TileType.Fish, amount = 2 }
    };
    [SerializeField] private int maxPlacements = 12;
    [SerializeField] private int extraPlacementsOnFullBoard = 3;
    [SerializeField] private LevelConfig levelConfig;
    [Header("World Resource Goals")]
    [SerializeField] private Transform resourceGoalRoot;
    [SerializeField] private List<ResourceGoalSlot> resourceGoalPrefabs = new List<ResourceGoalSlot>();
    [SerializeField] private float resourceGoalSpacing = 1f;
    [Header("End Truck")]
    [SerializeField] private GameObject truckPrefab;
    [SerializeField] private Transform truck;
    [SerializeField] private Sprite loadedTruckSprite;
    [SerializeField] private Vector3 truckExitWorldPosition = new Vector3(5f, 0f, 0f);
    [SerializeField] private float truckMoveSeconds = 0.35f;
    [SerializeField] private float basketPickupSeconds = 0.12f;
    [SerializeField] private GameObject basketPickupEffectPrefab;
    [SerializeField] private float resourceFlySeconds = 0.35f;
    [SerializeField] private float resourceFlyScale = 1.5f;
    [SerializeField] private AudioClip fullBoardBonusSound;
    private const float WinCardDelayAfterTruckStart = 3f;

    private PlayableUI playableUI;
    private GameManager gameManager;
    private BoardManager currentBoard;
    private GameObject spawnedTruck;
    private int wheat;
    private int meat;
    private int flower;
    private int fish;
    private int remainingPlacements;
    private bool completionSequenceStarted;
    private bool levelEnding;
    private bool boardHarvestInProgress;
    private int pendingHarvestAnimations;
    private int pendingFullBoardBonusPlacements;
    private bool fullBoardBonusInProgress;
    private bool fullBoardBonusDelayInProgress;
    private Coroutine fullBoardBonusDelayRoutine;
    private int pendingWheatFlyValue;
    private int pendingMeatFlyValue;
    private int pendingFlowerFlyValue;
    private int pendingFishFlyValue;
    private int pendingAnimalActions;
    private bool placementResolveInProgress;
    private bool levelCompleted;
    private bool winCardDelayScheduled;
    private Coroutine winCardDelayRoutine;
    private readonly List<RuntimeResourceGoal> activeResourceGoals = new List<RuntimeResourceGoal>();

    public int WheatGoal => GetTotalGoal(ResourceGoalKind.Wheat);
    public int FishGoal => GetTotalGoal(ResourceGoalKind.Fish);
    public int Wheat => wheat;
    public int Meat => meat;
    public int Flower => flower;
    public int Fish => fish;
    public int RemainingPlacements => remainingPlacements;
    public LevelConfig Config => levelConfig;
    public bool IsGoalComplete => AreResourceGoalsComplete(false);
    public bool IsLevelOver => pendingAnimalActions == 0
        && (levelEnding
            || IsGoalComplete
            || (remainingPlacements == 0 && pendingFullBoardBonusPlacements <= 0 && !fullBoardBonusInProgress));

    private void Awake()
    {
        playableUI = FindObjectOfType<PlayableUI>();
        gameManager = FindObjectOfType<GameManager>();
    }

    public void ResetObjectives()
    {
        ClearResourceGoals();
        ApplyLevelConfig();
        wheat = 0;
        meat = 0;
        flower = 0;
        fish = 0;
        remainingPlacements = maxPlacements;
        completionSequenceStarted = false;
        levelEnding = false;
        boardHarvestInProgress = false;
        pendingHarvestAnimations = 0;
        pendingFullBoardBonusPlacements = 0;
        fullBoardBonusInProgress = false;
        fullBoardBonusDelayInProgress = false;
        if (fullBoardBonusDelayRoutine != null)
        {
            StopCoroutine(fullBoardBonusDelayRoutine);
            fullBoardBonusDelayRoutine = null;
        }

        pendingWheatFlyValue = 0;
        pendingMeatFlyValue = 0;
        pendingFlowerFlyValue = 0;
        pendingFishFlyValue = 0;
        pendingAnimalActions = 0;
        placementResolveInProgress = false;
        levelCompleted = false;
        winCardDelayScheduled = false;
        if (winCardDelayRoutine != null)
        {
            StopCoroutine(winCardDelayRoutine);
            winCardDelayRoutine = null;
        }

        currentBoard = null;
        SpawnResourceGoals();
        UpdateUI();
    }

    public void Configure(LevelConfig config)
    {
        levelConfig = config;
        ResetObjectives();
    }

    public void ResolvePlacement(BoardManager board, List<Vector2Int> placedCells)
    {
        if (board == null || placedCells == null || placedCells.Count == 0)
        {
            return;
        }

        currentBoard = board;
        placementResolveInProgress = true;
        remainingPlacements = Mathf.Max(0, remainingPlacements - 1);
        var cellsToCheck = new List<CellData>(placedCells.Count * 5);

        foreach (var coordinate in placedCells)
        {
            AddUnique(cellsToCheck, board.GetCell(coordinate));

            foreach (var neighbor in board.GetNeighbors4(coordinate))
            {
                AddUnique(cellsToCheck, neighbor);
            }
        }

        var claimedTargets = new List<CellData>(cellsToCheck.Count);
        foreach (var cell in cellsToCheck)
        {
            ResolveCell(board, cell, claimedTargets);
        }

        board.ResolveDirt3x3Growth(placedCells);
        ResolveWaterYield(board, placedCells);
        placementResolveInProgress = false;
        FinishPlacementWhenReady(board);
        UpdateUI();
    }

    private void FinishPlacementWhenReady(BoardManager board)
    {
        if (pendingAnimalActions > 0)
        {
            return;
        }

        var boardIsFull = board.IsFull();
        var visibleGoalComplete = IsVisibleGoalComplete();

        if (boardIsFull && !visibleGoalComplete)
        {
            pendingFullBoardBonusPlacements = Mathf.Max(
                pendingFullBoardBonusPlacements,
                Mathf.Max(0, extraPlacementsOnFullBoard));
            if (pendingFullBoardBonusPlacements > 0 && !fullBoardBonusInProgress && !fullBoardBonusDelayInProgress)
            {
                if (Application.isPlaying)
                {
                    fullBoardBonusDelayInProgress = true;
                    fullBoardBonusDelayRoutine = StartCoroutine(StartFullBoardBonusAfterDelay());
                }
                else
                {
                    TryStartFullBoardBonus();
                }
            }
        }
        else if (visibleGoalComplete)
        {
            levelEnding = true;
        }

        if (boardIsFull || remainingPlacements == 0 || visibleGoalComplete)
        {
            HarvestBoard(board);
        }
    }

    private void ResolveCell(BoardManager board, CellData cell, List<CellData> claimedTargets)
    {
        if (cell == null)
        {
            return;
        }

        if (cell.resourceType == TileType.Boar)
        {
            var wheatCell = FirstAvailableNeighborResource(board, cell, TileType.Wheat, claimedTargets);
            if (wheatCell == null)
            {
                return;
            }

            AddUnique(claimedTargets, wheatCell);
            BeginAnimalAction();
            board.PlayAnimalEat(cell, wheatCell, true, () =>
            {
                board.SetResource(wheatCell, TileType.BabyBoar);
                UpdateUI();
            }, () => CompleteAnimalAction(board));
            return;
        }

        if (cell.resourceType == TileType.BabyBoar)
        {
            var wheatCell = FirstAvailableNeighborResource(board, cell, TileType.Wheat, claimedTargets);
            if (wheatCell == null)
            {
                return;
            }

            AddUnique(claimedTargets, wheatCell);
            BeginAnimalAction();
            board.PlayAnimalEat(cell, wheatCell, true, () =>
            {
                board.SetResource(wheatCell, TileType.Empty);
                board.SetResource(cell, TileType.Boar);
                UpdateUI();
            }, () => CompleteAnimalAction(board));
            return;
        }

        if (cell.resourceType == TileType.Bear)
        {
            var fishCell = FirstAvailableNeighborResource(board, cell, TileType.Fish, claimedTargets);
            if (fishCell == null)
            {
                return;
            }

            AddUnique(claimedTargets, fishCell);
            var fishValue = fishCell.resourceValue;
            var bearValue = cell.resourceValue;
            BeginAnimalAction();
            board.PlayAnimalEat(cell, fishCell, false, () =>
            {
                board.MoveResource(cell, fishCell, bearValue + fishValue);
                UpdateUI();
            }, () => CompleteAnimalAction(board));
        }
    }

    private void BeginAnimalAction()
    {
        pendingAnimalActions++;
    }

    private void CompleteAnimalAction(BoardManager board)
    {
        pendingAnimalActions = Mathf.Max(0, pendingAnimalActions - 1);
        if (pendingAnimalActions == 0 && !placementResolveInProgress)
        {
            FinishPlacementWhenReady(board);
        }

        UpdateUI();
    }

    private void ResolveWaterYield(BoardManager board, List<Vector2Int> placedCells)
    {
        var boostedCells = new List<CellData>(4);

        foreach (var coordinate in placedCells)
        {
            var cell = board.GetCell(coordinate);
            if (cell == null)
            {
                continue;
            }

            if (cell.tileType == TileType.Water)
            {
                AddWaterConnectedResources(board, cell, boostedCells);
            }
            else if ((cell.resourceType == TileType.Wheat || cell.resourceType == TileType.Flower)
                && PlayNeighborWaterEffect(board, cell))
            {
                AddUnique(boostedCells, cell);
            }
        }

        foreach (var cell in boostedCells)
        {
            board.AddResourceValue(cell, 1);
        }
    }

    private void HarvestBoard(BoardManager board)
    {
        boardHarvestInProgress = Application.isPlaying && HasBoardResources(board);
        board.ClearBoard(
            (resourceType, value, worldPosition) => CollectHarvestResource(board, resourceType, value, worldPosition),
            () => CompleteBoardClear(board));
        if (!Application.isPlaying || !boardHarvestInProgress)
        {
            boardHarvestInProgress = false;
        }
    }

    private void CompleteBoardClear(BoardManager board)
    {
        boardHarvestInProgress = false;
        if (pendingHarvestAnimations == 0 && (levelEnding || IsGoalComplete))
        {
            BeginCompletionSequence();
            return;
        }

        UpdateUI();
    }

    private void AddHarvest(TileType resourceType, int value)
    {
        switch (resourceType)
        {
            case TileType.Wheat:
                wheat += value;
                break;
            case TileType.Flower:
                flower += value;
                break;
            case TileType.Fish:
                fish += value;
                break;
            case TileType.Boar:
            case TileType.BabyBoar:
            case TileType.Bear:
            case TileType.Pig:
                meat += value;
                break;
        }
    }

    private void CollectHarvestResource(BoardManager board, TileType resourceType, int value, Vector3 worldPosition)
    {
        if (!Application.isPlaying)
        {
            AddHarvest(resourceType, value);
            ConsumeResourceGoal(resourceType, value);
            UpdateUI();
            return;
        }

        pendingHarvestAnimations++;
        var goal = GetResourceGoalView(resourceType);
        AddPendingFlyValue(resourceType, value);
        StartCoroutine(FlyHarvestResource(board, resourceType, value, worldPosition, goal));
    }

    private IEnumerator FlyHarvestResource(BoardManager board, TileType resourceType, int value, Vector3 from, ResourceGoalView goal)
    {
        var to = goal != null ? goal.TargetWorldPosition : from;
        var sprite = board != null ? board.GetTileSprite(resourceType) : (goal != null ? goal.ResourceSprite : null);

        if (AudioManager.ins != null)
        {
            AudioManager.ins.PlayResourceFly();
        }

        var marker = new GameObject("ResourceGoalFly_" + resourceType);
        marker.transform.position = from;
        marker.transform.localScale = Vector3.one * resourceFlyScale;

        var renderer = marker.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 80;

        yield return MoveWorld(marker.transform, from, to, resourceFlySeconds);
        Destroy(marker);

        AddHarvest(resourceType, value);
        ConsumeResourceGoal(resourceType, value);
        RemovePendingFlyValue(resourceType, value);
        pendingHarvestAnimations = Mathf.Max(0, pendingHarvestAnimations - 1);
        if (pendingHarvestAnimations == 0)
        {
            boardHarvestInProgress = false;
        }

        UpdateUI();
    }

    private ResourceGoalView GetResourceGoalView(TileType resourceType)
    {
        var kind = GetGoalKind(resourceType);
        var pendingValue = GetPendingFlyValue(kind);
        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal == null || goal.kind != kind || goal.view == null)
            {
                continue;
            }

            if (pendingValue >= goal.remainingAmount)
            {
                pendingValue -= goal.remainingAmount;
                continue;
            }

            if (goal.remainingAmount > 0)
            {
                return goal.view;
            }
        }

        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal != null && goal.kind == kind && goal.view != null)
            {
                return goal.view;
            }
        }

        return null;
    }

    private void ConsumeResourceGoal(TileType resourceType, int value)
    {
        var kind = GetGoalKind(resourceType);
        var remainingValue = Mathf.Max(0, value);
        for (var i = 0; i < activeResourceGoals.Count && remainingValue > 0; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal == null || goal.kind != kind || goal.remainingAmount <= 0)
            {
                continue;
            }

            var consumed = Mathf.Min(goal.remainingAmount, remainingValue);
            goal.remainingAmount -= consumed;
            remainingValue -= consumed;
        }
    }

    private ResourceGoalKind GetGoalKind(TileType resourceType)
    {
        switch (resourceType)
        {
            case TileType.Wheat: return ResourceGoalKind.Wheat;
            case TileType.Flower: return ResourceGoalKind.Flower;
            case TileType.Fish: return ResourceGoalKind.Fish;
            default: return ResourceGoalKind.Meat;
        }
    }

    private void AddPendingFlyValue(TileType resourceType, int value)
    {
        AddPendingFlyValue(GetGoalKind(resourceType), value);
    }

    private void RemovePendingFlyValue(TileType resourceType, int value)
    {
        AddPendingFlyValue(GetGoalKind(resourceType), -value);
    }

    private void AddPendingFlyValue(ResourceGoalKind kind, int value)
    {
        switch (kind)
        {
            case ResourceGoalKind.Wheat:
                pendingWheatFlyValue = Mathf.Max(0, pendingWheatFlyValue + value);
                break;
            case ResourceGoalKind.Meat:
                pendingMeatFlyValue = Mathf.Max(0, pendingMeatFlyValue + value);
                break;
            case ResourceGoalKind.Flower:
                pendingFlowerFlyValue = Mathf.Max(0, pendingFlowerFlyValue + value);
                break;
            case ResourceGoalKind.Fish:
                pendingFishFlyValue = Mathf.Max(0, pendingFishFlyValue + value);
                break;
        }
    }

    private int GetPendingFlyValue(ResourceGoalKind kind)
    {
        switch (kind)
        {
            case ResourceGoalKind.Wheat: return pendingWheatFlyValue;
            case ResourceGoalKind.Meat: return pendingMeatFlyValue;
            case ResourceGoalKind.Flower: return pendingFlowerFlyValue;
            case ResourceGoalKind.Fish: return pendingFishFlyValue;
            default: return 0;
        }
    }

    private void AddWaterConnectedResources(BoardManager board, CellData startCell, List<CellData> resourceCells)
    {
        var open = new List<CellData> { startCell };
        var visited = new List<CellData>(8);

        for (var i = 0; i < open.Count; i++)
        {
            var waterCell = open[i];
            AddUnique(visited, waterCell);

            foreach (var neighbor in board.GetNeighbors4(waterCell.coordinate))
            {
                if (neighbor.tileType == TileType.Water && !ContainsCell(visited, neighbor) && !ContainsCell(open, neighbor))
                {
                    open.Add(neighbor);
                }

                if (neighbor.resourceType == TileType.Wheat || neighbor.resourceType == TileType.Flower)
                {
                    board.PlayWaterYieldEffect(waterCell.coordinate, neighbor.coordinate);
                    AddUnique(resourceCells, neighbor);
                }
            }
        }
    }

    private bool PlayNeighborWaterEffect(BoardManager board, CellData cell)
    {
        foreach (var neighbor in board.GetNeighbors4(cell.coordinate))
        {
            if (neighbor.tileType == TileType.Water)
            {
                board.PlayWaterYieldEffect(neighbor.coordinate, cell.coordinate);
                return true;
            }
        }

        return false;
    }

    private CellData FirstAvailableNeighborResource(BoardManager board, CellData cell, TileType tileType, List<CellData> claimedTargets)
    {
        foreach (var neighbor in board.GetNeighbors4(cell.coordinate))
        {
            if (neighbor.resourceType == tileType && !ContainsCell(claimedTargets, neighbor))
            {
                return neighbor;
            }
        }

        return null;
    }

    private void AddUnique(List<CellData> cells, CellData cell)
    {
        if (cell == null)
        {
            return;
        }

        foreach (var existing in cells)
        {
            if (existing.coordinate == cell.coordinate)
            {
                return;
            }
        }

        cells.Add(cell);
    }

    private bool ContainsCell(List<CellData> cells, CellData cell)
    {
        foreach (var existing in cells)
        {
            if (existing.coordinate == cell.coordinate)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateUI()
    {
        if (playableUI == null)
        {
            playableUI = FindObjectOfType<PlayableUI>();
        }

        UpdateResourceGoals();

        if (playableUI != null)
        {
            playableUI.SetHarvestCounts(BuildGoalSummary(), remainingPlacements);
        }

        if (!levelEnding && !boardHarvestInProgress && pendingHarvestAnimations == 0 && pendingAnimalActions == 0 && currentBoard != null && HasBoardResources(currentBoard) && IsVisibleGoalComplete())
        {
            levelEnding = true;
            HarvestBoard(currentBoard);
            return;
        }

        if (IsGoalComplete && !boardHarvestInProgress && pendingHarvestAnimations == 0 && pendingAnimalActions == 0)
        {
            BeginCompletionSequence();
        }
        else if (TryStartFullBoardBonus())
        {
            return;
        }
        else if (remainingPlacements == 0
            && pendingFullBoardBonusPlacements <= 0
            && !fullBoardBonusInProgress
            && !boardHarvestInProgress
            && pendingHarvestAnimations == 0
            && pendingAnimalActions == 0)
        {
            FailLevel();
        }
    }

    private bool TryStartFullBoardBonus()
    {
        if (pendingFullBoardBonusPlacements <= 0
            || fullBoardBonusInProgress
            || fullBoardBonusDelayInProgress
            || levelEnding
            || pendingAnimalActions > 0)
        {
            return false;
        }

        var bonusPlacements = pendingFullBoardBonusPlacements;
        pendingFullBoardBonusPlacements = 0;
        fullBoardBonusInProgress = true;

        if (playableUI == null)
        {
            playableUI = FindObjectOfType<PlayableUI>();
        }

        if (playableUI != null && Application.isPlaying)
        {
            PlayFullBoardBonusSound();
            playableUI.PlayFullBoardMoveBonus(bonusPlacements, () => CompleteFullBoardBonus(bonusPlacements));
        }
        else
        {
            CompleteFullBoardBonus(bonusPlacements);
        }

        return true;
    }

    private void PlayFullBoardBonusSound()
    {
        if (AudioManager.ins == null)
        {
            return;
        }

        if (fullBoardBonusSound != null)
        {
            AudioManager.ins.PlaySound(fullBoardBonusSound);
            return;
        }

        AudioManager.ins.PlayResourceGain();
    }

    private IEnumerator StartFullBoardBonusAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        fullBoardBonusDelayInProgress = false;
        fullBoardBonusDelayRoutine = null;
        TryStartFullBoardBonus();
    }

    private void CompleteFullBoardBonus(int bonusPlacements)
    {
        remainingPlacements += Mathf.Max(0, bonusPlacements);
        fullBoardBonusInProgress = false;
        UpdateUI();
    }

    private void SpawnResourceGoals()
    {
        var parent = resourceGoalRoot != null ? resourceGoalRoot : transform;
        var activeCount = CountActiveResourceGoals();
        var spawnedIndex = 0;

        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (!IsActiveResourceGoal(goal))
            {
                continue;
            }

            var goalObject = Instantiate(goal.prefab, parent);
            goalObject.transform.localPosition = Vector3.right * ((spawnedIndex - (activeCount - 1) * 0.5f) * resourceGoalSpacing);
            goalObject.transform.localRotation = Quaternion.identity;
            goal.view = goalObject.GetComponent<ResourceGoalView>();
            if (goal.view == null)
            {
                goal.view = goalObject.AddComponent<ResourceGoalView>();
            }

            goal.view.Initialize();
            spawnedIndex++;
        }
    }

    private int CountActiveResourceGoals()
    {
        var count = 0;
        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            if (IsActiveResourceGoal(activeResourceGoals[i]))
            {
                count++;
            }
        }

        return count;
    }

    private bool IsActiveResourceGoal(RuntimeResourceGoal goal)
    {
        return goal != null && goal.prefab != null && goal.amount > 0;
    }

    private void ClearResourceGoals()
    {
        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal == null || goal.view == null)
            {
                continue;
            }

            DestroyObject(goal.view.gameObject);
            goal.view = null;
        }

        if (spawnedTruck != null)
        {
            DestroyObject(spawnedTruck);
            spawnedTruck = null;
        }
    }

    private void UpdateResourceGoals()
    {
        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal == null || goal.view == null)
            {
                continue;
            }

            goal.view.SetValue(goal.amount - goal.remainingAmount, goal.amount);
        }
    }

    private bool AreResourceGoalsComplete(bool includeBoard)
    {
        var hasGoals = false;
        var wheatAvailable = includeBoard ? CountBoardResource(ResourceGoalKind.Wheat) : 0;
        var meatAvailable = includeBoard ? CountBoardResource(ResourceGoalKind.Meat) : 0;
        var flowerAvailable = includeBoard ? CountBoardResource(ResourceGoalKind.Flower) : 0;
        var fishAvailable = includeBoard ? CountBoardResource(ResourceGoalKind.Fish) : 0;

        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal == null || goal.amount <= 0)
            {
                continue;
            }

            hasGoals = true;
            var remainingAmount = goal.remainingAmount;
            if (includeBoard)
            {
                var consumed = Mathf.Min(remainingAmount, GetAvailableResource(goal.kind, wheatAvailable, meatAvailable, flowerAvailable, fishAvailable));
                remainingAmount -= consumed;
                SubtractAvailableResource(goal.kind, consumed, ref wheatAvailable, ref meatAvailable, ref flowerAvailable, ref fishAvailable);
            }

            if (remainingAmount > 0)
            {
                return false;
            }
        }

        return hasGoals;
    }

    private int GetAvailableResource(ResourceGoalKind kind, int wheatAvailable, int meatAvailable, int flowerAvailable, int fishAvailable)
    {
        switch (kind)
        {
            case ResourceGoalKind.Wheat: return wheatAvailable;
            case ResourceGoalKind.Meat: return meatAvailable;
            case ResourceGoalKind.Flower: return flowerAvailable;
            case ResourceGoalKind.Fish: return fishAvailable;
            default: return 0;
        }
    }

    private void SubtractAvailableResource(
        ResourceGoalKind kind,
        int amount,
        ref int wheatAvailable,
        ref int meatAvailable,
        ref int flowerAvailable,
        ref int fishAvailable)
    {
        switch (kind)
        {
            case ResourceGoalKind.Wheat:
                wheatAvailable -= amount;
                break;
            case ResourceGoalKind.Meat:
                meatAvailable -= amount;
                break;
            case ResourceGoalKind.Flower:
                flowerAvailable -= amount;
                break;
            case ResourceGoalKind.Fish:
                fishAvailable -= amount;
                break;
        }
    }

    private int GetTotalGoal(ResourceGoalKind kind)
    {
        var total = 0;
        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal != null && goal.kind == kind)
            {
                total += goal.amount;
            }
        }

        return total;
    }

    private List<string> BuildGoalSummary()
    {
        var summary = new List<string>(activeResourceGoals.Count);
        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var goal = activeResourceGoals[i];
            if (goal == null || goal.amount <= 0)
            {
                continue;
            }

            summary.Add(GetGoalName(goal.kind) + " " + goal.remainingAmount);
        }

        return summary;
    }

    private string GetGoalName(ResourceGoalKind kind)
    {
        switch (kind)
        {
            case ResourceGoalKind.Wheat: return "Wheat";
            case ResourceGoalKind.Meat: return "Meat";
            case ResourceGoalKind.Flower: return "Flower";
            case ResourceGoalKind.Fish: return "Fish";
            default: return "Goal";
        }
    }

    private int CountBoardResource(ResourceGoalKind kind)
    {
        if (currentBoard == null || boardHarvestInProgress || pendingHarvestAnimations > 0 || pendingAnimalActions > 0)
        {
            return 0;
        }

        var total = 0;
        var cells = currentBoard.Cells;
        var size = currentBoard.BoardSize;
        for (var x = 0; x < size.x; x++)
        {
            for (var y = 0; y < size.y; y++)
            {
                var cell = cells[x, y];
                if (cell != null && cell.resourceType != TileType.Empty && GetGoalKind(cell.resourceType) == kind)
                {
                    total += cell.resourceValue;
                }
            }
        }

        return total;
    }

    private void BeginCompletionSequence()
    {
        if (completionSequenceStarted)
        {
            return;
        }

        completionSequenceStarted = true;
        if (LunaManager.ins != null)
        {
            LunaManager.ins.CancelTimedEndCard();
        }

        if (Application.isPlaying)
        {
            StartCoroutine(CompletionSequence());
        }
        else
        {
            CompleteLevel();
        }
    }

    private bool IsVisibleGoalComplete()
    {
        return AreResourceGoalsComplete(true);
    }

    private bool HasBoardResources(BoardManager board)
    {
        if (board == null)
        {
            return false;
        }

        var cells = board.Cells;
        var size = board.BoardSize;
        for (var x = 0; x < size.x; x++)
        {
            for (var y = 0; y < size.y; y++)
            {
                var cell = cells[x, y];
                if (cell != null && cell.resourceType != TileType.Empty)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator CompletionSequence()
    {
        yield return RunTruckPickup();
        if (!winCardDelayScheduled)
        {
            CompleteLevel();
        }
    }

    private IEnumerator RunTruckPickup()
    {
        var truckTransform = GetTruckTransform();
        if (truckTransform == null)
        {
            yield break;
        }

        var stops = BuildTruckPickupStops(truckTransform.position, truckExitWorldPosition);
        for (var i = 0; i < stops.Count; i++)
        {
            var view = stops[i].view;

            if (AudioManager.ins != null)
            {
                AudioManager.ins.PlayTruckMove();
            }

            ScheduleWinCardAfterTruckStart();
            yield return MoveWorld(truckTransform, truckTransform.position, stops[i].stopPosition, truckMoveSeconds);
            PlayBasketPickupEffect(view.transform.position);
            view.Pickup();
            ApplyLoadedTruckSprite(truckTransform);

            if (basketPickupSeconds > 0f)
            {
                yield return new WaitForSeconds(basketPickupSeconds);
            }
        }

        if (AudioManager.ins != null)
        {
            AudioManager.ins.PlayTruckMove();
        }

        ScheduleWinCardAfterTruckStart();
        yield return MoveWorld(truckTransform, truckTransform.position, truckExitWorldPosition, truckMoveSeconds);
        HideTruckAtExit(truckTransform);
    }

    private void ScheduleWinCardAfterTruckStart()
    {
        if (winCardDelayScheduled)
        {
            return;
        }

        winCardDelayScheduled = true;
        winCardDelayRoutine = StartCoroutine(ShowWinCardAfterTruckDelay());
    }

    private IEnumerator ShowWinCardAfterTruckDelay()
    {
        yield return new WaitForSeconds(WinCardDelayAfterTruckStart);
        winCardDelayRoutine = null;
        CompleteLevel();
    }

    private void ApplyLoadedTruckSprite(Transform truckTransform)
    {
        if (loadedTruckSprite == null || truckTransform == null)
        {
            return;
        }

        var renderer = truckTransform.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null)
        {
            renderer.sprite = loadedTruckSprite;
        }
    }

    private List<TruckPickupStop> BuildTruckPickupStops(Vector3 start, Vector3 end)
    {
        var stops = new List<TruckPickupStop>(activeResourceGoals.Count);
        var route = end - start;
        var routeSqrMagnitude = route.sqrMagnitude;
        if (routeSqrMagnitude <= 0.001f)
        {
            return stops;
        }

        for (var i = 0; i < activeResourceGoals.Count; i++)
        {
            var view = activeResourceGoals[i] != null ? activeResourceGoals[i].view : null;
            if (view == null)
            {
                continue;
            }

            var progress = Mathf.Clamp01(Vector3.Dot(view.transform.position - start, route) / routeSqrMagnitude);
            stops.Add(new TruckPickupStop
            {
                view = view,
                stopPosition = Vector3.Lerp(start, end, progress),
                progress = progress
            });
        }

        stops.Sort((a, b) => a.progress.CompareTo(b.progress));
        return stops;
    }

    private void PlayBasketPickupEffect(Vector3 position)
    {
        if (AudioManager.ins != null)
        {
            AudioManager.ins.PlayBasketPickup();
        }

        if (basketPickupEffectPrefab == null)
        {
            return;
        }

        var effect = Instantiate(basketPickupEffectPrefab, position, Quaternion.identity);
        Destroy(effect, 2f);
    }

    private Transform GetTruckTransform()
    {
        if (truck != null)
        {
            return truck;
        }

        if (truckPrefab == null)
        {
            return null;
        }

        spawnedTruck = Instantiate(truckPrefab);
        return spawnedTruck.transform;
    }

    private void HideTruckAtExit(Transform truckTransform)
    {
        if (truckTransform == null)
        {
            return;
        }

        if (spawnedTruck != null && truckTransform.gameObject == spawnedTruck)
        {
            DestroyObject(spawnedTruck);
            spawnedTruck = null;
            return;
        }

        truckTransform.gameObject.SetActive(false);
    }

    private IEnumerator MoveWorld(Transform target, Vector3 from, Vector3 to, float seconds)
    {
        seconds = Mathf.Max(0.01f, seconds);
        for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
            yield return null;
        }

        target.position = to;
    }

    private void CompleteLevel()
    {
        if (levelCompleted)
        {
            return;
        }

        levelCompleted = true;
        levelEnding = true;
        completionSequenceStarted = true;
        if (LunaManager.ins != null)
        {
            LunaManager.ins.showwincard();
            return;
        }

        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.CompletePrototype();
        }
        else if (playableUI != null)
        {
            playableUI.ShowCta();
        }
    }

    private void FailLevel()
    {
        if (levelEnding)
        {
            return;
        }

        levelEnding = true;
        if (LunaManager.ins != null)
        {
            LunaManager.ins.showlosecard();
        }
        else if (playableUI != null)
        {
            playableUI.ShowCta();
        }
    }

    private void DestroyObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void ApplyLevelConfig()
    {
        if (levelConfig != null)
        {
            maxPlacements = levelConfig.maxPlacements;
        }

        RebuildActiveResourceGoals();
    }

    private void RebuildActiveResourceGoals()
    {
        activeResourceGoals.Clear();

        var configuredGoals = levelConfig != null && levelConfig.resourceGoals != null && levelConfig.resourceGoals.Count > 0
            ? levelConfig.resourceGoals
            : defaultResourceGoals;

        for (var i = 0; i < configuredGoals.Count; i++)
        {
            var configuredGoal = configuredGoals[i];
            if (configuredGoal == null || configuredGoal.resourceType == TileType.Empty || configuredGoal.amount <= 0)
            {
                continue;
            }

            var kind = GetGoalKind(configuredGoal.resourceType);
            activeResourceGoals.Add(new RuntimeResourceGoal
            {
                resourceType = configuredGoal.resourceType,
                kind = kind,
                amount = configuredGoal.amount,
                remainingAmount = configuredGoal.amount,
                prefab = GetResourceGoalPrefab(kind)
            });
        }
    }

    private GameObject GetResourceGoalPrefab(ResourceGoalKind kind)
    {
        for (var i = 0; i < resourceGoalPrefabs.Count; i++)
        {
            var slot = resourceGoalPrefabs[i];
            if (slot != null && slot.kind == kind)
            {
                return slot.prefab;
            }
        }

        return null;
    }
}
