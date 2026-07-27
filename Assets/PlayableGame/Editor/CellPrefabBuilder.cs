#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CellPrefabBuilder
{
    private const string PrefabPath = "Assets/PlayableGame/Prefabs/Cell.prefab";

    [MenuItem("Block Harvest/Create Cell Prefab")]
    public static void Create()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

        var root = new GameObject("Cell");
        var tileRenderer = root.AddComponent<SpriteRenderer>();
        tileRenderer.sortingOrder = -1;

        var resourceObject = new GameObject("Resource");
        resourceObject.transform.SetParent(root.transform, false);
        var resourceRenderer = resourceObject.AddComponent<SpriteRenderer>();
        resourceRenderer.sortingOrder = 0;

        var canvasObject = new GameObject("ResourceValueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(root.transform, false);
        var canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = Vector2.one;
        canvasRect.localPosition = Vector3.zero;

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30;

        var labelObject = new GameObject("ResourceValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(canvasObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0.25f, -0.25f);
        labelRect.sizeDelta = new Vector2(-0.5f, -0.5f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 0.7f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0f, 0.06005f, 1f);
        label.raycastTarget = false;
        label.text = string.Empty;
        label.enableWordWrapping = false;
        labelObject.SetActive(false);

        var tileView = root.AddComponent<TileView>();
        var serializedTileView = new SerializedObject(tileView);
        serializedTileView.FindProperty("tileRenderer").objectReferenceValue = tileRenderer;
        serializedTileView.FindProperty("resourceRenderer").objectReferenceValue = resourceRenderer;
        serializedTileView.FindProperty("resourceValueLabel").objectReferenceValue = label;
        serializedTileView.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created cell prefab: " + PrefabPath);
    }
}
#endif
