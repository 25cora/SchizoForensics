using UnityEngine;

namespace Forensics
{
    [DisallowMultipleComponent]
    public sealed class SolvedClueMarker : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;

        public void SetCamera(Camera cameraToFace) => playerCamera = cameraToFace;

        private void LateUpdate()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (playerCamera != null)
                transform.rotation = playerCamera.transform.rotation;
        }
    }
}
