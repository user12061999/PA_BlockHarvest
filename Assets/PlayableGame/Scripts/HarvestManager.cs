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
        [NonSerialized] public ResourceGoalView view;
    }

    private struct TruckPickupStop
    {
        public ResourceGoalView view;
        public Vector3 stopPosition;
        public float progress;
    }

    [SerializeField] private int wheatGoal = 3;
    [SerializeField] private int meatGoal = 12;
    [SerializeField] private int flowerGoal = 8;
    [SerializeField] private int fishGoal = 2;
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
    [SerializeField] private Vector3 truckExitWorldPosition = new Vector3(5f, 0f, 0f);
    [SerializeField] private float truckMoveSeconds = 0.35f;
    [SerializeField] private float basketPickupSeconds = 0.12f;
    [SerializeField] private GameObject basketPickupEffectPrefab;
    [SerializeField] private float resourceFlySeconds = 0.35f;
    [SerializeField] private float resourceFlyScale = 1.5f;

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

    public int WheatGoal => wheatGoal;
    public int FishGoal => fishGoal;
    public int Wheat => wheat;
    public int Meat => meat;
    public int Flower => flower;
    public int Fish => fish;
    public int RemainingPlacements => remainingPlacements;
    public bool IsGoalComplete => wheat >= wheatGoal && meat >= meatGoal && flower >= flowerGoal && fish >= fishGoal;
    public bool IsLevelOver => levelEnding || IsGoalComplete || remainingPlacements == 0;

    private void Awake()
    {
        playableUI = FindObjectOfType<PlayableUI>();
        gameManager = FindObjectOfType<GameManager>();
    }

    public void ResetObjectives()
    {
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
        currentBoard = null;
        ClearResourceGoals();
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

        ResolveWaterYield(board, placedCells);

        var boardIsFull = board.IsFull();
        var visibleGoalComplete = IsVisibleGoalComplete();

        if (boardIsFull && !visibleGoalComplete)
        {
            remainingPlacements += Mathf.Max(0, extraPlacementsOnFullBoard);
        }
        else if (visibleGoalComplete)
        {
            levelEnding = true;
        }

        if (boardIsFull || remainingPlacements == 0 || visibleGoalComplete)
        {
            HarvestBoard(board);
        }

        UpdateUI();
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
            board.PlayAnimalEat(cell, wheatCell, true, () =>
            {
                board.SetResource(wheatCell, TileType.BabyBoar);
                UpdateUI();
            });
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
            board.PlayAnimalEat(cell, wheatCell, true, () =>
            {
                board.SetResource(wheatCell, TileType.Empty);
                board.SetResource(cell, TileType.Boar);
                UpdateUI();
            });
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
            board.PlayAnimalEat(cell, fishCell, false, () =>
            {
                board.MoveResource(cell, fishCell, bearValue + fishValue);
                UpdateUI();
            });
        }
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
        board.ClearBoard((resourceType, value, worldPosition) => CollectHarvestResource(board, resourceType, value, worldPosition));
        if (!Application.isPlaying || !boardHarvestInProgress)
        {
            boardHarvestInProgress = false;
        }
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
            UpdateUI();
            return;
        }

        pendingHarvestAnimations++;
        StartCoroutine(FlyHarvestResource(board, resourceType, value, worldPosition));
    }

    private IEnumerator FlyHarvestResource(BoardManager board, TileType resourceType, int value, Vector3 from)
    {
        var goal = GetResourceGoalView(resourceType);
        var to = goal != null ? goal.TargetWorldPosition : from;
        var sprite = board != null ? board.GetTileSprite(resourceType) : (goal != null ? goal.ResourceSprite : null);

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
        for (var i = 0; i < resourceGoalPrefabs.Count; i++)
        {
            var slot = resourceGoalPrefabs[i];
            if (slot != null && slot.kind == kind)
            {
                return slot.view;
            }
        }

        return null;
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
            playableUI.SetHarvestCounts(
                GetCurrent(ResourceGoalKind.Wheat),
                wheatGoal,
                GetCurrent(ResourceGoalKind.Meat),
                meatGoal,
                GetCurrent(ResourceGoalKind.Flower),
                flowerGoal,
                GetCurrent(ResourceGoalKind.Fish),
                fishGoal,
                remainingPlacements);
        }

        if (!levelEnding && !boardHarvestInProgress && pendingHarvestAnimations == 0 && currentBoard != null && HasBoardResources(currentBoard) && IsVisibleGoalComplete())
        {
            levelEnding = true;
            HarvestBoard(currentBoard);
            return;
        }

        if (IsGoalComplete && !boardHarvestInProgress && pendingHarvestAnimations == 0)
        {
            BeginCompletionSequence();
        }
        else if (remainingPlacements == 0 && !boardHarvestInProgress && pendingHarvestAnimations == 0 && playableUI != null)
        {
            playableUI.ShowCta();
        }
    }

    private void SpawnResourceGoals()
    {
        var parent = resourceGoalRoot != null ? resourceGoalRoot : transform;
        var activeCount = CountActiveResourceGoals();
        var spawnedIndex = 0;

        for (var i = 0; i < resourceGoalPrefabs.Count; i++)
        {
            var slot = resourceGoalPrefabs[i];
            if (!IsActiveResourceGoal(slot))
            {
                continue;
            }

            var goalObject = Instantiate(slot.prefab, parent);
            goalObject.transform.localPosition = Vector3.right * ((spawnedIndex - (activeCount - 1) * 0.5f) * resourceGoalSpacing);
            goalObject.transform.localRotation = Quaternion.identity;
            slot.view = goalObject.GetComponent<ResourceGoalView>();
            if (slot.view == null)
            {
                slot.view = goalObject.AddComponent<ResourceGoalView>();
            }

            slot.view.Initialize();
            spawnedIndex++;
        }
    }

    private int CountActiveResourceGoals()
    {
        var count = 0;
        for (var i = 0; i < resourceGoalPrefabs.Count; i++)
        {
            if (IsActiveResourceGoal(resourceGoalPrefabs[i]))
            {
                count++;
            }
        }

        return count;
    }

    private bool IsActiveResourceGoal(ResourceGoalSlot slot)
    {
        return slot != null && slot.prefab != null && GetGoal(slot.kind) > 0;
    }

    private void ClearResourceGoals()
    {
        for (var i = 0; i < resourceGoalPrefabs.Count; i++)
        {
            var slot = resourceGoalPrefabs[i];
            if (slot == null || slot.view == null)
            {
                continue;
            }

            DestroyObject(slot.view.gameObject);
            slot.view = null;
        }

        if (spawnedTruck != null)
        {
            DestroyObject(spawnedTruck);
            spawnedTruck = null;
        }
    }

    private void UpdateResourceGoals()
    {
        for (var i = 0; i < resourceGoalPrefabs.Count; i++)
        {
            var slot = resourceGoalPrefabs[i];
            if (slot == null || slot.view == null)
            {
                continue;
            }

            slot.view.SetValue(GetCurrent(slot.kind), GetGoal(slot.kind));
        }
    }

    private int GetCurrent(ResourceGoalKind kind)
    {
        switch (kind)
        {
            case ResourceGoalKind.Wheat: return wheat + CountBoardResource(ResourceGoalKind.Wheat);
            case ResourceGoalKind.Meat: return meat + CountBoardResource(ResourceGoalKind.Meat);
            case ResourceGoalKind.Flower: return flower + CountBoardResource(ResourceGoalKind.Flower);
            case ResourceGoalKind.Fish: return fish + CountBoardResource(ResourceGoalKind.Fish);
            default: return 0;
        }
    }

    private int CountBoardResource(ResourceGoalKind kind)
    {
        if (currentBoard == null || boardHarvestInProgress || pendingHarvestAnimations > 0)
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

    private int GetGoal(ResourceGoalKind kind)
    {
        switch (kind)
        {
            case ResourceGoalKind.Wheat: return wheatGoal;
            case ResourceGoalKind.Meat: return meatGoal;
            case ResourceGoalKind.Flower: return flowerGoal;
            case ResourceGoalKind.Fish: return fishGoal;
            default: return 0;
        }
    }

    private void BeginCompletionSequence()
    {
        if (completionSequenceStarted)
        {
            return;
        }

        completionSequenceStarted = true;
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
        return GetCurrent(ResourceGoalKind.Wheat) >= wheatGoal
            && GetCurrent(ResourceGoalKind.Meat) >= meatGoal
            && GetCurrent(ResourceGoalKind.Flower) >= flowerGoal
            && GetCurrent(ResourceGoalKind.Fish) >= fishGoal;
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
        CompleteLevel();
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

            yield return MoveWorld(truckTransform, truckTransform.position, stops[i].stopPosition, truckMoveSeconds);
            PlayBasketPickupEffect(view.transform.position);
            view.Pickup();

            if (basketPickupSeconds > 0f)
            {
                yield return new WaitForSeconds(basketPickupSeconds);
            }
        }

        yield return MoveWorld(truckTransform, truckTransform.position, truckExitWorldPosition, truckMoveSeconds);
    }

    private List<TruckPickupStop> BuildTruckPickupStops(Vector3 start, Vector3 end)
    {
        var stops = new List<TruckPickupStop>(resourceGoalPrefabs.Count);
        var route = end - start;
        var routeSqrMagnitude = route.sqrMagnitude;
        if (routeSqrMagnitude <= 0.001f)
        {
            return stops;
        }

        for (var i = 0; i < resourceGoalPrefabs.Count; i++)
        {
            var view = resourceGoalPrefabs[i] != null ? resourceGoalPrefabs[i].view : null;
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
        if (levelConfig == null)
        {
            return;
        }

        wheatGoal = levelConfig.wheatGoal;
        meatGoal = levelConfig.meatGoal;
        flowerGoal = levelConfig.flowerGoal;
        fishGoal = levelConfig.fishGoal;
        maxPlacements = levelConfig.maxPlacements;
    }
}
