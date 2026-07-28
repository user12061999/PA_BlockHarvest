using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

[DisallowMultipleComponent]
public sealed class BlockPiece : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private BlockData data;

    private BoardManager boardManager;
    private HarvestManager harvestManager;
    private BlockManager blockManager;
    private Vector3 trayPosition;
    private Vector3 dragOffset;
    private Vector2Int targetOrigin;
    private Vector2Int targetAnchor;
    private bool hasTarget;
    private float pieceSize;
    private float dragOffsetY;
    private Vector3 trayScale;
    private Vector2 visualCenter;
    private readonly List<SpriteRenderer> tileRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> resourceRenderers = new List<SpriteRenderer>();

    public BlockData Data => data;
    public Vector2Int TargetOrigin => targetOrigin;
    public bool HasTarget => hasTarget;

    public void SetData(BlockData blockData, float pieceSize, BoardManager boardManager, HarvestManager harvestManager, BlockManager blockManager, float dragOffsetY)
    {
        data = blockData;
        this.pieceSize = pieceSize;
        this.dragOffsetY = dragOffsetY;
        this.boardManager = boardManager;
        this.harvestManager = harvestManager;
        this.blockManager = blockManager;
        trayPosition = transform.position;
        trayScale = transform.localScale;
        CalculateVisualCenter();
        BuildVisuals(pieceSize);
        ResizeCollider();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (boardManager == null)
        {
            return;
        }

        trayPosition = transform.position;
        trayScale = transform.localScale;
        if (blockManager != null)
        {
            blockManager.HideTutorial();
        }

        ScaleToGridCell();
        dragOffset = transform.position - ScreenToWorld(eventData) + Vector3.up * dragOffsetY;
        transform.position = ScreenToWorld(eventData) + dragOffset;
        SetSortingOrder(20);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (boardManager == null)
        {
            return;
        }

        transform.position = ScreenToWorld(eventData) + dragOffset;
        targetOrigin = GetTargetOrigin();
        hasTarget = true;
        boardManager.ShowPreview(targetOrigin, data);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (boardManager == null)
        {
            ReturnToTray();
            return;
        }

        if (!hasTarget)
        {
            targetOrigin = GetTargetOrigin();
        }

        if (TryPlaceAt(targetOrigin))
        {
            if (blockManager != null)
            {
                blockManager.RemoveBlock(this);
            }
            else
            {
                Destroy(gameObject);
            }

            return;
        }

        if (AudioManager.ins != null)
        {
            AudioManager.ins.PlayInvalidDrop();
        }

        ReturnToTray();
    }

    public bool TryPlaceAt(Vector2Int origin)
    {
        if (boardManager == null)
        {
            return false;
        }

        List<Vector2Int> placedCells;
        if (!boardManager.PlaceBlock(origin, data, out placedCells))
        {
            return false;
        }

        if (AudioManager.ins != null)
        {
            AudioManager.ins.PlayPlaceBlock();
        }

        if (harvestManager != null)
        {
            harvestManager.ResolvePlacement(boardManager, placedCells);
        }

        return true;
    }

    private void ReturnToTray()
    {
        if (boardManager != null)
        {
            boardManager.ClearPreview();
        }

        hasTarget = false;
        transform.position = trayPosition;
        transform.localScale = trayScale;
        SetSortingOrder(10);
    }

    private void ScaleToGridCell()
    {
        if (boardManager == null || pieceSize <= 0f)
        {
            return;
        }

        transform.localScale = trayScale * (boardManager.CellVisualSize / (pieceSize * 0.92f));
    }

    private void BuildVisuals(float pieceSize)
    {
        ClearVisuals();
        tileRenderers.Clear();
        resourceRenderers.Clear();

        if (data == null || !data.IsValid())
        {
            return;
        }

        for (var i = 0; i < data.positions.Count; i++)
        {
            var prefab = blockManager != null ? blockManager.GetTilePrefab(data.tileTypes[i]) : null;
            var pieceObject = prefab != null ? Instantiate(prefab) : new GameObject("Piece_" + i);
            pieceObject.name = "Piece_" + i;
            pieceObject.transform.SetParent(transform, false);
            var centeredPosition = (Vector2)data.positions[i] - visualCenter;
            pieceObject.transform.localPosition = new Vector3(centeredPosition.x, centeredPosition.y, 0f) * pieceSize;
            pieceObject.transform.localScale = Vector3.one * pieceSize * 0.92f;

            var renderers = pieceObject.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0)
            {
                renderers = new[] { pieceObject.AddComponent<SpriteRenderer>() };
            }

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (prefab == null)
                {
                    renderer.sprite = boardManager != null ? boardManager.GetTileSprite(data.tileTypes[i]) : null;
                    renderer.color = boardManager != null ? boardManager.GetTint(data.tileTypes[i]) : BoardManager.GetColor(data.tileTypes[i]);
                }

                renderer.sortingOrder = 10 + rendererIndex;
                tileRenderers.Add(renderer);
            }

            var resourceType = data.resourceTypes[i];
            UpdateResourceLabels(pieceObject, resourceType);
            if (resourceType == TileType.Empty)
            {
                continue;
            }

            var resourceObject = new GameObject("Resource");
            resourceObject.transform.SetParent(pieceObject.transform, false);
            resourceObject.transform.localScale = Vector3.one;

            var resourceRenderer = resourceObject.AddComponent<SpriteRenderer>();
            resourceRenderer.sprite = boardManager != null ? boardManager.GetTileSprite(resourceType) : null;
            resourceRenderer.color = boardManager != null ? boardManager.GetTint(resourceType) : BoardManager.GetColor(resourceType);
            resourceRenderer.sortingOrder = 50;
            resourceRenderers.Add(resourceRenderer);
        }
    }

    private void UpdateResourceLabels(GameObject pieceObject, TileType resourceType)
    {
        var labels = pieceObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        var showText = resourceType != TileType.Empty;
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i].raycastTarget = false;
            labels[i].gameObject.SetActive(showText);
            labels[i].text = showText ? BoardManager.GetDefaultResourceValue(resourceType).ToString() : string.Empty;
        }
    }

    private void ClearVisuals()
    {
        for (var i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private Vector3 ScreenToWorld(PointerEventData eventData)
    {
        var camera = eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
        if (camera == null)
        {
            return transform.position;
        }

        var screen = new Vector3(eventData.position.x, eventData.position.y, -camera.transform.position.z);
        var world = camera.ScreenToWorldPoint(screen);
        world.z = transform.position.z;
        return world;
    }

    private Vector2Int GetTargetOrigin()
    {
        return boardManager.WorldToCoordinate(transform.position) - targetAnchor;
    }

    private void SetSortingOrder(int sortingOrder)
    {
        for (var i = 0; i < tileRenderers.Count; i++)
        {
            var renderer = tileRenderers[i];
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder + i;
            }
        }

        for (var i = 0; i < resourceRenderers.Count; i++)
        {
            var renderer = resourceRenderers[i];
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder + 50 + i;
            }
        }
    }

    private void ResizeCollider()
    {
        var collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }

        Vector2Int min;
        Vector2Int max;
        GetBounds(out min, out max);

        var size = (Vector2)(max - min + Vector2Int.one) * pieceSize;
        collider.size = size;
        collider.offset = Vector2.zero;
    }

    private void CalculateVisualCenter()
    {
        if (data == null || data.positions == null || data.positions.Count == 0)
        {
            visualCenter = Vector2.zero;
            targetAnchor = Vector2Int.zero;
            return;
        }

        Vector2Int min;
        Vector2Int max;
        GetBounds(out min, out max);
        visualCenter = ((Vector2)min + max) * 0.5f;
        targetAnchor = Vector2Int.RoundToInt(visualCenter);
    }

    private void GetBounds(out Vector2Int min, out Vector2Int max)
    {
        min = data.positions[0];
        max = data.positions[0];

        foreach (var position in data.positions)
        {
            min = Vector2Int.Min(min, position);
            max = Vector2Int.Max(max, position);
        }
    }
}
