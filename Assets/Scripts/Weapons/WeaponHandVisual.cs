using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Draws the two held weapons on the character. Both sprites point at the mouse cursor; the
    /// off hand sits a little behind the main hand, and an attack plays a short arc swing.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("TanLuZhe/Weapon Hand Visual")]
    public sealed class WeaponHandVisual : MonoBehaviour
    {
        [Header("Renderers")]
        [SerializeField] private SpriteRenderer _mainRenderer;
        [SerializeField] private SpriteRenderer _offRenderer;
        [SerializeField] private int _sortingOrder = 12;

        [Header("Pose")]
        [Tooltip("Angular offset of the off hand relative to the main hand (degrees).")]
        [Range(-90f, 90f)] [SerializeField] private float _offAngleOffset = -24f;
        [Tooltip("Extra distance from the character centre for the off hand.")]
        [Range(-1f, 1f)] [SerializeField] private float _offDistanceOffset = -0.08f;

        [Header("Swing")]
        [Tooltip("Total arc the weapon sweeps during one attack (degrees).")]
        [Range(0f, 300f)] [SerializeField] private float _swingArc = 120f;
        [Tooltip("How long the swing animation lasts.")]
        [Range(0.03f, 1f)] [SerializeField] private float _swingTime = 0.16f;
        [Tooltip("Extra scale at the peak of the swing.")]
        [Range(1f, 2f)] [SerializeField] private float _swingPunch = 1.25f;

        private PlayerWeapons _weapons;
        private WeaponDefinition _main;
        private WeaponDefinition _off;
        private float _mainSwing;
        private float _offSwing;
        private float _mainSwingLength = 0.16f;
        private float _offSwingLength = 0.16f;

        public void SetWeapons(WeaponDefinition main, WeaponDefinition off)
        {
            _main = main;
            _off = off;
            ApplySprites();
        }

        /// <summary>Starts the swing animation on one hand.</summary>
        public void TriggerSwing(bool offHand, WeaponDefinition weapon)
        {
            if (offHand)
            {
                _offSwingLength = Mathf.Min(_swingTime, Mathf.Max(0.06f, weapon != null ? weapon.cooldown * 0.6f : _swingTime));
                _offSwing = _offSwingLength;
            }
            else
            {
                _mainSwingLength = Mathf.Min(_swingTime, Mathf.Max(0.06f, weapon != null ? weapon.cooldown * 0.6f : _swingTime));
                _mainSwing = _mainSwingLength;
            }
        }

        private void Awake()
        {
            if (_mainRenderer == null || _offRenderer == null)
            {
                SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (_mainRenderer == null && renderers[i].name.Contains("Main")) _mainRenderer = renderers[i];
                    else if (_offRenderer == null && renderers[i].name.Contains("Off")) _offRenderer = renderers[i];
                }
            }

            if (_mainRenderer != null) _mainRenderer.sortingOrder = _sortingOrder + 1;
            if (_offRenderer != null) _offRenderer.sortingOrder = _sortingOrder;
        }

        private void Start()
        {
            if (_weapons == null) _weapons = GetComponentInParent<PlayerWeapons>();
            if (_weapons != null) SetWeapons(_weapons.MainHand, _weapons.OffHand);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_mainSwing > 0f) _mainSwing -= dt;
            if (_offSwing > 0f) _offSwing -= dt;
        }

        private void LateUpdate()
        {
            if (_weapons == null) return;

            Vector2 aim = _weapons.AimDirection;
            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.right;
            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            UpdateHand(_offRenderer, _off, baseAngle + _offAngleOffset, _offSwing, _offSwingLength, _offDistanceOffset);
            UpdateHand(_mainRenderer, _main, baseAngle, _mainSwing, _mainSwingLength, 0f);
        }

        private void UpdateHand(SpriteRenderer renderer, WeaponDefinition weapon, float baseAngle,
            float swingTimer, float swingLength, float distanceOffset)
        {
            if (renderer == null) return;

            if (weapon == null)
            {
                if (renderer.enabled) renderer.enabled = false;
                return;
            }

            if (!renderer.enabled) renderer.enabled = true;
            if (renderer.sprite != weapon.worldSprite) renderer.sprite = weapon.worldSprite;
            renderer.color = weapon.tint;

            float swing = 0f;
            float punch = 1f;
            if (swingTimer > 0f && swingLength > 0f)
            {
                float t = 1f - Mathf.Clamp01(swingTimer / swingLength);
                float eased = 1f - (1f - t) * (1f - t);
                swing = Mathf.Lerp(-_swingArc * 0.5f, _swingArc * 0.5f, eased);
                punch = Mathf.Lerp(_swingPunch, 1f, eased);
            }

            float angle = baseAngle + weapon.holdAngle + swing;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            float distance = weapon.holdDistance + distanceOffset;
            Vector2 position = _weapons.HandPosition + dir * distance;

            renderer.transform.position = new Vector3(position.x, position.y, 0f);
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            renderer.flipY = angle > 90f || angle < -90f;

            float scale = weapon.holdScale * punch;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ApplySprites()
        {
            if (_mainRenderer != null)
            {
                _mainRenderer.enabled = _main != null;
                if (_main != null) _mainRenderer.sprite = _main.worldSprite;
            }
            if (_offRenderer != null)
            {
                _offRenderer.enabled = _off != null;
                if (_off != null) _offRenderer.sprite = _off.worldSprite;
            }
        }
    }
}
