using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Simple parallax layer. Follows the camera with a configurable factor and optionally
    /// wraps horizontally so a small sprite can cover a long level.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("TanLuZhe/Parallax Layer 2D")]
    public sealed class ParallaxLayer2D : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float _horizontalFactor = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float _verticalFactor = 0.15f;
        [SerializeField] private bool _followCamera = true;
        [SerializeField] private float _wrapWidth;
        [SerializeField] private float _verticalOffset;

        private Transform _camera;
        private Vector3 _startPosition;
        private Vector3 _cameraStart;

        private void Start()
        {
            _startPosition = transform.position;
            Camera cam = Camera.main;
            if (cam != null)
            {
                _camera = cam.transform;
                _cameraStart = _camera.position;
            }
        }

        private void LateUpdate()
        {
            if (!_followCamera || _camera == null) return;

            Vector3 delta = _camera.position - _cameraStart;
            float x = _startPosition.x + delta.x * _horizontalFactor;
            float y = _startPosition.y + delta.y * _verticalFactor + _verticalOffset;

            if (_wrapWidth > 0.001f)
            {
                float span = _wrapWidth;
                x = Mathf.Repeat(x - _startPosition.x + span * 0.5f, span) - span * 0.5f + _startPosition.x;
            }

            transform.position = new Vector3(x, y, _startPosition.z);
        }
    }
}
