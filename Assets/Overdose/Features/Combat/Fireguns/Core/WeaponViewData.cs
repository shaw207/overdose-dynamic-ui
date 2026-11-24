using UnityEngine;

/*
 * Estructura ligera con la informacion necesaria para mostrar un arma en la interfaz.
 */
public struct WeaponViewData
{
    public string id;
    public string name;
    public WeaponType type;
    public Sprite icon;
    public int stage;
    public bool supportsStageUpgrades;
    public bool equipped;
}
