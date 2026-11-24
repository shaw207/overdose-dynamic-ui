using UnityEngine;

// Existing UI navigation owns the remainder of the occupied cell. Let the actual mesh receive its own pixel hits.
public sealed class AndyVendingProductFilter : MonoBehaviour, ICanvasRaycastFilter
{
    [SerializeField] private AndyVendingProduct product;
    [SerializeField] private Camera stationCamera;
    public bool IsRaycastLocationValid(Vector2 point,Camera eventCamera)
    {
        return !product || !product.isActiveAndEnabled ||
            !product.HitBy(stationCamera.ScreenPointToRay(point),stationCamera.farClipPlane);
    }
}
