using UnityEngine;

namespace Survive.Economy
{
    public sealed class VendorNameBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
