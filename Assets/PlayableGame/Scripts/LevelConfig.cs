using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Block Harvest/Level Config")]
public sealed class LevelConfig : ScriptableObject
{
    [Serializable]
    public sealed class ResourceGoal
    {
        public TileType resourceType = TileType.Wheat;
        [Min(1)] public int amount = 1;
    }

    [Serializable]
    public sealed class BlockSpawnTurn
    {
        public List<BlockData> blocks = new List<BlockData>();
    }

    public List<Vector2Int> playableCoordinates = new List<Vector2Int>();
    public List<ResourceGoal> resourceGoals = new List<ResourceGoal>();
    public int maxPlacements = 12;
    [Header("Water Placement Animation")]
    public float waterBounceHeight = 0.18f;
    public float waterBounceSeconds = 0.24f;
    public bool useCustomBlockSpawns;
    public List<BlockSpawnTurn> blockSpawnTurns = new List<BlockSpawnTurn>();
}
