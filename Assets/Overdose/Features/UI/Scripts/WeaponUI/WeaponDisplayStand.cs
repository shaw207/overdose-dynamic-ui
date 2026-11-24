using UnityEngine;

public sealed class WeaponDisplayStand : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private bool rotate = true;
    [SerializeField] private float rotateSpeedDeg = 30f;

    public GameObject CurrentModel { get; private set; }

    private void Awake()
    {
        if (anchor == null) anchor = transform;
    }

    private void Update()
    {
        if (rotate && CurrentModel != null)
            anchor.Rotate(Vector3.up, rotateSpeedDeg * Time.unscaledDeltaTime, Space.Self);
    }

    public void Show(GameObject prefab, Vector3 localPos, Vector3 localEuler, Vector3 localScale)
    {
        Clear();
        if (prefab == null) return;

        CurrentModel = Instantiate(prefab, anchor != null ? anchor : transform);
        CurrentModel.transform.SetLocalPositionAndRotation(localPos, Quaternion.Euler(localEuler));
        CurrentModel.transform.localScale = localScale;
    }

    public void Clear()
    {
        if (CurrentModel == null) return;
        CurrentModel.SetActive(false);
        Destroy(CurrentModel);
        CurrentModel = null;
    }
}
