using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

[DisallowMultipleComponent]
public sealed class TileView : MonoBehaviour
{
    [Header("Designed Tile References")]
    [SerializeField] private SpriteRenderer tileRenderer;
    [SerializeField] private SpriteRenderer resourceRenderer;
    [SerializeField] private TextMeshProUGUI resourceValueLabel;
    [Header("Resource Visual")]
    [SerializeField] private float resourceScale = 1.2f;

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

    public void PlayPulse()
    {
        if (resourceRenderer != null)
        {
            StartCoroutine(Pulse(resourceRenderer.transform));
        }
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

    public IEnumerator PlayClear(float seconds)
    {
        StopIdleAnimation();

        var renderers = new List<SpriteRenderer>();
        AddRenderer(renderers, tileRenderer);
        AddRenderer(renderers, resourceRenderer);
        AddChildRenderers(renderers, tilePrefabInstance != null ? tilePrefabInstance.transform : null);

        var resourceSprites = transform.Find("ResourceSprites");
        AddChildRenderers(renderers, resourceSprites);

        if (renderers.Count == 0)
        {
            yield break;
        }

        seconds = Mathf.Max(0.01f, seconds);
        var scales = new Vector3[renderers.Count];
        var colors = new Color[renderers.Count];

        for (var i = 0; i < renderers.Count; i++)
        {
            scales[i] = renderers[i].transform.localScale;
            colors[i] = renderers[i].color;
        }

        for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
        {
            var t = elapsed / seconds;
            for (var i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                var color = colors[i];
                color.a = Mathf.Lerp(colors[i].a, 0f, t);
                renderers[i].color = color;
                renderers[i].transform.localScale = Vector3.Lerp(scales[i], Vector3.zero, t);
            }

            yield return null;
        }

        for (var i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].color = colors[i];
            renderers[i].transform.localScale = scales[i];
        }

        HideChildRenderers("ResourceSprites");
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
            tileRenderer.color = board.GetTint(cell.tileType);
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

    private void UpdateResourceSprites()
    {
        ClearChild("ResourceSprites");
        ClearChild("ResourceCount");
        resourceSpritePositions.Clear();

        if (cell.resourceType == TileType.Empty)
        {
            resourceRenderer.transform.localPosition = Vector3.zero;
            resourceRenderer.transform.localScale = GetResourceScale();
            return;
        }

        if (cell.resourceType != TileType.Wheat && cell.resourceType != TileType.Flower)
        {
            resourceRenderer.transform.localPosition = Vector3.zero;
            resourceRenderer.transform.localScale = GetResourceScale();
            resourceSpritePositions.Add(Vector3.zero);
            return;
        }

        AddResourceSpritePositions(cell.resourceValue);
        resourceRenderer.transform.localPosition = resourceSpritePositions[0];
        resourceRenderer.transform.localScale = GetResourceScale();

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
            marker.transform.localPosition = resourceSpritePositions[i];
            marker.transform.localScale = GetResourceScale();

            var markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = board.GetTileSprite(cell.resourceType);
            markerRenderer.color = board.GetTint(cell.resourceType);
            markerRenderer.sortingLayerID = resourceRenderer.sortingLayerID;
            markerRenderer.sortingOrder = resourceRenderer.sortingOrder;
        }
    }

    private Vector3 GetResourceScale()
    {
        return Vector3.one * Mathf.Max(0.01f, resourceScale);
    }

    private void AddResourceSpritePositions(int count)
    {
        count = Mathf.Clamp(count, 1, 9);

        if (count == 1)
        {
            resourceSpritePositions.Add(new Vector3(0f, 0.06f, 0f));
            return;
        }

        var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
        var rows = Mathf.CeilToInt((float)count / columns);
        var spacingX = columns <= 1 ? 0f : 0.48f / (columns - 1);
        var spacingY = rows <= 1 ? 0f : 0.42f / (rows - 1);
        var startX = -spacingX * (columns - 1) * 0.5f;
        var startY = 0.22f;

        for (var i = 0; i < count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            resourceSpritePositions.Add(new Vector3(startX + column * spacingX, startY - row * spacingY, 0f));
        }
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
