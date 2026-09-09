using System.Collections.Generic;
using UnityEngine;

namespace TanLuZhe
{
    public enum GrappleState
    {
        Idle = 0,
        Extending = 1,
        Attached = 2,
        Retracting = 3,
    }

    /// <summary>
    /// Mouse aimed grappling hook.
    ///
    /// Behaviour
    /// ---------
    /// * <b>E</b> fires the hook from the muzzle toward the pointer.
    /// * Hitting <b>world geometry</b> latches the rope to the impact point and winches the
    ///   player in. The rope is solved as a distance constraint: the radial (outward) velocity
    ///   is removed while the tangential component is left completely untouched, so the player
    ///   keeps swinging with the momentum they already had - a real pendulum, not a teleport.
    /// * Hitting an <b>enemy</b> solves the exact same constraint on <i>both</i> bodies with
    ///   equal and opposite impulses (mass weighted), so the enemy is dragged toward the player
    ///   and the player is dragged toward the enemy. Total momentum is conserved.
    /// * <b>Left Ctrl</b> cuts the rope. The rope simply stops existing, so the velocity the
    ///   player had at that instant is preserved bit-for-bit: momentum is kept, the pull stops.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("TanLuZhe/Grapple Hook 2D")]
    public sealed class GrappleHook2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _origin;
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private Sprite _hookSprite;
        [SerializeField] private Sprite _sparkSprite;

