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

        [Header("Audio")]
        [Tooltip("Played when the player collects this pickup. Goes through the shared SfxPlayer so " +
                 "the sound survives this object being destroyed.")]
        [SerializeField] private AudioClip _pickupSound;
        [Range(0f, 1f)] [SerializeField] private float _pickupVolume = 0.9f;
        [Tooltip("Random pitch spread so a line of coins does not sound like one long tone.")]
        [Range(0f, 0.5f)] [SerializeField] private float _pickupPitchJitter = 0.1f;

        private Vector3 _origin;
        private float _phase;
        private bool _taken;

        public AudioClip PickupSound => _pickupSound;

        /// <summary>How many times the pickup sound has fired. Handy for debugging and tests.</summary>
        public static int PickupSoundPlays { get; private set; }

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

            // Routed through the shared player: this object is destroyed on the next line, and an
            // AudioSource on it would take the sound down with it.
            if (_pickupSound != null && _pickupVolume > 0f)
            {
                SfxPlayer.PlayAt(_pickupSound, transform.position, _pickupVolume, _pickupPitchJitter);
                PickupSoundPlays++;
            }

            Destroy(gameObject);
        }
    }
}
