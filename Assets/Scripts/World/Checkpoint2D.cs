using UnityEngine;

namespace TanLuZhe
{
    /// <summary>Respawn point. The player's respawn is moved here the first time it is touched.</summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("TanLuZhe/Checkpoint 2D")]
    public sealed class Checkpoint2D : MonoBehaviour
    {
        [SerializeField] private Vector2 _respawnOffset = new Vector2(0f, 1f);
        [SerializeField] private SpriteRenderer _flagRenderer;
        [SerializeField] private Color _inactiveColor = new Color(0.55f, 0.6f, 0.7f, 1f);
        [SerializeField] private Color _activeColor = new Color(0.4f, 1f, 0.6f, 1f);

        private bool _activated;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            if (_flagRenderer != null) _flagRenderer.color = _inactiveColor;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_activated) return;
            PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;

            _activated = true;
            health.RespawnPoint = (Vector2)transform.position + _respawnOffset;
            if (_flagRenderer != null) _flagRenderer.color = _activeColor;

            FxManager.Sparks(transform.position, 14, 5f, _activeColor);
            GameManager.Instance?.ReportCheckpoint();
        }
    }
}
