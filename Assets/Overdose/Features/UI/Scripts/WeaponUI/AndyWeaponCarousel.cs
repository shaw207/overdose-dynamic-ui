using System.Collections.Generic;
using UnityEngine;

/// <summary>Each weapon rides its own two-plate shelf along the large arc at one edge of the screen.</summary>
public sealed class AndyWeaponCarousel : MonoBehaviour
{
    private const int InitialTrays = 4;
    private const float LevelSpacing = 1.14f;
    private const float ShelfSurface = .035f;
    private const float ReferenceHalfHeight = 2f;
    private const float ReferenceHalfWidth = ReferenceHalfHeight * 16f / 9f;
    private const float RackOriginX = 2.75f;
    private const float FrontX = 1.90f;
    private const float OuterRadius = 1.90f;
    private const float VisibleAngle = 56f;
    // Velocity is in shelves per second, independent of resolution and gameplay time scale.
    private const float FlickSpeed = 3f;
    private const float MaxSpinSpeed = 16f;
    private const float SpinFriction = 10f;
    private const float ReleaseWindow = .12f;

    [SerializeField] private bool secondaryRack;
    [SerializeField] private Transform[] levelMounts = new Transform[InitialTrays];
    [SerializeField] private WeaponDisplayStand[] stands = new WeaponDisplayStand[InitialTrays];
    [SerializeField, Min(.1f)] private float gunWidth = 1.13f;

    private readonly List<Weapon> inventory = new();
    private AndyWeaponStation owner;
    // One unit is one entry. A consecutive pair always stays consecutive around the loop.
    private float position, velocity, scrollRemainder;
    private float appliedPosition = float.NaN, appliedWidth, appliedHeight;
    private int destination, spinStop;
    private bool dragging, spinning, held;
    private float braking, sampleOrigin, previousSpeed, sampleDuration;
    private double sampleTime;
    private int dragOrigin;

    public bool IsMoving => dragging || spinning || (!held && position != destination);
    public int TrayCount => levelMounts.Length;
    public Transform MountAt(int physicalIndex) =>
        physicalIndex >= 0 && physicalIndex < levelMounts.Length ? levelMounts[physicalIndex] : null;
    public Weapon SelectedWeapon => At(destination);
    public Weapon WeaponAtBucket(int physicalIndex) =>
        physicalIndex >= 0 && physicalIndex < inventory.Count ? inventory[physicalIndex] : null;
    private int Period => Mathf.Max(3, inventory.Count);

    // Used by the Editor author before a running camera is available. At four items the
    // initial order is center, bottom, fully outside the frame, top.
    public static Vector3 RestPosition(int index, bool secondary, int stockSize = InitialTrays) =>
        ArcPosition(index, Mathf.Max(3, stockSize), secondary, 1f, 1f);

    public void Bind(AndyWeaponStation station, IReadOnlyList<Weapon> weapons)
    {
        owner = station;
        inventory.Clear();
        if (weapons != null)
            for (int i = 0; i < weapons.Count; i++)
                if (weapons[i] != null && !inventory.Contains(weapons[i])) inventory.Add(weapons[i]);

        EnsureTrays(inventory.Count);
        for (int i = 0; i < levelMounts.Length; i++)
        {
            var mount = levelMounts[i];
            if (mount == null || i >= stands.Length || stands[i] == null) continue;
            var weapon = WeaponAtBucket(i);
            mount.gameObject.SetActive(weapon != null);
            if (weapon == null || owner == null)
                stands[i].Clear();
            else
                PlaceOnShelf(mount, stands[i], weapon);
        }

        dragging = spinning = held = false;
        position = velocity = scrollRemainder = 0f;
        destination = 0;
        appliedPosition = float.NaN;
        ApplyPose();
    }

