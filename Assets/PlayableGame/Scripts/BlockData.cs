using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BlockData
{
    public string name;
    public List<Vector2Int> positions = new List<Vector2Int>();
    public List<TileType> tileTypes = new List<TileType>();
    public List<TileType> resourceTypes = new List<TileType>();

    public BlockData()
    {
    }

    public BlockData(string name, Vector2Int[] positions, TileType[] tileTypes)
        : this(name, positions, tileTypes, null)
    {
    }

    public BlockData(string name, Vector2Int[] positions, TileType[] tileTypes, TileType[] resourceTypes)
    {
        this.name = name;
        this.positions.AddRange(positions);
        this.tileTypes.AddRange(tileTypes);

        if (resourceTypes != null)
        {
            this.resourceTypes.AddRange(resourceTypes);
        }

        while (this.resourceTypes.Count < this.positions.Count)
        {
            this.resourceTypes.Add(TileType.Empty);
        }
    }

    public static BlockData Single(TileType tileType)
    {
        return FromShape("Dot", tileType, DotShape());
    }

    public static BlockData Domino(TileType left, TileType right)
    {
        return FromShape("Ix2", left, Ix2Shape());
    }

    public static BlockData Line(TileType a, TileType b, TileType c)
    {
        return FromShape("Ix3", a, Ix3Shape());
    }

    public static BlockData L(TileType a, TileType b, TileType c)
    {
        return FromShape("L", a, LShape());
    }

    public static BlockData Square(TileType a, TileType b, TileType c, TileType d)
    {
        return FromShape("O", a, OShape());
    }

    public static BlockData Random()
    {
        var positions = RandomShape();
        var ground = RandomGround();
        return FromShape(ground + " Block", ground, positions);
    }

    public static BlockData RandomFromShape(Vector2Int[] positions)
    {
        var ground = RandomGround();
        return FromShape(ground + " Block", ground, positions);
    }

    public static BlockData FromShape(string shapeName, TileType groundType, Vector2Int[] positions)
    {
        var grounds = new TileType[positions.Length];
        var resources = new TileType[positions.Length];

        for (var i = 0; i < positions.Length; i++)
        {
            grounds[i] = groundType;
            resources[i] = RandomResource(groundType);
        }

        return new BlockData(groundType + " " + shapeName, positions, grounds, resources);
    }

    public bool IsValid()
    {
        NormalizeResources();

        return positions != null
            && tileTypes != null
            && resourceTypes != null
            && positions.Count > 0
            && positions.Count == tileTypes.Count
            && positions.Count == resourceTypes.Count;
    }

    private void NormalizeResources()
    {
        if (positions == null)
        {
            return;
        }

        if (resourceTypes == null)
        {
            resourceTypes = new List<TileType>();
        }

        while (resourceTypes.Count < positions.Count)
        {
            resourceTypes.Add(TileType.Empty);
        }
    }

    private static Vector2Int[] RandomShape()
    {
        var shapes = ShapeVariants();
        return shapes[UnityEngine.Random.Range(0, shapes.Length)];
    }

    public static Vector2Int[][] ShapeVariants()
    {
        return new[]
        {
            DotShape(),
            Ix2Shape(),
            Rotate(Ix2Shape(), 1),
            Ix3Shape(),
            Rotate(Ix3Shape(), 1),
            Ix4Shape(),
            Rotate(Ix4Shape(), 1),
            OShape(),
            LShape(),
            Rotate(LShape(), 1),
            Rotate(LShape(), 2),
            Rotate(LShape(), 3),
            TShape(),
            Rotate(TShape(), 1),
            Rotate(TShape(), 2),
            Rotate(TShape(), 3),
            SShape(),
            Rotate(SShape(), 1),
            Rotate(SShape(), 2),
            Rotate(SShape(), 3)
        };
    }

    private static TileType RandomGround()
    {
        switch (UnityEngine.Random.Range(0, 3))
        {
            case 0: return TileType.Grass;
            case 1: return TileType.Dirt;
            default: return TileType.Water;
        }
    }

    public static TileType RandomResource(TileType ground)
    {
        if (LunaManager.ins != null)
        {
            return LunaManager.ins.RandomResourceForTile(ground);
        }

        switch (ground)
        {
            case TileType.Grass:
                switch (UnityEngine.Random.Range(0, 5))
                {
                    case 0: return TileType.Flower;
                    case 1: return TileType.Boar;
                    case 2: return TileType.BabyBoar;
                    case 3: return TileType.Bear;
                    default: return TileType.Empty;
                }
            case TileType.Dirt:
                return UnityEngine.Random.Range(0, 2) == 0 ? TileType.Empty : TileType.Wheat;
            case TileType.Water:
                return UnityEngine.Random.Range(0, 2) == 0 ? TileType.Fish : TileType.Empty;
            default:
                return TileType.Empty;
        }
    }

    private static Vector2Int[] DotShape()
    {
        return new[] { Vector2Int.zero };
    }

    private static Vector2Int[] Ix2Shape()
    {
        return new[] { Vector2Int.zero, Vector2Int.right };
    }

    private static Vector2Int[] Ix3Shape()
    {
        return new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 };
    }

    private static Vector2Int[] Ix4Shape()
    {
        return new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2, Vector2Int.right * 3 };
    }

    private static Vector2Int[] OShape()
    {
        return new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.one };
    }

    private static Vector2Int[] LShape()
    {
        return new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.up * 2, Vector2Int.right };
    }

    private static Vector2Int[] TShape()
    {
        return new[] { Vector2Int.zero, Vector2Int.left, Vector2Int.right, Vector2Int.down };
    }

    private static Vector2Int[] SShape()
    {
        return new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up + Vector2Int.right, Vector2Int.up + Vector2Int.right * 2 };
    }

    private static Vector2Int[] Rotate(Vector2Int[] positions, int quarterTurns)
    {
        var rotated = new Vector2Int[positions.Length];

        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i];
            for (var turn = 0; turn < quarterTurns; turn++)
            {
                position = new Vector2Int(-position.y, position.x);
            }

            rotated[i] = position;
        }

        return Normalize(rotated);
    }

    private static Vector2Int[] Normalize(Vector2Int[] positions)
    {
        var min = positions[0];
        for (var i = 1; i < positions.Length; i++)
        {
            min = Vector2Int.Min(min, positions[i]);
        }

        var normalized = new Vector2Int[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            normalized[i] = positions[i] - min;
        }

        return normalized;
    }
}
