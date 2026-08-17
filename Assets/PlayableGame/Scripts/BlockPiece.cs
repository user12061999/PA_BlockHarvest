using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

[DisallowMultipleComponent]
public sealed class BlockPiece : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const int TraySortingGroupOrder = 20;
    private const int DragSortingGroupOrder = 2000;
    private const int DragSortingOffset = 200;

    [SerializeField] private BlockData data;

    private BoardManager boardManager;
    private HarvestManager harvestManager;
    private BlockManager blockManager;
    private SortingGroup sortingGroup;
    private Vector3 trayPosition;
    private Vector3 dragOffset;
    private Vector2Int targetOrigin;
    private Vector2Int targetAnchor;
    private bool hasTarget;
    private float pieceSize;
    private float trayTileHorizontalSpacing = 1f;
    private float trayTileVerticalSpacing = 1f;
    private float currentTileHorizontalSpacing = 1f;
    private float currentTileVerticalSpacing = 1f;
    private float dragOffsetY;
    private Vector3 trayScale;
    private Vector2 visualCenter;
    private bool pointerBlockedByTutorialUi;
    private bool isDragging;

    public BlockData Data => data;
    public Vector2Int TargetOrigin => targetOrigin;
    public bool HasTarget => hasTarget;

    public void SetData(
        BlockData blockData,
        float pieceSize,
        float trayTileHorizontalSpacing,
        float trayTileVerticalSpacing,
        BoardManager boardManager,
        HarvestManager harvestManager,
        BlockManager blockManager,
        float dragOffsetY)
    {
        data = blockData;
        this.pieceSize = pieceSize;
        this.trayTileHorizontalSpacing = Mathf.Max(0.01f, trayTileHorizontalSpacing);
        this.trayTileVerticalSpacing = Mathf.Max(0.01f, trayTileVerticalSpacing);
        currentTileHorizontalSpacing = this.trayTileHorizontalSpacing;
        currentTileVerticalSpacing = this.trayTileVerticalSpacing;
        this.dragOffsetY = dragOffsetY;
        this.boardManager = boardManager;
        this.harvestManager = harvestManager;
        this.blockManager = blockManager;
        trayPosition = transform.position;
        trayScale = transform.localScale;

        EnsureSortingGroup();
        SetSortingGroupOrder(TraySortingGroupOrder);

        CalculateVisualCenter();
        BuildVisuals(pieceSize);
        ResizeCollider();
    }

    public void SetData(
        BlockData blockData,
        float pieceSize,
        float trayTileHorizontalSpacing,
        BoardManager boardManager,
        HarvestManager harvestManager,
        BlockManager blockManager,
        float dragOffsetY)
    {
        SetData(
            blockData,
            pieceSize,
            trayTileHorizontalSpacing,
            1f,
            boardManager,
            harvestManager,
            blockManager,
            dragOffsetY);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerBlockedByTutorialUi = blockManager != null && blockManager.IsTutorialUiVisible;
        if (pointerBlockedByTutorialUi)
        {
            return;
        }

        if (boardManager == null)
        {
            return;
        }

        if (AudioManager.ins != null)
        {
            AudioManager.ins.PlayPickupBlock();
        }

        trayPosition = transform.position;
        trayScale = transform.localScale;
        if (blockManager != null)
        {
            blockManager.HideTutorial();
        }

        ScaleToGridCell();
        dragOffset = transform.position - ScreenToWorld(eventData) + Vector3.up * dragOffsetY;

        SetSortingGroupOrder(DragSortingGroupOrder);
        if (!isDragging)
        {
            OffsetAllChildrenSortingOrder(DragSortingOffset);
            isDragging = true;
        }

        var pos = ScreenToWorld(eventData) + dragOffset;
        pos.z = -5f;
        transform.position = pos;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (pointerBlockedByTutorialUi)
        {
            return;
        }

        if (boardManager == null)
        {
            return;
        }

        var pos = ScreenToWorld(eventData) + dragOffset;
        pos.z = -3f;
        transform.position = pos;

        targetOrigin = GetTargetOrigin();
        hasTarget = true;
        boardManager.ShowPreview(targetOrigin, data);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pointerBlockedByTutorialUi)
        {
            pointerBlockedByTutorialUi = false;
            return;
        }

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
        SetTileSpacing(trayTileHorizontalSpacing, trayTileVerticalSpacing);

        SetSortingGroupOrder(TraySortingGroupOrder);
        if (isDragging)
        {
            OffsetAllChildrenSortingOrder(-DragSortingOffset);
            isDragging = false;
        }

        transform.position = trayPosition;
        transform.localScale = trayScale;
    }

    private void OffsetAllChildrenSortingOrder(int offset)
    {
        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].sortingOrder += offset;
            }
        }

        var canvases = GetComponentsInChildren<Canvas>(true);
        for (var i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
            {
                canvases[i].overrideSorting = true;
                canvases[i].sortingOrder += offset;
            }
        }
    }

    private void EnsureSortingGroup()
    {
        if (sortingGroup == null)
        {
            sortingGroup = GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = gameObject.AddComponent<SortingGroup>();
            }
        }
    }

    private void SetSortingGroupOrder(int order)
    {
        EnsureSortingGroup();
        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
        }
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

        if (data == null || !data.IsValid())
        {
            return;
        }

        for (var i = 0; i < data.positions.Count; i++)
        {
            var pos = data.positions[i];
            var prefab = blockManager != null ? blockManager.GetTilePrefab(data.tileTypes[i]) : null;
            var pieceObject = prefab != null ? Instantiate(prefab) : new GameObject("Piece_" + i);
            pieceObject.name = "Piece_" + i;
            pieceObject.transform.SetParent(transform, false);

            pieceObject.transform.localPosition = GetTileLocalPosition(pos);
            pieceObject.transform.localScale = Vector3.one * pieceSize * 0.92f;

            if (prefab == null)
            {
                var renderer = pieceObject.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = pieceObject.AddComponent<SpriteRenderer>();
                renderer.sprite = boardManager != null ? boardManager.GetTileSprite(data.tileTypes[i]) : null;
                renderer.color = boardManager != null ? boardManager.GetTint(data.tileTypes[i]) : BoardManager.GetColor(data.tileTypes[i]);
                renderer.sortingOrder = 0;
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
            resourceObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

            var resourceRenderer = resourceObject.AddComponent<SpriteRenderer>();
            resourceRenderer.sprite = boardManager != null ? boardManager.GetTileSprite(resourceType) : null;
            resourceRenderer.color = boardManager != null ? boardManager.GetTint(resourceType) : BoardManager.GetColor(resourceType);
            resourceRenderer.sortingOrder = 50;
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

    public void SetTileSpacing(float horizontalSpacing, float verticalSpacing)
    {
        if (data == null || data.positions == null)
        {
            return;
        }

        currentTileHorizontalSpacing = Mathf.Max(0.01f, horizontalSpacing);
        currentTileVerticalSpacing = Mathf.Max(0.01f, verticalSpacing);
        for (var i = 0; i < data.positions.Count && i < transform.childCount; i++)
        {
            transform.GetChild(i).localPosition = GetTileLocalPosition(data.positions[i]);
        }

        ResizeCollider();
    }

    private Vector3 GetTileLocalPosition(Vector2Int position)
    {
        var centeredPosition = (Vector2)position - visualCenter;
        var posX = centeredPosition.x * currentTileHorizontalSpacing * pieceSize;
        var posY = centeredPosition.y * currentTileVerticalSpacing * pieceSize;

        var localZ = position.y * 0.02f;
        return new Vector3(posX, posY, localZ);
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

        var size = new Vector2(
            (max.x - min.x) * currentTileHorizontalSpacing + 1f,
            max.y - min.y + 1f) * pieceSize;
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