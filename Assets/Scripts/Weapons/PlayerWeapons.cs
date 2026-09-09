using System;
using System.Collections.Generic;
using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Loadout and attacks.
    ///
    /// The player carries one weapon in each hand plus a backpack:
    /// * <b>F</b> picks up a nearby weapon into the backpack,
    /// * <b>B</b> opens the backpack UI where weapons can be equipped into either hand,
    /// * <b>left mouse</b> attacks with the main hand, <b>right mouse</b> with the off hand.
    ///
    /// Attacks are blocked while the grapple rope owns the character (same rule as every other
    /// action key) and while the backpack is open.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [AddComponentMenu("TanLuZhe/Player Weapons")]
    public sealed class PlayerWeapons : MonoBehaviour
    {
        [Header("Starting loadout")]
        [SerializeField] private WeaponDefinition _startingMainHand;
        [SerializeField] private WeaponDefinition _startingOffHand;
        [Range(1, 24)] [SerializeField] private int _backpackCapacity = 8;

        [Header("References")]
        [Tooltip("Where weapon sprites and hit boxes originate. Defaults to the player transform.")]
        [SerializeField] private Transform _handOrigin;
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private WeaponHandVisual _handVisual;
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Feel")]
        [Tooltip("How long an attack press is remembered while on cooldown.")]
        [Range(0f, 0.5f)] [SerializeField] private float _inputBuffer = 0.12f;
        [Tooltip("Holding the button keeps attacking as soon as the cooldown is over.")]
        [SerializeField] private bool _holdToRepeat = true;

        private IInputSource _input;
        private PlayerController2D _controller;
        private readonly List<WeaponDefinition> _backpack = new List<WeaponDefinition>();
        private readonly List<Collider2D> _hits = new List<Collider2D>(16);
        private ContactFilter2D _attackFilter;

        private WeaponDefinition _mainHand;
        private WeaponDefinition _offHand;

        private float _mainCooldown;
        private float _offCooldown;
        private float _mainBuffered;
        private float _offBuffered;
        private WeaponPickup2D _nearbyPickup;

        // ------------------------------------------------------------------ public API
        public IReadOnlyList<WeaponDefinition> Backpack => _backpack;
        public WeaponDefinition MainHand => _mainHand;
        public WeaponDefinition OffHand => _offHand;
        public int Capacity => _backpackCapacity;
        public bool IsInventoryOpen { get; private set; }
        public WeaponPickup2D NearbyPickup => _nearbyPickup;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public bool IsBusy => _controller != null && _controller.IsBeingPulled;
        public float MainCooldownRemaining => _mainCooldown;
        public float OffCooldownRemaining => _offCooldown;
        public IInputSource InputSource => _input;

        public Vector2 HandPosition => _handOrigin != null ? (Vector2)_handOrigin.position : (Vector2)transform.position;

        public event Action LoadoutChanged;
        public event Action InventoryToggled;
        public event Action<WeaponDefinition, bool> Attacked;

        public void SetInputSource(IInputSource source) => _input = source;
        public void SetAimCamera(Camera cam) => _aimCamera = cam;
        public void SetHandVisual(WeaponHandVisual visual) => _handVisual = visual;

        // ------------------------------------------------------------------ unity
        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _attackFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = _hitMask,
            };

            if (_handOrigin == null) _handOrigin = transform;
            if (_handVisual == null) _handVisual = GetComponentInChildren<WeaponHandVisual>();

            if (_startingMainHand != null) _mainHand = _startingMainHand;
            if (_startingOffHand != null) _offHand = _startingOffHand;
        }

        private void Start()
        {
            if (_input == null)
            {
                _input = GetComponent<IInputSource>();
                if (_input == null) _input = GetComponentInChildren<IInputSource>();
            }

            if (_aimCamera == null) _aimCamera = Camera.main;
            if (_handVisual == null) _handVisual = GetComponentInChildren<WeaponHandVisual>();

            _handVisual?.SetWeapons(_mainHand, _offHand);
            LoadoutChanged?.Invoke();
        }

        private void Update()
        {
            if (_input == null) return;

            if (_input.InventoryDown) ToggleInventory();

            if (IsInventoryOpen) return;          // clicks belong to the UI while it is open
            if (IsBusy) return;                   // the rope owns every action key

            if (_input.InteractDown) TryPickUpNearby();

            if (_input.AttackMainDown) _mainBuffered = _inputBuffer;
            if (_input.AttackOffDown) _offBuffered = _inputBuffer;

            if (_holdToRepeat)
            {
                if (_input.AttackMainHeld) _mainBuffered = Mathf.Max(_mainBuffered, 0.001f);
                if (_input.AttackOffHeld) _offBuffered = Mathf.Max(_offBuffered, 0.001f);
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (_mainCooldown > 0f) _mainCooldown -= dt;
            if (_offCooldown > 0f) _offCooldown -= dt;
            if (_mainBuffered > 0f) _mainBuffered -= dt;
            if (_offBuffered > 0f) _offBuffered -= dt;

            AimDirection = ComputeAimDirection();

            if (IsInventoryOpen || IsBusy)
            {
                _mainBuffered = 0f;
                _offBuffered = 0f;
                return;
            }

            if (_mainBuffered > 0f && _mainCooldown <= 0f && _mainHand != null)
            {
                _mainBuffered = 0f;
                ExecuteAttack(_mainHand, false);
            }

            if (_offBuffered > 0f && _offCooldown <= 0f && _offHand != null)
            {
                _offBuffered = 0f;
                ExecuteAttack(_offHand, true);
            }
        }

        private Vector2 ComputeAimDirection()
        {
            if (_input == null) return Vector2.right;

            Camera cam = _aimCamera != null ? _aimCamera : Camera.main;
            if (cam == null)
            {
                return _controller != null
                    ? new Vector2(_controller.FacingSign, 0f)
                    : Vector2.right;
            }

            Vector2 screen = _input.PointerScreen;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            Vector2 dir = (Vector2)world - HandPosition;
            if (dir.sqrMagnitude < 0.0004f)
            {
                return _controller != null ? new Vector2(_controller.FacingSign, 0f) : Vector2.right;
            }
            return dir.normalized;
        }

        // ------------------------------------------------------------------ loadout
        /// <summary>Adds a weapon to the backpack. Returns false when there is no room.</summary>
        public bool TryAddWeapon(WeaponDefinition weapon)
        {
            if (weapon == null) return false;
            if (_backpack.Count >= _backpackCapacity) return false;

            _backpack.Add(weapon);
            LoadoutChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Equips a weapon into a hand. The weapon is removed from the backpack if it was there,
        /// and whatever was in that hand goes back to the backpack.
        /// </summary>
        public bool Equip(WeaponDefinition weapon, bool offHand)
        {
            if (weapon == null) return false;

            if (offHand)
            {
                if (_offHand == weapon) return true;
                WeaponDefinition previous = _offHand;
                _backpack.Remove(weapon);
                _offHand = weapon;
                if (previous != null) _backpack.Add(previous);
            }
            else
            {
                if (_mainHand == weapon) return true;
                WeaponDefinition previous = _mainHand;
                _backpack.Remove(weapon);
                _mainHand = weapon;
                if (previous != null) _backpack.Add(previous);
            }

            _handVisual?.SetWeapons(_mainHand, _offHand);
            LoadoutChanged?.Invoke();
            return true;
        }

        /// <summary>Moves a hand's weapon back into the backpack (if there is room).</summary>
        public bool Unequip(bool offHand)
        {
            WeaponDefinition weapon = offHand ? _offHand : _mainHand;
            if (weapon == null) return false;
            if (_backpack.Count >= _backpackCapacity) return false;

            if (offHand) _offHand = null;
            else _mainHand = null;

            _backpack.Add(weapon);
            _handVisual?.SetWeapons(_mainHand, _offHand);
            LoadoutChanged?.Invoke();
            return true;
        }

        public void ToggleInventory() => SetInventoryOpen(!IsInventoryOpen);

        public void SetInventoryOpen(bool open)
        {
            if (IsInventoryOpen == open) return;
            IsInventoryOpen = open;
            InventoryToggled?.Invoke();
        }

        /// <summary>Called by a pickup when the player walks into it.</summary>
        public void SetNearbyPickup(WeaponPickup2D pickup) => _nearbyPickup = pickup;

        /// <summary>Picks up the weapon the player is currently standing on, if any.</summary>
        public bool TryPickUpNearby()
        {
            WeaponPickup2D pickup = _nearbyPickup;
            if (pickup == null) return false;

            if (!pickup.TryCollect(this))
            {
                _nearbyPickup = null;
                return false;
            }

            _nearbyPickup = null;
            return true;
        }

        // ------------------------------------------------------------------ attacks
        /// <summary>Fires one attack with the given hand. Public so tests and cutscenes can drive it.</summary>
        public bool ExecuteAttack(WeaponDefinition weapon, bool offHand)
        {
            if (weapon == null) return false;

            if (offHand) _offCooldown = weapon.Cooldown;
            else _mainCooldown = weapon.Cooldown;

            Vector2 dir = AimDirection.sqrMagnitude > 0.0001f ? AimDirection : Vector2.right;
            if (weapon.kind == WeaponKind.Melee) DoMeleeAttack(weapon, dir);
            else DoRangedAttack(weapon, dir);

            _handVisual?.TriggerSwing(offHand, weapon);
            Attacked?.Invoke(weapon, offHand);
            return true;
        }

        private void DoMeleeAttack(WeaponDefinition weapon, Vector2 dir)
        {
            Vector2 center = HandPosition + dir * weapon.meleeReach;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            int count = Physics2D.OverlapBox(center, weapon.meleeSize, angle, _attackFilter, _hits);
            int hits = 0;

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = _hits[i];
                if (collider == null) continue;

                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;
                if (target is PlayerHealth) continue;   // never hit yourself

                Vector2 knockback = dir * weapon.knockback + Vector2.up * (weapon.knockback * 0.35f);
                target.TakeDamage(new DamageInfo(weapon.damage, HandPosition, knockback, gameObject));
                hits++;
            }

            FxManager.Slash(center, dir, Mathf.Max(weapon.meleeSize.x, weapon.meleeSize.y) * 1.1f);
            FxManager.Sparks(center, hits > 0 ? 10 : 4, hits > 0 ? 6f : 2.5f,
                hits > 0 ? new Color(1f, 0.9f, 0.5f) : new Color(0.8f, 0.85f, 1f));
            if (hits > 0 && weapon.hitShake > 0f) CameraRig2D.Shake(weapon.hitShake, 0.14f);
        }

        private void DoRangedAttack(WeaponDefinition weapon, Vector2 dir)
        {
            int count = Mathf.Max(1, weapon.projectileCount);
            for (int i = 0; i < count; i++)
            {
                float offset = count == 1
                    ? 0f
                    : Mathf.Lerp(-weapon.spread, weapon.spread, i / (float)(count - 1));
                Vector2 shotDir = Rotate(dir, offset);
                Projectile2D.Spawn(weapon, HandPosition + shotDir * 0.5f, shotDir, gameObject, _hitMask);
            }

            FxManager.Sparks(HandPosition + dir * 0.4f, 4, 3f, weapon.tint);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
