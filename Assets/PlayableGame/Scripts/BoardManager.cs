using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class BoardManager : MonoBehaviour
{
    [Serializable]
    public sealed class AnimalFrameSet
    {
        public TileType resourceType;
        public float framesPerSecond = 8f;
        public Sprite[] idleLandFrames;
        public Sprite[] moveLandFrames;
        public Sprite[] eatLandFrames;
        public Sprite[] idleWaterFrames;
        public Sprite[] moveWaterFrames;
        public Sprite[] eatWaterFrames;
    }

    private const int Width = 7;
    private const int Height = 8;

    private static readonly Vector2Int[] NeighborOffsets =
    {
        Vector2Int.up,
        Vector2Int.right,
        Vector2Int.down,
        Vector2Int.left
    };

    private struct ClearTile
    {
        public TileView view;
        public TileType resourceType;
        public int resourceValue;
        public Vector3 worldPosition;
    }

    [SerializeField] private bool createVisuals = true;
    [Header("Camera Layout")]
    [SerializeField] private bool centerOnCamera = true;
    [SerializeField] private bool centerGameplayWithTray = true;
    [SerializeField] private Vector2 cameraViewportPosition = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 cameraCenterOffset;
    [Header("Cell Layout")]
    [SerializeField] private float cellSize = 0.9f;
    [SerializeField] private float cellVisualScale = 1f;
    [SerializeField] private float cellGap = 0f;
    [Header("Cell Prefab")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private LevelConfig levelConfig;
    [SerializeField] private List<Vector2Int> playableCoordinates = new List<Vector2Int>();
    [SerializeField] private Sprite emptySprite;
    [SerializeField] private Sprite grassSprite;
    [SerializeField] private Sprite dirtSprite;
    [SerializeField] private Sprite dirt3x3Sprite;
    [SerializeField] private Sprite waterSprite;
    [SerializeField] private Sprite wheatSprite;
    [SerializeField] private Sprite flowerSprite;
    [SerializeField] private Sprite fishSprite;
    [SerializeField] private Sprite boarSprite;
    [SerializeField] private Sprite babyBoarSprite;
    [SerializeField] private Sprite bearSprite;
    [SerializeField] private Sprite pigSprite;
    [SerializeField] private GameObject yieldEffectPrefab;
    [SerializeField] private GameObject clearResourceEffectPrefab;
    [SerializeField] private GameObject dirt3x3EffectPrefab;
    [SerializeField] private AudioClip yieldAudioClip;
    [SerializeField] private AudioClip animalActionAudioClip;
    [SerializeField] private float boarIdleScale = 0.06f;
    [SerializeField] private float boarIdleSpeed = 4f;
    [SerializeField] private float boarTravelSeconds = 0.35f;
    [Header("Clear Animation")]
    [SerializeField] private float clearTileSeconds = 0.08f;
    [SerializeField] private float clearStepDelay = 0.03f;
    [Header("Animal Frame Animations")]
    [SerializeField] private List<AnimalFrameSet> animalAnimations = new List<AnimalFrameSet>();

    private CellData[,] cells;
    private TileView[,] tileViews;
    private readonly List<Vector2Int> previewCells = new List<Vector2Int>(4);
    private Sprite cellSprite;
    private Coroutine clearBoardRoutine;
    private BlockManager blockManager;

    public Vector2Int BoardSize => new Vector2Int(Width, Height);
    public float CellSize => cellSize;
    public float CellVisualSize => cellSize * Mathf.Max(0.01f, cellVisualScale);
    public float CellStride => Mathf.Max(0.01f, cellSize + cellGap);
    public Vector2 BoardVisualSize => new Vector2(
        (Width - 1) * CellStride + CellVisualSize,
        (Height - 1) * CellStride + CellVisualSize);
    public Vector2 VisibleBoardVisualSize
    {
        get
        {
            Vector2Int min;
            Vector2Int max;
            GetVisibleBounds(out min, out max);
            return new Vector2(
                (max.x - min.x) * CellStride + CellVisualSize,
                (max.y - min.y) * CellStride + CellVisualSize);
        }
    }

    public Vector3 VisibleBoardCenterWorld => transform.TransformPoint(GetVisibleCenterLocal());
    public CellData[,] Cells => cells;

    private void LateUpdate()
    {
        if (Application.isPlaying)
        {
            CenterOnCamera();
        }
    }

    public void SetCellSize(float size)
    {
        size = Mathf.Max(0.01f, size);
        if (Mathf.Approximately(cellSize, size))
        {
            return;
        }

        cellSize = size;
        if (createVisuals && cells != null)
        {
            BuildVisuals();
        }
    }

    public void SetPlayableCoordinates(List<Vector2Int> coordinates)
    {
        playableCoordinates = coordinates != null ? new List<Vector2Int>(coordinates) : new List<Vector2Int>();
        ResetBoard();
    }

    public void ResetBoard()
    {
        if (levelConfig != null && levelConfig.playableCoordinates.Count > 0)
        {
            playableCoordinates = new List<Vector2Int>(levelConfig.playableCoordinates);
        }

        cells = new CellData[Width, Height];

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                cells[x, y] = new CellData(new Vector2Int(x, y), TileType.Empty, false);
            }
        }

        if (createVisuals)
        {
            CenterOnCamera();
            BuildVisuals();
        }
    }

    private void CenterOnCamera()
    {
        if (!centerOnCamera)
        {
            return;
        }

        var mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }

        if (mainCamera == null)
        {
            return;
        }

        var distance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        var target = mainCamera.ViewportToWorldPoint(new Vector3(cameraViewportPosition.x, cameraViewportPosition.y, distance));
        if (blockManager == null)
        {
            blockManager = FindObjectOfType<BlockManager>();
        }

        if (centerGameplayWithTray && blockManager != null)
        {
            target += blockManager.GetBoardCenterOffsetForCenteredLayout(this);
        }

        target += new Vector3(cameraCenterOffset.x, cameraCenterOffset.y, 0f);
        target -= transform.TransformVector(GetVisibleCenterLocal());
        target.z = transform.position.z;
        if ((transform.position - target).sqrMagnitude < 0.000001f)
        {
            return;
        }

        transform.position = target;
        if (blockManager != null)
        {
            blockManager.LayoutFromBoard();
        }
    }

    public CellData GetCell(Vector2Int coordinate)
    {
        EnsureGrid();
        return IsInside(coordinate) ? cells[coordinate.x, coordinate.y] : null;
    }

    public bool IsInside(Vector2Int coordinate)
    {
        if (coordinate.x < 0 || coordinate.x >= Width || coordinate.y < 0 || coordinate.y >= Height)
        {
            return false;
        }

        return playableCoordinates == null || playableCoordinates.Count == 0 || playableCoordinates.Contains(coordinate);
    }

    public bool IsEmpty(Vector2Int coordinate)
    {
        var cell = GetCell(coordinate);
        return cell != null && !cell.occupied && cell.tileType == TileType.Empty;
    }

    public bool CanPlace(Vector2Int coordinate)
    {
        return IsEmpty(coordinate);
    }

    public bool CanPlace(Vector2Int origin, Vector2Int[] shape)
    {
        if (shape == null || shape.Length == 0)
        {
            return false;
        }

        foreach (var offset in shape)
        {
            if (!IsEmpty(origin + offset))
            {
                return false;
            }
        }

        return true;
    }

    public bool CanPlace(Vector2Int origin, BlockData blockData)
    {
        if (blockData == null || !blockData.IsValid())
        {
            return false;
        }

        foreach (var offset in blockData.positions)
        {
            if (!IsEmpty(origin + offset))
            {
                return false;
            }
        }

        return true;
    }

    public bool HasAnyPlacement(BlockData blockData)
    {
        if (blockData == null || !blockData.IsValid())
        {
            return false;
        }

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                if (CanPlace(new Vector2Int(x, y), blockData))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsFull()
    {
        EnsureGrid();

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var coordinate = new Vector2Int(x, y);
                if (IsInside(coordinate) && !cells[x, y].occupied)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool PlaceTile(Vector2Int coordinate, TileType tileType)
    {
        return PlaceTile(coordinate, tileType, TileType.Empty);
    }

    public bool PlaceTile(Vector2Int coordinate, TileType tileType, TileType resourceType)
    {
        if (!CanPlace(coordinate))
        {
            return false;
        }

        var cell = cells[coordinate.x, coordinate.y];
        cell.tileType = tileType;
        cell.resourceType = resourceType;
        cell.resourceValue = GetDefaultResourceValue(resourceType);
        cell.occupied = tileType != TileType.Empty;
        cell.dirt3x3Boosted = false;
        UpdateVisual(cell);
        return true;
    }

    public bool PlaceTile(Vector2Int origin, Vector2Int[] shape, TileType tileType)
    {
        if (!CanPlace(origin, shape))
        {
            return false;
        }

        foreach (var offset in shape)
        {
            PlaceTile(origin + offset, tileType);
        }

        return true;
    }

    public bool PlaceBlock(Vector2Int origin, BlockData blockData)
    {
        List<Vector2Int> placedCells;
        return PlaceBlock(origin, blockData, out placedCells);
    }

    public bool PlaceBlock(Vector2Int origin, BlockData blockData, out List<Vector2Int> placedCells)
    {
        placedCells = null;

        if (!CanPlace(origin, blockData))
        {
            return false;
        }

        placedCells = new List<Vector2Int>(blockData.positions.Count);

        for (var i = 0; i < blockData.positions.Count; i++)
        {
            var coordinate = origin + blockData.positions[i];
            PlaceTile(coordinate, blockData.tileTypes[i], blockData.resourceTypes[i]);
            placedCells.Add(coordinate);
        }

        ClearPreview();
        return true;
    }

    public Vector2Int WorldToCoordinate(Vector3 worldPosition)
    {
        var local = transform.InverseTransformPoint(worldPosition);
        return new Vector2Int(
            Mathf.RoundToInt(local.x / CellStride + (Width - 1) * 0.5f),
            Mathf.RoundToInt(local.y / CellStride + (Height - 1) * 0.5f));
    }

    public Vector3 CoordinateToWorld(Vector2Int coordinate)
    {
        return transform.TransformPoint(BoardToLocal(coordinate));
    }

    public bool ShowPreview(Vector2Int origin, BlockData blockData)
    {
        ClearPreview();

        if (blockData == null || !blockData.IsValid())
        {
            return false;
        }

        var isValid = CanPlace(origin, blockData);
        var previewColor = isValid ? new Color(0.2f, 0.95f, 0.25f) : new Color(1f, 0.18f, 0.14f);

        foreach (var offset in blockData.positions)
        {
            var coordinate = origin + offset;
            if (!IsInside(coordinate))
            {
                continue;
            }

            previewCells.Add(coordinate);
            var view = GetTileView(coordinate);
            if (view != null)
            {
                view.SetTileColor(previewColor);
            }
        }

        return isValid;
    }

    public void ClearPreview()
    {
        if (tileViews == null)
        {
            return;
        }

        foreach (var coordinate in previewCells)
        {
            var cell = cells[coordinate.x, coordinate.y];
            UpdateVisual(cell);
        }

        previewCells.Clear();
    }

    public List<CellData> GetNeighbors4(Vector2Int coordinate)
    {
        EnsureGrid();
        var neighbors = new List<CellData>(4);

        foreach (var offset in NeighborOffsets)
        {
            var neighbor = GetCell(coordinate + offset);
            if (neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }

        return neighbors;
    }

    public void SetResource(CellData cell, TileType resourceType)
    {
        if (cell == null)
        {
            return;
        }

        cell.resourceType = resourceType;
        cell.resourceValue = GetDefaultResourceValue(resourceType);
        UpdateVisual(cell);
        PlayYieldFeedback(cell);
    }

    public void SetResourceValue(CellData cell, int value)
    {
        if (cell == null || cell.resourceType == TileType.Empty)
        {
            return;
        }

        cell.resourceValue = value;
        UpdateVisual(cell);
        PlayYieldFeedback(cell);
    }

    public void AddResourceValue(CellData cell, int amount)
    {
        if (cell == null || cell.resourceType == TileType.Empty)
        {
            return;
        }

        cell.resourceValue += amount;
        UpdateVisual(cell);
        PlayYieldFeedback(cell);
    }

    public void MoveResource(CellData fromCell, CellData toCell, int newValue)
    {
        if (fromCell == null || toCell == null || fromCell.resourceType == TileType.Empty)
        {
            return;
        }

        var resourceType = fromCell.resourceType;
        fromCell.resourceType = TileType.Empty;
        fromCell.resourceValue = 0;
        toCell.resourceType = resourceType;
        toCell.resourceValue = newValue;
        UpdateVisual(fromCell);
        UpdateVisual(toCell);
        PlayYieldFeedback(toCell);
    }

    public void ClearBoard()
    {
        ClearBoard(null);
    }

    public void ClearBoard(Action<TileType, int, Vector3> onResourceCleared)
    {
        EnsureGrid();

        if (!Application.isPlaying || tileViews == null)
        {
            ClearBoardImmediate(onResourceCleared);
            return;
        }

        if (clearBoardRoutine != null)
        {
            StopCoroutine(clearBoardRoutine);
            clearBoardRoutine = null;
            RefreshVisuals();
        }

        var clearTiles = new List<ClearTile>();
        for (var y = Height - 1; y >= 0; y--)
        {
            for (var x = Width - 1; x >= 0; x--)
            {
                var cell = cells[x, y];
                if (IsInside(cell.coordinate) && (cell.tileType != TileType.Empty || cell.resourceType != TileType.Empty))
                {
                    var view = GetTileView(cell.coordinate);
                    if (view != null)
                    {
                        clearTiles.Add(new ClearTile
                        {
                            view = view,
                            resourceType = cell.resourceType,
                            resourceValue = cell.resourceValue,
                            worldPosition = CoordinateToWorld(cell.coordinate)
                        });
                    }
                }

                ClearCellState(cell);
            }
        }

        if (clearTiles.Count > 0 && AudioManager.ins != null)
        {
            AudioManager.ins.PlayClearBoard();
        }

        clearBoardRoutine = clearTiles.Count > 0 ? StartCoroutine(ClearBoardRoutine(clearTiles, onResourceCleared)) : null;
        if (clearTiles.Count == 0)
        {
            RefreshVisuals();
        }
    }

    public void PlayBoarEatWheat(CellData boarCell, CellData wheatCell)
    {
        PlayAnimalEat(boarCell, wheatCell, true);
    }

    public void PlayAnimalEat(CellData animalCell, CellData targetCell, bool returnToStart)
    {
        PlayAnimalEat(animalCell, targetCell, returnToStart, null, null);
    }

    public void PlayAnimalEat(CellData animalCell, CellData targetCell, bool returnToStart, Action onEat)
    {
        PlayAnimalEat(animalCell, targetCell, returnToStart, onEat, null);
    }

    public void PlayAnimalEat(CellData animalCell, CellData targetCell, bool returnToStart, Action onEat, Action onComplete)
    {
        if (!Application.isPlaying || animalCell == null || targetCell == null || tileViews == null)
        {
            if (onEat != null)
            {
                onEat();
            }

            if (onComplete != null)
            {
                onComplete();
            }

            return;
        }

        var animalView = GetTileView(animalCell.coordinate);
        var animalRenderer = animalView != null ? animalView.ResourceRenderer : null;
        if (animalRenderer == null || animalRenderer.sprite == null)
        {
            if (onEat != null)
            {
                onEat();
            }

            if (onComplete != null)
            {
                onComplete();
            }

            return;
        }

        var animationSet = GetAnimalAnimation(animalCell.resourceType);
        StartCoroutine(PlayAnimalEatRoutine(
            animalRenderer,
            animalCell.coordinate,
            targetCell.coordinate,
            returnToStart,
            IsWaterTile(animalCell.tileType),
            IsWaterTile(targetCell.tileType),
            animationSet,
            onEat,
            onComplete));
    }

    public void PlayWaterYieldEffect(Vector2Int waterCoordinate, Vector2Int resourceCoordinate)
    {
        var waterCell = GetCell(waterCoordinate);
        if (waterCell == null || waterCell.tileType != TileType.Water)
        {
            return;
        }

        var view = GetTileView(waterCoordinate);
        if (view != null)
        {
            view.PlayWaterEffect(resourceCoordinate - waterCoordinate);
        }
    }

    private TileView GetTileView(Vector2Int coordinate)
    {
        return tileViews != null && IsInside(coordinate) ? tileViews[coordinate.x, coordinate.y] : null;
    }

    private void EnsureGrid()
    {
        if (cells == null)
        {
            ResetBoard();
        }
    }

    private void BuildVisuals()
    {
        var oldVisuals = transform.Find("BoardVisuals");
        if (oldVisuals != null)
        {
            if (Application.isPlaying)
            {
                Destroy(oldVisuals.gameObject);
            }
            else
            {
                DestroyImmediate(oldVisuals.gameObject);
            }
        }

        tileViews = new TileView[Width, Height];

        var root = new GameObject("BoardVisuals").transform;
        root.SetParent(transform, false);

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                var coordinate = new Vector2Int(x, y);
                if (!IsInside(coordinate))
                {
                    continue;
                }

                var cellObject = cellPrefab != null ? Instantiate(cellPrefab) : new GameObject("Cell_" + x + "_" + y);
                cellObject.name = "Cell_" + x + "_" + y;
                cellObject.transform.SetParent(root, false);
                cellObject.transform.localPosition = BoardToLocal(coordinate);

                var tileView = cellObject.GetComponent<TileView>();
                if (tileView == null)
                {
                    tileView = cellObject.AddComponent<TileView>();
                }

                tileView.Initialize(this, cells[x, y], CellVisualSize);
                tileViews[x, y] = tileView;
            }
        }
    }

    private Vector3 BoardToLocal(Vector2Int coordinate)
    {
        return new Vector3(
            (coordinate.x - (Width - 1) * 0.5f) * CellStride,
            (coordinate.y - (Height - 1) * 0.5f) * CellStride,
            0f);
    }

    private Vector3 GetVisibleCenterLocal()
    {
        Vector2Int min;
        Vector2Int max;
        GetVisibleBounds(out min, out max);
        return (BoardToLocal(min) + BoardToLocal(max)) * 0.5f;
    }

    private void GetVisibleBounds(out Vector2Int min, out Vector2Int max)
    {
        min = Vector2Int.zero;
        max = new Vector2Int(Width - 1, Height - 1);

        if (playableCoordinates == null || playableCoordinates.Count == 0)
        {
            return;
        }

        var found = false;
        for (var i = 0; i < playableCoordinates.Count; i++)
        {
            var coordinate = playableCoordinates[i];
            if (coordinate.x < 0 || coordinate.x >= Width || coordinate.y < 0 || coordinate.y >= Height)
            {
                continue;
            }

            if (!found)
            {
                min = coordinate;
                max = coordinate;
                found = true;
                continue;
            }

            min = Vector2Int.Min(min, coordinate);
            max = Vector2Int.Max(max, coordinate);
        }
    }

    public Sprite GetTileSprite(TileType tileType)
    {
        var sprite = GetCustomSprite(tileType);
        return sprite != null ? sprite : GetCellSprite();
    }

    public Color GetTint(TileType tileType)
    {
        return GetCustomSprite(tileType) != null ? Color.white : GetColor(tileType);
    }

    public Sprite GetTileSprite(CellData cell)
    {
        if (cell != null && cell.tileType == TileType.Dirt && cell.dirt3x3Boosted && dirt3x3Sprite != null)
        {
            return dirt3x3Sprite;
        }

        return cell != null ? GetTileSprite(cell.tileType) : GetTileSprite(TileType.Empty);
    }

    public GameObject GetTilePrefab(TileType tileType)
    {
        if (blockManager == null)
        {
            blockManager = FindObjectOfType<BlockManager>();
        }

        return blockManager != null ? blockManager.GetTilePrefab(tileType) : null;
    }

    private Sprite GetCustomSprite(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Empty: return emptySprite;
            case TileType.Grass: return grassSprite;
            case TileType.Dirt: return dirtSprite;
            case TileType.Water: return waterSprite;
            case TileType.Wheat: return wheatSprite;
            case TileType.Flower: return flowerSprite;
            case TileType.Fish: return fishSprite;
            case TileType.Boar: return boarSprite;
            case TileType.BabyBoar: return babyBoarSprite;
            case TileType.Bear: return bearSprite;
            case TileType.Pig: return pigSprite;
            default: return null;
        }
    }

    private Sprite GetCellSprite()
    {
        if (cellSprite != null)
        {
            return cellSprite;
        }

        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        cellSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        cellSprite.hideFlags = HideFlags.HideAndDontSave;
        return cellSprite;
    }

    private void UpdateVisual(CellData cell)
    {
        var view = cell != null ? GetTileView(cell.coordinate) : null;
        if (view != null)
        {
            view.Render();
        }
    }

    public void ResolveDirt3x3Growth(List<Vector2Int> placedCells)
    {
        if (placedCells == null || placedCells.Count == 0)
        {
            return;
        }

        for (var i = 0; i < placedCells.Count; i++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    TryBoostDirt3x3(placedCells[i] + new Vector2Int(dx, dy));
                }
            }
        }
    }

    private void TryBoostDirt3x3(Vector2Int center)
    {
        if (center.x <= 0 || center.x >= Width - 1 || center.y <= 0 || center.y >= Height - 1)
        {
            return;
        }

        var hasNewCell = false;
        for (var x = center.x - 1; x <= center.x + 1; x++)
        {
            for (var y = center.y - 1; y <= center.y + 1; y++)
            {
                var cell = GetCell(new Vector2Int(x, y));
                if (cell == null || cell.tileType != TileType.Dirt)
                {
                    return;
                }

                hasNewCell = hasNewCell || !cell.dirt3x3Boosted;
            }
        }

        if (!hasNewCell)
        {
            return;
        }

        for (var x = center.x - 1; x <= center.x + 1; x++)
        {
            for (var y = center.y - 1; y <= center.y + 1; y++)
            {
                var cell = cells[x, y];
                cell.dirt3x3Boosted = true;
                cell.resourceType = TileType.Wheat;
                cell.resourceValue += 1;
                UpdateVisual(cell);
            }
        }

        PlayDirt3x3Effect(center);
    }

    private void PlayDirt3x3Effect(Vector2Int center)
    {
        if (!Application.isPlaying || dirt3x3EffectPrefab == null)
        {
            return;
        }

        var effect = Instantiate(dirt3x3EffectPrefab, CoordinateToWorld(center), Quaternion.identity, transform);
        Destroy(effect, 2f);
    }

    private void ClearBoardImmediate(Action<TileType, int, Vector3> onResourceCleared)
    {
        for (var y = Height - 1; y >= 0; y--)
        {
            for (var x = Width - 1; x >= 0; x--)
            {
                var cell = cells[x, y];
                if (cell.resourceType != TileType.Empty && onResourceCleared != null)
                {
                    onResourceCleared(cell.resourceType, cell.resourceValue, CoordinateToWorld(cell.coordinate));
                }

                ClearCellState(cell);
                UpdateVisual(cell);
            }
        }
    }

    private IEnumerator ClearBoardRoutine(List<ClearTile> clearTiles, Action<TileType, int, Vector3> onResourceCleared)
    {
        for (var i = 0; i < clearTiles.Count; i++)
        {
            var clearTile = clearTiles[i];
            if (clearTile.resourceType != TileType.Empty && onResourceCleared != null)
            {
                onResourceCleared(clearTile.resourceType, clearTile.resourceValue, clearTile.worldPosition);
            }

            var view = clearTile.view;
            if (view != null)
            {
                PlayClearResourceFeedback(clearTile);
                yield return view.PlayClear(clearTileSeconds);
                view.Render();
            }

            if (clearStepDelay > 0f)
            {
                yield return new WaitForSeconds(clearStepDelay);
            }
        }

        RefreshVisuals();
        clearBoardRoutine = null;
    }

    private void PlayClearResourceFeedback(ClearTile clearTile)
    {
        if (!Application.isPlaying || clearTile.resourceType == TileType.Empty || clearResourceEffectPrefab == null)
        {
            return;
        }

        var effect = Instantiate(clearResourceEffectPrefab, clearTile.worldPosition, Quaternion.identity, transform);
        Destroy(effect, 2f);
    }

    private void ClearCellState(CellData cell)
    {
        cell.tileType = TileType.Empty;
        cell.resourceType = TileType.Empty;
        cell.resourceValue = 0;
        cell.occupied = false;
        cell.dirt3x3Boosted = false;
    }

    private void RefreshVisuals()
    {
        if (tileViews == null)
        {
            return;
        }

        for (var x = 0; x < Width; x++)
        {
            for (var y = 0; y < Height; y++)
            {
                UpdateVisual(cells[x, y]);
            }
        }
    }

    private IEnumerator PlayAnimalEatRoutine(
        SpriteRenderer sourceRenderer,
        Vector2Int animalCoordinate,
        Vector2Int targetCoordinate,
        bool returnToStart,
        bool startsInWater,
        bool eatsInWater,
        AnimalFrameSet animationSet,
        Action onEat,
        Action onComplete)
    {
        var animationObject = new GameObject("AnimalEatAnimation");
        var animationTransform = animationObject.transform;
        animationTransform.localScale = Vector3.one * CellVisualSize;

        var animationRenderer = animationObject.AddComponent<SpriteRenderer>();
        animationRenderer.sprite = sourceRenderer.sprite;
        animationRenderer.color = sourceRenderer.color;
        animationRenderer.sortingOrder = sourceRenderer.sortingOrder + 10;

        var start = CoordinateToWorld(animalCoordinate);
        var target = CoordinateToWorld(targetCoordinate);
        animationTransform.position = start;
        sourceRenderer.enabled = false;

        yield return Move(animationTransform, start, target, boarTravelSeconds, animationRenderer, GetMoveFrames(animationSet, startsInWater));
        if (AudioManager.ins != null && AudioManager.ins.animalEatSound != null)
        {
            AudioManager.ins.PlayAnimalEat();
        }
        else
        {
            PlayClip(animalActionAudioClip);
        }

        yield return PlayFrames(animationRenderer, GetEatFrames(animationSet, eatsInWater), GetFrameSeconds(animationSet), 1);

        if (onEat != null)
        {
            onEat();
        }

        if (returnToStart)
        {
            sourceRenderer.enabled = false;
        }

        PlayYieldFeedback(GetCell(targetCoordinate));
        yield return new WaitForSeconds(0.12f);

        if (returnToStart)
        {
            yield return Move(animationTransform, target, start, boarTravelSeconds, animationRenderer, GetMoveFrames(animationSet, eatsInWater));
            sourceRenderer.enabled = true;
        }

        Destroy(animationObject);
        if (onComplete != null)
        {
            onComplete();
        }
    }

    private bool IsAnimatedAnimal(TileType resourceType)
    {
        return resourceType == TileType.Boar
            || resourceType == TileType.BabyBoar
            || resourceType == TileType.Bear
            || resourceType == TileType.Pig;
    }

    private IEnumerator Move(Transform target, Vector3 from, Vector3 to, float seconds, SpriteRenderer renderer, Sprite[] frames)
    {
        seconds = Mathf.Max(0.01f, seconds);
        var frameSeconds = frames != null && frames.Length > 0 ? Mathf.Max(0.01f, seconds / frames.Length) : seconds;
        var nextFrameAt = 0f;
        var frameIndex = 0;

        for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            if (frames != null && frames.Length > 0 && elapsed >= nextFrameAt)
            {
                renderer.sprite = frames[frameIndex % frames.Length];
                frameIndex++;
                nextFrameAt += frameSeconds;
            }

            var t = Mathf.SmoothStep(0f, 1f, elapsed / seconds);
            target.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        target.position = to;
    }

    private IEnumerator PlayFrames(SpriteRenderer renderer, Sprite[] frames, float frameSeconds, int loops)
    {
        if (frames == null || frames.Length == 0)
        {
            yield break;
        }

        loops = Mathf.Max(1, loops);
        for (var loop = 0; loop < loops; loop++)
        {
            for (var i = 0; i < frames.Length; i++)
            {
                renderer.sprite = frames[i];
                yield return new WaitForSeconds(frameSeconds);
            }
        }
    }

    public AnimalFrameSet GetAnimalAnimation(TileType resourceType)
    {
        for (var i = 0; i < animalAnimations.Count; i++)
        {
            var animationSet = animalAnimations[i];
            if (animationSet != null && animationSet.resourceType == resourceType)
            {
                return animationSet;
            }
        }

        return null;
    }

    public Sprite[] GetIdleFrames(TileType resourceType, TileType tileType)
    {
        var animationSet = GetAnimalAnimation(resourceType);
        if (animationSet == null)
        {
            return null;
        }

        return IsWaterTile(tileType) ? animationSet.idleWaterFrames : animationSet.idleLandFrames;
    }

    public float GetFrameSeconds(TileType resourceType)
    {
        return GetFrameSeconds(GetAnimalAnimation(resourceType));
    }

    private float GetFrameSeconds(AnimalFrameSet animationSet)
    {
        return animationSet != null ? 1f / Mathf.Max(1f, animationSet.framesPerSecond) : 0.12f;
    }

    private Sprite[] GetMoveFrames(AnimalFrameSet animationSet, bool inWater)
    {
        return animationSet == null ? null : inWater ? animationSet.moveWaterFrames : animationSet.moveLandFrames;
    }

    private Sprite[] GetEatFrames(AnimalFrameSet animationSet, bool inWater)
    {
        return animationSet == null ? null : inWater ? animationSet.eatWaterFrames : animationSet.eatLandFrames;
    }

    private bool IsWaterTile(TileType tileType)
    {
        return tileType == TileType.Water;
    }

    private void PlayYieldFeedback(CellData cell)
    {
        if (!Application.isPlaying || cell == null || cell.resourceType == TileType.Empty)
        {
            return;
        }

        if (yieldEffectPrefab != null)
        {
            var effect = Instantiate(yieldEffectPrefab, CoordinateToWorld(cell.coordinate), Quaternion.identity, transform);
            Destroy(effect, 2f);
        }

        if (AudioManager.ins != null && AudioManager.ins.resourceGainSound != null)
        {
            AudioManager.ins.PlayResourceGain();
        }
        else
        {
            PlayClip(yieldAudioClip);
        }

        var view = GetTileView(cell.coordinate);
        if (view != null)
        {
            view.PlayPulse();
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        var source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.PlayOneShot(clip);
    }

    public static int GetDefaultResourceValue(TileType resourceType)
    {
        switch (resourceType)
        {
            case TileType.Wheat: return 1;
            case TileType.Flower: return 1;
            case TileType.Fish: return 2;
            case TileType.BabyBoar: return 2;
            case TileType.Boar: return 4;
            case TileType.Bear: return 4;
            case TileType.Pig: return 2;
            default: return 0;
        }
    }

    public static Color GetColor(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Grass: return new Color(0.48f, 0.72f, 0.32f);
            case TileType.Dirt: return new Color(0.55f, 0.38f, 0.22f);
            case TileType.Wheat: return new Color(0.94f, 0.72f, 0.24f);
            case TileType.Water: return new Color(0.22f, 0.52f, 0.9f);
            case TileType.Flower: return new Color(0.9f, 0.35f, 0.72f);
            case TileType.Fish: return new Color(0.18f, 0.78f, 0.88f);
            case TileType.Boar: return new Color(0.5f, 0.28f, 0.18f);
            case TileType.BabyBoar: return new Color(0.72f, 0.42f, 0.25f);
            case TileType.Bear: return new Color(0.32f, 0.2f, 0.14f);
            case TileType.Pig: return new Color(0.96f, 0.55f, 0.68f);
            default: return new Color(0.82f, 0.76f, 0.62f);
        }
    }
}
