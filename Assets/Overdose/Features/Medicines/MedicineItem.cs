using UnityEngine;

[CreateAssetMenu(fileName = "Medicine", menuName = "Overdose/Medicine/MedicineItem")]
public sealed class MedicineItem : ScriptableObject
{
    public string DisplayName;
    public Sprite Icon;
    [TextArea] public string EffectDescription;
    public GameObject ConsumptionModel;
    public Vector3 ConsumptionModelRotation;
    public MedicineClass MedicineClass;
    public AdvancedMedicineProfile AdvancedProfile;
}
