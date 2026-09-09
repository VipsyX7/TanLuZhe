using UnityEngine;

namespace TanLuZhe
{
    /// <summary>Level exit. Touching it wins the level.</summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("TanLuZhe/Level Goal 2D")]
    public sealed class LevelGoal2D : MonoBehaviour
    {
        [SerializeField] private float _spinSpeed = 45f;
        [SerializeField] private float _pulseAmplitude = 0.08f;
        [SerializeField] private float _pulseSpeed = 3f;

        private Vector3 _baseScale;
        private bool _reached;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f);
            float k = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmplitude;
            transform.localScale = _baseScale * k;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_reached) return;
            if (other.GetComponentInParent<PlayerController2D>() == null) return;

            _reached = true;
            FxManager.Sparks(transform.position, 30, 9f, new Color(0.65f, 1f, 0.9f));
            GameManager.Instance?.Win();
        }
    }
}
