using UnityEngine;
using UnityEngine.InputSystem;

namespace TanLuZhe
{
    /// <summary>
    /// Default <see cref="IInputSource"/> implementation built on the new Input System's
    /// low level device API. The project is configured for "Input System Package (New)",
    /// so the legacy <c>Input</c> class is unavailable by design.
    ///
    /// Controls
    /// --------
    /// A / D or arrows : move
    /// Space / W / Up  : jump (hold longer = higher)
    /// E               : fire the grapple hook toward the mouse cursor
    /// Left Ctrl       : cut the rope (keeps the momentum, stops the pull)
    /// S / Down        : used together with jump to drop through one-way platforms
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("TanLuZhe/Player Input Source")]
    public sealed class PlayerInputSource : MonoBehaviour, IInputSource
    {
        [Header("Bindings")]
        [SerializeField] private Key _leftKey = Key.A;
        [SerializeField] private Key _rightKey = Key.D;
        [SerializeField] private Key _jumpKey = Key.Space;
        [SerializeField] private Key _jumpAltKey = Key.W;
        [SerializeField] private Key _grappleKey = Key.E;
        [SerializeField] private Key _releaseKey = Key.LeftCtrl;
        [SerializeField] private Key _downKey = Key.S;
        [SerializeField] private Key _interactKey = Key.F;
        [SerializeField] private Key _inventoryKey = Key.B;

        private bool _jumpDown;
        private bool _grappleDown;
        private bool _jumpWasDown;
        private bool _grappleWasDown;

        public float Horizontal { get; private set; }
        public bool JumpDown => _jumpDown;
        public bool JumpHeld { get; private set; }
        public bool GrappleDown => _grappleDown;
        public bool ReleaseHeld { get; private set; }
        public bool DownHeld { get; private set; }
        public bool InteractDown { get; private set; }
        public bool InventoryDown { get; private set; }
        public bool AttackMainDown { get; private set; }
        public bool AttackMainHeld { get; private set; }
        public bool AttackOffDown { get; private set; }
        public bool AttackOffHeld { get; private set; }
        public Vector2 PointerScreen { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (mouse != null)
            {
                PointerScreen = mouse.position.ReadValue();
                AttackMainDown = mouse.leftButton.wasPressedThisFrame;
                AttackMainHeld = mouse.leftButton.isPressed;
                AttackOffDown = mouse.rightButton.wasPressedThisFrame;
                AttackOffHeld = mouse.rightButton.isPressed;
            }
            else
            {
                AttackMainDown = false;
                AttackMainHeld = false;
                AttackOffDown = false;
                AttackOffHeld = false;
            }

            if (keyboard == null)
            {
                // No keyboard device (e.g. headless batch mode): report neutral input.
                Horizontal = 0f;
                JumpHeld = false;
                DownHeld = false;
                ReleaseHeld = false;
                InteractDown = false;
                InventoryDown = false;
                _jumpDown = false;
                _grappleDown = false;
                return;
            }

            float raw = 0f;
            if (keyboard[_leftKey].isPressed || keyboard.leftArrowKey.isPressed) raw -= 1f;
            if (keyboard[_rightKey].isPressed || keyboard.rightArrowKey.isPressed) raw += 1f;
            Horizontal = raw;

            bool jumpHeld = keyboard[_jumpKey].isPressed
                            || keyboard[_jumpAltKey].isPressed
                            || keyboard.upArrowKey.isPressed;
            bool jumpNow = keyboard[_jumpKey].wasPressedThisFrame
                           || keyboard[_jumpAltKey].wasPressedThisFrame
                           || keyboard.upArrowKey.wasPressedThisFrame;
            _jumpDown = jumpNow && !_jumpWasDown;
            _jumpWasDown = jumpNow;
            JumpHeld = jumpHeld;

            bool grappleNow = keyboard[_grappleKey].wasPressedThisFrame;
            _grappleDown = grappleNow && !_grappleWasDown;
            _grappleWasDown = grappleNow;

            ReleaseHeld = keyboard[_releaseKey].isPressed;
            DownHeld = keyboard[_downKey].isPressed || keyboard.downArrowKey.isPressed;
            InteractDown = keyboard[_interactKey].wasPressedThisFrame;
            InventoryDown = keyboard[_inventoryKey].wasPressedThisFrame;
        }
    }
}
