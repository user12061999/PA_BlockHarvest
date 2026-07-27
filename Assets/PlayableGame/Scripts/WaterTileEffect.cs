using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WaterTileEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer up;
    [SerializeField] private SpriteRenderer right;
    [SerializeField] private SpriteRenderer down;
    [SerializeField] private SpriteRenderer left;
    [SerializeField] private float seconds = 0.35f;
    [SerializeField] private float pushDistance = 0.25f;

    private Coroutine routine;
    private Vector3 upStart;
    private Vector3 rightStart;
    private Vector3 downStart;
    private Vector3 leftStart;
    private bool cachedStarts;

    private void Awake()
    {
        CacheStarts();
        HideAll();
    }

    private void CacheStarts()
    {
        upStart = up != null ? up.transform.localPosition : Vector3.zero;
        rightStart = right != null ? right.transform.localPosition : Vector3.zero;
        downStart = down != null ? down.transform.localPosition : Vector3.zero;
        leftStart = left != null ? left.transform.localPosition : Vector3.zero;
        cachedStarts = true;
    }

    public void Play(Vector2Int direction)
    {
        if (!cachedStarts)
        {
            CacheStarts();
        }

        var renderer = GetRenderer(direction);
        if (renderer == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(PlayRoutine(renderer, GetPushDirection(direction)));
    }

    private IEnumerator PlayRoutine(SpriteRenderer renderer, Vector3 pushDirection)
    {
        HideAll();
        var start = renderer.transform.localPosition;
        var end = start + pushDirection * pushDistance;
        var color = renderer.color;
        renderer.enabled = true;

        var animator = renderer.GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play(0, 0, 0f);
        }

        var duration = Mathf.Max(0.01f, seconds);
        for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            renderer.transform.localPosition = Vector3.Lerp(start, end, t);
            yield return null;
        }

        renderer.transform.localPosition = start;
        renderer.color = color;
        renderer.enabled = false;
        routine = null;
    }

    private SpriteRenderer GetRenderer(Vector2Int direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x > 0 ? right : left;
        }

        return direction.y > 0 ? up : down;
    }

    private Vector3 GetPushDirection(Vector2Int direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x > 0 ? Vector3.right : Vector3.left;
        }

        return direction.y > 0 ? Vector3.up : Vector3.down;
    }

    private void HideAll()
    {
        SetVisible(up, false, upStart);
        SetVisible(right, false, rightStart);
        SetVisible(down, false, downStart);
        SetVisible(left, false, leftStart);
    }

    private void SetVisible(SpriteRenderer renderer, bool visible, Vector3 localPosition)
    {
        if (renderer != null)
        {
            renderer.transform.localPosition = localPosition;
            renderer.enabled = visible;
        }
    }
}
