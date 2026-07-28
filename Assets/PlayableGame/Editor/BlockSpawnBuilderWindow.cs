#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class BlockSpawnBuilderWindow : EditorWindow
{
    private const int DrawGridSize = 4;
    private const int CellPixels = 34;

    private LevelConfig levelConfig;
    private SerializedObject serializedConfig;
    private int selectedTurnIndex;
    private int selectedBlockIndex;
    private TileType paintTileType = TileType.Grass;

    [MenuItem("Block Harvest/Block Spawn Builder")]
    public static void Open()
    {
        GetWindow<BlockSpawnBuilderWindow>("Block Spawns");
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        levelConfig = (LevelConfig)EditorGUILayout.ObjectField("Level Config", levelConfig, typeof(LevelConfig), false);
        if (EditorGUI.EndChangeCheck())
        {
            serializedConfig = levelConfig != null ? new SerializedObject(levelConfig) : null;
        }

        if (levelConfig == null)
        {
            if (GUILayout.Button("Create Level Config"))
            {
                CreateLevelConfig();
            }

            return;
        }

        if (serializedConfig == null)
        {
            serializedConfig = new SerializedObject(levelConfig);
        }

        serializedConfig.Update();
        EditorGUILayout.PropertyField(serializedConfig.FindProperty("useCustomBlockSpawns"));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Turn"))
            {
                AddTurn();
            }

            if (GUILayout.Button("Remove Turn"))
            {
                RemoveTurn();
            }

            if (GUILayout.Button("Save"))
            {
                Save();
            }
        }

        DrawQuickEditor();
        EditorGUILayout.HelpBox("This tool only saves terrain shape. Resources are generated randomly by gameplay spawn logic.", MessageType.Info);

        serializedConfig.ApplyModifiedProperties();
    }

    private void AddTurn()
    {
        Undo.RecordObject(levelConfig, "Add Block Spawn Turn");
        var turn = new LevelConfig.BlockSpawnTurn();
        turn.blocks.Add(BlockData.Single(TileType.Grass));
        turn.blocks.Add(BlockData.Single(TileType.Dirt));
        turn.blocks.Add(BlockData.Single(TileType.Water));
        levelConfig.blockSpawnTurns.Add(turn);
        MarkDirty();
        serializedConfig = new SerializedObject(levelConfig);
    }

    private void RemoveTurn()
    {
        if (levelConfig.blockSpawnTurns == null || levelConfig.blockSpawnTurns.Count == 0)
        {
            return;
        }

        Undo.RecordObject(levelConfig, "Remove Block Spawn Turn");
        selectedTurnIndex = Mathf.Clamp(selectedTurnIndex, 0, levelConfig.blockSpawnTurns.Count - 1);
        levelConfig.blockSpawnTurns.RemoveAt(selectedTurnIndex);
        selectedTurnIndex = Mathf.Max(0, selectedTurnIndex - 1);
        MarkDirty();
        serializedConfig = new SerializedObject(levelConfig);
    }

    private void DrawQuickEditor()
    {
        if (levelConfig.blockSpawnTurns == null || levelConfig.blockSpawnTurns.Count == 0)
        {
            EditorGUILayout.HelpBox("Add a turn, then draw the 3 spawn blocks here.", MessageType.Info);
            return;
        }

        selectedTurnIndex = Mathf.Clamp(selectedTurnIndex, 0, levelConfig.blockSpawnTurns.Count - 1);
        selectedTurnIndex = EditorGUILayout.IntSlider("Turn", selectedTurnIndex, 0, levelConfig.blockSpawnTurns.Count - 1);
        selectedBlockIndex = GUILayout.Toolbar(Mathf.Clamp(selectedBlockIndex, 0, 2), new[] { "Block 1", "Block 2", "Block 3" });
        paintTileType = (TileType)EditorGUILayout.EnumPopup("Paint Tile", paintTileType);
        if (!IsPaintTile(paintTileType))
        {
            paintTileType = TileType.Grass;
        }

        var block = GetSelectedBlock();
        if (block == null)
        {
            return;
        }

        EditorGUILayout.LabelField("Click cells to paint. Use Empty to erase.");
        for (var y = DrawGridSize - 1; y >= 0; y--)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var x = 0; x < DrawGridSize; x++)
                {
                    var coordinate = new Vector2Int(x, y);
                    var tileType = GetBlockTile(block, coordinate);
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = GetTileColor(tileType);

                    if (GUILayout.Button(GetTileLabel(tileType), GUILayout.Width(CellPixels), GUILayout.Height(CellPixels)))
                    {
                        SetBlockTile(block, coordinate, paintTileType);
                    }

                    GUI.backgroundColor = oldColor;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Block"))
            {
                ClearBlock(block);
            }

            if (GUILayout.Button("Fill 1x1"))
            {
                ClearBlock(block);
                SetBlockTile(block, Vector2Int.zero, paintTileType == TileType.Empty ? TileType.Grass : paintTileType);
            }
        }
    }

    private BlockData GetSelectedBlock()
    {
        var turn = levelConfig.blockSpawnTurns[selectedTurnIndex];
        while (turn.blocks.Count <= selectedBlockIndex)
        {
            turn.blocks.Add(BlockData.Single(TileType.Grass));
        }

        if (turn.blocks[selectedBlockIndex] == null)
        {
            turn.blocks[selectedBlockIndex] = BlockData.Single(TileType.Grass);
        }

        return turn.blocks[selectedBlockIndex];
    }

    private TileType GetBlockTile(BlockData block, Vector2Int coordinate)
    {
        for (var i = 0; i < block.positions.Count; i++)
        {
            if (block.positions[i] == coordinate)
            {
                return i < block.tileTypes.Count ? block.tileTypes[i] : TileType.Empty;
            }
        }

        return TileType.Empty;
    }

    private void SetBlockTile(BlockData block, Vector2Int coordinate, TileType tileType)
    {
        Undo.RecordObject(levelConfig, "Paint Spawn Block");
        NormalizeBlock(block);
        for (var i = block.positions.Count - 1; i >= 0; i--)
        {
            if (block.positions[i] != coordinate)
            {
                continue;
            }

            if (tileType == TileType.Empty)
            {
                block.positions.RemoveAt(i);
                block.tileTypes.RemoveAt(i);
                block.resourceTypes.RemoveAt(i);
            }
            else
            {
                block.tileTypes[i] = tileType;
            }

            UpdateBlockName(block);
            MarkDirty();
            return;
        }

        if (tileType == TileType.Empty)
        {
            return;
        }

        block.positions.Add(coordinate);
        block.tileTypes.Add(tileType);
        block.resourceTypes.Add(TileType.Empty);
        UpdateBlockName(block);
        MarkDirty();
    }

    private void ClearBlock(BlockData block)
    {
        Undo.RecordObject(levelConfig, "Clear Spawn Block");
        block.positions.Clear();
        block.tileTypes.Clear();
        block.resourceTypes.Clear();
        block.name = "Empty Block";
        MarkDirty();
    }

    private void NormalizeBlock(BlockData block)
    {
        while (block.tileTypes.Count < block.positions.Count)
        {
            block.tileTypes.Add(TileType.Grass);
        }

        while (block.resourceTypes.Count < block.positions.Count)
        {
            block.resourceTypes.Add(TileType.Empty);
        }
    }

    private void UpdateBlockName(BlockData block)
    {
        block.name = "Custom " + block.positions.Count + " Tile Block";
    }

    private string GetTileLabel(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Grass: return "G";
            case TileType.Dirt: return "D";
            case TileType.Water: return "W";
            default: return ".";
        }
    }

    private Color GetTileColor(TileType tileType)
    {
        switch (tileType)
        {
            case TileType.Grass: return new Color(0.45f, 0.85f, 0.35f);
            case TileType.Dirt: return new Color(0.62f, 0.42f, 0.25f);
            case TileType.Water: return new Color(0.25f, 0.58f, 0.9f);
            default: return new Color(0.32f, 0.32f, 0.32f);
        }
    }

    private bool IsPaintTile(TileType tileType)
    {
        return tileType == TileType.Empty || tileType == TileType.Grass || tileType == TileType.Dirt || tileType == TileType.Water;
    }

    private void CreateLevelConfig()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Level Config",
            "LevelConfig",
            "asset",
            "Choose where to save the block spawn config.");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
        AssetDatabase.CreateAsset(levelConfig, path);
        serializedConfig = new SerializedObject(levelConfig);
        Save();
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(levelConfig);
    }

    private void Save()
    {
        MarkDirty();
        AssetDatabase.SaveAssets();
    }
}
#endif
