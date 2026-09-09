using UnityEngine;

namespace TanLuZhe
{
    /// <summary>Pickup that adds score and can be hoovered up by the grapple swing.</summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("TanLuZhe/Collectible 2D")]
    public sealed class Collectible2D : MonoBehaviour
    {
        [SerializeField] private int _value = 1;
        [SerializeField] private float _bobAmplitude = 0.12f;
        [SerializeField] private float _bobSpeed = 2.4f;
        [SerializeField] private float _spinSpeed = 90f;

        private Vector3 _origin;
        private float _phase;
        private bool _taken;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            _origin = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (_taken) return;
            float y = _origin.y + Mathf.Sin(Time.time * _bobSpeed + _phase) * _bobAmplitude;
            transform.position = new Vector3(_origin.x, y, _origin.z);
            transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_taken) return;
            PlayerController2D player = other.GetComponentInParent<PlayerController2D>();
            if (player == null) return;

            _taken = true;
            GameManager.Instance?.AddScore(_value);
            FxManager.Sparks(transform.position, 8, 4f, new Color(1f, 0.92f, 0.45f));
            Destroy(gameObject);
        }
    }
}
