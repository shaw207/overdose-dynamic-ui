using UnityEngine;

/// <summary>Switches the physical screen with the station menu that owns it.</summary>
public sealed class AndyComputerPower : MonoBehaviour
{
    [SerializeField] private GameObject display;

    private void OnEnable()
    {
        if (display != null) display.SetActive(true);
    }

    private void OnDisable()
    {
        if (display != null) display.SetActive(false);
    }
}
