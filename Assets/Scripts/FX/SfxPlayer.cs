using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Central one-shot sound player.
    ///
    /// Anything that is destroyed the moment it makes a noise (a coin, a projectile) cannot use an
    /// AudioSource on itself - the sound would be cut off together with the object. This keeps a
    /// small pool of voices alive on a separate object and round-robins through them, so a sound
    /// always outlives its emitter.
    ///
    /// Long lived objects (enemies) can keep their own AudioSource; both routes coexist.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("TanLuZhe/Sfx Player")]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [Header("Voices")]
        [Tooltip("How many simultaneous one-shots can play before voices start being reused.")]
        [Range(1, 32)] [SerializeField] private int _voices = 12;
        [Tooltip("0 = flat 2D sound, 1 = fully positional. A 2D platformer usually wants a low value.")]
        [Range(0f, 1f)] [SerializeField] private float _spatialBlend = 0.25f;
        [SerializeField] private float _minDistance = 3f;
        [SerializeField] private float _maxDistance = 26f;

        private static SfxPlayer _instance;
        private AudioSource[] _sources;
        private int _cursor;

        public static SfxPlayer Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;

                _instance = FindFirstObjectByType<SfxPlayer>();
                if (_instance == null)
                {
                    GameObject host = new GameObject("Sfx Player");
                    _instance = host.AddComponent<SfxPlayer>();
                }
                return _instance;
            }
        }

        public int VoiceCount => _sources != null ? _sources.Length : 0;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            BuildVoices();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void BuildVoices()
        {
            int count = Mathf.Clamp(_voices, 1, 32);
            _sources = new AudioSource[count];

            for (int i = 0; i < count; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = _spatialBlend;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = _minDistance;
                source.maxDistance = _maxDistance;
                source.dopplerLevel = 0f;
                _sources[i] = source;
            }
        }

        /// <summary>Plays a clip once, flat in the mix (UI, pickups, anything without a position).</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
        {
            PlayInternal(clip, volume, pitchJitter, Vector2.zero, false);
        }

        /// <summary>Plays a clip once at a world position.</summary>
        public static void PlayAt(AudioClip clip, Vector2 position, float volume = 1f, float pitchJitter = 0f)
        {
            PlayInternal(clip, volume, pitchJitter, position, true);
        }

        private static void PlayInternal(AudioClip clip, float volume, float pitchJitter, Vector2 position, bool usePosition)
        {
            if (clip == null || volume <= 0f) return;

            SfxPlayer player = Instance;
            if (player == null || player._sources == null || player._sources.Length == 0) return;

            AudioSource source = player._sources[player._cursor];
            player._cursor = (player._cursor + 1) % player._sources.Length;

            if (usePosition) source.transform.position = new Vector3(position.x, position.y, 0f);

            float jitter = Mathf.Max(0f, pitchJitter);
            source.pitch = jitter > 0f ? Random.Range(1f - jitter, 1f + jitter) : 1f;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
