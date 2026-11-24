using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A three-row cabinet opens by medicine class. Individual bottles retain their own identities.</summary>
public sealed class AndyMedicineDrawers : MonoBehaviour
{
    private const int DisplayLayer = 30, PointerLayer = 2;
    private const int VisibleRows = 3, TierCount = 3;
    private const float RowSpacing = .82f * 1.08f, HiddenRowDistance = 2.86f;
    private const float Extension = .88f, LiftTravel = .285f;
    private const float TrayRise = .12f;
    private const float DrawerSmoothTime = .115f;
    // In the final braking segment the tiers have risen far enough for inspection and pointer hits.
    private const float AccessibleExtension = .90f;
    private const float ModelHeight = .94f, BottleHeight = .22f;

    [Serializable]
    private struct TierPlacement
    {
        public MedicineItem medicine;
        [Range(0, TierCount - 1)] public int tier;
    }

    [SerializeField] private Transform leftColumn, previewPivot;
    [SerializeField] private AndyMedicineDrawerGesture drawerTemplate;
    [Tooltip("Optional shelf for the single medicine displayed in each category drawer.")]
    [SerializeField] private TierPlacement[] tierPlacements = Array.Empty<TierPlacement>();

    private sealed class Drawer
    {
        public MedicineClass category;
        public AndyMedicineDrawerGesture target;
        public Vector3 closed, lowered;
        public float amount, speed, peakOpeningSpeed;
        public Bottle displayed;
        public readonly List<Bottle> contents = new();
    }

    private sealed class Bottle
    {
        public MedicineItem item;
        public Drawer drawer;
        public int tier;
        public AndyMedicineDrawerGesture stand;
    }

    private readonly List<Drawer> rows = new();
    private readonly Dictionary<MedicineItem, Bottle> bottles = new();
    private readonly Dictionary<MedicineClass, Drawer> classes = new();
    private AndyMedicineDrawerGesture[] gestures = Array.Empty<AndyMedicineDrawerGesture>();
    private Camera stationCamera;
    private Drawer requested, pulled;
    private Bottle pending;
    private float jumpTime, jumpGravity, jumpApex;
    private float? pendingOpeningSpeed;
    private double pullTime;
    private float pullOrigin, pullSampleDuration, lowestPullAmount;
    private bool open, inputEnabled;
    private int? dragPointer;
    private float destination, scrollVelocity, wheel, lastOffset = float.NaN, lastHeight;
    private float yaw, pitch;
    private int MaxOffset => Mathf.Max(0, rows.Count - VisibleRows);

    public event Action<MedicineItem> SelectionChanged;
    public MedicineItem Selected { get; private set; }
    public GameObject CurrentModel { get; private set; }
    public Transform SelectedStand { get; private set; }
    public Transform JumpingStand => pending != null && jumpApex > 0f ? pending.stand.transform : null;
    public float LastLaunchSpeed { get; private set; }
    public float JumpHeightMeters { get; private set; }
    public MedicineClass? OpenClass
    {
        get
        {
            if (requested != null && requested.amount > 0f) return requested.category;
            foreach (var row in rows) if (row.amount > 0f) return row.category;
            return null;
        }
    }
    public bool CanInteract => open && inputEnabled && isActiveAndEnabled;
    public float ColumnOffset { get; private set; }
    public Transform DrawerFor(MedicineItem item) => Lookup(item)?.drawer.target.transform;
    public Transform ItemStandFor(MedicineItem item) => Lookup(item)?.stand.transform;
    public int TierFor(MedicineItem item) => Lookup(item)?.tier ?? -1;
    public float OpenAmount(MedicineItem item) => Lookup(item)?.drawer.amount ?? 0f;
    public float LiftAmount(MedicineItem item) => LiftProgress(OpenAmount(item));
    public float LastOpeningSpeed(MedicineItem item) => Lookup(item)?.drawer.peakOpeningSpeed ?? 0f;
    public int ShelfCount(MedicineItem item, int tier)
    {
        var bottle = Lookup(item);
        if (bottle == null) return 0;
        return bottle.drawer.displayed?.tier == tier ? 1 : 0;
    }
    public float JumpAmount(MedicineItem item) => pending != null && pending.item == item && jumpApex > 0f ?
        Mathf.Clamp01(JumpHeightMeters / jumpApex) : 0f;
    private Bottle Lookup(MedicineItem item) => item != null && bottles.TryGetValue(item, out var bottle) ? bottle : null;

