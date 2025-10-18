using UnityEngine;

namespace HexGlobeProject.UI
{
    // Simple runtime billboard: face the active camera each frame (LateUpdate)
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;
        }
    }
}
