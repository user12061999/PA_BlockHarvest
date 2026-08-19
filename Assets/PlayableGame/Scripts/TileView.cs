using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[DisallowMultipleComponent]
public sealed class TileView : MonoBehaviour
{
    private const int PlacementPreviewSortingOrder = 5;

    [Header("Designed Tile References")]
    [SerializeField] private SpriteRenderer tileRenderer;
    [SerializeField] private SpriteRenderer resourceRenderer;
    [SerializeField] private TextMeshProUGUI resourceValueLabel;
    [Header("Grid Cell Visual")]
    [SerializeField] private Color gridCellColor = Color.white;
    [Header("Resource Visual")]
    [SerializeField] private float resourceScale = 1.2f;
    [SerializeField] private float resourceYOffset = 0.1f;

    private BoardManager board;
    private CellData cell;
    private readonly List<Vector3> resourceSpritePositions = new List<Vector3>(8);
    private Coroutine idleAnimation;
    private TileType idleResourceType;
    private TileType idleTileType;
    private GameObject tilePrefabInstance;
    private TileType tilePrefabType = TileType.Empty;
    private SpriteRenderer[] tilePrefabRenderers;
    private Color[] tilePrefabColors;
    private Sprite[] tilePrefabSprites;
    private Coroutine bounceAnimation;
    private Coroutine yieldPopupAnimation;
    private Coroutine resourceScaleInAnimation;
    private float resourceScaleInAmount = 1f;
    private GameObject yieldPopupObject;
    private GameObject placementPreviewObject;
    private Vector3 baseLocalPosition;

    public Vector2Int Coordinate => cell != null ? cell.coordinate : Vector2Int.zero;
    public TileType BlockType => cell != null ? cell.tileType : TileType.Empty;
    public TileType ResourceType => cell != null ? cell.resourceType : TileType.Empty;
    public int ResourceValue => cell != null ? cell.resourceValue : 0;
    public IReadOnlyList<Vector3> ResourceSpritePositions => resourceSpritePositions;
    public SpriteRenderer ResourceRenderer => resourceRenderer;

    public void Initialize(BoardManager board, CellData cell, float cellScale)
    {
        this.board = board;
        this.cell = cell;
        transform.localScale = Vector3.one * cellScale;
        BuildVisuals();
        Render();
    }

    public void Render()
    {
        if (board == null || cell == null)
        {
            return;
        }

        UpdateTileVisual();

        resourceRenderer.enabled = cell.resourceType != TileType.Empty;
        resourceRenderer.sprite = cell.resourceType == TileType.Empty ? null : board.GetTileSprite(cell.resourceType);
        resourceRenderer.color = cell.resourceType == TileType.Empty ? Color.clear : board.GetTint(cell.resourceType);
        UpdateResourceSprites();
        UpdateResourceValueLabel();
        UpdateIdleAnimation();
    }

    public void SetTileColor(Color color)
    {
        if (tileRenderer != null)
        {
            tileRenderer.color = color;
        }

        SetTilePrefabTint(color);
    }