    public void Bind(Camera camera, IReadOnlyList<MedicineItem> catalogue)
    {
        stationCamera = camera;
        if (camera == null || leftColumn == null || previewPivot == null || drawerTemplate == null ||
            drawerTemplate.Slide == null || drawerTemplate.Lift == null || !drawerTemplate.HasThreeTiers)
        {
            Debug.LogError("[AndyMedicineDrawers] Missing cabinet, three-tier lift, preview pivot or camera reference.", this);
            return;
        }

        MedicineItem previous = Selected;
        ResetCabinet();
        foreach (var row in rows)
        {
            row.target.gameObject.SetActive(false);
            Destroy(row.target.gameObject);
        }
        rows.Clear();
        classes.Clear();
        bottles.Clear();
        dragPointer = null;
        ColumnOffset = destination = scrollVelocity = wheel = 0f;
        lastOffset = float.NaN;

        drawerTemplate.gameObject.SetActive(false);
        if (catalogue != null)
        {
            foreach (var item in catalogue)
            {
                if (!item || !item.AdvancedProfile || !item.ConsumptionModel || bottles.ContainsKey(item)) continue;
                if (!classes.TryGetValue(item.MedicineClass, out var row)) row = AddDrawer(item.MedicineClass);
                int tier = item.AdvancedProfile.Kind switch
                {
                    AdvancedMedicineKind.SubZeroHold => 2,
                    AdvancedMedicineKind.VampireDrain => 1,
                    _ => 0
                };
                foreach (var placement in tierPlacements)
                    if (placement.medicine == item) { tier = Mathf.Clamp(placement.tier, 0, TierCount - 1); break; }
                var bottle = new Bottle { item = item, drawer = row, tier = tier };
                row.contents.Add(bottle);
                bottles.Add(item, bottle);
            }
        }
        foreach (var row in rows) BuildContents(row);
        gestures = GetComponentsInChildren<AndyMedicineDrawerGesture>(true);
        ApplyHits();
        Layout(0f);
        if (previous && CanInteract) Select(previous);
    }

    private Drawer AddDrawer(MedicineClass category)
    {
        var target = Instantiate(drawerTemplate, leftColumn, false);
        target.name = category + " drawer";
        target.transform.localRotation = Quaternion.identity;
        target.SetupDrawer(this, category);
        var row = new Drawer { category = category, target = target, closed = target.Slide.localPosition,
            lowered = target.Lift.localPosition };
        rows.Add(row);
        classes.Add(category, row);
        target.Lift.gameObject.SetActive(false);
        target.gameObject.SetActive(true);
        return row;
    }

    private void BuildContents(Drawer row)
    {
        foreach (var bottle in row.contents)
        {
            int tier = bottle.tier;
            var stand = new GameObject(bottle.item.DisplayName + " shelf " + (tier + 1));
            stand.layer = PointerLayer;
            stand.transform.SetParent(row.target.Tier(tier), false);
            CreateVisual(stand.transform, bottle.item, BottleHeight, true);
            var hit = stand.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = Vector3.up * (BottleHeight * .50f);
            hit.size = new Vector3(BottleHeight * 1.15f, BottleHeight * 1.08f, BottleHeight * .68f);
            bottle.stand = stand.AddComponent<AndyMedicineDrawerGesture>();
            bottle.stand.SetupBottle(this, bottle.item);
            stand.SetActive(bottle == row.contents[0]);
        }
        row.displayed = row.contents[0];
    }

    private void ShowShelfBottle(Bottle bottle)
    {
        var row = bottle.drawer;
        if (row.displayed == bottle) return;
        row.displayed.stand.EnableHits(false);
        row.displayed.stand.gameObject.SetActive(false);
        row.displayed = bottle;
        bottle.stand.gameObject.SetActive(true);
        bottle.stand.EnableHits(CanInteract);
    }

    public void SetOpen(bool value)
    {
        open = value;
        SetInputEnabled(value);
        if (open) Layout(0f);
        else ResetCabinet();
    }

    private void ResetCabinet()
    {
        ClearSelection();
        requested = pulled = null;
        foreach (var row in rows)
        {
            row.amount = row.speed = row.peakOpeningSpeed = 0f;
            ShowShelfBottle(row.contents[0]);
            row.target.Slide.localPosition = row.closed;
            row.target.Lift.localPosition = row.lowered;
            row.target.ApplyFront(0f);
            row.target.Lift.gameObject.SetActive(false);
        }
        Physics.SyncTransforms();
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
        if (!CanInteract)
        {
            dragPointer = null;
            pulled = null;
            wheel = scrollVelocity = 0f;
            destination = Mathf.Clamp(Mathf.Round(ColumnOffset), 0f, MaxOffset);
        }
        ApplyHits();
    }

