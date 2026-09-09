using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Draws the rope between the muzzle and the hook head. A taut rope is a straight line;
    /// a slack rope gets a gravity sag so the player can read whether they are actually being
    /// pulled or just hanging on a loose line.
    /// </summary>
    [RequireComponent(typeof(GrappleHook2D))]
    [AddComponentMenu("TanLuZhe/Grapple Rope Renderer")]
    public sealed class GrappleRopeRenderer : MonoBehaviour
    {
        [SerializeField] private int _segments = 14;
        [SerializeField] private float _width = 0.09f;
        [SerializeField] private Color _color = new Color(0.82f, 0.95f, 1f, 1f);
        [SerializeField] private Color _tautColor = new Color(0.55f, 1f, 0.9f, 1f);
        [SerializeField] private float _maxSag = 0.9f;
        [SerializeField] private string _sortingLayerName = "Default";
        [SerializeField] private int _sortingOrder = 30;
        [SerializeField] private Material _material;

        private GrappleHook2D _hook;
        private LineRenderer _line;

        private void Awake()
        {
            _hook = GetComponent<GrappleHook2D>();
            _line = GetComponent<LineRenderer>();
            if (_line == null) _line = gameObject.AddComponent<LineRenderer>();

            _line.useWorldSpace = true;
            _line.positionCount = 0;
            _line.widthMultiplier = _width;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 2;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Stretch;
            _line.sortingLayerName = _sortingLayerName;
            _line.sortingOrder = _sortingOrder;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            if (_material != null) _line.sharedMaterial = _material;
            else
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null) _line.sharedMaterial = new Material(shader);
            }
        }

        private void LateUpdate()
        {
            if (_hook == null || _line == null) return;

            bool active = _hook.State != GrappleState.Idle;
            if (!active)
            {
                if (_line.positionCount != 0) _line.positionCount = 0;
                return;
            }

            Vector2 a = _hook.OriginPosition;
            Vector2 b = _hook.HeadPosition;

            float ropeLength = _hook.RopeLength;
            float distance = Vector2.Distance(a, b);
            float slack = Mathf.Max(0f, ropeLength - distance);
            float sag = Mathf.Min(_maxSag, slack * 0.35f);

            bool taut = _hook.IsAttached && distance >= ropeLength - 0.03f;

            int count = Mathf.Max(2, _segments);
            if (_line.positionCount != count) _line.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (float)(count - 1);
                Vector2 p = Vector2.Lerp(a, b, t);
                // Parabolic sag, zero at both ends.
                p.y -= sag * 4f * t * (1f - t);
                _line.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }

            Color c = taut ? _tautColor : _color;
            _line.startColor = c;
            _line.endColor = new Color(c.r, c.g, c.b, 0.85f);
            _line.widthMultiplier = taut ? _width : _width * 0.8f;
        }
    }
}
