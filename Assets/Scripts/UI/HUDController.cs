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

        [Header("Weapons")]
        [SerializeField] private Text _weaponText;
        [SerializeField] private Text _pickupText;

        [Header("Banner")]
        [SerializeField] private GameObject _winPanel;
        [SerializeField] private Text _winText;

        [Header("Colors")]
        [SerializeField] private Color _healthGood = new Color(0.4f, 1f, 0.6f, 1f);
        [SerializeField] private Color _healthLow = new Color(1f, 0.4f, 0.4f, 1f);

        private GameManager _game;
        private PlayerHealth _player;
        private GrappleHook2D _grapple;
        private PlayerWeapons _weapons;

        private void Start()
        {
            _game = GameManager.Instance;
            _player = FindFirstObjectByType<PlayerHealth>();
            _grapple = FindFirstObjectByType<GrappleHook2D>();
            _weapons = FindFirstObjectByType<PlayerWeapons>();

            if (_game != null)
            {
                _game.StateChanged += RefreshScore;
                _game.Won += OnWon;
            }
            if (_player != null) _player.HealthChanged += OnHealthChanged;
            if (_weapons != null) _weapons.LoadoutChanged += RefreshWeapons;

            if (_hintText != null)
            {
                _hintText.text = "A / D  move      SPACE  jump (hold = higher)\n" +
                                 "E  fire grapple at cursor      LEFT CTRL  cut the rope\n" +
                                 "LEFT MOUSE  main hand attack      RIGHT MOUSE  off hand attack\n" +
                                 "F  pick up weapon      B  backpack      S + SPACE  drop through      R  restart";
            }

            if (_winPanel != null) _winPanel.SetActive(false);
            RefreshScore();
            RefreshWeapons();
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
            if (_weapons != null) _weapons.LoadoutChanged -= RefreshWeapons;
        }

        private void RefreshWeapons()
        {
            if (_weaponText == null || _weapons == null) return;

            string main = _weapons.MainHand != null ? _weapons.MainHand.displayName : "empty";
            string off = _weapons.OffHand != null ? _weapons.OffHand.displayName : "empty";
            _weaponText.text = $"MAIN [LMB]  {main}      OFF [RMB]  {off}      BAG {_weapons.Backpack.Count}/{_weapons.Capacity}  [B]";
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
                _grappleText.text =
                    $"HOOK {state}   rope {_grapple.RopeLength:0.00} m   pull {_grapple.CurrentPullSpeed:0.0} m/s" +
                    "   |   ACTION LOCKED  (CTRL = cut)";
            }
            else
            {
                _grappleText.text = $"HOOK {state}";
            }

            if (_pickupText != null)
            {
                WeaponPickup2D pickup = _weapons != null ? _weapons.NearbyPickup : null;
                _pickupText.text = pickup != null && pickup.HasWeapon
                    ? $"F   pick up  {pickup.Weapon.displayName.ToUpperInvariant()}"
                    : string.Empty;
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
