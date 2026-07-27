#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class GridBuilderWindow : EditorWindow
{
    private const int DefaultWidth = 7;
    private const int DefaultHeight = 8;
    private const int CellPixels = 30;

    private readonly HashSet<Vector2Int> activeCells = new HashSet<Vector2Int>();
    private LevelConfig levelConfig;
    private BoardManager boardManager;
    private int paintMode = 1;

    [MenuItem("Block Harvest/Grid Builder")]
    public static void Open()
    {
        GetWindow<GridBuilderWindow>("Grid Builder");
    }

    private void OnEnable()
    {
        boardManager = FindObjectOfType<BoardManager>();
        LoadFromConfig();
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        levelConfig = (LevelConfig)EditorGUILayout.ObjectField("Level Config", levelConfig, typeof(LevelConfig), false);
        boardManager = (BoardManager)EditorGUILayout.ObjectField("Board Manager", boardManager, typeof(BoardManager), true);
        if (EditorGUI.EndChangeCheck())
        {
            LoadFromConfig();
        }

        if (levelConfig == null && GUILayout.Button("Create Level Config"))
        {
            CreateLevelConfig();
        }

        EditorGUILayout.Space();
        paintMode = GUILayout.Toolbar(paintMode, new[] { "Disabled", "Active" });

        DrawGrid();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fill All"))
            {
                FillAll();
            }

            if (GUILayout.Button("Clear"))
            {
                activeCells.Clear();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load"))
            {
                LoadFromConfig();
            }

            if (GUILayout.Button("Save"))
            {
                SaveToConfig();
            }

            if (GUILayout.Button("Apply To Board"))
            {
                ApplyToBoard();
            }
        }
    }

    private void DrawGrid()
    {
        var size = boardManager != null ? boardManager.BoardSize : new Vector2Int(DefaultWidth, DefaultHeight);
        for (var y = size.y - 1; y >= 0; y--)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var x = 0; x < size.x; x++)
                {
                    var coordinate = new Vector2Int(x, y);
                    var isActive = activeCells.Contains(coordinate);
                    var oldColor = GUI.backgroundColor;
                    GUI.backgroundColor = isActive ? new Color(0.45f, 0.85f, 0.35f) : new Color(0.35f, 0.35f, 0.35f);

                    if (GUILayout.Button(x + "," + y, GUILayout.Width(CellPixels), GUILayout.Height(CellPixels)))
                    {
                        SetCell(coordinate, paintMode == 1);
                    }

                    GUI.backgroundColor = oldColor;
                }
            }
        }
    }

    private void SetCell(Vector2Int coordinate, bool active)
    {
        if (active)
        {
            activeCells.Add(coordinate);
            return;
        }

        activeCells.Remove(coordinate);
    }

    private void FillAll()
    {
        activeCells.Clear();
        var size = boardManager != null ? boardManager.BoardSize : new Vector2Int(DefaultWidth, DefaultHeight);
        for (var x = 0; x < size.x; x++)
        {
            for (var y = 0; y < size.y; y++)
            {
                activeCells.Add(new Vector2Int(x, y));
            }
        }
    }

    private void LoadFromConfig()
    {
        activeCells.Clear();
        if (levelConfig == null)
        {
            return;
        }

        for (var i = 0; i < levelConfig.playableCoordinates.Count; i++)
        {
            activeCells.Add(levelConfig.playableCoordinates[i]);
        }
    }

    private void SaveToConfig()
    {
        if (levelConfig == null)
        {
            Debug.LogWarning("Assign a LevelConfig before saving grid.");
            return;
        }

        Undo.RecordObject(levelConfig, "Save Grid Layout");
        levelConfig.playableCoordinates.Clear();

        var size = boardManager != null ? boardManager.BoardSize : new Vector2Int(DefaultWidth, DefaultHeight);
        for (var y = 0; y < size.y; y++)
        {
            for (var x = 0; x < size.x; x++)
            {
                var coordinate = new Vector2Int(x, y);
                if (activeCells.Contains(coordinate))
                {
                    levelConfig.playableCoordinates.Add(coordinate);
                }
            }
        }

        EditorUtility.SetDirty(levelConfig);
        AssetDatabase.SaveAssets();
    }

    private void CreateLevelConfig()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Level Config",
            "LevelConfig",
            "asset",
            "Choose where to save the grid config.");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        levelConfig = ScriptableObject.CreateInstance<LevelConfig>();
        AssetDatabase.CreateAsset(levelConfig, path);
        AssetDatabase.SaveAssets();
        SaveToConfig();
    }

    private void ApplyToBoard()
    {
        if (boardManager == null)
        {
            Debug.LogWarning("Assign a BoardManager before applying grid.");
            return;
        }

        SaveToConfig();
        boardManager.SetPlayableCoordinates(new List<Vector2Int>(activeCells));
        EditorUtility.SetDirty(boardManager);
    }
}
#endif
