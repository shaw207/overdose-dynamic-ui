using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class AndyWeaponStation : MonoBehaviour
{
    private const int DisplayLayer = 30; // Andy station camera's display meshes.
    private const float InspectionWidth = 1.50f;
    private const float InspectionCrossSection = .62f;

    [SerializeField] private Weapon[] initialWeapons = new Weapon[2];
    [SerializeField] private MedicineItem[] initialMedicines = new MedicineItem[3];

    [SerializeField] private Camera stationCamera;
    [SerializeField] private AndyWeaponStationView view;
    [SerializeField] private AndyWeaponCarousel primaryRack;
    [SerializeField] private WeaponDisplayStand stand;
    [SerializeField] private Transform previewPivot;
    [SerializeField] private Weapon[] primaryWeapons, secondaryWeapons;
    [SerializeField] private AndyStationNavigation navigation;
    [SerializeField] private GameObject weaponContent;
    [SerializeField] private GameObject[] weaponDisplay = System.Array.Empty<GameObject>();
    [SerializeField] private AndyMedicinePanel medicinePanel;
    [SerializeField] private AndyMedicineDrawers medicineDrawers;
    [SerializeField] private AndyDeploymentPanel deployment;
    [SerializeField] private GameObject deploymentBay;

    private readonly List<Weapon> catalogue = new();
    private readonly Dictionary<Weapon, int> slots = new();
    private Weapon[] equippedWeapons;
    private MedicineItem[] equippedMedicines;
    private bool initialized;
    private PhysicsRaycaster pointerRaycaster;
    private AndyWeaponGesture[] gestures;
    private AndyWeaponCarousel activeRack;
    private float yaw, pitch, restingOrthoSize;
    public bool IsOpen { get; private set; }
    public Weapon Selected { get; private set; }
    public IReadOnlyList<Weapon> Catalogue => catalogue;
    public Camera StationCamera => stationCamera;
    internal bool CanBrowseWeapons => IsOpen && (navigation == null || navigation.IsWeapons);
    public Weapon Slot(int index) => equippedWeapons != null && index >= 0 && index < equippedWeapons.Length
        ? equippedWeapons[index] : null;
    public int MedicineSlotCount => equippedMedicines?.Length ?? 0;
    public MedicineItem GetMedicineInSlot(int slot) => slot >= 0 && slot < MedicineSlotCount
        ? equippedMedicines[slot] : null;

    // Moving a medicine between occupied slots swaps their contents, just as in the game inventory.
    public bool TrySetMedicineInSlot(int slot, MedicineItem medicine)
    {
        if (medicine == null || slot < 0 || slot >= MedicineSlotCount) return false;
        int previous = System.Array.IndexOf(equippedMedicines, medicine);
        if (previous == slot) return true;
        if (previous >= 0) equippedMedicines[previous] = equippedMedicines[slot];
        equippedMedicines[slot] = medicine;
        return true;
    }
    public GameObject PreviewFor(Weapon weapon) => weapon != null ? weapon.DisplayPrefab : null;

    private void Awake()
    {
        equippedWeapons = (Weapon[])initialWeapons.Clone();
        equippedMedicines = (MedicineItem[])initialMedicines.Clone();
        if (stationCamera != null)
        {
            restingOrthoSize = stationCamera.orthographicSize;
            pointerRaycaster = stationCamera.GetComponent<PhysicsRaycaster>();
            SetStationCamera(false);
        }
        if (view != null) view.Show(false);
    }

    private void Start()
    {
        if (!stationCamera || !view || !stand || !previewPivot || !primaryRack || !navigation ||
            !medicinePanel || !medicineDrawers || !weaponContent || !deployment || !deploymentBay ||
            equippedWeapons.Length != 2 || equippedMedicines.Length != 3)
        {
            Debug.LogError("The station needs its display, three stations and five loadout slots.", this);
            return;
        }

        RegisterRack(primaryWeapons, 0);
        RegisterRack(secondaryWeapons, 1);
        primaryRack.Bind(this, catalogue);
        gestures = GetComponentsInChildren<AndyWeaponGesture>(true);
        view.Bind(this);
        if (navigation != null)
        {
            medicineDrawers.Bind(stationCamera, medicinePanel.Catalogue);
            navigation.Departed += OnNodeDeparted;
            navigation.Arrived += OnNodeArrived;
            medicinePanel.SetOpen(false);
            medicineDrawers.SetOpen(false);
            medicineDrawers.gameObject.SetActive(false);
        }
        deployment?.SetOpen(false);
        if (deploymentBay != null) deploymentBay.SetActive(false);
        initialized = true;
        Open();
    }

    private void RegisterRack(Weapon[] weapons, int slot)
    {
        if (weapons == null) return;
        foreach (var weapon in weapons)
        {
            if (PreviewFor(weapon) == null || slots.ContainsKey(weapon)) continue;
            catalogue.Add(weapon);
            slots.Add(weapon, slot);
        }
    }

    private void OnEnable()
    {
        if (initialized) Open();
    }

    private void OnDisable() => EndSession();

    private void OnDestroy()
    {
        if (navigation == null) return;
        navigation.Departed -= OnNodeDeparted;
        navigation.Arrived -= OnNodeArrived;
    }

    private void OnNodeDeparted(AndyStationNavigation.Node node)
    {
        foreach (var gesture in gestures)
            if (gesture != null) gesture.CancelDrag();
        primaryRack.StopMotion();
        weaponContent.SetActive(false);
        SetWeaponDisplay(true);
        primaryRack.ShowFrontShelvesOnly(true);
        medicinePanel.SetOpen(false);
        medicineDrawers.gameObject.SetActive(true);
        medicineDrawers.SetInputEnabled(false);
        deployment?.SetOpen(false);
        if (deploymentBay != null) deploymentBay.SetActive(true);
    }

    private void OnNodeArrived(AndyStationNavigation.Node node)
    {
        bool medicine = node == AndyStationNavigation.Node.Medicine;
        bool weapons = node == AndyStationNavigation.Node.Weapons;
        bool deploying = node == AndyStationNavigation.Node.Deployment;
        SetWeaponDisplay(weapons);
        if (weapons) primaryRack.ShowFrontShelvesOnly(false);
        if (deploymentBay != null) deploymentBay.SetActive(deploying);
        deployment?.SetOpen(deploying);
        medicineDrawers.gameObject.SetActive(medicine);
        medicineDrawers.SetOpen(medicine);
        medicinePanel.SetOpen(medicine);
        weaponContent.SetActive(weapons);
        if (weapons) view.Refresh();
    }

    private void SetWeaponDisplay(bool visible)
    {
        foreach (var display in weaponDisplay)
            if (display != null) display.SetActive(visible);
    }

    public void Open()
    {
        if (!initialized || IsOpen || catalogue.Count == 0) return;
        IsOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetStationCamera(true);
        FitCamera();
        primaryRack.Focus(Slot(0), true);
        SelectPreview(null);
        view.Show(true);
        navigation?.BeginSession();
        view.Refresh();
    }

    // This standalone station opens directly. The original close button now exits the demo.
    public void Close()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EndSession()
    {
        if (!IsOpen) return;
        IsOpen = false;
        foreach (var gesture in gestures)
            if (gesture != null) gesture.CancelDrag();
        primaryRack.StopMotion();
        activeRack = null;
        if (navigation != null)
        {
            navigation.EndSession();
            deployment?.SetOpen(false);
            if (deploymentBay != null) deploymentBay.SetActive(false);
            medicinePanel.SetOpen(false);
            medicineDrawers.SetOpen(false);
            medicineDrawers.gameObject.SetActive(false);
            weaponContent.SetActive(true);
            SetWeaponDisplay(true);
            primaryRack.ShowFrontShelvesOnly(false);
        }
        if (view != null) view.Show(false);
        SetStationCamera(false);
    }

    private void Update()
    {
        if (!IsOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (view.IsSearchFocused || medicinePanel.IsSearchFocused) return;
        if (navigation.IsTransitioning || !navigation.IsWeapons) navigation.GoWeapons();
        else if (Selected != null) PreviewWeapon(null);
        else Close();
    }

    private void SetStationCamera(bool active)
    {
        if (stationCamera != null) stationCamera.enabled = active;
        if (pointerRaycaster != null) pointerRaycaster.enabled = active;
    }

    private void LateUpdate()
    {
        if (IsOpen) FitCamera();
    }

    private void FitCamera()
    {
        // Preserve the inspection area and controls when the game view gets narrower.
        stationCamera.orthographicSize = restingOrthoSize *
            Mathf.Max(1f, (16f / 9f) / Mathf.Max(.1f, stationCamera.aspect));
    }

    internal void ActivateRack(AndyWeaponCarousel rack) => activeRack = rack;

    public void SelectFromCarousel(Weapon weapon)
    {
        if (CanBrowseWeapons && activeRack != null && activeRack.SelectedWeapon == weapon) SelectPreview(weapon);
    }

    public void PreviewWeapon(Weapon weapon)
    {
        if (!CanBrowseWeapons || (weapon != null && !slots.ContainsKey(weapon))) return;
        activeRack = primaryRack;
        if (weapon != null) primaryRack.Focus(weapon, false);
        else primaryRack.StopMotion();
        SelectPreview(weapon);
    }

    public void EquipPreview()
    {
        if (!CanBrowseWeapons || Selected == null || !slots.TryGetValue(Selected, out int slot)) return;
        int other = 1 - slot;
        if (equippedWeapons[other] == Selected) equippedWeapons[other] = equippedWeapons[slot];
        equippedWeapons[slot] = Selected;
        view.Refresh();
    }

    private void SelectPreview(Weapon weapon)
    {
        if (Selected == weapon) return;
        Selected = weapon;
        yaw = -8f;
        pitch = 0f;
        previewPivot.localRotation = Quaternion.identity;
        if (weapon == null) stand.Clear();
        else ShowModel(stand, weapon, InspectionWidth, InspectionCrossSection);
        previewPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        view.Refresh();
    }

    public void SetPreviewDragging(bool dragging)
    {
        if (CanBrowseWeapons && dragging && activeRack != null) activeRack.SettleSpin();
    }

    public void RotatePreview(Vector2 delta)
    {
        if (!IsOpen) return;
        float degreesPerPixel = 240f / Mathf.Max(1, stationCamera.pixelHeight);
        yaw -= delta.x * degreesPerPixel;
        pitch = Mathf.Clamp(pitch + delta.y * degreesPerPixel, -75f, 75f);
        previewPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public static GameObject ShowModel(WeaponDisplayStand display, Weapon weapon, float width,
        float maxCrossSection = float.PositiveInfinity)
    {
        if (display == null || weapon == null || weapon.DisplayPrefab == null) return null;
        display.Show(weapon.DisplayPrefab, weapon.DisplayPos, weapon.DisplayEuler, weapon.DisplayScale);
        var model = display.CurrentModel;
        foreach (var mesh in model.GetComponentsInChildren<Renderer>(true))
            mesh.gameObject.layer = DisplayLayer;
        var frame = display.transform;
        if (!TryVisualBounds(frame, model, out var b)) return model;
        // The project also contains meshes authored with the barrel on Z.
        if (b.size.z > b.size.x) model.transform.localRotation = Quaternion.Euler(0f,-90f,0f) * model.transform.localRotation;
        if (!TryVisualBounds(frame, model, out b)) return model;
        float scale = width / Mathf.Max(.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y,b.size.z)));
        // Limit the swept height around the length of the gun so tall grips fit the central stand.
        float crossSection = new Vector2(b.size.y, b.size.z).magnitude;
        scale = Mathf.Min(scale, maxCrossSection / Mathf.Max(.001f, crossSection));
        model.transform.localScale *= scale;
        if (TryVisualBounds(frame, model, out b))
            model.transform.position += frame.TransformVector(-b.center);
        return model;
    }

    internal static bool TryVisualBounds(Transform frame, GameObject model, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (model == null) return false;
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.enabled) continue;
            var local = renderer.localBounds;
            var toFrame = frame.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i&1)==0?local.min.x:local.max.x,
                    (i&2)==0?local.min.y:local.max.y,(i&4)==0?local.min.z:local.max.z);
                var p = toFrame.MultiplyPoint3x4(corner);
                if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                else bounds.Encapsulate(p);
            }
        }
        return found;
    }
}
