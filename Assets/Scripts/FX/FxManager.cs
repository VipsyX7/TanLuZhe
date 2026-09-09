using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Tiny pooled sprite particle system. Written by hand instead of using a ParticleSystem
    /// so that the effect is fully deterministic, allocation free after warm-up and trivially
    /// testable in batch mode.
    /// </summary>
    [AddComponentMenu("TanLuZhe/FX Manager")]
    public sealed class FxManager : MonoBehaviour
    {
        private struct Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Gravity;
            public float Drag;
            public float Spin;
            public Color StartColor;
            public bool Alive;
        }

        [SerializeField] private Sprite _sparkSprite;
        [SerializeField] private Sprite _dustSprite;
        [SerializeField] private Sprite _slashSprite;
        [Tooltip("Extra rotation applied to the slash arc. The art is drawn as a '(' opening toward +X, " +
                 "so 180 deg makes the arc bulge along the attack direction with its hollow facing the " +
                 "attacker - which is how a slash trail should read.")]
        [Range(-180f, 180f)] [SerializeField] private float _slashAngleOffset = 180f;
        [SerializeField] private int _poolSize = 160;
        [SerializeField] private string _sortingLayerName = "Default";

        private static FxManager _instance;
        private Particle[] _pool;
        private int _cursor;

        public static FxManager Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<FxManager>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            BuildPool();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void BuildPool()
        {
            _pool = new Particle[Mathf.Max(8, _poolSize)];
            for (int i = 0; i < _pool.Length; i++)
            {
                GameObject go = new GameObject("FxParticle");
                go.transform.SetParent(transform, false);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _sparkSprite;
                sr.sortingLayerName = _sortingLayerName;
                sr.sortingOrder = 60;
                go.SetActive(false);
                _pool[i] = new Particle { Transform = go.transform, Renderer = sr, Alive = false };
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _pool == null) return;

            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Alive) continue;

                ref Particle p = ref _pool[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    p.Alive = false;
                    p.Transform.gameObject.SetActive(false);
                    continue;
                }

                p.Velocity += Vector2.down * (p.Gravity * dt);
                p.Velocity *= Mathf.Clamp01(1f - p.Drag * dt);
                p.Transform.position += (Vector3)(p.Velocity * dt);
                p.Transform.Rotate(0f, 0f, p.Spin * dt);

                float k = p.Life / Mathf.Max(0.0001f, p.MaxLife);
                Color c = p.StartColor;
                c.a *= k * k;
                p.Renderer.color = c;
            }
        }

        private Particle Emit(Sprite sprite, Vector2 position, Vector2 velocity, float life,
            float size, Color color, float gravity, float drag, float spin)
        {
            if (_pool == null) return default;

            for (int attempt = 0; attempt < _pool.Length; attempt++)
            {
                int index = (_cursor + attempt) % _pool.Length;
                if (_pool[index].Alive) continue;

                _cursor = (index + 1) % _pool.Length;
                ref Particle p = ref _pool[index];
                p.Alive = true;
                p.Life = life;
                p.MaxLife = life;
                p.Velocity = velocity;
                p.Gravity = gravity;
                p.Drag = drag;
                p.Spin = spin;
                p.StartColor = color;

                GameObject go = p.Transform.gameObject;
                go.SetActive(true);
                p.Transform.position = new Vector3(position.x, position.y, 0f);
                p.Transform.localScale = Vector3.one * size;
                p.Transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                p.Renderer.sprite = sprite != null ? sprite : _sparkSprite;
                p.Renderer.color = color;
                return p;
            }

            return default;
        }

        // ------------------------------------------------------------------ static API
        public static void Sparks(Vector2 position, int count, float speed, Color color)
        {
            FxManager fx = Instance;
            if (fx == null) return;

            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
                float s = speed * Random.Range(0.45f, 1.25f);
                fx.Emit(fx._sparkSprite, position, dir * s, Random.Range(0.18f, 0.42f),
                    Random.Range(0.12f, 0.3f), color, 22f, 3.5f, Random.Range(-360f, 360f));
            }
        }

        public static void LandingDust(Vector2 position, int count, float strength)
        {
            FxManager fx = Instance;
            if (fx == null) return;

            for (int i = 0; i < count; i++)
            {
                Vector2 dir = new Vector2(Random.Range(-1f, 1f), Random.Range(0.05f, 0.6f)).normalized;
                fx.Emit(fx._dustSprite, position + Vector2.down * 0.65f, dir * (strength * Random.Range(1.2f, 2.6f)),
                    Random.Range(0.25f, 0.5f), Random.Range(0.25f, 0.55f),
                    new Color(0.85f, 0.86f, 0.95f, 0.75f), 6f, 4.5f, Random.Range(-180f, 180f));
            }
        }

        public static void Burst(Vector2 position, int count, Color color, float speed = 6f)
        {
            Sparks(position, count, speed, color);
        }

        /// <summary>One-shot slash arc, rotated to match an attack direction.</summary>
        public static Transform Slash(Vector2 position, Vector2 direction, float size = 1.6f, float life = 0.14f)
        {
            FxManager fx = Instance;
            if (fx == null) return null;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + fx._slashAngleOffset;
            Particle p = fx.Emit(fx._slashSprite, position, Vector2.zero, life, size,
                new Color(0.8f, 0.95f, 1f, 0.9f), 0f, 0f, 0f);
            if (p.Transform != null) p.Transform.rotation = Quaternion.Euler(0f, 0f, angle);
            return p.Transform;
        }

        /// <summary>Deterministic seeding for tests.</summary>
        public static void Seed(int seed)
        {
            Random.InitState(seed);
        }
    }
}
