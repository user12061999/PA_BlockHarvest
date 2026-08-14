#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class HarvestManagerSelfTest
{
    [MenuItem("Block Harvest/Run Harvest Self Test")]
    public static void Run()
    {
        TestWaterWheat();
        TestBoarWheat();
        TestFish();
        TestFlower();
        TestWaterFlower();
        TestWaterBoostsMultipleResources();
        TestWaterNetworkWheat();
        TestWaterNetworkFlower();
        TestDirt3x3Growth();
        TestPigletWheat();
        TestBearFish();
        TestFinalHarvest();
        TestFullBoardAddsPlacementsWhenGoalIncomplete();
        TestVisibleGoalCompleteEndsWithPlacementsLeft();

        Debug.Log("Block Harvest harvest self test passed.");
    }

    private static void TestWaterWheat()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Water);
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Dirt, TileType.Wheat);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));

            Check(context.board.GetCell(new Vector2Int(1, 0)).resourceValue == 2, "Water should increase wheat yield from 1 to 2.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestBoarWheat()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Dirt, TileType.Wheat);
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Grass, TileType.Boar);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));

            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceType == TileType.BabyBoar, "Boar should convert neighboring wheat into baby boar.");
            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceValue == 2, "Baby boar yield should be 2.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestFish()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Water, TileType.Fish);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(0, 0)));
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Grass);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));

            Check(context.harvest.Fish == 0, "Fish should not be harvested before grid full or final placement.");
            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceType == TileType.Fish, "Fish should remain on grid before harvest.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestFlower()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Grass, TileType.Flower);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(0, 0)));

            Check(context.harvest.Flower == 0, "Flower should not be harvested before grid full or final placement.");
            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceType == TileType.Flower, "Flower should remain on grid before harvest.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestWaterFlower()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Grass, TileType.Flower);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(0, 0)));
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Water);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));

            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceValue == 2, "Water should increase flower resource value.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestWaterBoostsMultipleResources()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Water);
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Dirt, TileType.Wheat);
            context.board.PlaceTile(new Vector2Int(0, 1), TileType.Grass, TileType.Flower);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(0, 0)));

            Check(context.board.GetCell(new Vector2Int(1, 0)).resourceValue == 2, "One water should increase neighboring wheat yield.");
            Check(context.board.GetCell(new Vector2Int(0, 1)).resourceValue == 2, "One water should increase neighboring flower yield.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestWaterNetworkWheat()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Dirt, TileType.Wheat);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(0, 0)));
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Water);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));
            context.board.PlaceTile(new Vector2Int(2, 0), TileType.Water);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(2, 0)));

            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceValue == 3, "Wheat resource value should increase through connected water.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestWaterNetworkFlower()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Grass, TileType.Flower);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(0, 0)));
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Water);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));
            context.board.PlaceTile(new Vector2Int(2, 0), TileType.Water);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(2, 0)));

            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceValue == 3, "Flower resource value should increase through connected water.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestPigletWheat()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Dirt, TileType.Wheat);
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Grass, TileType.BabyBoar);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));

            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceType == TileType.Empty, "Piglet should eat and clear the wheat cell.");
            Check(context.board.GetCell(new Vector2Int(1, 0)).resourceType == TileType.Boar, "Piglet should return and grow into boar on its source cell.");
            Check(context.board.GetCell(new Vector2Int(1, 0)).resourceValue == 4, "Returned grown boar yield should be 4.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestDirt3x3Growth()
    {
        var context = CreateContext();
        try
        {
            for (var x = 0; x < 3; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    context.board.PlaceTile(new Vector2Int(x, y), TileType.Dirt);
                }
            }

            context.board.SetResource(context.board.GetCell(new Vector2Int(0, 0)), TileType.Wheat);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(2, 2)));

            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceValue == 2, "Dirt 3x3 should add 1 wheat yield to existing wheat.");
            Check(context.board.GetCell(new Vector2Int(1, 1)).resourceType == TileType.Wheat, "Dirt 3x3 should add wheat to empty dirt cells.");
            Check(context.board.GetCell(new Vector2Int(1, 1)).resourceValue == 1, "Dirt 3x3 empty dirt cell should become wheat yield 1.");
            Check(context.board.GetCell(new Vector2Int(1, 1)).dirt3x3Boosted, "Dirt 3x3 cells should be marked boosted.");

            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(2, 2)));
            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceValue == 2, "Dirt 3x3 should not boost the same square twice.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestBearFish()
    {
        var context = CreateContext();
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Water, TileType.Fish);
            context.board.PlaceTile(new Vector2Int(1, 0), TileType.Grass, TileType.Bear);
            context.board.SetResourceValue(context.board.GetCell(new Vector2Int(1, 0)), 10);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(1, 0)));

            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceType == TileType.Bear, "Bear should move onto water fish cell.");
            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceValue == 12, "Bear yield should keep its current value and add fish yield.");
            Check(context.board.GetCell(new Vector2Int(1, 0)).resourceType == TileType.Empty, "Bear source cell should lose resource after moving.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestFinalHarvest()
    {
        var context = CreateContext(1);
        try
        {
            context.board.PlaceTile(new Vector2Int(0, 0), TileType.Dirt, TileType.Wheat);
            context.harvest.ResolvePlacement(context.board, One(new Vector2Int(0, 0)));

            Check(context.harvest.Wheat == 1, "Final placement should harvest wheat.");
            Check(context.board.GetCell(new Vector2Int(0, 0)).resourceType == TileType.Empty, "Harvest should clear resource.");
            Check(context.board.GetCell(new Vector2Int(0, 0)).tileType == TileType.Empty, "Harvest should clear tile.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestFullBoardAddsPlacementsWhenGoalIncomplete()
    {
        var context = CreateContext(1);
        try
        {
            context.board.SetPlayableCoordinates(new List<Vector2Int> { Vector2Int.zero });
            context.board.PlaceTile(Vector2Int.zero, TileType.Grass);
            context.harvest.ResolvePlacement(context.board, One(Vector2Int.zero));

            Check(context.harvest.RemainingPlacements == 3, "Full board without enough goals should add extra placements.");
            Check(!context.harvest.IsLevelOver, "Bonus placements should keep the level running.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static void TestVisibleGoalCompleteEndsWithPlacementsLeft()
    {
        var context = CreateContext(12);
        try
        {
            context.config.resourceGoals.Clear();
            context.config.resourceGoals.Add(new LevelConfig.ResourceGoal
            {
                resourceType = TileType.Wheat,
                amount = 1
            });
            context.harvest.Configure(context.config);
            context.board.SetPlayableCoordinates(new List<Vector2Int> { Vector2Int.zero });
            context.board.PlaceTile(Vector2Int.zero, TileType.Dirt, TileType.Wheat);
            context.harvest.ResolvePlacement(context.board, One(Vector2Int.zero));

            Check(context.harvest.Wheat == 1, "Visible completed goals should harvest board resources.");
            Check(context.harvest.IsLevelOver, "Visible completed goals should end even with placements left.");
        }
        finally
        {
            context.Destroy();
        }
    }

    private static TestContext CreateContext()
    {
        return CreateContext(12);
    }

    private static TestContext CreateContext(int maxPlacements)
    {
        var boardObject = new GameObject("HarvestSelfTestBoard");
        var harvestObject = new GameObject("HarvestSelfTestManager");
        var config = ScriptableObject.CreateInstance<LevelConfig>();
        config.maxPlacements = maxPlacements;
        var context = new TestContext(boardObject, harvestObject, config);

        context.board.ResetBoard();
        context.harvest.Configure(config);
        context.harvest.ResetObjectives();
        return context;
    }

    private static List<Vector2Int> One(Vector2Int coordinate)
    {
        return new List<Vector2Int> { coordinate };
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private sealed class TestContext
    {
        private readonly GameObject boardObject;
        private readonly GameObject harvestObject;
        public readonly LevelConfig config;

        public readonly BoardManager board;
        public readonly HarvestManager harvest;

        public TestContext(GameObject boardObject, GameObject harvestObject, LevelConfig config)
        {
            this.boardObject = boardObject;
            this.harvestObject = harvestObject;
            this.config = config;
            board = boardObject.AddComponent<BoardManager>();
            harvest = harvestObject.AddComponent<HarvestManager>();
        }

        public void Destroy()
        {
            UnityEngine.Object.DestroyImmediate(harvestObject);
            UnityEngine.Object.DestroyImmediate(boardObject);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
#endif
