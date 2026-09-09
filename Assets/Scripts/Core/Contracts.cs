using UnityEngine;

namespace TanLuZhe
{
    /// <summary>Kind of surface a grapple hook latched onto.</summary>
    public enum GrappleAnchorType
    {
        None = 0,
        Wall = 1,
        Enemy = 2,
        Moving = 3,
    }

    /// <summary>Directional damage payload used by the damage system.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector2 SourcePosition;
        public readonly Vector2 Knockback;
        public readonly GameObject Instigator;

        public DamageInfo(float amount, Vector2 sourcePosition, Vector2 knockback, GameObject instigator = null)
        {
            Amount = amount;
            SourcePosition = sourcePosition;
            Knockback = knockback;
            Instigator = instigator;
        }
    }

    /// <summary>Anything that can receive damage.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(in DamageInfo info);
    }

    /// <summary>
    /// A rigidbody that a grapple hook can latch onto. Implemented by enemies and by
    /// kinematic moving platforms so the hook can pull on them with correct mass.
    /// </summary>
    public interface IGrappleTarget
    {
        /// <summary>World-space point the rope should attach to.</summary>
        Vector2 GetGrappleAnchorPoint(Vector2 hitPoint);

        /// <summary>Body that the rope pulls on. Null for fully static geometry.</summary>
        Rigidbody2D GetGrappleBody();

        /// <summary>Called once when the hook latches on.</summary>
        void OnGrappleAttached(Vector2 anchorPoint, in DamageInfo impact);

        /// <summary>Called once when the rope is released, cut, or the target dies.</summary>
        void OnGrappleDetached();

        /// <summary>Relative mass the rope should fight against; 1 = normal.</summary>
        float GrappleResistance { get; }
    }
}
