using UnityEngine;

public enum WeaponFireType { Raycast, Projectile }

public enum WeaponType
{
    Pistol,
    Rifle,
    Shotgun,
    Sniper,
    Laser,
    Other
}

[CreateAssetMenu(menuName = "Overdose/Weapons/Weapon Archetype")]
public class WeaponArchetypeSO : ScriptableObject
{
    [Header("Identity")]
    public string weaponId;

    [Header("UI")]
    public string displayName;
    public WeaponType weaponType = WeaponType.Other;
    public Sprite icon;
    
    [Header("Display (Upgrade Panel Stand)")]
    public GameObject displayPrefab;
    public Vector3 displayLocalPos;
    public Vector3 displayLocalEuler;
    public Vector3 displayLocalScale = Vector3.one;

    [Header("Fire")]
    public WeaponFireType fireType;

    [SerializeField, TextArea] private string[] stageEffectDescriptions = new string[3];

    public string GetStageEffectDescription(int stage)
    {
        if (stage < 1 || stage > 3 || stageEffectDescriptions == null || stageEffectDescriptions.Length < stage)
            return "";
        return stageEffectDescriptions[stage - 1] ?? "";
    }

    [Header("Stats")]
    public WeaponStats baseStats = WeaponStats.Default;
}