        [Header("Launch")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [Tooltip("How fast the hook head flies out toward the cursor (m/s).")]
        [Range(10f, 120f)] [SerializeField] private float _hookSpeed = 48f;
        [Tooltip("Maximum reach of the hook (world units).")]
        [Range(2f, 40f)] [SerializeField] private float _maxRange = 16f;
        [Tooltip("Thickness of the hook's sweep test; a bigger value cannot slip through corners.")]
        [Range(0.01f, 0.5f)] [SerializeField] private float _hookRadius = 0.08f;
        [Tooltip("How fast a released / missed hook flies back to the player (m/s).")]
        [Range(10f, 200f)] [SerializeField] private float _retractSpeed = 70f;

        [Header("Pull")]
        [Tooltip("Constant acceleration applied to the player toward the hook's landing point (m/s^2). " +
                 "Applied every physics step while hooked, so the pull builds speed instead of snapping. " +
                 "Must comfortably exceed gravity or a grounded player cannot be lifted.")]
        [Range(20f, 400f)] [SerializeField] private float _pullAcceleration = 140f;
        [Tooltip("Speed cap for the pull so the player cannot accelerate forever.")]
        [Range(5f, 60f)] [SerializeField] private float _maxPullSpeed = 30f;
        [Tooltip("The hook detaches automatically once the player gets this close to the landing point.")]
        [Range(0.1f, 4f)] [SerializeField] private float _releaseDistance = 1f;
        [Tooltip("1 = inextensible rope (outward velocity fully cancelled). <1 = stretchy rope.")]
        [Range(0f, 1f)] [SerializeField] private float _ropeGrip = 1f;
        [Tooltip("Position error correction, in 1/s. Keeps the rope taut when a frame overshoots.")]
        [Range(0f, 40f)] [SerializeField] private float _positionCorrection = 8f;
        [Tooltip("Rope snaps if stretched beyond this multiple of its rest length.")]
        [Range(1f, 5f)] [SerializeField] private float _breakStretch = 2.2f;

        [Header("Enemy hook")]
        [Tooltip("While hooked onto an enemy the head is pinned onto it; the rope lets go as soon as the " +
                 "player's body touches that enemy.")]
        [Range(0f, 1f)] [SerializeField] private float _contactReleaseDistance = 0.08f;

        [Header("Combat")]
        [Tooltip("Damage dealt the instant the hook latches onto an enemy.")]
        [Range(0f, 100f)] [SerializeField] private float _attachDamage = 14f;
        [Tooltip("Knockback applied to the enemy at the moment of the hook impact.")]
        [Range(0f, 30f)] [SerializeField] private float _enemyHitKnockback = 4f;

        [Header("Debug")]
        [SerializeField] private bool _drawDebug;

        // ------------------------------------------------------------------ runtime
        private Rigidbody2D _playerBody;
        private PlayerController2D _player;
        private IInputSource _input;
        private GrappleRopeRenderer _rope;

        private GameObject _headVisual;
        private SpriteRenderer _headRenderer;

        private GrappleState _state = GrappleState.Idle;
        private GrappleAnchorType _anchorType = GrappleAnchorType.None;

        private Vector2 _direction = Vector2.right;
        private Vector2 _head;
        private float _travelled;

        private Vector2 _anchorPoint;
        private Vector2 _anchorLocalOffset;
        private Rigidbody2D _anchorBody;
        private Collider2D _anchorCollider;
        private IGrappleTarget _anchorTarget;
        private float _ropeLength;
        private float _currentPullSpeed;

        private Collider2D _playerCollider;

        private readonly List<RaycastHit2D> _hits = new List<RaycastHit2D>(8);
        private ContactFilter2D _hitFilter;

        // ------------------------------------------------------------------ public API
        public GrappleState State => _state;
        public GrappleAnchorType AnchorType => _anchorType;
        public bool IsAttached => _state == GrappleState.Attached;
        public bool IsAttachedToWall => _state == GrappleState.Attached && _anchorType == GrappleAnchorType.Wall;
        public bool IsAttachedToEnemy => _state == GrappleState.Attached &&
                                         (_anchorType == GrappleAnchorType.Enemy || _anchorType == GrappleAnchorType.Moving);
        public Vector2 HeadPosition => _head;
        public Vector2 OriginPosition => _origin != null ? (Vector2)_origin.position : (Vector2)transform.position;
        public Vector2 AnchorPoint => _state == GrappleState.Attached ? CurrentAnchorWorld : _head;
        public float RopeLength => _ropeLength;

        /// <summary>How fast the player is currently being pulled toward the anchor (m/s).</summary>
        public float CurrentPullSpeed => _currentPullSpeed;

        /// <summary>Read-only mirrors of the tuning values, for the inspector readout.</summary>
        public float PullAcceleration => _pullAcceleration;
        public float MaxPullSpeed => _maxPullSpeed;
        public float ReleaseDistance => _releaseDistance;

        public float MaxRange => _maxRange;
        public IGrappleTarget AnchorTarget => _anchorTarget;

        /// <summary>World position the rope is currently latched to.</summary>
        public Vector2 CurrentAnchorWorld
        {
            get
            {
                if (_state != GrappleState.Attached) return _head;
                if (_anchorBody != null) return _anchorBody.transform.TransformPoint(_anchorLocalOffset);
                return _anchorPoint;
            }
        }

        /// <summary>True when the rope is taut enough that it is currently applying force.</summary>
        public bool IsRopeTaut
        {
            get
            {
                if (_state != GrappleState.Attached) return false;
                float d = Vector2.Distance(OriginPosition, CurrentAnchorWorld);
                return d >= _ropeLength - 0.02f;
            }
        }

        public void SetInputSource(IInputSource source) => _input = source;
        public void SetAimCamera(Camera cam) => _aimCamera = cam;

        // ------------------------------------------------------------------ unity
        private void Awake()
        {
            _playerBody = GetComponentInParent<Rigidbody2D>();
            _player = GetComponentInParent<PlayerController2D>();
            if (_playerBody != null) _playerCollider = _playerBody.GetComponent<Collider2D>();
            _rope = GetComponent<GrappleRopeRenderer>();
            if (_rope == null) _rope = gameObject.AddComponent<GrappleRopeRenderer>();

            _hitFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = _hitMask,
            };

            if (_origin == null)
            {
                Transform t = transform.Find("GrappleOrigin");
                if (t == null)
                {
                    GameObject go = new GameObject("GrappleOrigin");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                    t = go.transform;
                }
                _origin = t;
            }

            CreateHeadVisual();

            _head = OriginPosition;
        }

        private void CreateHeadVisual()
        {
            _headVisual = new GameObject("HookHead");
            _headVisual.transform.SetParent(transform.parent != null ? transform.parent : transform, true);
            _headRenderer = _headVisual.AddComponent<SpriteRenderer>();
            _headRenderer.sprite = _hookSprite;
            _headRenderer.color = new Color(0.72f, 0.96f, 1f, 1f);
            _headRenderer.sortingOrder = 40;
            _headVisual.SetActive(false);
        }

        private void Start()
        {
            if (_input == null)
            {
                _input = GetComponentInParent<IInputSource>();
                if (_input == null) _input = FindFirstObjectByType<PlayerInputSource>();
            }

            if (_aimCamera == null) _aimCamera = Camera.main;
        }

        private void Update()
        {
            if (_input != null)
            {
                if (_input.GrappleDown) OnGrapplePressed();
                if (_input.ReleaseHeld && _state == GrappleState.Attached) Release();
            }

            UpdateHeadVisual();
        }

