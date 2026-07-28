using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class BlockManager : MonoBehaviour
{
    [SerializeField] private bool autoLayoutFromBoard = true;
    [SerializeField] private float blockSpacing = 1.9f;
    [SerializeField] private float gridCellSize = 1f;
    [SerializeField] private float pieceSize = 0.48f;
    [SerializeField] private float spawnCellScale = 0.48f;
    [SerializeField] private float minSpawnSpacingInCells = 1.8f;
    [SerializeField] private float dragOffsetY = 0.6f;
    [SerializeField] private float trayHeightInCells = 2f;
    [SerializeField] private float trayGap = 0.08f;
    [SerializeField] private Vector3 bottomOffset = new Vector3(0f, -4.25f, 0f);
    [SerializeField] private Transform trayRoot;
    [SerializeField] private Transform[] spawnSlots = new Transform[3];
    [SerializeField] private LevelConfig levelConfig;
    [Header("Tutorial Hand")]
    [SerializeField] private Sprite tutorialHandNormalSprite;
    [SerializeField] private Sprite tutorialHandPressSprite;
    [SerializeField] private Vector2Int tutorialTargetCell = new Vector2Int(3, 3);
    [SerializeField] private Vector3 tutorialHandOffset = new Vector3(0.25f, -0.25f, 0f);
    [SerializeField] private float tutorialDelay = 0.45f;
    [SerializeField] private float tutorialHoldSeconds = 0.18f;
    [SerializeField] private float tutorialMoveSeconds = 0.85f;
    [SerializeField] private float tutorialLoopDelay = 0.35f;
    [SerializeField] private int tutorialSortingOrder = 200;
    [Header("Tile Prefabs")]
    [SerializeField] private GameObject grassTilePrefab;
    [SerializeField] private GameObject dirtTilePrefab;
    [SerializeField] private GameObject waterTilePrefab;

    private readonly List<BlockPiece> activeBlocks = new List<BlockPiece>(3);
    private BoardManager boardManager;
    private HarvestManager harvestManager;
    private int customSpawnTurnIndex;
    private SpriteRenderer tutorialHand;
    private Coroutine tutorialRoutine;
    private bool tutorialDismissed;

    public IReadOnlyList<BlockPiece> ActiveBlocks => activeBlocks;
    public BoardManager Board => boardManager;
    public HarvestManager Harvest => harvestManager;

    public void SetBoard(BoardManager boardManager)
    {
        this.boardManager = boardManager;
    }

    public void SetHarvestManager(HarvestManager harvestManager)
    {
        this.harvestManager = harvestManager;
    }

    public void SetLevelConfig(LevelConfig levelConfig)
    {
        this.levelConfig = levelConfig;
    }

    private void Awake()
    {
        boardManager = FindObjectOfType<BoardManager>();
        harvestManager = FindObjectOfType<HarvestManager>();
        SyncBoardCellSize();
        LayoutFromBoard();
        EnsurePointerInput();
    }

    public void PrepareStartingBlocks()
    {
        if (boardManager == null)
        {
            boardManager = FindObjectOfType<BoardManager>();
        }

        if (harvestManager == null)
        {
            harvestManager = FindObjectOfType<HarvestManager>();
        }

        SyncBoardCellSize();
        LayoutFromBoard();
        EnsurePointerInput();
        customSpawnTurnIndex = 0;
        CreatePlayableBlocks();
        StartTutorial();
    }

    public List<BlockData> CreateExampleBlocks()
    {
        return new List<BlockData>
        {
            BlockData.Domino(TileType.Dirt, TileType.Dirt),
            BlockData.Line(TileType.Water, TileType.Water, TileType.Water),
            BlockData.L(TileType.Grass, TileType.Grass, TileType.Grass),
            BlockData.Single(TileType.Grass),
            BlockData.Domino(TileType.Water, TileType.Water)
        };
    }

    private void CreatePlayableBlocks()
    {
        ClearBlocks();
        var candidates = FindPlayableBlocks();
        var customBlocks = GetCustomSpawnBlocks();

        for (var i = 0; i < 3; i++)
        {
            var blockData = customBlocks != null && i < customBlocks.Count && customBlocks[i] != null && customBlocks[i].IsValid()
                ? CopyBlockData(customBlocks[i])
                : candidates.Count > 0
                ? candidates[Random.Range(0, candidates.Count)]
                : BlockData.Random();
            CreateBlockPiece(blockData, i);
        }
    }

    private List<BlockData> GetCustomSpawnBlocks()
    {
        var config = GetLevelConfig();
        if (config == null || !config.useCustomBlockSpawns || config.blockSpawnTurns == null || customSpawnTurnIndex >= config.blockSpawnTurns.Count)
        {
            return null;
        }

        var turn = config.blockSpawnTurns[customSpawnTurnIndex];
        customSpawnTurnIndex++;
        return turn != null ? turn.blocks : null;
    }

    private LevelConfig GetLevelConfig()
    {
        if (levelConfig == null && harvestManager != null)
        {
            levelConfig = harvestManager.Config;
        }

        return levelConfig;
    }

    private BlockData CopyBlockData(BlockData source)
    {
        var resources = new TileType[source.positions.Count];
        for (var i = 0; i < resources.Length; i++)
        {
            var tileType = i < source.tileTypes.Count ? source.tileTypes[i] : TileType.Empty;
            resources[i] = BlockData.RandomResource(tileType);
        }

        return new BlockData(
            source.name,
            source.positions.ToArray(),
            source.tileTypes.ToArray(),
            resources);
    }

    private void CreateBlockPiece(BlockData blockData, int index)
    {
        var blockObject = new GameObject("Block_" + index + "_" + blockData.name);
        blockObject.transform.SetParent(transform, false);

        blockObject.transform.position = GetSpawnPosition(index);

        var blockPiece = blockObject.AddComponent<BlockPiece>();
        blockPiece.SetData(blockData, pieceSize, boardManager, harvestManager, this, dragOffsetY);
        activeBlocks.Add(blockPiece);
    }

    public void RemoveBlock(BlockPiece blockPiece)
    {
        HideTutorial();
        if (harvestManager != null && harvestManager.IsLevelOver)
        {
            ClearBlocks();
            return;
        }

        CreatePlayableBlocks();
    }

    public GameObject GetTilePrefab(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Grass: return grassTilePrefab;
            case TileType.Dirt: return dirtTilePrefab;
            case TileType.Water: return waterTilePrefab;
            default: return null;
        }
    }

    public void HideTutorial()
    {
        tutorialDismissed = true;
        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
            tutorialRoutine = null;
        }

        if (tutorialHand != null)
        {
            tutorialHand.gameObject.SetActive(false);
        }
    }

    private void EnsurePointerInput()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<Physics2DRaycaster>() == null)
        {
            mainCamera.gameObject.AddComponent<Physics2DRaycaster>();
        }

        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(transform.root, false);
        }
    }

    private void ClearBlocks()
    {
        for (var i = activeBlocks.Count - 1; i >= 0; i--)
        {
            var block = activeBlocks[i];
            if (block == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(block.gameObject);
            }
            else
            {
                DestroyImmediate(block.gameObject);
            }
        }

        activeBlocks.Clear();
    }

    private Transform GetSpawnSlot(int index)
    {
        return spawnSlots != null && index >= 0 && index < spawnSlots.Length ? spawnSlots[index] : null;
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (autoLayoutFromBoard && boardManager != null)
        {
            return transform.TransformPoint(Vector3.right * ((index - 1) * blockSpacing));
        }

        var spawnSlot = GetSpawnSlot(index);
        if (spawnSlot != null)
        {
            return spawnSlot.position;
        }

        return transform.TransformPoint(bottomOffset + Vector3.right * ((index - 1) * blockSpacing));
    }

    private void SyncBoardCellSize()
    {
        if (boardManager != null)
        {
            boardManager.SetCellSize(gridCellSize);
        }
    }

    public void LayoutFromBoard()
    {
        if (!autoLayoutFromBoard || boardManager == null)
        {
            return;
        }

        var boardScale = boardManager.transform.lossyScale;
        var boardVisualSize = boardManager.VisibleBoardVisualSize;
        var boardWidth = boardVisualSize.x * Mathf.Abs(boardScale.x);
        var boardHeight = boardVisualSize.y * Mathf.Abs(boardScale.y);
        var trayHeight = boardManager.CellVisualSize * trayHeightInCells;
        var boardCenter = boardManager.VisibleBoardCenterWorld;

        transform.position = new Vector3(
            boardCenter.x,
            boardCenter.y - boardHeight * 0.5f - trayGap - trayHeight * 0.5f,
            transform.position.z);

        blockSpacing = Mathf.Max(boardWidth / 3f, boardManager.CellVisualSize * minSpawnSpacingInCells);
        pieceSize = boardManager.CellVisualSize * spawnCellScale / 0.92f;
        bottomOffset = Vector3.zero;
        ScaleTray(boardWidth, trayHeight);
        PositionSpawnSlots();
    }

    public Vector3 GetBoardCenterOffsetForCenteredLayout(BoardManager board)
    {
        if (!autoLayoutFromBoard || board == null)
        {
            return Vector3.zero;
        }

        var trayHeight = board.CellVisualSize * trayHeightInCells;
        return Vector3.up * ((trayGap + trayHeight) * 0.5f);
    }

    private void ScaleTray(float width, float height)
    {
        var root = GetTrayRoot();
        if (root == null)
        {
            return;
        }

        var renderer = root.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            return;
        }

        var bounds = renderer.bounds.size;
        var scale = root.localScale;
        if (bounds.x > 0.001f)
        {
            scale.x *= width / bounds.x;
        }

        if (bounds.y > 0.001f)
        {
            scale.y *= height / bounds.y;
        }

        root.localScale = scale;
    }

    private Transform GetTrayRoot()
    {
        if (trayRoot != null)
        {
            return trayRoot;
        }

        return transform.childCount > 0 ? transform.GetChild(0) : null;
    }

    private void PositionSpawnSlots()
    {
        for (var i = 0; i < 3; i++)
        {
            var spawnSlot = GetSpawnSlot(i);
            if (spawnSlot != null)
            {
                spawnSlot.position = GetSpawnPosition(i);
            }
        }
    }

    private void StartTutorial()
    {
        if (!Application.isPlaying || tutorialDismissed || (tutorialHandNormalSprite == null && tutorialHandPressSprite == null))
        {
            return;
        }

        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
        }

        tutorialRoutine = StartCoroutine(PlayTutorial());
    }

    private IEnumerator PlayTutorial()
    {
        yield return new WaitForSeconds(tutorialDelay);
        EnsureTutorialHand();

        while (!tutorialDismissed && activeBlocks.Count > 0 && boardManager != null && tutorialHand != null)
        {
            var block = activeBlocks[0];
            if (block == null)
            {
                yield break;
            }

            var from = block.transform.position + tutorialHandOffset;
            var to = boardManager.CoordinateToWorld(tutorialTargetCell) + tutorialHandOffset;
            tutorialHand.gameObject.SetActive(true);
            tutorialHand.sprite = tutorialHandNormalSprite != null ? tutorialHandNormalSprite : tutorialHandPressSprite;
            tutorialHand.transform.position = from;
            yield return new WaitForSeconds(tutorialHoldSeconds);

            tutorialHand.sprite = tutorialHandPressSprite != null ? tutorialHandPressSprite : tutorialHandNormalSprite;
            for (var elapsed = 0f; elapsed < tutorialMoveSeconds; elapsed += Time.deltaTime)
            {
                tutorialHand.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / tutorialMoveSeconds));
                yield return null;
            }

            tutorialHand.transform.position = to;
            tutorialHand.sprite = tutorialHandNormalSprite != null ? tutorialHandNormalSprite : tutorialHandPressSprite;
            yield return new WaitForSeconds(tutorialLoopDelay);
            tutorialHand.gameObject.SetActive(false);
            yield return new WaitForSeconds(tutorialLoopDelay);
        }
    }

    private void EnsureTutorialHand()
    {
        if (tutorialHand != null)
        {
            return;
        }

        var handObject = new GameObject("TutorialHand");
        handObject.transform.SetParent(transform.root, false);
        tutorialHand = handObject.AddComponent<SpriteRenderer>();
        tutorialHand.sortingOrder = tutorialSortingOrder;
        tutorialHand.gameObject.SetActive(false);
    }

    private List<BlockData> FindPlayableBlocks()
    {
        var candidates = new List<BlockData>(24);

        if (boardManager == null)
        {
            return candidates;
        }

        var shapes = BlockData.ShapeVariants();
        for (var i = 0; i < shapes.Length; i++)
        {
            AddCandidate(candidates, shapes[i], TileType.Grass);
            AddCandidate(candidates, shapes[i], TileType.Dirt);
            AddCandidate(candidates, shapes[i], TileType.Water);
        }

        return candidates;
    }

    private void AddCandidate(List<BlockData> candidates, Vector2Int[] shape, TileType groundType)
    {
        var candidate = BlockData.FromShape(groundType + " Block", groundType, shape);
        if (boardManager.HasAnyPlacement(candidate))
        {
            candidates.Add(candidate);
        }
    }

}
