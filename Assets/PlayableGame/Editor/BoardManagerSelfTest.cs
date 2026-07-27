#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BoardManagerSelfTest
{
    [MenuItem("Block Harvest/Run Board Self Test")]
    public static void Run()
    {
        var boardObject = new GameObject("BoardManagerSelfTest");
        var board = boardObject.AddComponent<BoardManager>();

        try
        {
            board.ResetBoard();

            Check(board.BoardSize == new Vector2Int(7, 8), "Board size must be 7x8.");
            Check(board.IsInside(new Vector2Int(6, 7)), "Max board coordinate should be inside.");
            Check(!board.IsInside(new Vector2Int(7, 8)), "Outside coordinate should fail.");
            Check(board.IsEmpty(new Vector2Int(0, 0)), "Fresh cell should be empty.");
            Check(board.PlaceTile(new Vector2Int(2, 3), TileType.Dirt, TileType.Wheat), "Should place dirt with wheat.");
            Check(!board.IsEmpty(new Vector2Int(2, 3)), "Placed cell should not be empty.");
            Check(!board.CanPlace(new Vector2Int(2, 3)), "Occupied cell should reject placement.");
            Check(board.GetCell(new Vector2Int(2, 3)).tileType == TileType.Dirt, "Placed ground tile should persist.");
            Check(board.GetCell(new Vector2Int(2, 3)).resourceType == TileType.Wheat, "Placed resource should persist.");
            Check(board.GetNeighbors4(new Vector2Int(0, 0)).Count == 2, "Corner should have 2 neighbors.");
            Check(board.GetNeighbors4(new Vector2Int(3, 3)).Count == 4, "Middle should have 4 neighbors.");
            Check(board.CanPlace(new Vector2Int(4, 4), new[] { Vector2Int.zero, Vector2Int.right }), "Shape should fit empty cells.");

            board.SetPlayableCoordinates(new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1) });
            Check(board.IsInside(new Vector2Int(1, 1)), "Listed coordinate should be inside custom board.");
            Check(!board.IsInside(new Vector2Int(2, 0)), "Unlisted coordinate should be outside custom board.");

            Debug.Log("Block Harvest board self test passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(boardObject);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
#endif
