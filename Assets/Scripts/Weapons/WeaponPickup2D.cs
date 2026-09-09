using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// A weapon lying in the world. Standing on it registers it with <see cref="PlayerWeapons"/>,
    /// which then lets the player press <b>F</b> to put it in the backpack.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("TanLuZhe/Weapon Pickup 2D")]
    public sealed class WeaponPickup2D : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition _weapon;
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private float _bobAmplitude = 0.14f;
        [SerializeField] private float _bobSpeed = 2.2f;
        [SerializeField] private float _spinSpeed = 35f;
        [SerializeField] private Color _idleTint = Color.white;
        [SerializeField] private Color _highlightTint = new Color(1f, 1f, 0.6f, 1f);

        private Vector3 _origin;
        private float _phase;
        private PlayerWeapons _playerInRange;

        public WeaponDefinition Weapon => _weapon;
        public bool HasWeapon => _weapon != null;

        public void Configure(WeaponDefinition definition, SpriteRenderer renderer = null)
        {
            _weapon = definition;
            if (renderer != null) _renderer = renderer;
            ApplyVisual();
        }

        private void Awake()
        {
            Collider2D collider = GetComponent<Collider2D>();
            collider.isTrigger = true;

            if (_renderer == null) _renderer = GetComponentInChildren<SpriteRenderer>();
            _origin = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (_renderer == null) return;

            if (_weapon != null)
            {
                _renderer.sprite = _weapon.icon != null ? _weapon.icon : _weapon.worldSprite;
                _renderer.color = _weapon.tint;
            }
            _idleTint = _renderer.color;
        }

        private void Update()
        {
            float y = _origin.y + Mathf.Sin(Time.time * _bobSpeed + _phase) * _bobAmplitude;
            transform.position = new Vector3(_origin.x, y, _origin.z);
            transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);

            if (_renderer != null && _playerInRange != null)
                _renderer.color = Color.Lerp(_idleTint, _highlightTint, 0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            else if (_renderer != null)
                _renderer.color = _idleTint;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerWeapons player = other.GetComponentInParent<PlayerWeapons>();
            if (player == null) return;

            _playerInRange = player;
            player.SetNearbyPickup(this);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerWeapons player = other.GetComponentInParent<PlayerWeapons>();
            if (player == null || player != _playerInRange) return;

            _playerInRange = null;
            player.SetNearbyPickup(null);
        }

        private void OnDisable()
        {
            if (_playerInRange != null) _playerInRange.SetNearbyPickup(null);
            _playerInRange = null;
        }

        /// <summary>Moves this weapon into the player's backpack and removes it from the world.</summary>
        public bool TryCollect(PlayerWeapons player)
        {
            if (_weapon == null || player == null) return false;
            if (!player.TryAddWeapon(_weapon)) return false;

            FxManager.Sparks(transform.position, 12, 5f, new Color(1f, 0.95f, 0.6f));
            Destroy(gameObject);
            return true;
        }
    }
}