    public void ShowPlacementPreview(TileType tileType, TileType resourceType, float alpha)
    {
        ClearPlacementPreview();

        if (tileType == TileType.Empty)
        {
            return;
        }

        alpha = Mathf.Clamp01(alpha);
        placementPreviewObject = new GameObject("PlacementPreview");
        placementPreviewObject.transform.SetParent(transform, false);
        placementPreviewObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        placementPreviewObject.transform.localRotation = Quaternion.identity;
        placementPreviewObject.transform.localScale = Vector3.one;

        var prefab = board != null ? board.GetTilePrefab(tileType) : null;
        if (prefab != null)
        {
            var tileObject = Instantiate(prefab, placementPreviewObject.transform);
            tileObject.name = "Tile";
            tileObject.transform.localPosition = Vector3.zero;
            tileObject.transform.localRotation = Quaternion.identity;
            tileObject.transform.localScale = Vector3.one;
            DisablePreviewColliders(tileObject);

            var renderers = tileObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                ApplyPreviewRenderer(renderers[i], alpha, renderers[i].sortingOrder);
            }

            UpdatePreviewLabels(tileObject, TileType.Empty, alpha);
        }
        else
        {
            var renderer = placementPreviewObject.AddComponent<SpriteRenderer>();
            renderer.sprite = board != null ? board.GetTileSprite(tileType) : null;
            renderer.color = ApplyAlpha(board != null ? board.GetTint(tileType) : BoardManager.GetColor(tileType), alpha);
            renderer.sortingOrder = 0;
        }
    }

    public void ClearPlacementPreview()
    {
        if (placementPreviewObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(placementPreviewObject);
        }
        else
        {
            DestroyImmediate(placementPreviewObject);
        }

        placementPreviewObject = null;
    }

    private void ApplyPreviewRenderer(SpriteRenderer renderer, float alpha, int sortingOrder)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.color = ApplyAlpha(renderer.color, alpha);
        renderer.sortingOrder = sortingOrder;
    }

    private Color ApplyAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    private void UpdatePreviewLabels(GameObject previewObject, TileType resourceType, float alpha)
    {
        var labels = previewObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        var showText = resourceType != TileType.Empty;
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i].raycastTarget = false;
            labels[i].gameObject.SetActive(showText);
            labels[i].text = showText ? BoardManager.GetDefaultResourceValue(resourceType).ToString() : string.Empty;
            labels[i].color = ApplyAlpha(labels[i].color, alpha);
        }
    }

    private void DisablePreviewColliders(GameObject previewObject)
    {
        var colliders2D = previewObject.GetComponentsInChildren<Collider2D>(true);
        for (var i = 0; i < colliders2D.Length; i++)
        {
            colliders2D[i].enabled = false;
        }

        var colliders3D = previewObject.GetComponentsInChildren<Collider>(true);
        for (var i = 0; i < colliders3D.Length; i++)
        {
            colliders3D[i].enabled = false;
        }
    }

    public void PlayPulse()
    {
        if (resourceRenderer != null)
        {
            StartCoroutine(Pulse(resourceRenderer.transform));
        }
    }

    public void PlayResourceScaleIn(float seconds = 0.22f, float peakMultiplier = 1.18f)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (resourceScaleInAnimation != null)
        {
            StopCoroutine(resourceScaleInAnimation);
        }

        resourceScaleInAnimation = StartCoroutine(PlayResourceScaleInRoutine(seconds, peakMultiplier));
    }

    public void PlayYieldPopup(int amount, GameObject popupPrefab = null)
    {
        if (!Application.isPlaying || amount <= 0)
        {
            return;
        }

        if (yieldPopupAnimation != null)
        {
            StopCoroutine(yieldPopupAnimation);
            yieldPopupAnimation = null;
        }

        if (yieldPopupObject != null)
        {
            Destroy(yieldPopupObject);
        }

        if (popupPrefab != null)
        {
            yieldPopupObject = Instantiate(popupPrefab, transform);
            yieldPopupObject.name = "YieldPopup";
            yieldPopupObject.transform.localPosition = Vector3.zero;
            yieldPopupObject.transform.localRotation = Quaternion.identity;
            SetYieldPopupText(yieldPopupObject, amount);
            yieldPopupAnimation = StartCoroutine(PlayYieldPopupRoutine(yieldPopupObject));
            return;
        }

        yieldPopupObject = new GameObject("YieldPopup", typeof(RectTransform));
        yieldPopupObject.transform.SetParent(transform, false);

        var canvasRect = yieldPopupObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = Vector2.one;
        canvasRect.localPosition = Vector3.zero;

        var canvas = yieldPopupObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 45;

        var labelObject = new GameObject("YieldPopupText", typeof(RectTransform));
        labelObject.transform.SetParent(yieldPopupObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 0.12f);
        labelRect.sizeDelta = Vector2.zero;

        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 0.65f;
        label.fontStyle = FontStyles.Bold;
        label.text = "+" + amount;
        label.color = BoardManager.GetColor(TileType.Wheat);
        label.raycastTarget = false;
        label.enableWordWrapping = false;

        yieldPopupAnimation = StartCoroutine(PlayYieldPopupRoutine(yieldPopupObject, label, labelRect));
    }

    public void PlayWaterEffect(Vector2Int direction)
    {
        if (tilePrefabInstance == null)
        {
            return;
        }

        var effect = tilePrefabInstance.GetComponentInChildren<WaterTileEffect>(true);
        if (effect != null)
        {
            effect.Play(direction);
        }
    }

    public void PlayBounce(float height, float seconds)
    {
        PlayBounce(height, seconds, 0f);
    }

    public void PlayBounce(float height, float seconds, float delay)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (bounceAnimation != null)
        {
            StopCoroutine(bounceAnimation);
            transform.localPosition = baseLocalPosition;
        }

        baseLocalPosition = transform.localPosition;
        bounceAnimation = StartCoroutine(Bounce(height, seconds, delay));
    }

    public IEnumerator PlayClear(float bounceHeight, float bounceSeconds, float shrinkSeconds, bool keepResourceVisuals = false)
    {
        StopIdleAnimation();
        if (bounceAnimation != null)
        {
            StopCoroutine(bounceAnimation);
            bounceAnimation = null;
        }

        var renderers = new List<SpriteRenderer>();
        AddRenderer(renderers, tileRenderer);
        AddChildRenderers(renderers, tilePrefabInstance != null ? tilePrefabInstance.transform : null);

        var resourceSprites = transform.Find("ResourceSprites");
        if (!keepResourceVisuals)
        {
            AddRenderer(renderers, resourceRenderer);
            AddChildRenderers(renderers, resourceSprites);
        }

        if (renderers.Count == 0)
        {
            yield break;
        }

        bounceSeconds = Mathf.Max(0.01f, bounceSeconds);
        shrinkSeconds = Mathf.Max(0.01f, shrinkSeconds);

        var startPos = baseLocalPosition != Vector3.zero ? baseLocalPosition : transform.localPosition;
        var peakPos = startPos + Vector3.up * bounceHeight;

        var scales = new Vector3[renderers.Count];
        var colors = new Color[renderers.Count];

        for (var i = 0; i < renderers.Count; i++)
        {
            scales[i] = renderers[i].transform.localScale;
            colors[i] = renderers[i].color;
        }

        // --- Phase 1: Nảy lên đỉnh ---
        var halfBounce = bounceSeconds * 0.5f;
        for (var elapsed = 0f; elapsed < halfBounce; elapsed += Time.deltaTime)
        {
            var t = Mathf.SmoothStep(0f, 1f, elapsed / halfBounce);
            transform.localPosition = Vector3.Lerp(startPos, peakPos, t);
            yield return null;
        }
        transform.localPosition = peakPos;

        // --- Phase 2: Rơi lại về vị trí ban đầu ---
        for (var elapsed = 0f; elapsed < halfBounce; elapsed += Time.deltaTime)
        {
            var t = Mathf.SmoothStep(0f, 1f, elapsed / halfBounce);
            transform.localPosition = Vector3.Lerp(peakPos, startPos, t);
            yield return null;
        }
        transform.localPosition = startPos;

        // --- Phase 3: Thu nhỏ và mờ dần biến mất ---
        for (var elapsed = 0f; elapsed < shrinkSeconds; elapsed += Time.deltaTime)
        {
            var t = elapsed / shrinkSeconds;
            for (var i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null) continue;
                var color = colors[i];
                color.a = Mathf.Lerp(colors[i].a, 0f, t);
                renderers[i].color = color;
                renderers[i].transform.localScale = Vector3.Lerp(scales[i], Vector3.zero, t);
            }
            yield return null;
        }

        // Khôi phục scale/color gốc cho Prefab
        for (var i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].color = colors[i];
            renderers[i].transform.localScale = scales[i];
        }
        transform.localPosition = startPos;

        // --- XÓA/ẨN Ô GẠCH NGAY LẬP TỨC KHI HẾT ANIMATION ---
        UpdateTileVisual();

        if (!keepResourceVisuals)
        {
            if (resourceRenderer != null)
            {
                resourceRenderer.enabled = false;
                resourceRenderer.sprite = null;
            }
            HideChildRenderers("ResourceSprites");
            ClearChild("ResourceSprites");
            if (resourceValueLabel != null)
            {
                resourceValueLabel.gameObject.SetActive(false);
            }
        }
    }

    private void BuildVisuals()
    {
        var createdTileRenderer = false;
        if (tileRenderer == null)
        {
            tileRenderer = GetComponent<SpriteRenderer>();
        }

        if (tileRenderer == null)
        {
            tileRenderer = gameObject.AddComponent<SpriteRenderer>();
            createdTileRenderer = true;
        }

        if (createdTileRenderer)
        {
            tileRenderer.sortingOrder = -1;
        }

        var createdResourceRenderer = false;
        if (resourceRenderer == null)
        {
            var resourceTransform = transform.Find("Resource");
            resourceRenderer = resourceTransform != null ? resourceTransform.GetComponent<SpriteRenderer>() : null;
        }

        if (resourceRenderer == null)
        {
            var resourceObject = new GameObject("Resource");
            resourceObject.transform.SetParent(transform, false);
            resourceObject.transform.localScale = Vector3.one;
            resourceRenderer = resourceObject.AddComponent<SpriteRenderer>();
            createdResourceRenderer = true;
        }

        if (createdResourceRenderer)
        {
            resourceRenderer.sortingOrder = 0;
        }

        if (resourceValueLabel == null)
        {
            resourceValueLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (resourceValueLabel != null)
        {
            resourceValueLabel.raycastTarget = false;
            return;
        }

        var valueCanvasObject = new GameObject("ResourceValueCanvas", typeof(RectTransform));
        valueCanvasObject.transform.SetParent(transform, false);

        var valueCanvasRect = valueCanvasObject.GetComponent<RectTransform>();
        valueCanvasRect.sizeDelta = Vector2.one;
        valueCanvasRect.localPosition = Vector3.zero;

        var valueCanvas = valueCanvasObject.AddComponent<Canvas>();
        valueCanvas.renderMode = RenderMode.WorldSpace;
        valueCanvas.overrideSorting = true;
        valueCanvas.sortingOrder = 30;

        var valueObject = new GameObject("ResourceValue", typeof(RectTransform));
        valueObject.transform.SetParent(valueCanvasObject.transform, false);

        var valueRect = valueObject.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.pivot = new Vector2(0.5f, 0.5f);
        valueRect.anchoredPosition = new Vector2(0.25f, -0.25f);
        valueRect.sizeDelta = new Vector2(-0.5f, -0.5f);

        resourceValueLabel = valueObject.AddComponent<TextMeshProUGUI>();
        resourceValueLabel.alignment = TextAlignmentOptions.Center;
        resourceValueLabel.fontSize = 0.7f;
        resourceValueLabel.fontStyle = FontStyles.Bold;
        resourceValueLabel.text = string.Empty;
        resourceValueLabel.color = new Color(0f, 0.06005f, 1f);
        resourceValueLabel.raycastTarget = false;
        resourceValueLabel.enableWordWrapping = false;
    }

    private void UpdateTileVisual()
    {
        var tileSprite = board.GetTileSprite(cell);
        var prefab = board.GetTilePrefab(cell.tileType);
        if (prefab == null || cell.tileType == TileType.Empty)
        {
            ClearTilePrefab();
            tileRenderer.enabled = true;
            tileRenderer.sprite = tileSprite;
            tileRenderer.color = GetTileRendererColor(cell.tileType);
            return;
        }

        tileRenderer.enabled = false;
        if (tilePrefabInstance == null || tilePrefabType != cell.tileType)
        {
            ClearTilePrefab();
            tilePrefabType = cell.tileType;
            tilePrefabInstance = Instantiate(prefab, transform);
            tilePrefabInstance.name = "TilePrefab";
            tilePrefabInstance.transform.localPosition = Vector3.zero;
            tilePrefabInstance.transform.localRotation = Quaternion.identity;
            tilePrefabInstance.transform.localScale = Vector3.one;
            CacheTilePrefabRenderers();
        }

        RestoreTilePrefabColors();
        ApplyTilePrefabSprite(tileSprite);
    }

    private Color GetTileRendererColor(TileType tileType)
    {
        var tint = board != null ? board.GetTint(tileType) : BoardManager.GetColor(tileType);
        return tileType == TileType.Empty ? tint * gridCellColor : tint;
    }

    private void ClearTilePrefab()
    {
        tilePrefabType = TileType.Empty;
        tilePrefabRenderers = null;
        tilePrefabColors = null;
        tilePrefabSprites = null;

        if (tilePrefabInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(tilePrefabInstance);
        }
        else
        {
            DestroyImmediate(tilePrefabInstance);
        }

        tilePrefabInstance = null;
    }

    private void CacheTilePrefabRenderers()
    {
        tilePrefabRenderers = tilePrefabInstance != null
            ? tilePrefabInstance.GetComponentsInChildren<SpriteRenderer>(true)
            : null;
        if (tilePrefabRenderers == null)
        {
            tilePrefabColors = null;
            tilePrefabSprites = null;
            return;
        }

        tilePrefabColors = new Color[tilePrefabRenderers.Length];
        tilePrefabSprites = new Sprite[tilePrefabRenderers.Length];
        for (var i = 0; i < tilePrefabRenderers.Length; i++)
        {
            tilePrefabColors[i] = tilePrefabRenderers[i].color;
            tilePrefabSprites[i] = tilePrefabRenderers[i].sprite;
        }
    }

    private void RestoreTilePrefabColors()
    {
        if (tilePrefabRenderers == null || tilePrefabColors == null)
        {
            return;
        }

        for (var i = 0; i < tilePrefabRenderers.Length; i++)
        {
            if (tilePrefabRenderers[i] != null)
            {
                tilePrefabRenderers[i].color = tilePrefabColors[i];
            }
        }
    }

    private void ApplyTilePrefabSprite(Sprite sprite)
    {
        if (tilePrefabRenderers == null || tilePrefabSprites == null)
        {
            return;
        }

        for (var i = 0; i < tilePrefabRenderers.Length; i++)
        {
            if (tilePrefabRenderers[i] != null)
            {
                tilePrefabRenderers[i].sprite = tilePrefabSprites[i];
            }
        }

        if (cell.tileType == TileType.Dirt && cell.dirt3x3Boosted && sprite != null && tilePrefabRenderers.Length > 0 && tilePrefabRenderers[0] != null)
        {
            tilePrefabRenderers[0].sprite = sprite;
        }
    }

    private void SetTilePrefabTint(Color tint)
    {
        if (tilePrefabRenderers == null || tilePrefabColors == null)
        {
            return;
        }

        for (var i = 0; i < tilePrefabRenderers.Length; i++)
        {
            if (tilePrefabRenderers[i] == null)
            {
                continue;
            }

            tilePrefabRenderers[i].color = tilePrefabColors[i] * tint;
        }
    }
   

    private Vector3 GetResourceScale()
    {
        return Vector3.one * Mathf.Max(0.01f, resourceScale);
    }

    private Vector3 GetAnimatedResourceScale()
    {
        return GetResourceScale() * resourceScaleInAmount;
    }

    private Vector3 GetResourcePosition(Vector3 position)
    {
        return position + Vector3.up * resourceYOffset;
    }

    private void AddCustomResourceSpritePositions(int count)
    {
        count = Mathf.Clamp(count, 1, 9);

        switch (count)
        {
            case 1:
                resourceSpritePositions.Add(new Vector3(0f, 0.06f, 0f));
                break;
            case 2:
                resourceSpritePositions.Add(new Vector3(-0.18f, 0.06f, 0f));
                resourceSpritePositions.Add(new Vector3(0.18f, 0.06f, 0f));
                break;
            case 3:
                resourceSpritePositions.Add(new Vector3(0f, 0.28f, 0f));
                resourceSpritePositions.Add(new Vector3(-0.22f, -0.08f, 0f));
                resourceSpritePositions.Add(new Vector3(0.22f, -0.08f, 0f));
                break;
            case 4:
                AddSquareCorners(0.2f, 0.18f);
                break;
            case 5:
                AddSquareCorners(0.27f, 0.25f);
                resourceSpritePositions.Add(Vector3.zero);
                break;
            case 6:
                AddResourceRow(0.28f, 0.18f);
                AddResourceRow(0.28f, -0.18f);
                break;
            case 7:
                AddResourceRow(0.28f, 0.28f);
                resourceSpritePositions.Add(Vector3.zero);
                AddResourceRow(0.28f, -0.28f);
                break;
            case 8:
                AddResourceRow(0.28f, 0.28f);
                resourceSpritePositions.Add(new Vector3(-0.28f, 0f, 0f));
                resourceSpritePositions.Add(Vector3.zero);
                AddResourceRow(0.28f, -0.28f);
                break;
            default:
                AddResourceRow(0.28f, 0.28f);
                AddResourceRow(0.28f, 0f);
                AddResourceRow(0.28f, -0.28f);
                break;
        }
    }

    private void AddSquareCorners(float x, float y)
    {
        resourceSpritePositions.Add(new Vector3(-x, y, 0f));
        resourceSpritePositions.Add(new Vector3(x, y, 0f));
        resourceSpritePositions.Add(new Vector3(-x, -y, 0f));
        resourceSpritePositions.Add(new Vector3(x, -y, 0f));
    }

    private void AddResourceRow(float x, float y)
    {
        resourceSpritePositions.Add(new Vector3(-x, y, 0f));
        resourceSpritePositions.Add(new Vector3(0f, y, 0f));
        resourceSpritePositions.Add(new Vector3(x, y, 0f));
    }

    private void UpdateResourceSprites()
    {
        ClearChild("ResourceSprites");
        ClearChild("ResourceCount");
        resourceSpritePositions.Clear();

        var baseSortingOrder = 1;

        if (cell.resourceType == TileType.Empty)
        {
            resourceRenderer.transform.localPosition = GetResourcePosition(Vector3.zero);
            resourceRenderer.transform.localScale = GetAnimatedResourceScale();
            resourceRenderer.sortingOrder = baseSortingOrder;
            return;
        }

        if (cell.resourceType != TileType.Wheat && cell.resourceType != TileType.Flower)
        {
            resourceRenderer.transform.localPosition = GetResourcePosition(Vector3.zero);
            resourceRenderer.transform.localScale = GetAnimatedResourceScale();
            resourceRenderer.sortingOrder = baseSortingOrder;
            resourceSpritePositions.Add(Vector3.zero);
            return;
        }

        AddCustomResourceSpritePositions(cell.resourceValue);

        resourceRenderer.transform.localPosition = GetResourcePosition(resourceSpritePositions[0]);
        resourceRenderer.transform.localScale = GetAnimatedResourceScale();
        resourceRenderer.sortingOrder = GetResourceSortingOrder(baseSortingOrder, resourceSpritePositions[0]);

        if (resourceSpritePositions.Count <= 1)
        {
            return;
        }

        var root = new GameObject("ResourceSprites").transform;
        root.SetParent(transform, false);

        for (var i = 1; i < resourceSpritePositions.Count; i++)
        {
            var marker = new GameObject(cell.resourceType + "_" + i);
            marker.transform.SetParent(root, false);
            marker.transform.localPosition = GetResourcePosition(resourceSpritePositions[i]);
            marker.transform.localScale = GetAnimatedResourceScale();

            var markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = board.GetTileSprite(cell.resourceType);
            markerRenderer.color = board.GetTint(cell.resourceType);
            markerRenderer.sortingLayerID = resourceRenderer.sortingLayerID;
            markerRenderer.sortingOrder = GetResourceSortingOrder(baseSortingOrder, resourceSpritePositions[i]);
        }
    }

    private int GetResourceSortingOrder(int baseSortingOrder, Vector3 position)
    {
        return baseSortingOrder + Mathf.RoundToInt((0.5f - position.y) * 10f);
    }

    private IEnumerator Pulse(Transform target)
    {
        var baseScale = target.localScale;
        var peakScale = baseScale * 1.18f;

        for (var elapsed = 0f; elapsed < 0.18f; elapsed += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(baseScale, peakScale, elapsed / 0.18f);
            yield return null;
        }

        for (var elapsed = 0f; elapsed < 0.18f; elapsed += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(peakScale, baseScale, elapsed / 0.18f);
            yield return null;
        }

        target.localScale = baseScale;
    }

    private void ApplyResourceScaleToCurrentSprites()
    {
        var scale = GetAnimatedResourceScale();

        if (resourceRenderer != null)
        {
            resourceRenderer.transform.localScale = scale;
        }

        var resourceSprites = transform.Find("ResourceSprites");
        if (resourceSprites == null)
        {
            return;
        }

        var spriteRenderers = resourceSprites.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].transform.localScale = scale;
            }
        }
    }

    private IEnumerator PlayResourceScaleInRoutine(float seconds, float peakMultiplier)
    {
        seconds = Mathf.Max(0.01f, seconds);
        peakMultiplier = Mathf.Max(1f, peakMultiplier);
        resourceScaleInAmount = 1f;
        ApplyResourceScaleToCurrentSprites();

        var halfSeconds = seconds * 0.5f;
        for (var elapsed = 0f; elapsed < halfSeconds; elapsed += Time.deltaTime)
        {
            resourceScaleInAmount = Mathf.Lerp(1f, peakMultiplier, Mathf.SmoothStep(0f, 1f, elapsed / halfSeconds));
            ApplyResourceScaleToCurrentSprites();
            yield return null;
        }

        for (var elapsed = 0f; elapsed < halfSeconds; elapsed += Time.deltaTime)
        {
            resourceScaleInAmount = Mathf.Lerp(peakMultiplier, 1f, Mathf.SmoothStep(0f, 1f, elapsed / halfSeconds));
            ApplyResourceScaleToCurrentSprites();
            yield return null;
        }

        resourceScaleInAmount = 1f;
        ApplyResourceScaleToCurrentSprites();
        resourceScaleInAnimation = null;
    }

    private IEnumerator Bounce(float height, float seconds, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        seconds = Mathf.Max(0.01f, seconds);
        var from = baseLocalPosition;
        var to = from + Vector3.up * height;
        var halfSeconds = seconds * 0.5f;

        for (var elapsed = 0f; elapsed < halfSeconds; elapsed += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / halfSeconds));
            yield return null;
        }

        for (var elapsed = 0f; elapsed < halfSeconds; elapsed += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(to, from, Mathf.SmoothStep(0f, 1f, elapsed / halfSeconds));
            yield return null;
        }

        transform.localPosition = from;
        bounceAnimation = null;
    }

    private IEnumerator PlayYieldPopupRoutine(GameObject popupObject, TextMeshProUGUI label, RectTransform labelRect)
    {
        var start = labelRect.anchoredPosition;
        var end = start + Vector2.up * 0.42f;
        var startColor = label.color;
        var duration = 0.7f;

        for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            var t = Mathf.Clamp01(elapsed / duration);
            labelRect.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));

            var color = startColor;
            color.a = Mathf.Lerp(1f, 0f, t);
            label.color = color;

            yield return null;
        }

        if (yieldPopupObject == popupObject)
        {
            yieldPopupObject = null;
            yieldPopupAnimation = null;
        }

        Destroy(popupObject);
    }

    private IEnumerator PlayYieldPopupRoutine(GameObject popupObject)
    {
        var start = popupObject.transform.localPosition;
        var end = start + Vector3.up * 0.42f;
        var textRenderers = popupObject.GetComponentsInChildren<TMP_Text>(true);
        var textColors = new Color[textRenderers.Length];
        for (var i = 0; i < textRenderers.Length; i++)
        {
            textColors[i] = textRenderers[i].color;
        }

        var spriteRenderers = popupObject.GetComponentsInChildren<SpriteRenderer>(true);
        var spriteColors = new Color[spriteRenderers.Length];
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            spriteColors[i] = spriteRenderers[i].color;
        }

        var duration = 0.7f;
        for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            var t = Mathf.Clamp01(elapsed / duration);
            popupObject.transform.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            ApplyPopupAlpha(textRenderers, textColors, spriteRenderers, spriteColors, 1f - t);
            yield return null;
        }

        if (yieldPopupObject == popupObject)
        {
            yieldPopupObject = null;
            yieldPopupAnimation = null;
        }

        Destroy(popupObject);
    }

    private void SetYieldPopupText(GameObject popupObject, int amount)
    {
        var texts = popupObject.GetComponentsInChildren<TMP_Text>(true);
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i].text = "+" + amount;
        }
    }

    private void ApplyPopupAlpha(
        TMP_Text[] textRenderers,
        Color[] textColors,
        SpriteRenderer[] spriteRenderers,
        Color[] spriteColors,
        float alpha)
    {
        for (var i = 0; i < textRenderers.Length; i++)
        {
            var color = textColors[i];
            color.a *= alpha;
            textRenderers[i].color = color;
        }

        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            var color = spriteColors[i];
            color.a *= alpha;
            spriteRenderers[i].color = color;
        }
    }

    private void UpdateResourceValueLabel()
    {
        if (resourceValueLabel == null)
        {
            return;
        }

        var showText = cell.resourceType != TileType.Empty && cell.resourceValue > 0;
        resourceValueLabel.gameObject.SetActive(showText);
        resourceValueLabel.text = showText ? cell.resourceValue.ToString() : string.Empty;
    }

    private void UpdateIdleAnimation()
    {
        var frames = cell.resourceType == TileType.Empty ? null : board.GetIdleFrames(cell.resourceType, cell.tileType);
        if (frames == null || frames.Length == 0)
        {
            StopIdleAnimation();
            return;
        }

        if (idleAnimation != null && idleResourceType == cell.resourceType && idleTileType == cell.tileType)
        {
            return;
        }

        StopIdleAnimation();
        idleResourceType = cell.resourceType;
        idleTileType = cell.tileType;
        idleAnimation = StartCoroutine(PlayIdleFrames(frames, board.GetFrameSeconds(cell.resourceType)));
    }

    private IEnumerator PlayIdleFrames(Sprite[] frames, float frameSeconds)
    {
        var index = 0;
        while (true)
        {
            if (resourceRenderer != null && resourceRenderer.enabled)
            {
                resourceRenderer.sprite = frames[index % frames.Length];
            }

            index++;
            yield return new WaitForSeconds(frameSeconds);
        }
    }

    private void StopIdleAnimation()
    {
        if (idleAnimation != null)
        {
            StopCoroutine(idleAnimation);
            idleAnimation = null;
        }
    }

    private void AddRenderer(List<SpriteRenderer> renderers, SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.sprite != null && !renderers.Contains(spriteRenderer))
        {
            renderers.Add(spriteRenderer);
        }
    }

    private void AddChildRenderers(List<SpriteRenderer> renderers, Transform root)
    {
        if (root == null)
        {
            return;
        }

        var childRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < childRenderers.Length; i++)
        {
            AddRenderer(renderers, childRenderers[i]);
        }
    }

    private void HideChildRenderers(string childName)
    {
        var child = transform.Find(childName);
        if (child == null)
        {
            return;
        }

        var renderers = child.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
    }

    private void ClearChild(string childName)
    {
        var child = transform.Find(childName);
        if (child == null)
        {
            return;
        }

        HideChildRenderers(childName);

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
