using System;
using UnityEngine;

[Serializable]
/*
 * Agrupa las estadisticas numericas que definen el comportamiento de un arma.
 */
public struct WeaponStats
{
    public float damage;
    public float fireRate;
    public float range;
    public float projectileSpeed;
    public float spreadAngle;
    public float recoilPitch;
    public float recoilYaw;
    public int magSize;
    public float reloadTime;

    public static WeaponStats Default => new WeaponStats
    {
        damage = 10f,
        fireRate = 8f,
        range = 50f,
        projectileSpeed = 40f,
        spreadAngle = 0f,
        recoilPitch = 0f,
        recoilYaw = 0f,
        magSize = 0,
        reloadTime = 0f
    };
}
