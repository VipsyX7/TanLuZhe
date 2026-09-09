using UnityEngine;
using UnityEngine.UI;

namespace TanLuZhe
{
    /// <summary>
    /// HUD: health, score, control hints and a live grapple readout so the player can see what
    /// the rope is actually doing (state, anchor kind, rope length, winch speed).
    /// </summary>
    [AddComponentMenu("TanLuZhe/HUD Controller")]
    public sealed class HUDController : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Image _healthFill;
        [SerializeField] private Text _healthText;

        [Header("Score")]
        [SerializeField] private Text _scoreText;

        [Header("Hints")]
        [SerializeField] private Text _hintText;
        [SerializeField] private Text _grappleText;

        [Header("Banner")]
        [SerializeField] private GameObject _winPanel;
        [SerializeField] private Text _winText;

        [Header("Colors")]
        [SerializeField] private Color _healthGood = new Color(0.4f, 1f, 0.6f, 1f);
        [SerializeField] private Color _healthLow = new Color(1f, 0.4f, 0.4f, 1f);

        private GameManager _game;
        private PlayerHealth _player;
        private GrappleHook2D _grapple;

        private void Start()
        {
            _game = GameManager.Instance;
            _player = FindFirstObjectByType<PlayerHealth>();
            _grapple = FindFirstObjectByType<GrappleHook2D>();

            if (_game != null)
            {
                _game.StateChanged += RefreshScore;
                _game.Won += OnWon;
            }
            if (_player != null) _player.HealthChanged += OnHealthChanged;

            if (_hintText != null)
            {
                _hintText.text = "A / D  move      SPACE  jump (hold = higher)\n" +
                                 "E  fire grapple at cursor\n" +
                                 "LEFT CTRL  cut the rope (keeps your momentum)\n" +
                                 "S + SPACE  drop through one-way platforms      R  restart";
            }

            if (_winPanel != null) _winPanel.SetActive(false);
            RefreshScore();
            if (_player != null) OnHealthChanged(_player.Health, _player.MaxHealth);
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.StateChanged -= RefreshScore;
                _game.Won -= OnWon;
            }
            if (_player != null) _player.HealthChanged -= OnHealthChanged;
        }

        private void Update()
        {
            if (_grappleText == null || _grapple == null) return;

            string state;
            switch (_grapple.State)
            {
                case GrappleState.Extending: state = "FIRING"; break;
                case GrappleState.Attached:
                    state = _grapple.AnchorType == GrappleAnchorType.Wall ? "HOOKED - WALL" :
                            _grapple.AnchorType == GrappleAnchorType.Enemy ? "HOOKED - ENEMY" :
                            _grapple.AnchorType == GrappleAnchorType.Moving ? "HOOKED - PLATFORM" : "HOOKED";
                    break;
                case GrappleState.Retracting: state = "RETRACTING"; break;
                default: state = "READY"; break;
            }

            if (_grapple.State == GrappleState.Attached)
            {
                _grappleText.text = $"HOOK {state}   rope {_grapple.RopeLength:0.00} m   winch {_grapple.CurrentReelSpeed:0.0} m/s";
            }
            else
            {
                _grappleText.text = $"HOOK {state}";
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_healthFill != null)
            {
                _healthFill.fillAmount = max > 0f ? current / max : 0f;
                _healthFill.color = Color.Lerp(_healthLow, _healthGood, max > 0f ? current / max : 0f);
            }
            if (_healthText != null) _healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void RefreshScore()
        {
            if (_scoreText == null || _game == null) return;
            _scoreText.text = $"COINS {_game.Score:00}    DEATHS {_game.Deaths:00}    CHECKPOINTS {_game.CheckpointsReached}";
        }

        private void OnWon()
        {
            if (_winPanel != null) _winPanel.SetActive(true);
            if (_winText != null && _game != null)
            {
                _winText.text = $"LEVEL CLEAR!\ncoins {_game.Score}   deaths {_game.Deaths}\n\npress R to run it again";
            }
        }
    }
}