    private void ApplyHits()
    {
        foreach (var target in gestures)
            if (target != null && target != drawerTemplate)
                target.EnableHits(CanInteract);
    }

    internal void ToggleDrawer(MedicineClass category)
    {
        if (!CanInteract || !classes.TryGetValue(category, out var row)) return;
        if (requested == row)
        {
            requested = null;
            ClearSelection();
            ApplyHits();
            return;
        }
        RequestDrawer(row);
        ClearSelection();
    }

    internal void ToggleStand(AndyMedicineDrawerGesture target)
    {
        var bottle = Lookup(target.Item);
        if (!CanInteract || bottle == null || bottle.stand != target) return;
        if (SelectedStand == target.transform || pending == bottle) ClearSelection();
        else SelectBottle(bottle, 0f);
    }

    public void Toggle(MedicineItem item)
    {
        var bottle = Lookup(item);
        if (bottle != null) ToggleStand(bottle.stand);
    }

    public void Select(MedicineItem item)
    {
        var bottle = Lookup(item);
        if (bottle == null) return;
        bool opening = requested != bottle.drawer || bottle.drawer.amount < AccessibleExtension;
        SelectBottle(bottle, opening ? (float?)null : 0f);
    }

    private void SelectBottle(Bottle bottle, float? openingSpeed)
    {
        if (!CanInteract || bottle == null) return;
        RequestDrawer(bottle.drawer);
        if (SelectedStand == bottle.stand.transform || pending == bottle) return;
        ClearSelection();
        ShowShelfBottle(bottle);
        pending = bottle;
        pendingOpeningSpeed = openingSpeed;
    }

    private void RequestDrawer(Drawer drawer)
    {
        bool changed = requested != drawer;
        if (changed) drawer.peakOpeningSpeed = 0f;
        pulled = null;
        requested = drawer;
        int index = rows.IndexOf(drawer);
        dragPointer = null;
        destination = Mathf.Clamp(Mathf.Round(destination), Mathf.Max(0, index - VisibleRows + 1),
            Mathf.Min(index, MaxOffset));
        if (changed) ApplyHits();
    }

    public void ClearSelection()
    {
        if (pending?.stand != null) pending.stand.transform.localPosition = Vector3.zero;
        pending = null;
        pendingOpeningSpeed = null;
        jumpTime = jumpApex = JumpHeightMeters = 0f;
        SelectedStand = null;
        bool notify = Selected != null;
        Selected = null;
        ClearPreview();
        if (notify) SelectionChanged?.Invoke(null);
    }

    public void ScrollColumn(int steps)
    {
        if (!CanInteract || dragPointer.HasValue || steps == 0) return;
        destination = Mathf.Clamp(Mathf.Round(destination) + steps, 0f, MaxOffset);
        CloseIfScrollingAway();
    }

    internal void ScrollBy(float ticks)
    {
        if (!CanInteract || dragPointer.HasValue) return;
        if (wheel * ticks < 0f) wheel = 0f;
        wheel += ticks;
        int steps = (int)wheel;
        wheel -= steps;
        ScrollColumn(steps);
    }

    internal bool BeginDrag(int pointer)
    {
        if (!CanInteract || dragPointer.HasValue || MaxOffset == 0) return false;
        dragPointer = pointer;
        scrollVelocity = wheel = 0f;
        return true;
    }

    internal void Drag(int pointer, float pixels)
    {
        if (!CanInteract || dragPointer != pointer || pulled != null || stationCamera == null) return;
        float bottom = stationCamera.WorldToScreenPoint(leftColumn.position).y;
        float top = stationCamera.WorldToScreenPoint(leftColumn.TransformPoint(Vector3.up * RowSpacing)).y;
        ColumnOffset = Mathf.Clamp(ColumnOffset + pixels / Mathf.Max(1f, Mathf.Abs(top - bottom)), 0f, MaxOffset);
        destination = ColumnOffset;
        CloseIfScrollingAway();
        Layout(0f);
    }