        private void OnGrapplePressed()
        {
            if (_state == GrappleState.Attached)
            {
                // Pressing the grapple key again while attached simply re-fires after release.
                return;
            }

            Fire(GetAimDirection());
        }

        private Vector2 GetAimDirection()
        {
            if (_input == null) return Vector2.right;

            Vector2 screen = _input.PointerScreen;
            Camera cam = _aimCamera != null ? _aimCamera : Camera.main;
            if (cam == null) return _player != null ? Vector2.right * _player.FacingSign : Vector2.right;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            Vector2 dir = (Vector2)world - OriginPosition;
            if (dir.sqrMagnitude < 0.0004f) return _player != null ? Vector2.right * _player.FacingSign : Vector2.right;
            return dir.normalized;
        }

        /// <summary>Fires the hook toward a world-space point. Public so gameplay code and tests can drive it.</summary>
        public bool FireAt(Vector2 worldPoint)
        {
            Vector2 dir = worldPoint - OriginPosition;
            if (dir.sqrMagnitude < 1e-6f) return false;
            return Fire(dir.normalized);
        }

        /// <summary>Fires the hook along a direction. Returns false when the hook could not launch.</summary>
        public bool Fire(Vector2 direction)
        {
            if (_state == GrappleState.Extending || _state == GrappleState.Attached) return false;
            if (direction.sqrMagnitude < 1e-6f) return false;

            DetachInternal(false);

            _direction = direction.normalized;
            _head = OriginPosition;
            _travelled = 0f;
            _state = GrappleState.Extending;
            _anchorType = GrappleAnchorType.None;

            _headVisual.SetActive(true);
            FxManager.Sparks(_head, 4, 2.5f, new Color(0.6f, 0.95f, 1f));
            return true;
        }

        /// <summary>
        /// Cuts the rope. The rigidbody velocity is deliberately left untouched, which is what
        /// "keep the inertia, lose the pull" means in practice.
        /// </summary>
        public void Release()
        {
            if (_state == GrappleState.Idle) return;

            Vector2 at = _state == GrappleState.Attached ? CurrentAnchorWorld : _head;
            DetachInternal(true);
            FxManager.Sparks(at, 6, 3f, new Color(0.65f, 1f, 0.85f));
        }

        // ------------------------------------------------------------------ fixed step
        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            switch (_state)
            {
                case GrappleState.Extending:
                    StepExtending(dt);
                    break;
                case GrappleState.Attached:
                    StepAttached(dt);
                    break;
                case GrappleState.Retracting:
                    StepRetracting(dt);
                    break;
            }

