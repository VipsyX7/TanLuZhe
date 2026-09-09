using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Kinematic platform that travels between two points. Riders are carried by the player
    /// controller (which reads the platform's frame delta), and the grapple hook can latch
    /// onto it because it is a valid <see cref="IGrappleTarget"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [AddComponentMenu("TanLuZhe/Moving Platform 2D")]
    public sealed class MovingPlatform2D : MonoBehaviour, IGrappleTarget
    {
        [SerializeField] private Vector2 _travel = new Vector2(5f, 0f);
        [SerializeField] private float _speed = 2.2f;
        [SerializeField] private float _waitAtEnds = 0.5f;
        [SerializeField] private bool _startAtEnd;

        private Rigidbody2D _rb;
        private Vector2 _a;
        private Vector2 _b;
        private float _t;
        private int _dir = 1;
        private float _waitTimer;

        public Vector2 PlatformVelocity { get; private set; }
        public Rigidbody2D GetGrappleBody() => _rb;
        public float GrappleResistance => 1f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _a = _rb.position;
            _b = _a + _travel;
            _t = _startAtEnd ? 1f : 0f;
            if (_startAtEnd) _dir = -1;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            float distance = Vector2.Distance(_a, _b);
            if (distance < 0.0001f)
            {
                PlatformVelocity = Vector2.zero;
                return;
            }

            if (_waitTimer > 0f)
            {
                _waitTimer -= dt;
                PlatformVelocity = Vector2.zero;
                return;
            }

            Vector2 previous = _rb.position;
            _t += _dir * (_speed / distance) * dt;

            if (_t >= 1f)
            {
                _t = 1f;
                _dir = -1;
                _waitTimer = _waitAtEnds;
            }
            else if (_t <= 0f)
            {
                _t = 0f;
                _dir = 1;
                _waitTimer = _waitAtEnds;
            }

            Vector2 next = Vector2.Lerp(_a, _b, _t);
            _rb.MovePosition(next);
            PlatformVelocity = (next - previous) / dt;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
            Vector3 a = Application.isPlaying ? (Vector3)_a : transform.position;
            Vector3 b = Application.isPlaying ? (Vector3)_b : transform.position + (Vector3)_travel;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(b, 0.2f);
        }

        public Vector2 GetGrappleAnchorPoint(Vector2 hitPoint) => hitPoint;
        public void OnGrappleAttached(Vector2 anchorPoint, in DamageInfo impact) { }
        public void OnGrappleDetached() { }
    }
}