    private void EnsureTrays(int count)
    {
        int oldCount = levelMounts.Length;
        if (count <= oldCount || oldCount == 0 || levelMounts[0] == null) return;
        System.Array.Resize(ref levelMounts, count);
        System.Array.Resize(ref stands, count);
        for (int i = oldCount; i < count; i++)
        {
            var mount = Instantiate(levelMounts[0], levelMounts[0].parent);
            mount.name = "Weapon shelf " + (i + 1);
            var stand = mount.GetComponentInChildren<WeaponDisplayStand>(true);
            // The authored anchor is empty. On a later rebind, a cloned template may also
            // contain the previous runtime exhibit; remove only the copy under this anchor.
            foreach (Transform child in stand.transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            foreach (var hit in mount.GetComponentsInChildren<AndyWeaponGesture>(true))
                hit.SetupCarousel(this, i);
            levelMounts[i] = mount;
            stands[i] = stand;
        }
    }
    public void Focus(Weapon weapon, bool immediate)
    {
        int index = inventory.IndexOf(weapon);
        if (index < 0) return;
        // A search result can be on the out-of-frame return arc.
        int closest = index + Mathf.RoundToInt((position - index) / Period) * Period;
        SetDestination(closest, immediate, false);
    }

    public void Browse(int step)
    {
        if (owner == null || !owner.CanBrowseWeapons || inventory.Count == 0 || step == 0) return;
        owner.ActivateRack(this);
        // Keep consecutive wheel events relative to the requested entry, even mid-animation.
        // For one/two-item catalogues the third physical position is empty and is skipped.
        int count = inventory.Count;
        int current = Wrap(destination, Period);
        int completeTurns = (destination - current) / Period;
        int delta = step % count;
        if (delta == 0 && count > 1) delta = step > 0 ? count : -count;
        int nextOrdinal = current + delta;
        int next = Wrap(nextOrdinal, count);
        completeTurns += (nextOrdinal - next) / count;
        SetDestination(completeTurns * Period + next, false, true);
    }

    internal void Scroll(float ticks)
    {
        if (owner == null || !owner.CanBrowseWeapons) return;
        if (scrollRemainder * ticks < 0f) scrollRemainder = 0f;
        scrollRemainder += ticks;
        int steps = (int)scrollRemainder;
        if (steps == 0) return;
        scrollRemainder -= steps;
        Browse(steps);
    }

    public void SelectBucket(int physicalIndex)
    {
        Weapon weapon = WeaponAtBucket(physicalIndex);
        if (owner != null && owner.CanBrowseWeapons && weapon != null)
            owner.PreviewWeapon(weapon);
    }

    internal bool BeginShelfDrag()
    {
        if (owner == null || !owner.CanBrowseWeapons || inventory.Count < 2) return false;
        owner.ActivateRack(this);
        dragOrigin = destination;
        dragging = true;
        held = spinning = false;
        velocity = scrollRemainder = 0f;
        previousSpeed = 0f;
        sampleOrigin = position;
        sampleTime = Time.unscaledTimeAsDouble;
        sampleDuration = Mathf.Max(.000001f, Time.unscaledDeltaTime);
        return true;
    }

    internal bool CatchSpin()
    {
        if (!spinning) return false;
        spinning = false;
        held = true;
        velocity = 0f;
        owner.ActivateRack(this);
        owner.SelectFromCarousel(SelectedWeapon);
        return true;
    }

    internal void ReleaseCatch() => held = false;

    internal void SettleSpin()
    {
        if (spinning) SetDestination(destination, false, false);
    }

    internal void DragShelf(float deltaY, Camera camera)
    {
        if (!dragging || owner == null || !owner.CanBrowseWeapons || camera == null || deltaY == 0f) return;
        float bottom = camera.WorldToScreenPoint(transform.position).y;
        float spacing = camera.orthographic ? LevelSpacing * camera.orthographicSize / ReferenceHalfHeight : LevelSpacing;
        float top = camera.WorldToScreenPoint(transform.TransformPoint(Vector3.up * spacing)).y;
        double now = Time.unscaledTimeAsDouble;
        if (now > sampleTime)
        {
            sampleDuration = (float)(now - sampleTime);
            sampleTime = now;
            sampleOrigin = position;
            previousSpeed = sampleDuration < ReleaseWindow ? velocity : 0f;
        }
        position += deltaY / Mathf.Max(1f, Mathf.Abs(top - bottom));
        // Aggregate events sharing a frame and favor the last few frames on a direction change.
        float speed = (position - sampleOrigin) / sampleDuration;
        float prior = speed * previousSpeed < 0f ? 0f : previousSpeed;
        velocity = Mathf.Lerp(prior, speed, 1f - Mathf.Exp(-sampleDuration / .035f));
        UpdateCenteredWeapon();
        ApplyPose();
    }

    private void UpdateCenteredWeapon()
    {
        int nearest = NearestAvailable(position);
        bool changed = At(nearest) != SelectedWeapon;
        destination = nearest;
        if (changed) owner.SelectFromCarousel(SelectedWeapon);
    }

    internal void EndShelfDrag(bool commit)
    {
        if (!dragging) return;
        bool preview = commit && owner != null && owner.CanBrowseWeapons;
        float idle = (float)(Time.unscaledTimeAsDouble - sampleTime);
        float release = Mathf.Clamp(velocity, -MaxSpinSpeed, MaxSpinSpeed) *
            (1f - Mathf.InverseLerp(.04f, ReleaseWindow, idle));
        dragging = false;
        if (preview && Mathf.Abs(release) >= FlickSpeed)
        {
            // Round the natural braking distance to a real shelf so the wheel lands without reversing.
            spinStop = NearestAvailable(position + release * Mathf.Abs(release) / (2f * SpinFriction));
            float distance = spinStop - position;
            if (distance * release > 0f)
            {
                spinning = true;
                velocity = release;
                braking = release * release / (2f * Mathf.Abs(distance));
                owner.SelectFromCarousel(SelectedWeapon);
                return;
            }
        }
        velocity = 0f;
        SetDestination(preview ? NearestAvailable(position) : dragOrigin, false, preview);
    }

    private int NearestAvailable(float at)
    {
        int nearest = Mathf.FloorToInt(at + .5f);
        if (At(nearest) != null) return nearest;
        // Only a two-item catalogue can have an empty stop while dragging.
        return at < nearest ? nearest - 1 : nearest + 1;
    }

    private Weapon At(int index)
    {
        int wrapped = Wrap(index, Period);
        return wrapped < inventory.Count ? inventory[wrapped] : null;
    }

    private static int Wrap(int value, int count)
    {
        int result = value % count;
        return result < 0 ? result + count : result;
    }

    private void SetDestination(int next, bool immediate, bool notify)
    {
        if (dragging || spinning || held) velocity = 0f;
        destination = next;
        dragging = spinning = held = false;
        if (immediate)
        {
            velocity = scrollRemainder = 0f;
            if (position != next)
            {
                position = next;
                ApplyPose();
            }
        }
        else if (velocity * (next - position) < 0f) velocity = 0f;
        if (notify && owner != null) owner.SelectFromCarousel(SelectedWeapon);
    }

    private void Update()
    {
        if (owner == null || !owner.CanBrowseWeapons) return;
        if (spinning)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, Mathf.Abs(velocity) / braking);
            float nextSpeed = Mathf.MoveTowards(velocity, 0f, braking * dt);
            position += (velocity + nextSpeed) * .5f * dt;
            velocity = nextSpeed;
            if (Mathf.Abs(nextSpeed) < .001f)
            {
                position = Wrap(spinStop, Period);
                velocity = 0f;
                spinning = false;
            }
            UpdateCenteredWeapon();
        }
        else if (!dragging && !held && position != destination)
        {
            position = Mathf.SmoothDamp(position, destination, ref velocity, .18f,
                float.PositiveInfinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(position - destination) < .002f)
            {
                destination = Wrap(destination, Period);
                position = destination;
                velocity = 0f;
            }
        }
        ApplyPose();
    }

    private void ApplyPose()
    {
        // Keep the entry/exit path truly outside the view after an aspect-ratio change.
        float width = 1f, height = 1f;
        var camera = owner != null ? owner.StationCamera : null;
        if (camera != null && camera.orthographic)
        {
            width = camera.orthographicSize * camera.aspect / ReferenceHalfWidth;
            height = camera.orthographicSize / ReferenceHalfHeight;
        }
        if (appliedPosition == position && appliedWidth == width && appliedHeight == height) return;
        appliedPosition = position;
        appliedWidth = width;
        appliedHeight = height;
        for (int i = 0; i < inventory.Count && i < levelMounts.Length; i++)
        {
            var mount = levelMounts[i];
            if (mount != null) mount.localPosition = ArcPosition(i - position, Period, secondaryRack, width, height);
        }
        // The station freezes FixedUpdate. Keep world pointer hits on the moving physical plates.
        Physics.SyncTransforms();
    }

    private static Vector3 ArcPosition(float offset, int period, bool secondary, float width, float height)
    {
        float half = period * .5f;
        float signed = Mathf.Repeat(offset + half, period) - half;
        float distance = Mathf.Abs(signed);
        float degrees;
        if (distance <= 1f)
            degrees = distance * VisibleAngle;
        else
        {
            // The visible neighbors lie on the near arc. The next stop must clear the entire
            // horizontal camera edge (including plate width); any further stops share the far arc.
            float hiddenStop = Mathf.Min(2f, half);
            float hiddenAngle = half <= 2f ? 180f : 135f;
            if (distance < hiddenStop)
            {
                float t = (distance - 1f) / (hiddenStop - 1f);
                float tangent = VisibleAngle * (hiddenStop - 1f);
                degrees = VisibleAngle + t * (tangent + t * (hiddenAngle - VisibleAngle - tangent));
            }
            else
                degrees = hiddenStop == half ? 180f : Mathf.Lerp(hiddenAngle, 180f, (distance - hiddenStop) / (half - hiddenStop));
        }
        float radians = degrees * Mathf.Deg2Rad;
        float absoluteX = FrontX + OuterRadius * (1f - Mathf.Cos(radians));
        float verticalRadius = LevelSpacing / Mathf.Sin(VisibleAngle * Mathf.Deg2Rad);
        float y = -Mathf.Sign(signed) * Mathf.Sin(radians) * verticalRadius;
        float depth = Mathf.SmoothStep(-.30f, .12f, Mathf.Clamp01(distance));
        return new Vector3((secondary ? 1f : -1f) * (absoluteX * width - RackOriginX),
            (y - .12f) * height, depth);
    }
    private void PlaceOnShelf(Transform mount, WeaponDisplayStand display, Weapon weapon)
    {
        var model = AndyWeaponStation.ShowModel(display, weapon, gunWidth);
        if (!AndyWeaponStation.TryVisualBounds(mount, model, out var bounds)) return;
        const float maxHeight = .55f;
        if (bounds.size.y > maxHeight)
        {
            model.transform.localScale *= maxHeight / bounds.size.y;
            if (!AndyWeaponStation.TryVisualBounds(mount, model, out bounds)) return;
        }
        model.transform.position += mount.TransformVector(Vector3.up * (ShelfSurface + .003f - bounds.min.y));
    }
    internal void StopMotion() => SetDestination(Wrap(destination, Period), true, false);

    // From a neighboring bench the off-camera return arc would otherwise cross into view.
    internal void ShowFrontShelvesOnly(bool frontOnly)
    {
        for (int i = 0; i < inventory.Count && i < levelMounts.Length; i++)
        {
            if (levelMounts[i] == null) continue;
            int offset = Wrap(i - destination, Period);
            levelMounts[i].gameObject.SetActive(!frontOnly || offset <= 1 || offset == Period - 1);
        }
    }

    private void OnDisable() => StopMotion();
}
