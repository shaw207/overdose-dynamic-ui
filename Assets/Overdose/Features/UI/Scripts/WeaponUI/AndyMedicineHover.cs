using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The cabinet supplies a PhysicsRaycaster hit and the vending machine a GraphicRaycaster hit.
[DisallowMultipleComponent]
public sealed class AndyMedicineHover : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler,
    IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private AndyMedicineTooltip tooltip;
    [SerializeField] private MedicineItem medicine;
    private AndyMedicineDrawers drawers;
    [SerializeField] private Camera displayCamera;
    private bool pointerHere;
    public MedicineItem Item => medicine;

    public static void BindBottle(AndyMedicineDrawerGesture gesture, AndyMedicineDrawers cabinet, MedicineItem item)
    {
        var station = cabinet.GetComponentInParent<AndyWeaponStation>(true);
        var tip = station ? station.GetComponentInChildren<AndyMedicineTooltip>(true) : null;
        if (!tip) return;
        if (!gesture.TryGetComponent(out AndyMedicineHover hover)) hover=gesture.gameObject.AddComponent<AndyMedicineHover>();
        hover.tooltip=tip;
        hover.medicine=item;
        hover.drawers=cabinet;
        hover.displayCamera=station.StationCamera;
    }

    public static void Clear(AndyMedicineDrawerGesture gesture)
    {
        if (gesture.TryGetComponent(out AndyMedicineHover hover)) hover.Release();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (drawers && (!drawers.CanInteract || !drawers.CanPickBottle(medicine))) return;
        pointerHere=true;
        ShowPointer(e);
    }

    public void OnPointerMove(PointerEventData e)
    {
        if (pointerHere && !e.dragging) ShowPointer(e);
    }

    public void OnPointerExit(PointerEventData e) => Release();

    public void OnSelect(BaseEventData e)
    {
        if (e is PointerEventData || !tooltip || !(transform is RectTransform rect)) return;
        var canvas=GetComponentInParent<Canvas>();
        if (canvas && canvas.TryGetComponent(out GraphicRaycaster raycaster) && raycaster.isActiveAndEnabled)
            tooltip.Show(this,canvas.worldCamera,rect.TransformPoint(rect.rect.center));
    }

    private void ShowPointer(PointerEventData data)
    {
        if (!tooltip) return;
        if (!displayCamera && transform is RectTransform) displayCamera=GetComponentInParent<Canvas>()?.worldCamera;
        if (!displayCamera) return;
        Vector3 point=data.pointerCurrentRaycast.worldPosition;
        if (transform is RectTransform rect)
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rect,data.position,displayCamera,out point);
        tooltip.Show(this,displayCamera,point);
    }

    public void OnDeselect(BaseEventData e)
    {
        if (!pointerHere && tooltip) tooltip.Release(this);
    }

    private void OnDisable() => Release();

    private void Release()
    {
        pointerHere=false;
        if (tooltip) tooltip.Release(this);
    }
}
