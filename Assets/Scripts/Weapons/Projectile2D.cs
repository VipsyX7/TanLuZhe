using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Swept raycast projectile. No collider and no rigidbody: the flight path is raycast every
    /// physics step, so fast shots cannot tunnel through walls or enemies.
    /// </summary>
    [AddComponentMenu("TanLuZhe/Projectile 2D")]
    public sealed class Projectile2D : MonoBehaviour
    {
        private WeaponDefinition _weapon;
        private Vector2 _direction;
        private float _speed;
        private float _damage;
        private float _knockback;
        private float _life;
        private int _mask;
        private GameObject _owner;

        private readonly RaycastHit2D[] _hits = new RaycastHit2D[8];

        /// <summary>Creates and configures a projectile.</summary>
        public static Projectile2D Spawn(WeaponDefinition weapon, Vector2 position, Vector2 direction,
            GameObject owner, int mask)
        {
            if (weapon == null) return null;

            GameObject go = new GameObject($"Projectile ({weapon.displayName})");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = weapon.projectileSprite;
            renderer.color = weapon.tint;
            renderer.sortingOrder = 25;

            float scale = Mathf.Max(0.1f, weapon.projectileScale);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            Projectile2D projectile = go.AddComponent<Projectile2D>();
            projectile._weapon = weapon;
            projectile._direction = direction.normalized;
            projectile._speed = weapon.projectileSpeed;
            projectile._damage = weapon.damage;
            projectile._knockback = weapon.knockback;
            projectile._life = weapon.projectileLifetime;
            projectile._mask = mask;
            projectile._owner = owner;
            return projectile;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float step = _speed * dt;
            if (step <= 0f) return;

            Vector2 origin = transform.position;
            int count = Physics2D.RaycastNonAlloc(origin, _direction, _hits, step, _mask);

            RaycastHit2D best = default;
            bool found = false;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (_owner != null && hit.collider.transform.IsChildOf(_owner.transform)) continue;
                if (hit.distance >= bestDistance) continue;

                best = hit;
                bestDistance = hit.distance;
                found = true;
            }

            if (found)
            {
                Impact(best);
                return;
            }

            transform.position += (Vector3)(_direction * step);

            _life -= dt;
            if (_life <= 0f) Destroy(gameObject);
        }

        private void Impact(RaycastHit2D hit)
        {
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive && !(target is PlayerHealth))
            {
                Vector2 knockback = _direction * _knockback + Vector2.up * (_knockback * 0.25f);
                target.TakeDamage(new DamageInfo(_damage, transform.position, knockback, _owner));
                FxManager.Sparks(hit.point, 9, 6f, _weapon != null ? _weapon.tint : Color.white);
                if (_weapon != null && _weapon.hitShake > 0f) CameraRig2D.Shake(_weapon.hitShake, 0.12f);
            }
            else
            {
                FxManager.Sparks(hit.point, 5, 3.5f, new Color(0.8f, 0.88f, 1f));
            }

            Destroy(gameObject);
        }
    }
}
