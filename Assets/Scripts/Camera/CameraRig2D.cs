using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Smooth follow camera with a dead zone, velocity look-ahead, level bounds and screen shake.
    /// </summary>
    [DefaultExecutionOrder(300)]
    [AddComponentMenu("TanLuZhe/Camera Rig 2D")]
    public sealed class CameraRig2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;
        [SerializeField] private Vector2 _offset = new Vector2(0f, 1.3f);

        [Header("Follow")]
        [Tooltip("Approximate time for the camera to catch up. 0 = locked to the target.")]
        [Range(0f, 1f)] [SerializeField] private float _smoothTime = 0.16f;
        [Tooltip("Rectangle around the focus point where the camera does not move at all.")]
        [SerializeField] private Vector2 _deadZone = new Vector2(1.1f, 0.9f);
        [Tooltip("How far the camera leads the player based on velocity.")]
        [SerializeField] private Vector2 _lookAhead = new Vector2(3.2f, 1.4f);
        [Tooltip("Smoothing applied to the look-ahead offset.")]
        [Range(0.01f, 2f)] [SerializeField] private float _lookAheadSmooth = 0.4f;
        [Tooltip("Player speed that produces the full look-ahead offset.")]
        [Range(1f, 40f)] [SerializeField] private float _maxLookAheadSpeed = 12f;

        [Header("Bounds")]
        [SerializeField] private bool _useBounds;
        [SerializeField] private Vector2 _minBounds = new Vector2(-30f, -10f);
        [SerializeField] private Vector2 _maxBounds = new Vector2(120f, 40f);

        private Camera _camera;
        private Rigidbody2D _targetBody;
        private Vector3 _velocity;
        private Vector2 _lookAheadCurrent;
        private Vector2 _lookAheadVelocity;

        private static CameraRig2D _instance;
        private float _shakeAmount;
        private float _shakeDecay;
        private float _shakeTime;

        public Camera Camera => _camera;

        public static void Shake(float amount, float duration)
        {
            if (_instance == null) _instance = FindFirstObjectByType<CameraRig2D>();
            if (_instance == null) return;
            _instance._shakeAmount = Mathf.Max(_instance._shakeAmount, amount);
            _instance._shakeTime = Mathf.Max(_instance._shakeTime, duration);
            _instance._shakeDecay = _instance._shakeTime > 0f ? _instance._shakeAmount / _instance._shakeTime : 1f;
        }

        private void Awake()
        {
            _instance = this;
            _camera = GetComponent<Camera>();
            if (_camera == null) _camera = Camera.main;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            if (_target == null)
            {
                PlayerController2D player = FindFirstObjectByType<PlayerController2D>();
                if (player != null)
                {
                    _target = player.transform;
                    _targetBody = player.Body;
                }
            }
            else
            {
                _targetBody = _target.GetComponent<Rigidbody2D>();
            }

            if (_target != null)
            {
                Vector3 p = _target.position + (Vector3)_offset;
                transform.position = new Vector3(p.x, p.y, transform.position.z);
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 wantedLookAhead = Vector2.zero;
            if (_targetBody != null)
            {
                Vector2 v = _targetBody.linearVelocity;
                wantedLookAhead = new Vector2(
                    Mathf.Clamp(v.x / _maxLookAheadSpeed, -1f, 1f) * _lookAhead.x,
                    Mathf.Clamp(v.y / _maxLookAheadSpeed, -1f, 1f) * _lookAhead.y);
            }

            _lookAheadCurrent = Vector2.SmoothDamp(_lookAheadCurrent, wantedLookAhead,
                ref _lookAheadVelocity, _lookAheadSmooth, Mathf.Infinity, dt);

            Vector2 focus = (Vector2)_target.position + _offset + _lookAheadCurrent;

            Vector2 current = transform.position;
            float dx = focus.x - current.x;
            float dy = focus.y - current.y;

            if (Mathf.Abs(dx) > _deadZone.x)
                current.x += Mathf.Sign(dx) * (Mathf.Abs(dx) - _deadZone.x);
            if (Mathf.Abs(dy) > _deadZone.y)
                current.y += Mathf.Sign(dy) * (Mathf.Abs(dy) - _deadZone.y);

            Vector3 desired = new Vector3(current.x, current.y, transform.position.z);
            Vector3 smoothed = Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime,
                Mathf.Infinity, dt);

            if (_useBounds && _camera != null)
            {
                float halfH = _camera.orthographicSize;
                float halfW = halfH * _camera.aspect;
                smoothed.x = Mathf.Clamp(smoothed.x, _minBounds.x + halfW, _maxBounds.x - halfW);
                smoothed.y = Mathf.Clamp(smoothed.y, _minBounds.y + halfH, _maxBounds.y - halfH);
                if (_minBounds.x + halfW > _maxBounds.x - halfW) smoothed.x = (_minBounds.x + _maxBounds.x) * 0.5f;
                if (_minBounds.y + halfH > _maxBounds.y - halfH) smoothed.y = (_minBounds.y + _maxBounds.y) * 0.5f;
            }

            Vector3 shake = Vector3.zero;
            if (_shakeTime > 0f)
            {
                _shakeTime -= dt;
                _shakeAmount = Mathf.Max(0f, _shakeAmount - _shakeDecay * dt);
                shake = (Vector3)(Random.insideUnitCircle * _shakeAmount);
                if (_shakeTime <= 0f) _shakeAmount = 0f;
            }

            transform.position = smoothed + shake;
        }
    }
}
