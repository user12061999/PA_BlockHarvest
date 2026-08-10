#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class BlockManagerSelfTest
{
    [MenuItem("Block Harvest/Run Block Self Test")]
    public static void Run()
    {
        var boardObject = new GameObject("BoardManagerSelfTestBoard");
        var board = boardObject.AddComponent<BoardManager>();
        var harvestObject = new GameObject("BlockManagerSelfTestHarvest");
        var harvest = harvestObject.AddComponent<HarvestManager>();
        var managerObject = new GameObject("BlockManagerSelfTest");
        var manager = managerObject.AddComponent<BlockManager>();
        var config = ScriptableObject.CreateInstance<LevelConfig>();

        try
        {
            board.ResetBoard();
            harvest.ResetObjectives();
            manager.SetBoard(board);
            manager.SetHarvestManager(harvest);

            var examples = manager.CreateExampleBlocks();
            Check(examples.Count == 5, "Expected five example blocks.");
            Check(examples[0].positions.Count == 2 && examples[0].tileTypes[0] == TileType.Dirt, "Dirt Ix2 should have two dirt tiles.");
            Check(BlockData.Single(TileType.Grass).positions.Count == 1, "Single should have one tile.");
            Check(BlockData.Line(TileType.Water, TileType.Water, TileType.Water).positions.Count == 3, "Line should have three tiles.");
            Check(HasShape(BlockData.ShapeVariants(), new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.up * 2 }), "Line shape should spawn vertically too.");
            Check(BlockData.L(TileType.Grass, TileType.Grass, TileType.Grass).tileTypes[0] == TileType.Grass, "L should use one ground type.");
            Check(BlockData.Square(TileType.Grass, TileType.Grass, TileType.Grass, TileType.Grass).positions.Count == 4, "Square should have four tiles.");
            for (var i = 0; i < 20; i++)
            {
                Check(BlockData.RandomResource(TileType.Water) != TileType.Bear, "Bear should not spawn on water blocks.");
            }

            manager.PrepareStartingBlocks();
            Check(manager.ActiveBlocks.Count == 3, "Should create three visible blocks.");
            Check(manager.transform.childCount == 3, "BlockManager should own three block objects.");
            Check(Mathf.Approximately(manager.transform.position.x, board.transform.position.x), "BlockManager should align horizontally with board.");
            Check(manager.transform.position.y < board.transform.position.y, "BlockManager should sit under board.");
            Check(Mathf.Approximately(manager.ActiveBlocks[0].transform.position.x, -board.BoardVisualSize.x / 3f), "First spawn block should use board visual width spacing.");

            config.useCustomBlockSpawns = true;
            var turn = new LevelConfig.BlockSpawnTurn();
            turn.blocks.Add(BlockData.Single(TileType.Dirt));
            turn.blocks.Add(BlockData.Single(TileType.Water));
            turn.blocks.Add(BlockData.Single(TileType.Grass));
            turn.blocks[1].resourceTypes[0] = TileType.Bear;
            config.blockSpawnTurns.Add(turn);
            manager.SetLevelConfig(config);
            manager.PrepareStartingBlocks();
            Check(manager.ActiveBlocks[0].Data.tileTypes[0] == TileType.Dirt, "Custom spawn turn should create dirt block first.");
            Check(manager.ActiveBlocks[1].Data.tileTypes[0] == TileType.Water, "Custom spawn turn should create water block second.");
            Check(manager.ActiveBlocks[2].Data.tileTypes[0] == TileType.Grass, "Custom spawn turn should create grass block third.");
            Check(manager.ActiveBlocks[1].Data.resourceTypes[0] != TileType.Bear, "Custom spawn should regenerate resource by tile type.");

            var blockObject = new GameObject("DeterministicBlock");
            blockObject.transform.SetParent(managerObject.transform, false);
            var block = blockObject.AddComponent<BlockPiece>();
            block.SetData(BlockData.Single(TileType.Grass), board.CellSize * 0.5f, 1f, board, harvest, null, 0f);
            block.OnPointerDown(new PointerEventData(EventSystem.current));
            Check(Mathf.Approximately(blockObject.transform.localScale.x, board.CellVisualSize / (board.CellSize * 0.5f * 0.92f)), "Picked block should scale up to board cell visual size.");
            var origin = new Vector2Int(3, 3);
            Check(block is IPointerDownHandler && block is IDragHandler && block is IPointerUpHandler, "Block should use EventSystem drag interfaces.");
            Check(block.GetComponent<BoxCollider2D>() != null, "Block should have a 2D collider for Physics2DRaycaster input.");
            Check(block.TryPlaceAt(origin), "Valid placement should succeed.");
            Check(board.GetCell(origin + block.Data.positions[0]).tileType == block.Data.tileTypes[0], "Placement should write the first ground tile.");
            Check(!board.CanPlace(origin, block.Data), "Occupied cells should reject the same block.");

            Debug.Log("Block Harvest block self test passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(managerObject);
            UnityEngine.Object.DestroyImmediate(harvestObject);
            UnityEngine.Object.DestroyImmediate(boardObject);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static bool HasShape(Vector2Int[][] shapes, Vector2Int[] expected)
    {
        foreach (var shape in shapes)
        {
            if (shape.Length != expected.Length)
            {
                continue;
            }

            var matches = true;
            foreach (var position in expected)
            {
                if (Array.IndexOf(shape, position) < 0)
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
