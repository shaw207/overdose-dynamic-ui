using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>Handles vertical shelf input and free rotation of the separate central inspection model.</summary>
public sealed class AndyWeaponGesture : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
    IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private AndyWeaponStation previewOwner;
    [SerializeField] private AndyWeaponCarousel carousel;
    [SerializeField] private int bucketIndex = -1;

    private bool dragging, hasPressedWeapon, caughtSpin;
    private int pointerId, pressPointerId;
    private Weapon pressedWeapon;

    public void SetupPreview(AndyWeaponStation station)
    {
        previewOwner = station;
        carousel = null;
        bucketIndex = -1;
    }

    public void SetupCarousel(AndyWeaponCarousel rack, int slot = -1)
    {
        carousel = rack;
        previewOwner = null;
        bucketIndex = slot;
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || carousel == null) return;
        pressPointerId = data.pointerId;
        caughtSpin = carousel.CatchSpin();
        hasPressedWeapon = bucketIndex >= 0 && !caughtSpin;
        pressedWeapon = carousel.WeaponAtBucket(bucketIndex);
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (!caughtSpin || data.pointerId != pressPointerId || data.button != PointerEventData.InputButton.Left) return;
        caughtSpin = false;
        carousel.ReleaseCatch();
        data.eligibleForClick = false;
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (carousel == null || bucketIndex < 0 || data.button != PointerEventData.InputButton.Left) return;
        // Ignore a stale press if the inventory was explicitly rebound before release.
        bool sameItem = !hasPressedWeapon || (pressPointerId == data.pointerId && pressedWeapon == carousel.WeaponAtBucket(bucketIndex));
        hasPressedWeapon = false;
        pressedWeapon = null;
        if (sameItem && data.eligibleForClick && !data.dragging)
            carousel.SelectBucket(bucketIndex);
    }

    public void OnScroll(PointerEventData data)
    {
        if (carousel == null || Mathf.Approximately(data.scrollDelta.y, 0f)) return;
        // Input System reports its configured logical units per notch (6 by default).
        // Legacy PointerEventData reports notches; precision devices can supply fractions.
        float ticks = -data.scrollDelta.y;
        if (data is ExtendedPointerEventData && data.currentInputModule is InputSystemUIInputModule module &&
            module.scrollDeltaPerTick > 0f) ticks /= module.scrollDeltaPerTick;
        carousel.Scroll(ticks);
        data.Use();
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (dragging || data.button != PointerEventData.InputButton.Left) return;
        if (previewOwner != null)
        {
            if (!previewOwner.CanBrowseWeapons) return;
        }
        else if (carousel == null || !carousel.BeginShelfDrag()) return;
        dragging = true;
        pointerId = data.pointerId;
        hasPressedWeapon = false;
        pressedWeapon = null;
        data.eligibleForClick = false;
        if (previewOwner != null) previewOwner.SetPreviewDragging(true);
    }

    public void OnDrag(PointerEventData data)
    {
        if (!dragging || data.pointerId != pointerId) return;
        if (previewOwner != null)
        {
            if (previewOwner.CanBrowseWeapons) previewOwner.RotatePreview(data.delta);
        }
        else if (carousel != null) carousel.DragShelf(data.delta.y, data.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData data)
    {
        if (dragging && data.pointerId == pointerId) End(true);
    }

    private void End(bool commit)
    {
        if (!dragging) return;
        dragging = false;
        if (previewOwner != null) previewOwner.SetPreviewDragging(false);
        if (carousel != null) carousel.EndShelfDrag(commit);
    }

    public void CancelDrag()
    {
        if (caughtSpin && carousel != null) carousel.ReleaseCatch();
        caughtSpin = false;
        hasPressedWeapon = false;
        pressedWeapon = null;
        End(false);
    }

    private void OnDisable() => CancelDrag();
}
