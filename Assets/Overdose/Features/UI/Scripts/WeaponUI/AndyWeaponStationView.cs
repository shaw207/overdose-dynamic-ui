using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AndyWeaponStationView : MonoBehaviour
{
    [SerializeField] private AndyWeaponStation station;
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private AndyStationComputer computer;
    [SerializeField] private Button equipButton;
    [SerializeField] private TMP_Text equipLabel;
    [SerializeField] private TMP_Text[] equippedNames = Array.Empty<TMP_Text>();

    public bool IsSearchFocused => computer != null && computer.IsSearchFocused;

    public void Bind(AndyWeaponStation owner)
    {
        if (station != owner && computer != null) computer.ClearEntries();
        station = owner;
        if (owner == null) Show(false);
        else Refresh();
    }

    private void OnEnable()
    {
        if (computer != null)
        {
            computer.ItemPressed += Preview;
            computer.Dismissed += ClearPreview;
        }
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (equipButton != null) equipButton.onClick.AddListener(Equip);
    }

    private void OnDisable()
    {
        if (computer != null)
        {
            computer.ItemPressed -= Preview;
            computer.Dismissed -= ClearPreview;
        }
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (equipButton != null) equipButton.onClick.RemoveListener(Equip);
    }

    private void Preview(UnityEngine.Object item) => station?.PreviewWeapon((Weapon)item);
    private void ClearPreview() => station?.PreviewWeapon(null);
    private void Equip() => station?.EquipPreview();
    private void Close() => station?.Close();

    public void Show(bool visible)
    {
        if (panel == null) return;
        panel.SetActive(visible);
        if (visible) Refresh();
    }

    public void Refresh()
    {
        if (station == null || !station.CanBrowseWeapons || panel == null || !panel.activeSelf || computer == null) return;
        var catalogue = station.Catalogue;
        if (!computer.HasCatalogue(catalogue))
        {
            computer.ClearEntries();
            foreach (var weapon in catalogue)
                if (weapon != null) computer.Add(weapon,weapon.WeaponId,weapon.DisplayName,weapon.Icon);
            computer.CompleteCatalogue();
        }
        for (int i=0; i<equippedNames.Length; i++)
        {
            var equipped=station.Slot(i);
            equippedNames[i].text=equipped != null ? equipped.DisplayName : "Empty";
        }
        var item=station.Selected;
        bool equippedHere=item!=null && (item==station.Slot(0) || item==station.Slot(1));
        equipButton.interactable=item!=null && !equippedHere;
        equipLabel.text=equippedHere ? "Equipped" : "Equip";
        if(item==null)
        {
            computer.ShowDetails(null,string.Empty,string.Empty,string.Empty);
            return;
        }
        var source=item.Archetype;
        string type=item.WeaponType.ToString();
        string effect=source!=null ? source.GetStageEffectDescription(item.Stage) : string.Empty;
        string description=source!=null ? type+"  /  "+source.fireType : type;
        if(!string.IsNullOrWhiteSpace(effect))description+="\n"+effect;
        WeaponStats stats=item.DisplayStats;
        bool hitscan=source!=null && source.fireType==WeaponFireType.Raycast;
        string speed=hitscan ? $"Speed setting  {stats.projectileSpeed:0.##}" : $"Projectile speed  {stats.projectileSpeed:0.##} m/s";
        string values=$"Damage  {stats.damage:0.##}\nRange  {stats.range:0.##} m\nSpread  {stats.spreadAngle:0.##}°\n{speed}\n"+
            $"Capacity  {stats.magSize}\nReload  {stats.reloadTime:0.##} s\nRate  {stats.fireRate:0.##}/s\n"+
            $"Recoil pitch  {stats.recoilPitch:0.##}°\nRecoil yaw  {stats.recoilYaw:0.##}°";
        computer.ShowDetails(item,item.DisplayName,description,values);
    }

}