    internal void EndDrag(int pointer)
    {
        if (dragPointer != pointer) return;
        dragPointer = null;
        if (pulled != null)
        {
            var row = pulled;
            pulled = null;
            if (row.amount < .5f)
            {
                requested = null;
                ClearSelection();
            }
            else if (row.amount - lowestPullAmount > .12f)
            {
                SelectBottle(row.displayed, row.peakOpeningSpeed);
            }
            ApplyHits();
        }
        destination = Mathf.Round(ColumnOffset);
    }

    internal bool BeginPull(MedicineClass category, int pointer, double began)
    {
        if (!CanInteract || dragPointer.HasValue || !classes.TryGetValue(category, out var row)) return false;
        RequestDrawer(row);
        ClearSelection();
        pulled = row;
        dragPointer = pointer;
        row.speed = row.peakOpeningSpeed = 0f;
        lowestPullAmount = pullOrigin = row.amount;
        pullTime = began;
        pullSampleDuration = Mathf.Max(.0001f, Time.unscaledDeltaTime);
        ApplyHits();
        return true;
    }

    internal void Pull(int pointer, float pixels)
    {
        if (!CanInteract || !stationCamera || pulled == null || dragPointer != pointer || pixels == 0f) return;
        var root = pulled.target.transform;
        float span = stationCamera.WorldToScreenPoint(root.TransformPoint(Vector3.back * Extension)).x -
            stationCamera.WorldToScreenPoint(root.position).x;
        if (Mathf.Abs(span) < 1f) span = Mathf.Max(1f, stationCamera.pixelHeight * .1f);
        float before = pulled.amount;
        float value = Mathf.Clamp01(before + pixels / span);
        lowestPullAmount = Mathf.Min(lowestPullAmount,value);
        double now = Time.unscaledTimeAsDouble;
        if (now > pullTime)
        {
            pullSampleDuration = Mathf.Max(.0001f,(float)(now - pullTime));
            pullOrigin = before;
            pullTime = now;
        }
        pulled.peakOpeningSpeed = Mathf.Max(pulled.peakOpeningSpeed, (value - pullOrigin) * Extension / pullSampleDuration);
        if (value == before) return;
        Pose(pulled, value);
        Physics.SyncTransforms();
    }

    private void CloseIfScrollingAway()
    {
        if (requested == null) return;
        int first = Mathf.RoundToInt(destination);
        int index = rows.IndexOf(requested);
        if (index >= first && index < first + VisibleRows) return;
        requested = null;
        ClearSelection();
        ApplyHits();
    }

