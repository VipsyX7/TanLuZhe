using UnityEngine;

namespace TanLuZhe
{
    /// <summary>Spikes / lava / saws. Damages or kills anything that touches it.</summary>
    [AddComponentMenu("TanLuZhe/Hazard 2D")]
    public sealed class Hazard2D : MonoBehaviour
    {
        [SerializeField] private float _damage = 34f;
        [SerializeField] private bool _instantKill;
        [SerializeField] private Vector2 _knockback = new Vector2(6f, 12f);

        private void OnTriggerEnter2D(Collider2D other) => Hit(other);
        private void OnTriggerStay2D(Collider2D other) => Hit(other);
        private void OnCollisionEnter2D(Collision2D collision) => Hit(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => Hit(collision.collider);

        private void Hit(Collider2D other)
        {
            PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
            if (health == null || !health.IsAlive) return;

            if (_instantKill)
            {
                health.Kill();
                return;
            }

            Vector2 away = ((Vector2)other.transform.position - (Vector2)transform.position);
            if (Mathf.Abs(away.x) < 0.05f) away.x = 1f;
            health.TakeDamage(new DamageInfo(_damage, transform.position,
                new Vector2(Mathf.Sign(away.x) * _knockback.x, _knockback.y), gameObject));
        }
    }
}
