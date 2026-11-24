using TMPro;
using UnityEngine;

public sealed class AndyMedicineTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text label;
    private AndyMedicineHover owner;
    private Camera displayCamera;
    private Vector3 localPoint;

    public MedicineItem VisibleItem => card != null && card.gameObject.activeInHierarchy && owner ? owner.Item : null;

    private void OnEnable() => Canvas.willRenderCanvases += PositionCard;
    private void OnDisable()
    {
        Canvas.willRenderCanvases -= PositionCard;
        Hide();
    }

    internal void Show(AndyMedicineHover source, Camera camera, Vector3 worldPoint)
    {
        if (!isActiveAndEnabled || !source || !source.Item || !camera || !card || !label) return;
        owner = source;
        displayCamera = camera;
        localPoint = source.transform.InverseTransformPoint(worldPoint);
        label.text = source.Item.DisplayName;
        card.gameObject.SetActive(true);
        PositionCard();
    }

    private void PositionCard()
    {
        if (!owner || !displayCamera || !card || !card.gameObject.activeSelf) return;
        var parent = (RectTransform)card.parent;
        Rect area = parent.rect;
        Vector2 normalizedPosition = displayCamera.WorldToViewportPoint(owner.transform.TransformPoint(localPoint));
        float width = Mathf.Min(label.GetPreferredValues(label.text).x + 30f, area.width);
        float height = Mathf.Min(48f, area.height);
        Vector2 half = new Vector2(width,height)*.5f;
        Vector2 pointer = Rect.NormalizedToPoint(area, normalizedPosition);
        bool left = normalizedPosition.x > .6f;
        float x = pointer.x + (left ? -1 : 1) * (half.x + 16f);
        float y = pointer.y + half.y + 13f;
        if (y + half.y > area.yMax) y = pointer.y - half.y - 13f;
        card.sizeDelta = half*2f;
        card.anchoredPosition = new Vector2(Mathf.Clamp(x,area.xMin+half.x,area.xMax-half.x),
            Mathf.Clamp(y,area.yMin+half.y,area.yMax-half.y)) - area.center;
    }

    internal void Release(AndyMedicineHover source)
    {
        if (owner == source) Hide();
    }

    public void Hide()
    {
        owner = null;
        if (card) card.gameObject.SetActive(false);
    }
}
