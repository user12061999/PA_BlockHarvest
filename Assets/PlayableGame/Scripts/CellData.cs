using UnityEngine;

public enum TileType
{
    Empty,
    Grass,
    Dirt,
    Water,
    Wheat,
    Flower,
    Fish,
    Boar,
    BabyBoar,
    Bear,
    Pig
}

public sealed class CellData
{
    public Vector2Int coordinate;
    public TileType tileType;
    public TileType resourceType;
    public int resourceValue;
    public bool occupied;

    public CellData(Vector2Int coordinate, TileType tileType, bool occupied)
    {
        this.coordinate = coordinate;
        this.tileType = tileType;
        resourceType = TileType.Empty;
        resourceValue = 0;
        this.occupied = occupied;
    }
}
