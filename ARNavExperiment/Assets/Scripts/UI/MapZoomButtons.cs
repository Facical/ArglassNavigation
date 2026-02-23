using UnityEngine;
using UnityEngine.UI;
using ARNavExperiment.BeamPro;

namespace ARNavExperiment.UI
{
    /// <summary>
    /// 글래스 WorldSpace에서 핀치-투-줌을 대체하는 +/- 버튼.
    /// InteractiveMapController.ZoomTo()를 호출하여 지도 줌 조작.
    /// </summary>
    public class MapZoomButtons : MonoBehaviour
    {
        [SerializeField] private InteractiveMapController mapController;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private float zoomStep = 0.3f;

        private float currentZoom = 1f;

        private void Start()
        {
            if (zoomInButton != null)
                zoomInButton.onClick.AddListener(OnZoomIn);
            if (zoomOutButton != null)
                zoomOutButton.onClick.AddListener(OnZoomOut);
        }

        private void OnZoomIn()
        {
            if (mapController == null) return;
            currentZoom += zoomStep;
            mapController.ZoomTo(currentZoom);
        }

        private void OnZoomOut()
        {
            if (mapController == null) return;
            currentZoom -= zoomStep;
            mapController.ZoomTo(currentZoom);
        }

        private void OnDestroy()
        {
            if (zoomInButton != null)
                zoomInButton.onClick.RemoveListener(OnZoomIn);
            if (zoomOutButton != null)
                zoomOutButton.onClick.RemoveListener(OnZoomOut);
        }
    }
}
