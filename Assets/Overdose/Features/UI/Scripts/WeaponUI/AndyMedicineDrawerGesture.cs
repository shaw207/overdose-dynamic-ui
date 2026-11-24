using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>A world-space pointer target on one drawer, a column's background, or the inspection model.</summary>
public sealed class AndyMedicineDrawerGesture : MonoBehaviour, IPointerClickHandler, IScrollHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Transform slide;
    [SerializeField] private Transform lift;
    [SerializeField] private Transform[] tierSurfaces;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Transform frontPanel, pullHandle;
    [SerializeField] private BoxCollider frontHit;
    [SerializeField] private AndyMedicineDrawers owner;
    [SerializeField] private bool preview;
    private MedicineItem item;
    private MedicineClass? category;
    private Collider[] hits;
    private bool dragging, chooseAxis, pulling;
    private double began;
    private int pointerId;
    private Vector3 panelSize, panelRest, handleRest, labelRest, hitRest, hitSize;

    internal Transform Slide => slide;
    internal Transform Lift => lift;
    internal bool HasThreeTiers => tierSurfaces != null && tierSurfaces.Length == 3 &&
        tierSurfaces[0] && tierSurfaces[1] && tierSurfaces[2];
    internal Transform Tier(int index) => tierSurfaces[index];
    public MedicineItem Item => item;

    public void SetupPreview(AndyMedicineDrawers drawers)
    {
        owner = drawers;
        preview = true;
        item = null;
    }

    public void SetupColumn(AndyMedicineDrawers drawers)
    {
        owner = drawers;
        preview = false;
        item = null;
    }

    internal void SetupDrawer(AndyMedicineDrawers drawers, MedicineClass medicineClass)
    {
        SetupColumn(drawers);
        category = medicineClass;
        if (label != null)
        {
            label.text = medicineClass.ToString();
            label.raycastTarget = false;
        }
        // Also supports already-authored three-tier cabinets before their references are resaved.
        if (!frontPanel) frontPanel = slide.Find("Box");
        if (!pullHandle) pullHandle = slide.Find("Handle");
        if (!frontHit) frontHit = slide.Find("Pointer")?.GetComponent<BoxCollider>();
        if (!frontPanel || !pullHandle || !frontHit || !label) return;
        panelRest = frontPanel.localPosition;
        panelSize = frontPanel.localScale;
        handleRest = pullHandle.localPosition;
        labelRest = label.transform.localPosition;
        hitRest = frontHit.transform.localPosition;
        hitSize = frontHit.size;
    }

    internal void ApplyFront(float progress)
    {
        if (!frontPanel || !pullHandle || !frontHit || !label) return;
        // The shallow open lip stays below the first bottle. Keep the handle and lettering at full scale.
        float height = Mathf.Lerp(1f, .34f, progress);
        frontPanel.localScale = new Vector3(panelSize.x, panelSize.y * height, panelSize.z);
        frontPanel.localPosition = panelRest + Vector3.down * (.142f * progress);
        pullHandle.localPosition = handleRest + Vector3.down * (.057f * progress);
        label.transform.localPosition = labelRest + Vector3.down * (.184f * progress);
        frontHit.size = new Vector3(hitSize.x, hitSize.y * height, hitSize.z);
        frontHit.transform.localPosition = hitRest + Vector3.down * (.142f * progress);
    }

    internal void SetupBottle(AndyMedicineDrawers drawers, MedicineItem medicine)
    {
        SetupColumn(drawers);
        item = medicine;
        hits = GetComponents<Collider>();
        AndyMedicineHover.BindBottle(this, drawers, medicine);
    }

    internal void EnableHits(bool enabled)
    {
        bool pickable = enabled && (item == null || owner.CanPickBottle(item));
        if (hits == null) hits = GetComponentsInChildren<Collider>(true);
        foreach (var hit in hits)
            if (hit != null && hit.gameObject.layer == 2 && hit.GetComponentInParent<AndyMedicineDrawerGesture>(true) == this)
                hit.enabled = pickable;
        if (item != null && !pickable) AndyMedicineHover.Clear(this);
        if (!enabled) EndDrag();
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (owner == null || !owner.CanInteract || data.button != PointerEventData.InputButton.Left ||
            !data.eligibleForClick || data.dragging) return;
        if (item != null && owner.CanPickBottle(item)) owner.ToggleStand(this);
        else if (category.HasValue) owner.ToggleDrawer(category.Value);
    }

    public void OnScroll(PointerEventData data)
    {
        if (owner == null || !owner.CanInteract || preview || Mathf.Approximately(data.scrollDelta.y, 0f)) return;
        float ticks = -data.scrollDelta.y;
        if (data is ExtendedPointerEventData && data.currentInputModule is InputSystemUIInputModule module &&
            module.scrollDeltaPerTick > 0f) ticks /= module.scrollDeltaPerTick;
        owner.ScrollBy(ticks);
        data.Use();
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (dragging || owner == null || !owner.CanInteract || data.button != PointerEventData.InputButton.Left) return;
        if (preview)
        {
            if (owner.CurrentModel == null) return;
        }
        else if (category.HasValue)
        {
            chooseAxis = true;
            began = Time.unscaledTimeAsDouble;
        }
        else if (!owner.BeginDrag(data.pointerId)) return;
        dragging = true;
        if (item != null) AndyMedicineHover.Clear(this);
        pointerId = data.pointerId;
        data.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData data)
    {
        if (!dragging || data.pointerId != pointerId || owner == null || !owner.CanInteract) return;
        if (chooseAxis)
        {
            if (data.delta == Vector2.zero) return;
            chooseAxis = false;
            pulling = Mathf.Abs(data.delta.x) > Mathf.Abs(data.delta.y);
            bool accepted = pulling ? owner.BeginPull(category.Value, pointerId, began) : owner.BeginDrag(pointerId);
            if (!accepted) { dragging = pulling = false; return; }
        }
        if (preview) owner.RotatePreview(data.delta);
        else if (pulling) owner.Pull(pointerId, data.delta.x);
        else owner.Drag(data.pointerId, data.delta.y);
    }

    public void OnEndDrag(PointerEventData data)
    {
        if (dragging && data.pointerId == pointerId) EndDrag();
    }

    private void EndDrag()
    {
        if (!dragging) return;
        dragging = chooseAxis = pulling = false;
        if (!preview && owner != null) owner.EndDrag(pointerId);
    }

    private void OnDisable() => EndDrag();
}
