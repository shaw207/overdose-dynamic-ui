using UnityEngine;

public sealed class AndyVendingDeliveryBottle : MonoBehaviour
{
    public int Contacts { get; private set; }
    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.rigidbody) Contacts++;
    }
}
