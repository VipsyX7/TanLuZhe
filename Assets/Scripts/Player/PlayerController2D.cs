using System.Collections.Generic;
using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Force driven 2D platformer character controller.
    ///
    /// Design notes
    /// ------------
    /// * Gravity, running and jumping are applied as <b>forces/velocity drives with a limited
    ///   acceleration budget</b> instead of hard velocity assignments. Anything else that
    ///   touches the rigidbody (the grapple rope, an enemy body slam, a moving platform) is
    ///   therefore able to inject momentum that the controller only gradually steers back to
    ///   the player's intent. This is what makes "keep your inertia" feel physical.
    /// * Ground/wall/ceiling information comes from the real contact manifold produced by the
    ///   simulation (<see cref="Rigidbody2D.GetContacts"/>), with a box-cast fallback so a
    ///   resting body never flickers between grounded and airborne.
    /// * Buffered jump + coyote time + variable jump height + apex hang + wall slide/wall jump
    ///   + one-way drop-through + moving platform carry are all implemented here.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("TanLuZhe/Player Controller 2D")]
    public sealed class PlayerController2D : MonoBehaviour
    {
        // ------------------------------------------------------------------ run
        [Header("Run")]
        [SerializeField] private float _maxRunSpeed = 8.5f;
        [SerializeField] private float _groundAcceleration = 75f;
        [SerializeField] private float _groundDeceleration = 95f;
        [SerializeField] private float _airAcceleration = 48f;
        [SerializeField] private float _airDeceleration = 26f;
        [SerializeField] private float _turnAroundBoost = 1.8f;
        [Tooltip("Air control multiplier applied while the grapple rope is pulling the player. " +
                 "Lower = more inertia, less ability to fight the rope.")]
        [SerializeField] private float _grappleAirControl = 0.45f;

        // ----------------------------------------------------------------- jump
        [Header("Jump")]
        [SerializeField] private float _jumpHeight = 3.1f;
        [Tooltip("Upward acceleration while rising (m/s^2). Higher = snappier, shorter arc.")]
        [SerializeField] private float _riseGravity = 42f;
        [Tooltip("Downward acceleration while falling (m/s^2).")]
        [SerializeField] private float _fallGravity = 74f;
        [Tooltip("Downward acceleration while hanging near the apex (m/s^2).")]
        [SerializeField] private float _apexGravity = 26f;
        [SerializeField] private float _apexVelocityWindow = 2.4f;
        [Tooltip("Extra gravity multiplier applied after the jump key is released early.")]
        [SerializeField] private float _jumpCutGravity = 2.8f;
        [SerializeField] private float _maxFallSpeed = 24f;
        [SerializeField] private float _coyoteTime = 0.1f;
        [SerializeField] private float _jumpBufferTime = 0.12f;
        [Tooltip("Downward force applied while grounded so the capsule stays glued to slopes.")]
        [SerializeField] private float _groundStickForce = 18f;

        // ----------------------------------------------------------------- wall
        [Header("Wall")]
        [SerializeField] private float _maxSlopeAngle = 50f;
        [SerializeField] private float _wallSlideSpeed = 4.5f;
        [SerializeField] private Vector2 _wallJumpVelocity = new Vector2(11f, 15.5f);
        [SerializeField] private float _wallJumpControlLock = 0.18f;
        [SerializeField] private float _wallCoyoteTime = 0.1f;
        [SerializeField] private float _wallJumpSameWallLock = 0.12f;

        // ----------------------------------------------------------------- misc
        [Header("World interaction")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField] private float _dropThroughTime = 0.35f;

        private Rigidbody2D _rb;
        private CapsuleCollider2D _col;
        private IInputSource _input;
        private GrappleHook2D _grapple;
        private PlayerHealth _health;

        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[16];
        private readonly List<RaycastHit2D> _probeHits = new List<RaycastHit2D>(8);
        private readonly List<Collider2D> _overlaps = new List<Collider2D>(8);
        private ContactFilter2D _solidFilter;

        private float _minGroundDot;
        private bool _isGrounded;
        private bool _wasGrounded;
        private Vector2 _groundNormal = Vector2.up;
        private Collider2D _groundCollider;
        private MovingPlatform2D _groundPlatform;

        private bool _isWallSliding;
        private int _wallDirection;          // -1 left, +1 right
        private Vector2 _wallNormal = Vector2.right;
        private float _lastWallContactTime = -99f;
        private float _lastWallJumpTime = -99f;

        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _wallCoyoteTimer;
        private float _controlLockTimer;
        private float _dropThroughTimer;

        private Collider2D _ignoredOneWay;
        private float _ignoredOneWayTimer;

        private Vector2 _platformVelocity;
        private Vector2 _platformLastPosition;
        private bool _hasPlatformReference;

        private bool _jumpCutRequested;
        private bool _jumpCutArmed;
        private int _facing = 1;
        private float _appliedGravity;

        // ------------------------------------------------------------- public API
        public bool IsGrounded => _isGrounded;
        public bool IsWallSliding => _isWallSliding;
        public bool IsBeingPulled { get; private set; }
        public Vector2 Velocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
        public float FacingSign => _facing;
        public Vector2 GroundNormal => _groundNormal;
        public bool IsDead => _health != null && !_health.IsAlive;
        public Rigidbody2D Body => _rb;
        public CapsuleCollider2D Collider => _col;

        /// <summary>Gravity magnitude currently being applied (m/s^2), for slope grip math.</summary>
        public float CurrentGravity => _appliedGravity > 0.01f ? _appliedGravity : _fallGravity;

        /// <summary>Hook used by the grapple system to flag "the rope is pulling me right now".</summary>
        public void SetBeingPulled(bool pulled) => IsBeingPulled = pulled;

        /// <summary>Injects the input source. Falls back to a component lookup when not set.</summary>
        public void SetInputSource(IInputSource source) => _input = source;

        // ------------------------------------------------------------------ unity
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _grapple = GetComponentInChildren<GrappleHook2D>();
            _health = GetComponent<PlayerHealth>();

            _minGroundDot = Mathf.Cos(_maxSlopeAngle * Mathf.Deg2Rad);

            _solidFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = _groundMask,
            };

            _rb.gravityScale = 0f;                       // gravity is applied manually
            _rb.freezeRotation = true;
            _rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Start()
        {
            if (_input == null)
            {
                _input = GetComponent<IInputSource>();
                if (_input == null) _input = GetComponentInChildren<IInputSource>();
            }

            // The grapple is usually created as a child after this component, so resolve it late.
            if (_grapple == null) _grapple = GetComponentInChildren<GrappleHook2D>();
        }

        private void Update()
        {
            if (_input == null) return;

            if (_input.JumpDown) _jumpBufferTimer = _jumpBufferTime;
            if (!_input.JumpHeld && _jumpCutArmed && _rb.linearVelocity.y > 0f) _jumpCutRequested = true;

            float h = _input.Horizontal;
            if (Mathf.Abs(h) > 0.01f) _facing = h > 0f ? 1 : -1;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (IsDead)
            {
                // Dead: still fall, but no player driven control at all.
                ApplyGravity(dt, false);
                ClampFallSpeed();
                return;
            }

            UpdateGroundState();
            UpdateWallState();
            UpdateTimers(dt);
            UpdateMovingPlatform(dt);

            float inputX = _input != null ? _input.Horizontal : 0f;
            float control = Mathf.Abs(inputX);

            if (_controlLockTimer <= 0f) ApplyHorizontalMovement(inputX, dt);
            ApplyGravity(dt, control > 0.01f);
            HandleJump(dt, inputX);
            HandleOneWayDrop();
            ClampFallSpeed();
            ApplyGroundStick(dt);

            _wasGrounded = _isGrounded;
        }

        // ------------------------------------------------------------------ ground
        private void UpdateGroundState()
        {
            _isGrounded = false;
            _groundNormal = Vector2.up;
            _groundCollider = null;

            int count = _rb.GetContacts(_contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D c = _contacts[i];
                if (c.collider == null || c.collider.isTrigger) continue;
                if (Vector2.Dot(c.normal, Vector2.up) < _minGroundDot) continue;
                if (c.point.y > _rb.position.y + 0.35f) continue;   // ceiling contact

                _isGrounded = true;
                _groundNormal = c.normal;
                _groundCollider = c.collider;
                break;
            }

            if (!_isGrounded)
            {
                // Box-cast fallback: catches the frame where contacts are not generated yet
                // (e.g. after a teleport or when resting exactly on a seam).
                Bounds b = _col.bounds;
                Vector2 origin = new Vector2(b.center.x, b.min.y + 0.06f);
                Vector2 size = new Vector2(Mathf.Max(0.02f, b.size.x * 0.9f), 0.08f);
                int hits = Physics2D.BoxCast(origin, size, 0f, Vector2.down, _solidFilter, _probeHits, 0.09f);
                for (int i = 0; i < hits; i++)
                {
                    RaycastHit2D h = _probeHits[i];
                    if (h.collider == null || h.collider == _col || h.collider.isTrigger) continue;
                    if (Vector2.Dot(h.normal, Vector2.up) < _minGroundDot) continue;

                    _isGrounded = true;
                    _groundNormal = h.normal;
                    _groundCollider = h.collider;
                    break;
                }
            }

            if (_isGrounded)
            {
                _coyoteTimer = _coyoteTime;
                _jumpCutArmed = false;
            }

            if (_groundCollider != null)
            {
                MovingPlatform2D platform = _groundCollider.GetComponentInParent<MovingPlatform2D>();
                if (platform != _groundPlatform)
                {
                    _groundPlatform = platform;
                    _hasPlatformReference = false;
                }
            }
            else
            {
                _groundPlatform = null;
                _hasPlatformReference = false;
            }
        }

        private void UpdateWallState()
        {
            _isWallSliding = false;
            _wallDirection = 0;

            if (_isGrounded) return;

            int dir = 0;
            if (_input != null && Mathf.Abs(_input.Horizontal) > 0.01f)
                dir = _input.Horizontal > 0f ? 1 : -1;
            else if (_facing != 0)
                dir = _facing;

            if (TryGetWall(dir, out Vector2 normal))
            {
                _wallNormal = normal;
                _wallDirection = dir;
                _lastWallContactTime = Time.time;
                _wallCoyoteTimer = _wallCoyoteTime;

                bool pressingIntoWall = _input != null && Mathf.Abs(_input.Horizontal) > 0.01f;
                if (pressingIntoWall && _rb.linearVelocity.y < 0f) _isWallSliding = true;
            }
        }

        private bool TryGetWall(int dir, out Vector2 normal)
        {
            normal = dir >= 0 ? Vector2.right : Vector2.left;
            if (dir == 0) return false;

            Bounds b = _col.bounds;
            Vector2 origin = new Vector2(b.center.x + dir * (b.extents.x - 0.02f), b.center.y);
            Vector2 size = new Vector2(0.1f, Mathf.Max(0.1f, b.size.y * 0.8f));
            int hits = Physics2D.OverlapBox(origin, size, 0f, _solidFilter, _overlaps);

            for (int i = 0; i < hits; i++)
            {
                Collider2D other = _overlaps[i];
                if (other == null || other == _col || other.isTrigger) continue;
                if (other.GetComponent<PlatformEffector2D>() != null) continue;
                if (other.GetComponentInParent<MovingPlatform2D>() != null) continue;

                Vector2 n = dir >= 0 ? Vector2.left : Vector2.right;
                ColliderDistance2D d = other.Distance(_col);
                if (d.isValid && !d.isOverlapped)
                {
                    Vector2 dn = d.normal;
                    if (Mathf.Abs(dn.x) > 0.5f) n = dn;
                }

                if (Mathf.Abs(Vector2.Dot(n, Vector2.up)) > _minGroundDot) continue; // it is a floor
                normal = n;
                return true;
            }

            return false;
        }

        private void UpdateTimers(float dt)
        {
            if (_coyoteTimer > 0f) _coyoteTimer -= dt;
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= dt;
            if (_wallCoyoteTimer > 0f) _wallCoyoteTimer -= dt;
            if (_controlLockTimer > 0f) _controlLockTimer -= dt;
            if (_dropThroughTimer > 0f) _dropThroughTimer -= dt;

            if (_ignoredOneWay != null)
            {
                _ignoredOneWayTimer -= dt;
                if (_ignoredOneWayTimer <= 0f)
                {
                    Physics2D.IgnoreCollision(_ignoredOneWay, _col, false);
                    _ignoredOneWay = null;
                }
            }
        }

        private void UpdateMovingPlatform(float dt)
        {
            if (_groundPlatform == null || !_isGrounded)
            {
                _platformVelocity = Vector2.zero;
                _hasPlatformReference = false;
                return;
            }

            Vector2 p = _groundPlatform.transform.position;
            if (!_hasPlatformReference)
            {
                _platformLastPosition = p;
                _hasPlatformReference = true;
                _platformVelocity = _groundPlatform.PlatformVelocity;
                return;
            }

            _platformVelocity = dt > 0f ? (p - _platformLastPosition) / dt : Vector2.zero;
            _platformLastPosition = p;

            // Carry the player with the platform without ever teleporting them:
            // blend the platform delta into the body velocity.
            Vector2 carry = _platformVelocity;
            Vector2 v = _rb.linearVelocity;
            _rb.linearVelocity = new Vector2(Mathf.Lerp(v.x, carry.x, 0.5f), Mathf.Max(v.y, carry.y));
        }

        // ------------------------------------------------------------------ motion
        private void ApplyHorizontalMovement(float inputX, float dt)
        {
            float vx = _rb.linearVelocity.x;
            float target = inputX * _maxRunSpeed;

            float accel;
            if (_isGrounded)
            {
                bool turning = Mathf.Abs(inputX) > 0.01f && Mathf.Sign(inputX) != Mathf.Sign(vx) && Mathf.Abs(vx) > 0.35f;
                accel = (Mathf.Abs(inputX) < 0.01f || Mathf.Abs(vx) > Mathf.Abs(target))
                    ? _groundDeceleration
                    : _groundAcceleration;
                if (turning) accel *= _turnAroundBoost;
            }
            else
            {
                bool noInput = Mathf.Abs(inputX) < 0.01f;
                if (noInput)
                {
                    // Never scrub air momentum when the player is not asking for anything:
                    // this is what lets a grapple swing, a rope release or an enemy knockback
                    // carry the character across the level.
                    accel = 0f;
                }
                else if (Mathf.Abs(vx) > Mathf.Abs(target) && Mathf.Sign(inputX) != Mathf.Sign(vx))
                {
                    accel = _airDeceleration;
                }
                else
                {
                    accel = _airAcceleration;
                }

                if (IsBeingPulled) accel *= _grappleAirControl;
            }

            float deltaV = Mathf.Clamp(target - vx, -accel * dt, accel * dt);

            Vector2 force = new Vector2(deltaV, 0f) * _rb.mass / dt;

            // On a slope, redirect the drive along the surface so running uphill keeps speed.
            if (_isGrounded && Mathf.Abs(_groundNormal.y) > 0.01f)
            {
                Vector2 tangent = new Vector2(_groundNormal.y, -_groundNormal.x).normalized;
                if (Mathf.Sign(tangent.x) != Mathf.Sign(_rb.linearVelocity.x) && Mathf.Abs(_rb.linearVelocity.x) > 0.1f)
                    tangent = -tangent;
                Vector2 along = Vector2.Dot(force, tangent) * tangent;
                force = Vector2.Lerp(force, along, 0.75f);
            }

            _rb.AddForce(force, ForceMode2D.Force);

            // The player capsule uses a frictionless physics material (so wall slides and rope
            // swings are not damped), which means slopes need an explicit grip force to stop
            // the character from creeping downhill while idle.
            if (_isGrounded && Mathf.Abs(inputX) < 0.01f)
            {
                float slopeAngle = Vector2.Angle(_groundNormal, Vector2.up);
                if (slopeAngle > 0.5f)
                {
                    Vector2 tangent = new Vector2(_groundNormal.y, -_groundNormal.x).normalized;
                    Vector2 gravityAccel = Vector2.down * CurrentGravity;
                    float tangential = Vector2.Dot(gravityAccel, tangent);
                    _rb.AddForce(-tangent * (tangential * _rb.mass), ForceMode2D.Force);
                }
            }

            if (_controlLockTimer > 0f && Mathf.Abs(inputX) > 0.01f)
            {
                // During a wall-jump lockout we simply do not fight the existing velocity.
            }
        }

        private void ApplyGravity(float dt, bool holdingDirection)
        {
            float vy = _rb.linearVelocity.y;
            float gravity;

            if (_isWallSliding)
            {
                gravity = _fallGravity * 0.35f;
            }
            else if (vy > 0.01f)
            {
                gravity = _riseGravity;
                if (_jumpCutRequested && !(_input != null && _input.JumpHeld))
                    gravity *= _jumpCutGravity;
            }
            else if (Mathf.Abs(vy) <= _apexVelocityWindow)
            {
                gravity = _apexGravity;
            }
            else
            {
                gravity = _fallGravity;
            }

            if (_grapple != null && _grapple.IsAttachedToWall && vy < 0f && !_isGrounded)
            {
                // Slightly heavier while hanging on the rope: keeps pendulum swings tight.
                gravity *= 1.1f;
            }

            _appliedGravity = gravity;
            _rb.AddForce(Vector2.down * (gravity * _rb.mass), ForceMode2D.Force);

            if (_jumpCutRequested)
            {
                Vector2 v = _rb.linearVelocity;
                if (v.y > 0f)
                {
                    float cut = _input != null && _input.JumpHeld ? 1f : 0.45f;
                    _rb.linearVelocity = new Vector2(v.x, v.y * cut);
                }
                _jumpCutRequested = false;
                _jumpCutArmed = false;
            }
        }

        private void ApplyGroundStick(float dt)
        {
            if (!_isGrounded) return;
            if (_rb.linearVelocity.y > 0.1f) return;
            _rb.AddForce(Vector2.down * (_groundStickForce * _rb.mass), ForceMode2D.Force);
        }

        private void ClampFallSpeed()
        {
            Vector2 v = _rb.linearVelocity;
            float maxFall = _isWallSliding ? _wallSlideSpeed : _maxFallSpeed;
            if (v.y < -maxFall) _rb.linearVelocity = new Vector2(v.x, -maxFall);
        }

        private void HandleJump(float dt, float inputX)
        {
            bool bufferReady = _jumpBufferTimer > 0f;
            if (!bufferReady) return;

            // 1. Normal / coyote jump.
            if (_isGrounded || _coyoteTimer > 0f)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, Mathf.Sqrt(2f * _riseGravity * _jumpHeight));
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
                _isGrounded = false;
                _jumpCutArmed = true;
                FxManager.LandingDust(transform.position, 5, 0.7f);
                return;
            }

            // 2. Wall jump.
            bool canWallJump = (_isWallSliding || _wallCoyoteTimer > 0f) && _wallDirection != 0;
            if (canWallJump && Time.time - _lastWallJumpTime > _wallJumpSameWallLock)
            {
                float away = -_wallDirection;
                _rb.linearVelocity = new Vector2(away * _wallJumpVelocity.x, _wallJumpVelocity.y);
                _jumpBufferTimer = 0f;
                _wallCoyoteTimer = 0f;
                _controlLockTimer = _wallJumpControlLock;
                _lastWallJumpTime = Time.time;
                _facing = (int)Mathf.Sign(away);
                _jumpCutArmed = true;
                FxManager.LandingDust(transform.position, 6, 1.1f);
            }
        }

        private void HandleOneWayDrop()
        {
            if (_input == null || !_input.DownHeld || !_input.JumpDown) return;
            if (!_isGrounded || _groundCollider == null) return;

            PlatformEffector2D effector = _groundCollider.GetComponent<PlatformEffector2D>();
            if (effector == null) return;

            if (_ignoredOneWay != null) Physics2D.IgnoreCollision(_ignoredOneWay, _col, false);
            _ignoredOneWay = _groundCollider;
            _ignoredOneWayTimer = _dropThroughTime;
            Physics2D.IgnoreCollision(_ignoredOneWay, _col, true);

            _isGrounded = false;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -1f);
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>True while the player is allowed to take control back (not wall-jump locked).</summary>
        public bool HasControl => _controlLockTimer <= 0f;

        /// <summary>Snaps the body to a position, clearing velocity. Used by respawn.</summary>
        public void Teleport(Vector2 position)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.position = position;
            transform.position = position;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            _wallCoyoteTimer = 0f;
            _controlLockTimer = 0f;
        }

        /// <summary>Called by the health component: a short loss of control after being hit.</summary>
        public void ApplyHitStun(float seconds) => _controlLockTimer = Mathf.Max(_controlLockTimer, seconds);
    }
}
