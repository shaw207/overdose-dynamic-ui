using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class AndyVendingProduct : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Button button;
    [SerializeField] private AndyDeploymentPanel panel;
    [SerializeField] private int index;
    public Button Button => button;
    public int Index => index;
    [SerializeField] private MeshCollider[] shapes;
    public MeshCollider[] Shapes => shapes;
    public bool HitBy(Ray ray,float distance)
    {
        foreach(var shape in shapes)
            if(shape.Raycast(ray,out _,distance)) return true;
        return false;
    }

    public void SetAvailable(bool available) => gameObject.SetActive(available);
    public void OnPointerClick(PointerEventData ev)
    {
        if (ev.button==PointerEventData.InputButton.Left && button.IsInteractable()) panel.SelectMedicine(index);
    }

    public static Button ButtonFor(GameObject target)
    {
        if (!target) return null;
        var physical=target.GetComponentInParent<AndyVendingProduct>();
        return physical ? physical.Button : target.GetComponentInParent<Button>();
    }
}
