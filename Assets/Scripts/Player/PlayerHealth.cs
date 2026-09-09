using System;
using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Player health, invulnerability frames, knockback and respawn.
    /// </summary>
    [AddComponentMenu("TanLuZhe/Player Health")]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _invulnerableTime = 1.1f;
        [SerializeField] private float _hitStun = 0.22f;

        [Header("Reaction")]
        [SerializeField] private Vector2 _knockback = new Vector2(7.5f, 12f);
        [SerializeField] private float _deathDelay = 0.9f;
        [SerializeField] private float _voidY = -45f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer _visual;
        [SerializeField] private Color _flashColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private float _flashInterval = 0.09f;

        private PlayerController2D _controller;
        private Rigidbody2D _rb;
        private float _health;
        private float _invulnerableTimer;
        private float _flashTimer;
        private bool _flashOn;
        private float _deathTimer;
        private bool _dead;
        private Color _baseColor = Color.white;

        public float Health => _health;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => !_dead;
        public bool IsInvulnerable => _invulnerableTimer > 0f;
        public Vector2 RespawnPoint { get; set; }
        public int DeathCount { get; private set; }

        public event Action<float, float> HealthChanged;
        public event Action<DamageInfo> Damaged;
        public event Action Respawned;

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _rb = GetComponent<Rigidbody2D>();
            _health = _maxHealth;
            if (_visual != null) _baseColor = _visual.color;
            RespawnPoint = transform.position;
        }

        private void Start()
        {
            HealthChanged?.Invoke(_health, _maxHealth);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_invulnerableTimer > 0f)
            {
                _invulnerableTimer -= dt;
                if (_visual != null)
                {
                    _flashTimer -= dt;
                    if (_flashTimer <= 0f)
                    {
                        _flashTimer = _flashInterval;
                        _flashOn = !_flashOn;
                        _visual.color = _flashOn ? _flashColor : _baseColor;
                    }
                }
            }
            else if (_visual != null && _visual.color != _baseColor)
            {
                _visual.color = _baseColor;
                _flashOn = false;
            }

            if (!_dead && transform.position.y < _voidY) Kill();

            if (_dead && _deathTimer > 0f)
            {
                _deathTimer -= dt;
                if (_deathTimer <= 0f) DoRespawn();
            }
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (_dead) return;
            if (_invulnerableTimer > 0f) return;

            _health = Mathf.Max(0f, _health - info.Amount);
            _invulnerableTimer = _invulnerableTime;
            _flashTimer = 0f;

            HealthChanged?.Invoke(_health, _maxHealth);
            Damaged?.Invoke(info);

            if (_rb != null && info.Knockback.sqrMagnitude > 0.0001f)
            {
                float dir = info.Knockback.x >= 0f ? 1f : -1f;
                Vector2 kb = new Vector2(Mathf.Abs(info.Knockback.x) * dir, info.Knockback.y);
                _rb.linearVelocity = kb;
            }

            if (_controller != null) _controller.ApplyHitStun(_hitStun);
            CameraRig2D.Shake(0.45f, 0.22f);
            FxManager.Sparks(transform.position, 8, 5f, new Color(1f, 0.45f, 0.45f));

            if (_health <= 0f) Kill();
        }

        public void Heal(float amount)
        {
            if (_dead) return;
            _health = Mathf.Min(_maxHealth, _health + amount);
            HealthChanged?.Invoke(_health, _maxHealth);
        }

        /// <summary>Instant death (spikes, pits, crushing).</summary>
        public void Kill()
        {
            if (_dead) return;
            _dead = true;
            _health = 0f;
            DeathCount++;
            _deathTimer = _deathDelay;
            _invulnerableTimer = 0f;

            HealthChanged?.Invoke(_health, _maxHealth);
            FxManager.Sparks(transform.position, 22, 8f, new Color(1f, 0.6f, 0.4f));
            CameraRig2D.Shake(0.8f, 0.35f);

            if (_visual != null)
            {
                Color c = _baseColor;
                c.a = 0.35f;
                _visual.color = c;
            }

            if (_controller != null) _controller.SetBeingPulled(false);
        }

        private void DoRespawn()
        {
            _dead = false;
            _health = _maxHealth;
            _invulnerableTimer = _invulnerableTime;
            _deathTimer = 0f;

            if (_controller != null) _controller.Teleport(RespawnPoint);
            else transform.position = RespawnPoint;

            if (_visual != null) _visual.color = _baseColor;

            HealthChanged?.Invoke(_health, _maxHealth);
            Respawned?.Invoke();
            FxManager.Sparks(RespawnPoint, 12, 4f, new Color(0.6f, 1f, 0.9f));
        }
    }
}
