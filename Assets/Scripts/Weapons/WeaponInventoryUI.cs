using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TanLuZhe
{
    /// <summary>
    /// Backpack panel, toggled with <b>B</b>. Built entirely in code so the scene only needs the
    /// one component. Interaction is handled through the project's own input abstraction (no
    /// EventSystem / input module needed):
    ///
    /// * click a hand slot to select it (main hand / off hand),
    /// * click a weapon in the backpack to equip it into the selected hand,
    /// * click the selected hand slot again to unequip it.
    /// </summary>
    [AddComponentMenu("TanLuZhe/Weapon Inventory UI")]
    public sealed class WeaponInventoryUI : MonoBehaviour
    {
        private sealed class Slot
        {
            public RectTransform Rect;
            public Image Background;
            public Image Icon;
            public Text Label;
            public WeaponDefinition Weapon;
            public int HandIndex = -1;   // -1 = backpack slot, 0 = main hand, 1 = off hand
        }

        [Header("References")]
        [SerializeField] private PlayerWeapons _weapons;
        [SerializeField] private Sprite _panelSprite;
        [SerializeField] private Sprite _slotSprite;
        [SerializeField] private Font _font;

        [Header("Layout")]
        [SerializeField] private Vector2 _panelSize = new Vector2(680f, 440f);
        [SerializeField] private float _slotSize = 88f;
        [SerializeField] private float _slotSpacing = 100f;

        [Header("Colors")]
        [SerializeField] private Color _panelColor = new Color(0.04f, 0.03f, 0.09f, 0.92f);
        [SerializeField] private Color _slotColor = new Color(1f, 1f, 1f, 0.10f);
        [SerializeField] private Color _slotHoverColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] private Color _handSelectedColor = new Color(0.4f, 1f, 0.8f, 0.35f);
        [SerializeField] private Color _handIdleColor = new Color(1f, 0.9f, 0.5f, 0.18f);

        private GameObject _panel;
        private Text _titleText;
        private Text _hintText;
        private Text _infoText;
        private readonly List<Slot> _handSlots = new List<Slot>();
        private readonly List<Slot> _backpackSlots = new List<Slot>();
        private int _selectedHand;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_weapons == null) _weapons = FindFirstObjectByType<PlayerWeapons>();
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildPanel();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (_weapons == null) return;
            _weapons.LoadoutChanged -= Refresh;
            _weapons.InventoryToggled -= OnInventoryToggled;
        }

        private void Start()
        {
            if (_weapons != null)
            {
                _weapons.LoadoutChanged += Refresh;
                _weapons.InventoryToggled += OnInventoryToggled;
            }
            Refresh();
        }

        private void OnInventoryToggled() => SetOpen(_weapons != null && _weapons.IsInventoryOpen);

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (_panel != null && _panel.activeSelf != open) _panel.SetActive(open);
            if (open)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                Refresh();
            }
        }

        public int HandSlotCount => _handSlots.Count;
        public int BackpackSlotCount => _backpackSlots.Count;
        public int SelectedHand => _selectedHand;

        /// <summary>Clicks a hand slot (0 = main, 1 = off). Returns false when the index is invalid.</summary>
        public bool ClickHandSlot(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _handSlots.Count) return false;
            Click(_handSlots[handIndex]);
            return true;
        }

        /// <summary>Clicks a backpack slot. Returns false when the index is invalid.</summary>
        public bool ClickBackpackSlot(int index)
        {
            if (index < 0 || index >= _backpackSlots.Count) return false;
            Click(_backpackSlots[index]);
            return true;
        }

        // ------------------------------------------------------------------ input
        private void Update()
        {
            if (!IsOpen || _weapons == null) return;

            IInputSource input = _weapons.InputSource;
            if (input == null) return;

            Vector2 pointer = input.PointerScreen;
            Slot hovered = FindSlotAt(pointer);

            UpdateHoverVisuals(hovered);

            if (input.AttackMainDown) Click(hovered);
        }

        private Slot FindSlotAt(Vector2 screenPoint)
        {
            for (int i = 0; i < _handSlots.Count; i++)
            {
                if (Contains(_handSlots[i].Rect, screenPoint)) return _handSlots[i];
            }
            for (int i = 0; i < _backpackSlots.Count; i++)
            {
                if (Contains(_backpackSlots[i].Rect, screenPoint)) return _backpackSlots[i];
            }
            return null;
        }

        private static bool Contains(RectTransform rect, Vector2 screenPoint)
        {
            if (rect == null) return false;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, camera);
        }

        private void Click(Slot slot)
        {
            if (slot == null) return;

            if (slot.HandIndex >= 0)
            {
                // Selecting a hand, or unequipping it when it is already selected.
                if (_selectedHand == slot.HandIndex && slot.Weapon != null)
                {
                    _weapons.Unequip(slot.HandIndex == 1);
                }
                _selectedHand = slot.HandIndex;
            }
            else if (slot.Weapon != null)
            {
                _weapons.Equip(slot.Weapon, _selectedHand == 1);
            }

            Refresh();
        }

        private void UpdateHoverVisuals(Slot hovered)
        {
            for (int i = 0; i < _handSlots.Count; i++)
            {
                Slot slot = _handSlots[i];
                bool selected = slot.HandIndex == _selectedHand;
                Color color = selected ? _handSelectedColor : _handIdleColor;
                if (hovered == slot) color = Color.Lerp(color, Color.white, 0.2f);
                if (slot.Background != null) slot.Background.color = color;
            }

            for (int i = 0; i < _backpackSlots.Count; i++)
            {
                Slot slot = _backpackSlots[i];
                if (slot.Background == null) continue;
                slot.Background.color = hovered == slot ? _slotHoverColor : _slotColor;
            }

            if (_infoText != null)
            {
                WeaponDefinition info = hovered != null ? hovered.Weapon : null;
                if (info == null)
                {
                    _infoText.text = "Click a hand slot to select it, then click a weapon to equip it. " +
                                     "Click the selected slot again to unequip.";
                }
                else
                {
                    string kind = info.kind == WeaponKind.Melee ? "MELEE" : "RANGED";
                    _infoText.text = $"{info.displayName.ToUpperInvariant()}  [{kind}]   " +
                                     $"DMG {info.damage:0}   CD {info.cooldown:0.00}s   KNOCKBACK {info.knockback:0}";
                }
            }
        }

        // ------------------------------------------------------------------ visuals
        private void Refresh()
        {
            if (_weapons == null) return;

            for (int i = 0; i < _handSlots.Count; i++)
            {
                Slot slot = _handSlots[i];
                slot.Weapon = i == 0 ? _weapons.MainHand : _weapons.OffHand;
                ApplySlotVisual(slot, i == 0 ? "MAIN [LMB]" : "OFF [RMB]");
            }

            IReadOnlyList<WeaponDefinition> backpack = _weapons.Backpack;
            for (int i = 0; i < _backpackSlots.Count; i++)
            {
                Slot slot = _backpackSlots[i];
                slot.Weapon = i < backpack.Count ? backpack[i] : null;
                ApplySlotVisual(slot, slot.Weapon != null ? slot.Weapon.displayName : "-");
            }

            if (_titleText != null) _titleText.text = $"BACKPACK   {backpack.Count}/{_weapons.Capacity}";
        }

        private void ApplySlotVisual(Slot slot, string label)
        {
            if (slot == null) return;

            bool has = slot.Weapon != null;
            if (slot.Icon != null)
            {
                slot.Icon.enabled = has;
                if (has)
                {
                    slot.Icon.sprite = slot.Weapon.icon != null ? slot.Weapon.icon : slot.Weapon.worldSprite;
                    slot.Icon.color = slot.Weapon.tint;
                }
            }

            if (slot.Label != null) slot.Label.text = label;
        }

        // ------------------------------------------------------------------ build
        private void BuildPanel()
        {
            _panel = new GameObject("Weapon Inventory Panel");
            _panel.transform.SetParent(transform, false);
            RectTransform panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = _panelSize;

            Image background = _panel.AddComponent<Image>();
            background.sprite = _panelSprite;
            background.color = _panelColor;

            _titleText = MakeText(_panel.transform, "Title", "BACKPACK",
                new Vector2(0f, _panelSize.y * 0.5f - 34f), new Vector2(_panelSize.x - 60f, 40f),
                TextAnchor.MiddleLeft, 28, new Color(0.75f, 1f, 0.92f));

            MakeText(_panel.transform, "Hands Label", "EQUIPPED", new Vector2(0f, 118f),
                new Vector2(_panelSize.x - 60f, 28f), TextAnchor.MiddleLeft, 20, new Color(0.8f, 0.85f, 1f));

            float handY = 60f;
            _handSlots.Add(MakeSlot(_panel.transform, "Main Hand Slot", new Vector2(-60f, handY), 0));
            _handSlots.Add(MakeSlot(_panel.transform, "Off Hand Slot", new Vector2(40f, handY), 1));

            MakeText(_panel.transform, "Backpack Label", "BACKPACK", new Vector2(0f, -6f),
                new Vector2(_panelSize.x - 60f, 28f), TextAnchor.MiddleLeft, 20, new Color(0.8f, 0.85f, 1f));

            int columns = 4;
            float startX = -((columns - 1) * _slotSpacing) * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                int column = i % columns;
                int row = i / columns;
                Vector2 position = new Vector2(startX + column * _slotSpacing, -64f - row * _slotSpacing);
                _backpackSlots.Add(MakeSlot(_panel.transform, $"Backpack Slot {i}", position, -1));
            }

            _hintText = MakeText(_panel.transform, "Hint", "B  close   |   left mouse  select / equip",
                new Vector2(0f, -_panelSize.y * 0.5f + 52f), new Vector2(_panelSize.x - 60f, 28f),
                TextAnchor.MiddleLeft, 18, new Color(0.7f, 0.75f, 0.9f));

            _infoText = MakeText(_panel.transform, "Info", string.Empty,
                new Vector2(0f, -_panelSize.y * 0.5f + 24f), new Vector2(_panelSize.x - 60f, 28f),
                TextAnchor.MiddleLeft, 18, new Color(1f, 0.95f, 0.7f));
        }

        private Slot MakeSlot(Transform parent, string name, Vector2 position, int handIndex)
        {
            Slot slot = new Slot { HandIndex = handIndex };

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(_slotSize, _slotSize);
            slot.Rect = rect;

            Image background = go.AddComponent<Image>();
            background.sprite = _slotSprite;
            background.color = _slotColor;
            slot.Background = background;

            GameObject iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            RectTransform iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 6f);
            iconRect.sizeDelta = new Vector2(_slotSize - 26f, _slotSize - 26f);
            Image icon = iconGO.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            slot.Icon = icon;

            slot.Label = MakeText(go.transform, "Label", "-", new Vector2(0f, -_slotSize * 0.5f + 12f),
                new Vector2(_slotSize + 12f, 24f), TextAnchor.MiddleCenter, 15, new Color(0.9f, 0.93f, 1f));
            slot.Label.raycastTarget = false;

            return slot;
        }

        private Text MakeText(Transform parent, string name, string content, Vector2 position,
            Vector2 size, TextAnchor anchor, int fontSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Text text = go.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }
    }
}
