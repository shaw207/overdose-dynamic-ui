using UnityEngine;

public enum AdvancedMedicineKind
{
    BioFilter,
    VampireDrain,
    SubZeroHold,
    LastStand
}

/// <summary>Stage-adjustable values for the four prototype medicines.</summary>
[CreateAssetMenu(fileName = "AdvancedMedicine", menuName = "Overdose/Medicine/Advanced Profile")]
public sealed class AdvancedMedicineProfile : ScriptableObject
{
    public AdvancedMedicineKind Kind;
    [Range(0, 3)] public int Stage;
    public float[] ActiveDurations = { 8f, 9.33f, 10.67f, 12f };
    public float[] ToxicityCosts = { 25f, 21.67f, 18.33f, 15f };
    public float[] IngestionSeconds = { 0.5f, 0.42f, 0.33f, 0.25f };
    public float[] CooldownSeconds = { 28f, 25.33f, 22.67f, 20f };
    public int[] MaxCharges = { 1, 1, 2, 2 };

    public float ActiveDuration => AtStage(ActiveDurations, 8f);
    public float ToxicityCost => AtStage(ToxicityCosts, 25f);
    public float IngestionTime => AtStage(IngestionSeconds, 0.5f);
    public float Cooldown => AtStage(CooldownSeconds, 28f);
    public int ChargeCapacity => MaxCharges != null && Stage >= 0 && Stage < MaxCharges.Length
        ? Mathf.Max(1, MaxCharges[Stage])
        : 1;

    private float AtStage(float[] values, float fallback)
    {
        return values != null && Stage >= 0 && Stage < values.Length
            ? Mathf.Max(0f, values[Stage])
            : fallback;
    }
}
