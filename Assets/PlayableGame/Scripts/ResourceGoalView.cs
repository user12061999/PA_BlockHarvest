using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResourceGoalView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer basketRenderer;
    [SerializeField] private SpriteRenderer resourceRenderer;
    [SerializeField] private SpriteRenderer fillRenderer;
    [SerializeField] private Image fillImage;
    [SerializeField] private SpriteRenderer completeRenderer;
    [SerializeField] private TextMeshProUGUI valueLabel;
    [Header("Bounce Animation Settings")]
    [SerializeField] private float bounceScaleMultiplier = 1.3f;
    [SerializeField] private float bounceDuration = 0.22f;

    private Vector3 fillRendererBaseScale = Vector3.one;
    private Vector3 baseScale = Vector3.one;
    private Coroutine bounceRoutine;

    public bool IsComplete { get; private set; }
    public Vector3 TargetWorldPosition => resourceRenderer != null ? resourceRenderer.transform.position : transform.position;
    public Sprite ResourceSprite => resourceRenderer != null ? resourceRenderer.sprite : null;

    public void Initialize()
    {
        baseScale = transform.localScale;

        if (basketRenderer == null)
        {
            var basket = transform.Find("Basket");
            basketRenderer = basket != null ? basket.GetComponent<SpriteRenderer>() : null;
        }

        if (resourceRenderer == null)
        {
            var resource = transform.Find("Resource");
            resourceRenderer = resource != null ? resource.GetComponent<SpriteRenderer>() : null;
        }

        if (fillRenderer == null || fillImage == null)
        {
            var fill = transform.Find("Fill");
            if (fill != null)
            {
                if (fillRenderer == null) fillRenderer = fill.GetComponent<SpriteRenderer>();
                if (fillImage == null) fillImage = fill.GetComponent<Image>();
            }
        }

        if (fillRenderer != null)
        {
            fillRendererBaseScale = fillRenderer.transform.localScale;
            if (fillRenderer.sprite == null && resourceRenderer != null)
            {
                fillRenderer.sprite = resourceRenderer.sprite;
                fillRenderer.color = resourceRenderer.color;
            }
        }

        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = 0;
            if (fillImage.sprite == null && resourceRenderer != null)
            {
                fillImage.sprite = resourceRenderer.sprite;
                fillImage.color = resourceRenderer.color;
            }
        }

        if (basketRenderer != null) basketRenderer.enabled = true;
        if (resourceRenderer != null) resourceRenderer.enabled = true;

        if (valueLabel == null)
        {
            valueLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (completeRenderer == null)
        {
            var check = transform.Find("Check");
            completeRenderer = check != null ? check.GetComponent<SpriteRenderer>() : null;
        }

        IsComplete = false;
        SetFill(0f);
        if (completeRenderer != null) completeRenderer.gameObject.SetActive(false);
        if (valueLabel != null) valueLabel.text = string.Empty;
    }

    public void SetValue(int current, int goal)
    {
        var wasComplete = IsComplete;
        IsComplete = goal <= 0 || current >= goal;
        var progress = goal <= 0 ? 1f : Mathf.Clamp01((float)current / goal);
        var remaining = Mathf.Max(0, goal - current);

        if (valueLabel != null)
        {
            valueLabel.text = remaining.ToString();
        }

        SetFill(progress);

        if (completeRenderer != null)
        {
            completeRenderer.gameObject.SetActive(IsComplete);
        }

        // --- PHÁT TIẾNG KHI VỪA HOÀN THÀNH ĐỦ MỤC TIÊU CỦA GIỎ ---
        if (!wasComplete && IsComplete)
        {
            if (AudioManager.ins != null)
            {
                AudioManager.ins.PlayGoalCompleted();
            }
        }
        // -------------------------------------------------------
    }

    /// <summary>
    /// Kích hoạt hiệu ứng nảy khi tài nguyên bay tới giỏ mục tiêu
    /// </summary>
    public void PlayBounce()
    {
        if (!gameObject.activeInHierarchy) return;

        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
        }
        bounceRoutine = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        var peakScale = baseScale * bounceScaleMultiplier;
        var halfDuration = Mathf.Max(0.01f, bounceDuration * 0.5f);

        // Nở to ra
        for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(baseScale, peakScale, Mathf.SmoothStep(0f, 1f, elapsed / halfDuration));
            yield return null;
        }

        // Thu về kích thước ban đầu
        for (var elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(peakScale, baseScale, Mathf.SmoothStep(0f, 1f, elapsed / halfDuration));
            yield return null;
        }

        transform.localScale = baseScale;
        bounceRoutine = null;
    }

    private void SetFill(float progress)
    {
        if (fillImage != null)
        {
            fillImage.fillAmount = progress;
            fillImage.gameObject.SetActive(progress > 0f);
        }

        if (fillRenderer != null)
        {
            var scale = fillRendererBaseScale;
            scale.y *= progress;
            fillRenderer.transform.localScale = scale;
            fillRenderer.enabled = progress > 0f;
        }
    }

    public void Pickup()
    {
        gameObject.SetActive(false);
    }
}