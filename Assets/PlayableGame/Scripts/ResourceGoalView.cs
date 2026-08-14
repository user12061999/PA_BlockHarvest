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

    private Vector3 fillRendererBaseScale = Vector3.one;

    public bool IsComplete { get; private set; }
    public Vector3 TargetWorldPosition => resourceRenderer != null ? resourceRenderer.transform.position : transform.position;
    public Sprite ResourceSprite => resourceRenderer != null ? resourceRenderer.sprite : null;

    public void Initialize()
    {
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
                if (fillRenderer == null)
                {
                    fillRenderer = fill.GetComponent<SpriteRenderer>();
                }

                if (fillImage == null)
                {
                    fillImage = fill.GetComponent<Image>();
                }
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

        if (basketRenderer != null)
        {
            basketRenderer.enabled = true;
        }

        if (resourceRenderer != null)
        {
            resourceRenderer.enabled = true;
        }

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
        if (completeRenderer != null)
        {
            completeRenderer.gameObject.SetActive(false);
        }

        if (valueLabel != null)
        {
            valueLabel.text = string.Empty;
        }
    }

    public void SetValue(int current, int goal)
    {
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