            if (_drawDebug && _state != GrappleState.Idle)
                Debug.DrawLine(OriginPosition, _head, Color.cyan);
        }

        private void StepExtending(float dt)
        {
            float step = _hookSpeed * dt;
            if (_travelled + step > _maxRange) step = _maxRange - _travelled;

            if (step <= 0f)
            {
                BeginRetract();
                return;
            }

            int count = Physics2D.CircleCast(_head, _hookRadius, _direction, _hitFilter, _hits, step);
            RaycastHit2D best = default;
            float bestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D h = _hits[i];
                if (h.collider == null || h.collider.isTrigger) continue;
                if (IsOwnCollider(h.collider)) continue;
                if (h.distance >= bestDist) continue;
                best = h;
                bestDist = h.distance;
                found = true;
            }

            if (found)
            {
                _head = best.point;
                _travelled += bestDist;
                AttachTo(best);
                return;
            }

            _head += _direction * step;
            _travelled += step;
        }

        /// <summary>
        /// True when the collider belongs to the shooter. A hierarchy-root comparison is not
        /// enough here: level geometry, enemies and the player can all live under the same
        /// root object, which would make the hook fly straight through its targets.
        /// </summary>
        private bool IsOwnCollider(Collider2D other)
        {
            if (other == null) return true;
            if (_playerBody != null && other.attachedRigidbody == _playerBody) return true;
            if (_player != null && other.transform.IsChildOf(_player.transform)) return true;
            return false;
        }

        private void AttachTo(RaycastHit2D hit)
        {
            IGrappleTarget target = hit.collider.GetComponentInParent<IGrappleTarget>();
            Rigidbody2D body = target != null ? target.GetGrappleBody() : null;

            if (target != null && body != null && body.bodyType != RigidbodyType2D.Static)
            {
                _anchorType = body.bodyType == RigidbodyType2D.Kinematic
                    ? GrappleAnchorType.Moving
                    : GrappleAnchorType.Enemy;
                _anchorTarget = target;
                _anchorBody = body;
                _anchorPoint = target.GetGrappleAnchorPoint(hit.point);
                _anchorLocalOffset = body.transform.InverseTransformPoint(_anchorPoint);
            }
            else
            {
                _anchorType = GrappleAnchorType.Wall;
                _anchorTarget = null;
                _anchorBody = null;
                _anchorPoint = hit.point;
            }

            _anchorCollider = hit.collider;
            _head = CurrentAnchorWorld;
            _ropeLength = Mathf.Max(0.05f, Vector2.Distance(OriginPosition, CurrentAnchorWorld));
            _currentPullSpeed = 0f;
            _state = GrappleState.Attached;

            if (_anchorTarget != null)
            {
                Vector2 dir = ((Vector2)_playerBody.position - CurrentAnchorWorld).normalized;
                DamageInfo info = new DamageInfo(
                    _attachDamage,
                    OriginPosition,
                    dir * _enemyHitKnockback,
                    _playerBody.gameObject);
                _anchorTarget.OnGrappleAttached(CurrentAnchorWorld, info);
            }

            if (_player != null) _player.SetBeingPulled(true);
            FxManager.Sparks(CurrentAnchorWorld, 10, 5f, new Color(0.7f, 0.98f, 1f));
        }

        private void StepAttached(float dt)
        {
            if (_playerBody == null)
            {
                Release();
                return;
            }

            // Target vanished (enemy died / platform destroyed).
            if (_anchorTarget != null && _anchorBody == null)
            {
                Release();
                return;
            }

            // While hooked the head rides along with whatever it latched onto, so a hooked
            // enemy literally drags the hook with it (and the rope end stays glued to it).
            _head = CurrentAnchorWorld;

            if (_anchorBody == null) SolveStaticAnchor(dt);
            else SolveTwoBodyAnchor(dt);

            if (_state != GrappleState.Attached) return;   // a solver may have released us

            if (_player != null) _player.SetBeingPulled(true);

            // Safety: rope snapped because something exploded the distance.
            float dist = Vector2.Distance(OriginPosition, CurrentAnchorWorld);
            if (dist > _ropeLength * _breakStretch + 1f) Release();
        }

        /// <summary>
        /// Rope latched to level geometry. Every physics step a constant acceleration is applied
        /// toward the landing point; the tangential velocity is never touched, so the player keeps
        /// whatever momentum they had and simply gets pulled in. The hook lets go by itself once
        /// the player arrives at the landing point.
        /// </summary>
        private void SolveStaticAnchor(float dt)
        {
            Vector2 origin = OriginPosition;
            Vector2 delta = _anchorPoint - origin;
            float dist = delta.magnitude;
            if (dist < 1e-4f)
            {
                Release();
                return;
            }

            Vector2 toward = delta / dist;      // player -> landing point
            Vector2 v = _playerBody.linearVelocity;

            // 1) continuous pull: acceleration toward the anchor, capped at _maxPullSpeed.
            float radial = Vector2.Dot(v, toward);
            if (radial < _maxPullSpeed)
            {
                float add = Mathf.Min(_pullAcceleration * dt, _maxPullSpeed - radial);
                v += toward * add;
            }

            // 2) the rope may not stretch: only the outward radial component is removed, so the
            //    tangential component (the swing / the inertia) survives untouched.
            if (dist >= _ropeLength - 1e-4f)
            {
                float outward = Vector2.Dot(v, toward);
                if (outward < 0f) v -= toward * (outward * _ropeGrip);
            }

            if (dist > _ropeLength)
            {
                float error = dist - _ropeLength;
                v += toward * (error * _positionCorrection);
            }

            _playerBody.linearVelocity = v;
            _currentPullSpeed = Mathf.Max(0f, Vector2.Dot(v, toward));

            // 3) arrived at the landing point -> detach automatically, keeping the momentum.
            if (dist <= _releaseDistance) Release();
        }

        /// <summary>
        /// Rope latched to a dynamic body: the same continuous pull is applied to both bodies as
        /// equal and opposite, mass weighted impulses, so the enemy is dragged toward the player
        /// and the player is dragged toward the enemy with total momentum conserved.
        /// </summary>
        private void SolveTwoBodyAnchor(float dt)
        {
            Vector2 origin = OriginPosition;
            Vector2 anchor = CurrentAnchorWorld;
            Vector2 delta = anchor - origin;
            float dist = delta.magnitude;
            Vector2 toward = dist > 1e-4f ? delta / dist : Vector2.zero; // player -> anchor

            float invP = _playerBody.bodyType == RigidbodyType2D.Dynamic ? 1f / Mathf.Max(0.0001f, _playerBody.mass) : 0f;
            float invE = _anchorBody.bodyType == RigidbodyType2D.Dynamic ? 1f / Mathf.Max(0.0001f, _anchorBody.mass) : 0f;
            float invSum = invP + invE;
            if (invSum <= 0f) return;

            Vector2 vp = _playerBody.linearVelocity;
            Vector2 ve = _anchorBody.linearVelocity;

            // 1) continuous pull toward each other (positive = already closing).
            float closing = Vector2.Dot(vp - ve, toward);
            if (closing < _maxPullSpeed)
            {
                float add = Mathf.Min(_pullAcceleration * dt, _maxPullSpeed - closing);
                float impulse = add / invSum;
                vp += toward * (impulse * invP);
                ve -= toward * (impulse * invE);
            }

            // 2) the rope may not stretch: remove the separating relative velocity.
            if (dist >= _ropeLength - 1e-4f)
            {
                float relative = Vector2.Dot(ve - vp, toward);
                if (relative > 0f)
                {
                    float impulse = (relative * _ropeGrip) / invSum;
                    vp += toward * (impulse * invP);
                    ve -= toward * (impulse * invE);
                }
            }

            if (dist > _ropeLength)
            {
                float error = dist - _ropeLength;
                float impulse = (error * _positionCorrection) / invSum;
                vp += toward * (impulse * invP);
                ve -= toward * (impulse * invE);
            }

            _playerBody.linearVelocity = vp;
            _anchorBody.linearVelocity = ve;
            _currentPullSpeed = Mathf.Max(0f, closing);

            // 3) the player touched the hooked enemy -> let go of the rope.
            if (_anchorType == GrappleAnchorType.Enemy && IsTouchingAnchor()) Release();
        }

        /// <summary>True once the player's body is in contact with the hooked collider.</summary>
        private bool IsTouchingAnchor()
        {
            if (_playerCollider == null || _anchorCollider == null) return false;

            ColliderDistance2D distance = _playerCollider.Distance(_anchorCollider);
            if (!distance.isValid) return false;
            return distance.isOverlapped || distance.distance <= _contactReleaseDistance;
        }

        private void StepRetracting(float dt)
        {
            Vector2 origin = OriginPosition;
            _head = Vector2.MoveTowards(_head, origin, _retractSpeed * dt);
            if (Vector2.SqrMagnitude(_head - origin) < 0.0025f)
            {
                _head = origin;
                _state = GrappleState.Idle;
                _anchorType = GrappleAnchorType.None;
                _headVisual.SetActive(false);
            }
        }

        private void BeginRetract()
        {
            if (_anchorTarget != null) _anchorTarget.OnGrappleDetached();
            _anchorTarget = null;
            _anchorBody = null;
            _anchorCollider = null;
            _anchorType = GrappleAnchorType.None;
            _state = GrappleState.Retracting;
            if (_player != null) _player.SetBeingPulled(false);
        }

        private void DetachInternal(bool retract)
        {
            if (_anchorTarget != null) _anchorTarget.OnGrappleDetached();
            _anchorTarget = null;
            _anchorBody = null;
            _anchorCollider = null;
            _anchorType = GrappleAnchorType.None;
            _currentPullSpeed = 0f;
            if (_player != null) _player.SetBeingPulled(false);

            if (retract)
            {
                _state = GrappleState.Retracting;
            }
            else
            {
                _state = GrappleState.Idle;
                _head = OriginPosition;
                if (_headVisual != null) _headVisual.SetActive(false);
            }
        }

        private void UpdateHeadVisual()
        {
            if (_headVisual == null) return;

            bool visible = _state != GrappleState.Idle;
            if (_headVisual.activeSelf != visible) _headVisual.SetActive(visible);
            if (!visible) return;

            // While attached the head is pinned to the anchor every frame (not only every
            // physics step) so a hooked enemy can never visually slip away from the hook.
            if (_state == GrappleState.Attached) _head = CurrentAnchorWorld;

            _headVisual.transform.position = new Vector3(_head.x, _head.y, 0f);

            // Attached: the hook points along the rope (so it visibly stays "stuck" to a moving
            // enemy). Extending: it points along the flight direction.
            Vector2 aim = _state == GrappleState.Attached
                ? _head - OriginPosition
                : _direction;
            if (aim.sqrMagnitude < 1e-6f) aim = _direction;

            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            _headVisual.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnDestroy()
        {
            if (_anchorTarget != null) _anchorTarget.OnGrappleDetached();
        }
    }
}
