using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Abstraction over player input so that gameplay code never touches the Input System
    /// directly. This keeps the character controller testable: automated tests inject a
    /// scripted source and drive the player deterministically.
    /// </summary>
    public interface IInputSource
    {
        /// <summary>Horizontal axis in [-1, 1].</summary>
        float Horizontal { get; }

        /// <summary>True only on the frame the jump key went down.</summary>
        bool JumpDown { get; }

        /// <summary>True while the jump key is held (used for variable jump height).</summary>
        bool JumpHeld { get; }

        /// <summary>True only on the frame the grapple key (E) went down.</summary>
        bool GrappleDown { get; }

        /// <summary>True while the release key (Left Ctrl) is held.</summary>
        bool ReleaseHeld { get; }

        /// <summary>True while the down/crouch key is held (drop through one-way platforms).</summary>
        bool DownHeld { get; }

        /// <summary>True only on the frame the interact key (F) went down: pick up a weapon.</summary>
        bool InteractDown { get; }

        /// <summary>True only on the frame the inventory key (B) went down.</summary>
        bool InventoryDown { get; }

        /// <summary>True only on the frame the main-hand attack button (left mouse) went down.</summary>
        bool AttackMainDown { get; }

        /// <summary>True while the main-hand attack button is held.</summary>
        bool AttackMainHeld { get; }

        /// <summary>True only on the frame the off-hand attack button (right mouse) went down.</summary>
        bool AttackOffDown { get; }

        /// <summary>True while the off-hand attack button is held.</summary>
        bool AttackOffHeld { get; }

        /// <summary>Pointer position in screen pixels, used to aim the grapple hook and weapons.</summary>
        Vector2 PointerScreen { get; }
    }
}