    public void RotatePreview(Vector2 delta)
    {
        if (!CanInteract || CurrentModel == null || stationCamera == null) return;
        float sensitivity = 240f / Mathf.Max(1, stationCamera.pixelHeight);
        yaw -= delta.x * sensitivity;
        pitch = Mathf.Clamp(pitch + delta.y * sensitivity, -70f, 70f);
        previewPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void Update()
    {
        if (CanInteract) Layout(Time.unscaledDeltaTime);
    }

    private void Layout(float delta)
    {
        if (!dragPointer.HasValue && ColumnOffset != destination && delta > 0f)
        {
            ColumnOffset = Mathf.SmoothDamp(ColumnOffset, destination, ref scrollVelocity, .15f, float.PositiveInfinity, delta);
            if (Mathf.Abs(ColumnOffset - destination) < .002f)
            {
                ColumnOffset = destination;
                scrollVelocity = 0f;
            }
        }
        float height = stationCamera != null && stationCamera.orthographic ? stationCamera.orthographicSize * .5f : 1f;
        bool shifted = lastOffset != ColumnOffset || lastHeight != height;
        bool moved = shifted;
        float extraTravel = Mathf.Max(0f, HiddenRowDistance * height - 1.5f * RowSpacing);
        for (int i = 0; i < rows.Count; i++)
        {
            var drawer = rows[i];
            if (shifted)
            {
                float row = 1f - i + ColumnOffset;
                // The departing drawer fully clears the camera by mid-step, before the next enters.
                float exit = Mathf.SmoothStep(0f, 1f, 2f * (Mathf.Abs(row) - 1f));
                float y = row * RowSpacing + Mathf.Sign(row) * exit * extraTravel;
                drawer.target.transform.localPosition = Vector3.up * y;
            }
        }
        moved |= AnimateDrawer(delta);
        lastOffset = ColumnOffset;
        lastHeight = height;
        if (moved) Physics.SyncTransforms();
    }

    private bool AnimateDrawer(float delta)
    {
        if (delta <= 0f) return false;
        bool moved = false;
        bool reachedWindow = !dragPointer.HasValue && Mathf.Abs(ColumnOffset - destination) <= .002f;
        foreach (var row in rows)
        {
            // A cabinet already open can follow a small manual scroll without dipping or restarting.
            float goal = row == requested && (reachedWindow || row.amount > 0f) ? 1f : 0f;
            if (row == pulled || row.amount == goal) continue;
            float value = Mathf.SmoothDamp(row.amount, goal, ref row.speed, DrawerSmoothTime, float.PositiveInfinity, delta);
            if (Mathf.Abs(value - goal) < .002f) { value = goal; row.speed = 0f; }
            row.peakOpeningSpeed = Mathf.Max(row.peakOpeningSpeed, (value - row.amount) * Extension / delta);
            bool entersReach = row == requested && row.amount < AccessibleExtension && value >= AccessibleExtension;
            Pose(row, value);
            if (entersReach) ApplyHits();
            moved = true;
        }
        if (pending == null || pending.drawer != requested || pulled != null || requested.amount < AccessibleExtension) return moved;
        if (jumpApex == 0f)
        {
            // Use the measured outward speed of this drawer's stroke. The analytic trajectory
            // follows gravity even while the inventory UI pauses the game's physics clock.
            float speed = pendingOpeningSpeed ?? requested.peakOpeningSpeed;
            LastLaunchSpeed = Mathf.Lerp(.88f, 1.72f, Mathf.InverseLerp(.35f, 7f, speed));
            jumpGravity = Mathf.Max(1f, -Vector3.Dot(Physics.gravity, pending.stand.transform.up));
            jumpApex = LastLaunchSpeed * LastLaunchSpeed / (2f * jumpGravity);
        }
        jumpTime += delta;
        JumpHeightMeters = Mathf.Max(0f, (LastLaunchSpeed - .5f * jumpGravity * jumpTime) * jumpTime);
        pending.stand.transform.localPosition = Vector3.up * JumpHeightMeters;
        if (JumpHeightMeters <= 0f)
        {
            pending.stand.transform.localPosition = Vector3.zero;
            Selected = pending.item;
            SelectedStand = pending.stand.transform;
            pending = null;
            jumpTime = jumpApex = 0f;
            yaw = -12f;
            pitch = 0f;
            previewPivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            CurrentModel = CreateVisual(previewPivot, Selected, ModelHeight, false);
            SelectionChanged?.Invoke(Selected);
        }
        return true;
    }

    private static void Pose(Drawer row, float value)
    {
        row.amount = value;
        float rise = LiftProgress(value);
        row.target.Slide.localPosition = row.closed + Vector3.back * (Extension * value) + Vector3.up * (TrayRise * rise);
        row.target.Lift.localPosition = row.lowered + Vector3.up * ((LiftTravel - TrayRise) * rise);
        row.target.ApplyFront(value);
        if (row.target.Lift.gameObject.activeSelf != (value > 0f)) row.target.Lift.gameObject.SetActive(value > 0f);
    }

    private static float LiftProgress(float extension) => Mathf.SmoothStep(0f, 1f, extension);

    internal bool CanPickBottle(MedicineItem item) => CanInteract && pulled == null && requested != null &&
        requested.displayed?.item == item && requested.amount >= AccessibleExtension;

    private static GameObject CreateVisual(Transform parent, MedicineItem item, float size, bool onSurface)
    {
        var visual = new GameObject(item.DisplayName + (onSurface ? " bottle" : " inspection"));
        visual.transform.SetParent(parent, false);
        visual.SetActive(false);
        var model = Instantiate(item.ConsumptionModel, visual.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(item.ConsumptionModelRotation);
        foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var hit in model.GetComponentsInChildren<Collider>(true)) hit.enabled = false;
        foreach (var body in model.GetComponentsInChildren<Rigidbody>(true))
        { body.isKinematic = true; body.detectCollisions = false; }
        foreach (var audio in model.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = DisplayLayer;
        if (AndyWeaponStation.TryVisualBounds(visual.transform, visual, out var bounds))
        {
            float length = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float scale = size / Mathf.Max(.001f, length);
            var pivot = bounds.center;
            if (onSurface) pivot.y = bounds.min.y;
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = -pivot * scale;
        }
        visual.SetActive(true);
        return visual;
    }

    private void ClearPreview()
    {
        if (CurrentModel != null)
        {
            CurrentModel.SetActive(false);
            Destroy(CurrentModel);
            CurrentModel = null;
        }
        if (previewPivot != null) previewPivot.localRotation = Quaternion.identity;
    }
}
