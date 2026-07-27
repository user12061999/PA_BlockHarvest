using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Block Harvest/Level Config")]
public sealed class LevelConfig : ScriptableObject
{
    public List<Vector2Int> playableCoordinates = new List<Vector2Int>();
    public int wheatGoal = 15;
    public int meatGoal = 12;
    public int flowerGoal = 8;
    public int fishGoal = 10;
    public int maxPlacements = 12;
}
