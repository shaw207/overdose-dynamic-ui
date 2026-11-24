using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*
 * Representa una entrada individual de arma dentro de la lista del panel de mejoras.
 * Muestra el nombre y el icono del arma, y marca visualmente si esta seleccionada.
 */
public class WeaponEntryUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject toggleImg;

    private string _weaponId;

    public string WeaponId => _weaponId;
    public Button Button => button;

    public void Bind(WeaponViewData data)
    {
        _weaponId = data.id;
        if (nameText) nameText.text = data.name + (data.supportsStageUpgrades ? $"\nStage {data.stage}" : "\nNo upgrades");
        if (iconImage)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (toggleImg) toggleImg.SetActive(selected);
    }
}
