using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Patrolling / chasing enemy with ledge and wall awareness. Implements
    /// <see cref="IGrappleTarget"/> so the hook can latch onto it and drag it around.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [AddComponentMenu("TanLuZhe/Enemy Controller 2D")]
    public sealed class EnemyController2D : MonoBehaviour, IDamageable, IGrappleTarget
    {
        [Header("Health")]
        [Tooltip("Hit points. The hook deals damage on latch, a stomp deals 30.")]
        [Range(5f, 500f)] [SerializeField] private float _maxHealth = 45f;
        [Tooltip("Upward speed the player gets from stomping this enemy.")]
        [Range(0f, 30f)] [SerializeField] private float _stompBounce = 13f;

        [Header("Movement")]
        [Tooltip("Walking speed while patrolling (m/s).")]
        [Range(0f, 20f)] [SerializeField] private float _patrolSpeed = 3f;
        [Tooltip("Walking speed while chasing the player (m/s).")]
        [Range(0f, 25f)] [SerializeField] private float _chaseSpeed = 4.8f;
        [Tooltip("How quickly the enemy reaches its target speed (m/s^2).")]
        [Range(1f, 200f)] [SerializeField] private float _acceleration = 34f;
        [Tooltip("Horizontal distance at which the enemy starts chasing.")]
        [Range(0f, 40f)] [SerializeField] private float _aggroRadius = 9f;
        [Tooltip("Vertical distance at which the enemy stops caring about the player.")]
        [Range(0f, 30f)] [SerializeField] private float _aggroHeight = 3.5f;
        [SerializeField] private LayerMask _groundMask = ~0;
        [Tooltip("How far ahead the enemy looks for a ledge before turning around.")]
        [Range(0.05f, 3f)] [SerializeField] private float _ledgeProbeDistance = 0.6f;
        [Tooltip("How far ahead the enemy looks for a wall before turning around.")]
        [Range(0.05f, 3f)] [SerializeField] private float _wallProbeDistance = 0.35f;

        [Header("Combat")]
        [Tooltip("Damage dealt by touching the player (unless the player is stomping).")]
        [Range(0f, 100f)] [SerializeField] private float _touchDamage = 18f;
        [SerializeField] private Vector2 _touchKnockback = new Vector2(9f, 10f);

        [Header("Grapple")]
        [Tooltip("Motor authority kept while a rope is attached. Lower = the hook wins the tug of war.")]
        [Range(0f, 1f)] [SerializeField] private float _grappleMotorFactor = 0.3f;
        [Tooltip("Stagger time right after the hook latches on.")]
        [Range(0f, 2f)] [SerializeField] private float _grappleStagger = 0.4f;

        [Header("Audio")]
        [Tooltip("Played every time this monster takes damage (weapon hit, grapple latch, stomp).")]
        [SerializeField] private AudioClip _hurtSound;
        [Range(0f, 1f)] [SerializeField] private float _hurtVolume = 0.85f;
        [Tooltip("Random pitch spread so a flurry of hits does not sound like one machine gun.")]
        [Range(0f, 0.5f)] [SerializeField] private float _hurtPitchJitter = 0.12f;
        [Tooltip("1 = fully positional 3D sound, 0 = flat 2D. A 2D platformer usually wants a low value.")]
        [Range(0f, 1f)] [SerializeField] private float _hurtSpatialBlend = 0.25f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer _visual;
        [SerializeField] private Sprite _idleSprite;
        [SerializeField] private Sprite _hurtSprite;
        [SerializeField] private Color _flashColor = Color.white;

        private Rigidbody2D _rb;
        private Collider2D _col;
        private AudioSource _audioSource;
        private PlayerController2D _player;
        private Transform _playerTransform;

        private float _health;
        private int _direction = 1;
        private bool _dead;
        private float _staggerTimer;
        private float _flashTimer;
        private Color _baseColor = Color.white;
        private bool _grappleAttached;

        private readonly RaycastHit2D[] _probes = new RaycastHit2D[4];
        private readonly Collider2D[] _overlaps = new Collider2D[4];

        public bool IsAlive => !_dead;
        public float Health => _health;
        public float GrappleResistance => 1f;
        public bool IsGrappled => _grappleAttached;
        public AudioClip HurtSound => _hurtSound;

        /// <summary>How many times the hurt sound has been fired. Handy for debugging and tests.</summary>
        public int HurtSoundPlays { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            _health = _maxHealth;

            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (_visual == null) _visual = GetComponentInChildren<SpriteRenderer>();
            if (_visual != null) _baseColor = _visual.color;
            if (_visual != null && _idleSprite == null) _idleSprite = _visual.sprite;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = _hurtSpatialBlend;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 3f;
            _audioSource.maxDistance = 22f;
        }

        private void Start()
        {
            _player = FindFirstObjectByType<PlayerController2D>();
            if (_player != null) _playerTransform = _player.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_staggerTimer > 0f) _staggerTimer -= dt;

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0f && _visual != null)
                {
                    _visual.sprite = _idleSprite;
                    _visual.color = _baseColor;
                }
            }
        }

        private void FixedUpdate()
        {
            if (_dead) return;

            float dt = Time.fixedDeltaTime;

            // Gravity is Unity's own for enemies: they are plain dynamic bodies.
            _rb.gravityScale = 1f;

            if (_staggerTimer > 0f)
            {
                ApplyMotor(0f, dt);
                return;
            }

            bool chasing = false;
            if (_playerTransform != null && _player != null && !_player.IsDead)
            {
                Vector2 toPlayer = (Vector2)_playerTransform.position - _rb.position;
                chasing = Mathf.Abs(toPlayer.x) < _aggroRadius && Mathf.Abs(toPlayer.y) < _aggroHeight;
                if (chasing)
                {
                    int want = toPlayer.x >= 0f ? 1 : -1;
                    if (want != _direction && HasGroundAhead(want) && !HasWallAhead(want)) _direction = want;
                    else if (want != _direction && Mathf.Abs(toPlayer.x) > 0.25f) _direction = want;
                }
            }

            if (!chasing)
            {
                if (!HasGroundAhead(_direction) || HasWallAhead(_direction))
                {
                    _direction = -_direction;
                    if (!HasGroundAhead(_direction)) _direction = 0;
                }
            }

            float speed = chasing ? _chaseSpeed : _patrolSpeed;
            if (_grappleAttached) speed *= _grappleMotorFactor;

            ApplyMotor(_direction * speed, dt);

            if (_visual != null)
            {
                Vector3 s = _visual.transform.localScale;
                if (_direction != 0) s.x = Mathf.Abs(s.x) * _direction;
                _visual.transform.localScale = s;
            }
        }

        private void ApplyMotor(float targetSpeed, float dt)
        {
            float vx = _rb.linearVelocity.x;
            float delta = Mathf.Clamp(targetSpeed - vx, -_acceleration * dt, _acceleration * dt);
            _rb.AddForce(new Vector2(delta, 0f) * _rb.mass / dt, ForceMode2D.Force);
        }

        private bool HasGroundAhead(int dir)
        {
            if (dir == 0) return true;
            Bounds b = _col.bounds;
            Vector2 origin = new Vector2(b.center.x + dir * (b.extents.x + 0.05f), b.min.y + 0.05f);
            int hits = Physics2D.RaycastNonAlloc(origin, Vector2.down, _probes, _ledgeProbeDistance, _groundMask);
            for (int i = 0; i < hits; i++)
            {
                Collider2D c = _probes[i].collider;
                if (c == null || c == _col || c.isTrigger) continue;
                if (_probes[i].normal.y > 0.5f) return true;
            }
            return false;
        }

        private bool HasWallAhead(int dir)
        {
            if (dir == 0) return false;
            Bounds b = _col.bounds;
            Vector2 origin = new Vector2(b.center.x, b.center.y);
            int hits = Physics2D.RaycastNonAlloc(origin, dir > 0 ? Vector2.right : Vector2.left, _probes,
                b.extents.x + _wallProbeDistance, _groundMask);
            for (int i = 0; i < hits; i++)
            {
                Collider2D c = _probes[i].collider;
                if (c == null || c == _col || c.isTrigger) continue;
                if (Mathf.Abs(_probes[i].normal.x) > 0.6f) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ damage
        public void TakeDamage(in DamageInfo info)
        {
            if (_dead) return;

            _health -= info.Amount;
            _flashTimer = 0.12f;
            _staggerTimer = Mathf.Max(_staggerTimer, 0.18f);

            if (_visual != null)
            {
                if (_hurtSprite != null) _visual.sprite = _hurtSprite;
                _visual.color = _flashColor;
            }

            if (info.Knockback.sqrMagnitude > 0.0001f && _rb != null)
            {
                _rb.linearVelocity = new Vector2(info.Knockback.x, Mathf.Max(info.Knockback.y, _rb.linearVelocity.y));
            }

            FxManager.Sparks(transform.position, 8, 5f, new Color(1f, 0.5f, 0.7f));
            CameraRig2D.Shake(0.25f, 0.15f);
            PlayHurtSound();

            if (_health <= 0f) Die();
        }

        /// <summary>Fires the impact sound. Called for every damage source: weapons, hook, stomp.</summary>
        private void PlayHurtSound()
        {
            if (_hurtSound == null || _audioSource == null) return;
            if (_hurtVolume <= 0f) return;

            float jitter = Mathf.Max(0f, _hurtPitchJitter);
            _audioSource.pitch = jitter > 0f ? Random.Range(1f - jitter, 1f + jitter) : 1f;
            _audioSource.PlayOneShot(_hurtSound, _hurtVolume);
            HurtSoundPlays++;
        }

        private void Die()
        {
            _dead = true;
            _grappleAttached = false;
            FxManager.Sparks(transform.position, 20, 8f, new Color(1f, 0.45f, 0.55f));
            CameraRig2D.Shake(0.5f, 0.25f);

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.bodyType = RigidbodyType2D.Kinematic;
            }
            if (_col != null) _col.enabled = false;

            if (_visual != null)
            {
                Color c = _visual.color;
                c.a = 0.25f;
                _visual.color = c;
            }

            GameManager.Instance?.ReportEnemyDefeated(this);
            Destroy(gameObject, 0.35f);
        }

        // ------------------------------------------------------------------ collisions
        private void OnCollisionEnter2D(Collision2D collision) => HandlePlayerContact(collision);
        private void OnCollisionStay2D(Collision2D collision) => HandlePlayerContact(collision);

        private void HandlePlayerContact(Collision2D collision)
        {
            if (_dead) return;

            PlayerHealth health = collision.collider.GetComponentInParent<PlayerHealth>();
            if (health == null || !health.IsAlive) return;

            PlayerController2D controller = collision.collider.GetComponentInParent<PlayerController2D>();
            float feetY = controller != null ? controller.Collider.bounds.min.y : collision.transform.position.y;
            bool fromAbove = feetY > _col.bounds.max.y - 0.25f;

            if (fromAbove && controller != null && controller.Velocity.y <= 0.5f)
            {
                // Stomp: hurt the enemy, bounce the player.
                TakeDamage(new DamageInfo(30f, transform.position, new Vector2(0f, 6f), controller.gameObject));
                controller.Body.linearVelocity = new Vector2(controller.Velocity.x, _stompBounce);
                return;
            }

            Vector2 away = ((Vector2)health.transform.position - _rb.position);
            if (Mathf.Abs(away.x) < 0.05f) away.x = -_direction;
            health.TakeDamage(new DamageInfo(_touchDamage, _rb.position,
                new Vector2(Mathf.Sign(away.x) * _touchKnockback.x, _touchKnockback.y), gameObject));
        }

        // ------------------------------------------------------------------ grapple
        public Vector2 GetGrappleAnchorPoint(Vector2 hitPoint)
        {
            Bounds b = _col != null ? _col.bounds : new Bounds(transform.position, Vector3.one);
            return new Vector2(Mathf.Clamp(hitPoint.x, b.min.x, b.max.x), Mathf.Clamp(hitPoint.y, b.min.y, b.max.y));
        }

        public Rigidbody2D GetGrappleBody() => _rb;

        public void OnGrappleAttached(Vector2 anchorPoint, in DamageInfo impact)
        {
            _grappleAttached = true;
            _staggerTimer = Mathf.Max(_staggerTimer, _grappleStagger);
            TakeDamage(impact);
        }

        public void OnGrappleDetached()
        {
            _grappleAttached = false;
        }
    }
}
