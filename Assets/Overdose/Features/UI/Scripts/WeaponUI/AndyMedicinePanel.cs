using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class AndyMedicinePanel : MonoBehaviour
{
    private const int SlotCount=3;
    [SerializeField] private GameObject panel;
    [SerializeField] private AndyWeaponStation station;
    [SerializeField] private AndyMedicineDrawers drawers;
    [SerializeField] private MedicineItem[] catalogue=Array.Empty<MedicineItem>();
    [SerializeField] private AndyStationComputer computer;
    [SerializeField] private Button[] equipButtons=new Button[SlotCount];
    [SerializeField] private TMP_Text[] equippedNames=new TMP_Text[SlotCount];
    private readonly UnityAction[] equipActions=new UnityAction[SlotCount];
    private bool open;

    private void Awake()
    {
        var unique = new HashSet<MedicineItem>();
        catalogue = Array.FindAll(catalogue, item => item != null && item.AdvancedProfile != null && unique.Add(item));
    }
    public IReadOnlyList<MedicineItem> Catalogue => catalogue;
    public MedicineItem Selected => drawers!=null ? drawers.Selected : null;
    public MedicineItem Slot(int index) => station!=null ? station.GetMedicineInSlot(index) : null;
    public bool IsSearchFocused => open && computer!=null && computer.IsSearchFocused;

    private void OnEnable()
    {
        if(drawers!=null) drawers.SelectionChanged+=OnSelectionChanged;
        if(computer!=null)
        {
            computer.ItemPressed+=Preview;
            computer.Dismissed+=ClearPreview;
        }
        for(int i=0;i<SlotCount && i<equipButtons.Length;i++)
        {
            if(equipButtons[i]==null) continue;
            int index=i;
            equipActions[i]=()=>Equip(index);
            equipButtons[i].onClick.AddListener(equipActions[i]);
        }
        Refresh();
    }

    private void OnDisable()
    {
        if(drawers!=null) drawers.SelectionChanged-=OnSelectionChanged;
        if(computer!=null)
        {
            computer.ItemPressed-=Preview;
            computer.Dismissed-=ClearPreview;
        }
        for(int i=0;i<SlotCount && i<equipButtons.Length;i++)
            if(equipButtons[i]!=null && equipActions[i]!=null) equipButtons[i].onClick.RemoveListener(equipActions[i]);
    }

    private void Preview(UnityEngine.Object item) { if(open && drawers!=null) drawers.Select((MedicineItem)item); }
    private void ClearPreview() { if(open && drawers!=null) drawers.ClearSelection(); }

    public void SetOpen(bool visible)
    {
        if(panel==null) return;
        open=visible;
        panel.SetActive(visible);
        if(visible) Refresh();
    }

    public void Equip(int index)
    {
        if(!open || Selected==null || station==null || index<0 || index>=SlotCount) return;
        if(station.TrySetMedicineInSlot(index,Selected)) Refresh();
    }

    public void Refresh()
    {
        if(!open || panel==null || !panel.activeInHierarchy || computer==null) return;
        if(!computer.HasCatalogue(catalogue))
        {
            computer.ClearEntries();
            foreach(var item in catalogue)
                if(item!=null) computer.Add(item,item.name,item.DisplayName,item.Icon,item.MedicineClass.ToString());
            computer.CompleteCatalogue();
        }
        OnSelectionChanged(Selected);
    }

    private void OnSelectionChanged(MedicineItem item)
    {
        if(!open || computer==null) return;
        for(int i=0;i<SlotCount;i++)
        {
            var equip=Slot(i);
            if(i<equippedNames.Length && equippedNames[i]!=null) equippedNames[i].text=equip!=null ? equip.DisplayName : "Empty";
            if(i<equipButtons.Length && equipButtons[i]!=null)
                equipButtons[i].interactable=item!=null && station!=null && i<station.MedicineSlotCount && equip!=item;
        }
        if(item==null)
        {
            computer.ShowDetails(null,string.Empty,string.Empty,string.Empty);
            return;
        }
        var p=item.AdvancedProfile;
        string values=$"{item.MedicineClass}\nDuration  {p.ActiveDuration:0.##} s\nToxicity  {p.ToxicityCost:0.##}\n"+
            $"Ingestion  {p.IngestionTime:0.##} s\nCooldown  {p.Cooldown:0.##} s\nMax charges  {p.ChargeCapacity}";
        computer.ShowDetails(item,item.DisplayName,item.EffectDescription,values);
    }
}
