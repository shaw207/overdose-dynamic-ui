using UnityEngine;

/// <summary>Presentation data from one equipped catalog entry in the original station.</summary>
public sealed class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponArchetypeSO archetype;
    [SerializeField] private WeaponStats stationStats;
    [SerializeField, Range(0, 3)] private int stationStage;
    [SerializeField] private bool supportsStages;

    public WeaponArchetypeSO Archetype => archetype;
    public WeaponStats DisplayStats => stationStats;
    public int Stage => stationStage;
    public bool SupportsStageUpgrades => supportsStages;
    public string WeaponId => archetype != null ? archetype.weaponId : string.Empty;
    public string DisplayName => archetype != null && !string.IsNullOrEmpty(archetype.displayName) ? archetype.displayName : name;
    public WeaponType WeaponType => archetype != null ? archetype.weaponType : WeaponType.Other;
    public Sprite Icon => archetype != null ? archetype.icon : null;
    public GameObject DisplayPrefab => archetype != null ? archetype.displayPrefab : null;
    public Vector3 DisplayPos => archetype != null ? archetype.displayLocalPos : Vector3.zero;
    public Vector3 DisplayEuler => archetype != null ? archetype.displayLocalEuler : Vector3.zero;
    public Vector3 DisplayScale => archetype != null ? archetype.displayLocalScale : Vector3.one;
}
