using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResourceGoalView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer basketRenderer;
    [SerializeField] private SpriteRenderer resourceRenderer;
    [SerializeField] private SpriteRenderer completeRenderer;
    [SerializeField] private TextMeshProUGUI valueLabel;

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

        SetValue(0, 0);
    }

    public void SetValue(int current, int goal)
    {
        IsComplete = goal <= 0 || current >= goal;

        if (valueLabel != null)
        {
            valueLabel.text = current + "/" + goal;
        }

        if (completeRenderer != null)
        {
            completeRenderer.gameObject.SetActive(IsComplete);
        }
    }

    public void Pickup()
    {
        gameObject.SetActive(false);
    }
}
