using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AndyDeploymentPanel : MonoBehaviour
{
    [SerializeField] private AndyWeaponStation station;
    [SerializeField] private MedicineItem[] catalogue;
    [SerializeField] private GameObject content;
    [SerializeField] private GraphicRaycaster vendingInput;
    [SerializeField] private WeaponDisplayStand[] weaponStands;
    [SerializeField] private TMP_Text[] weaponNames, effects, slotLabels, itemLabels;
    [SerializeField] private TMP_Text[] slotMedicineNames = System.Array.Empty<TMP_Text>();
    [SerializeField] private Button[] itemButtons, slotButtons;
    [SerializeField] private GameObject[] itemSelection;
    [SerializeField] private GameObject[] slotSelection = System.Array.Empty<GameObject>();
    [SerializeField] private AndyMedicineTooltip tooltip;
    [SerializeField] private AndyVendingDispenser dispenser;
    [SerializeField] private Button deployButton;
    [SerializeField] private TMP_Text status;
    private bool open;
    private int selectedSlot = -1;
    private int selectedProduct = -1;
    private bool transferring;
    private static readonly string[] Keys = { "V", "B", "N" };
    private readonly Weapon[] displayed = new Weapon[2];
    private readonly MedicineItem[] equipped = new MedicineItem[3];
    public int SelectedSlot => selectedSlot;
    public int SelectedProduct => selectedProduct;
    public int PendingSlot => transferring ? selectedSlot : -1;

    private void Awake()
    {
        deployButton.onClick.AddListener(Deploy);
        for (int i = 0; i < itemButtons.Length; i++)
        {
            int index = i;
            itemButtons[i].onClick.AddListener(() => SelectMedicine(index));
        }
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i;
            slotButtons[i].onClick.AddListener(() => SelectSlot(index));
        }
    }

    private void OnEnable()
    {
        if (!dispenser) return;
        dispenser.Arrived += OnArrived;
        dispenser.Cancelled += OnCancelled;
    }

    private void OnDisable()
    {
        if (open) SetOpen(false);
        if (!dispenser) return;
        dispenser.Arrived -= OnArrived;
        dispenser.Cancelled -= OnCancelled;
    }

    public void SetOpen(bool visible)
    {
        if (!visible)
        {
            ClearSelection();
            RefreshSelection();
        }
        open = visible;
        content.SetActive(visible);
        vendingInput.enabled = visible;
        if (dispenser) dispenser.SetOpen(visible);
        if (!visible)
        {
            if (tooltip) tooltip.Hide();
            return;
        }
        status.text = string.Empty;
        Refresh();
    }

    public void SelectMedicine(int index)
    {
        if (!open || transferring || index < 0 || index >= catalogue.Length || !catalogue[index]) return;
        status.text = string.Empty;
        selectedProduct = index;
        TryStartTransfer();
    }

    public void SelectSlot(int slot)
    {
        if (!open || transferring || slot < 0 || slot >= slotButtons.Length || slot >= station.MedicineSlotCount) return;
        status.text = string.Empty;
        selectedSlot = slot;
        TryStartTransfer();
    }

    private void TryStartTransfer()
    {
        if (selectedProduct >= 0 && selectedSlot >= 0)
        {
            if (station.GetMedicineInSlot(selectedSlot) == catalogue[selectedProduct]) ClearSelection();
            else if (dispenser) transferring = dispenser.TryTransfer(selectedProduct, selectedSlot);
        }
        RefreshSelection();
    }

    private void ClearSelection()
    {
        selectedProduct = selectedSlot = -1;
        transferring = false;
    }

    private void OnArrived(int product, int slot)
    {
        if (!open || !transferring || selectedProduct != product || selectedSlot != slot) return;
        ClearSelection();
        station.TrySetMedicineInSlot(slot, catalogue[product]);
        RefreshMedicine();
    }

    private void OnCancelled()
    {
        if (!open) return;
        ClearSelection();
        RefreshMedicine();
    }

    public void Deploy()
    {
        if (!open) return;
        if (transferring) return;
        status.text = "Loadout confirmed";
    }

    private void Refresh()
    {
        for (int i = 0; itemLabels != null && i < itemLabels.Length && i < catalogue.Length; i++)
            if (itemLabels[i]) itemLabels[i].text = catalogue[i] ? catalogue[i].DisplayName : string.Empty;

        for (int i = 0; i < weaponNames.Length; i++)
        {
            Weapon gun = station.Slot(i);
            weaponNames[i].text = gun != null ? gun.DisplayName : "Empty";
            var text = new StringBuilder();
            if (gun != null && gun.SupportsStageUpgrades && gun.Archetype != null)
                for (int stage = 1; stage <= gun.Stage; stage++)
                {
                    string effect = gun.Archetype.GetStageEffectDescription(stage);
                    if (string.IsNullOrWhiteSpace(effect)) continue;
                    if (text.Length != 0) text.AppendLine();
                    text.Append(effect);
                }
            effects[i].text = text.ToString();
            if (gun == displayed[i]) continue;
            displayed[i] = gun;
            if (gun != null) AndyWeaponStation.ShowModel(weaponStands[i], gun, 1.2f, .38f);
        }
        RefreshMedicine();
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < slotSelection.Length; i++)
            slotSelection[i].SetActive(i == selectedSlot);
        for (int i = 0; i < itemButtons.Length; i++)
        {
            bool active = i == selectedProduct;
            if (itemSelection != null && i < itemSelection.Length && itemSelection[i] != null)
                itemSelection[i].SetActive(active);
            else if (itemLabels != null && i < itemLabels.Length && itemLabels[i] != null)
            {
                itemLabels[i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                itemLabels[i].color = active ? new Color(.66f,.92f,.75f) : Color.white;
                itemButtons[i].targetGraphic.color = active ? new Color(.22f,.36f,.31f,.85f) : new Color(.12f,.15f,.17f,.75f);
            }
        }
    }

    private void RefreshMedicine()
    {
        RefreshSelection();
        for (int i = 0; i < equipped.Length; i++)
        {
            equipped[i] = station.GetMedicineInSlot(i);
            string name = equipped[i] ? equipped[i].DisplayName : "Empty";
            if (i < slotMedicineNames.Length && slotMedicineNames[i])
            {
                slotLabels[i].text = Keys[i];
                slotMedicineNames[i].text = name;
            }
            else slotLabels[i].text = Keys[i] + "\n" + name;
        }
        if (dispenser) dispenser.ShowSlots(equipped);
    }
}
