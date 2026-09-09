using UnityEngine;

namespace TanLuZhe
{
    public enum WeaponKind
    {
        Melee = 0,
        Ranged = 1,
    }

    /// <summary>
    /// Data driven weapon. One asset per weapon; the player holds two of them (main hand / off
    /// hand) and the rest live in the backpack.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "TanLuZhe/Weapon", order = 0)]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Weapon";
        [TextArea] public string description = "";
        [Tooltip("Inventory icon.")]
        public Sprite icon;
        [Tooltip("Sprite shown in the character's hand. Must point along +X.")]
        public Sprite worldSprite;
        public Color tint = Color.white;

        [Header("Stats")]
        public WeaponKind kind = WeaponKind.Melee;
        [Tooltip("Damage dealt per hit.")]
        [Range(1f, 200f)] public float damage = 25f;
        [Tooltip("Seconds between two attacks.")]
        [Range(0.05f, 3f)] public float cooldown = 0.45f;
        [Tooltip("Impulse applied to the target along the attack direction.")]
        [Range(0f, 40f)] public float knockback = 9f;
        [Tooltip("Screen shake on a successful hit.")]
        [Range(0f, 2f)] public float hitShake = 0.25f;

        [Header("Melee")]
        [Tooltip("Size of the hit box, in world units.")]
        public Vector2 meleeSize = new Vector2(1.8f, 1.4f);
        [Tooltip("How far in front of the hand the hit box is centred.")]
        [Range(0f, 4f)] public float meleeReach = 0.9f;

        [Header("Ranged")]
        [Tooltip("Projectile speed (m/s).")]
        [Range(1f, 80f)] public float projectileSpeed = 26f;
        [Tooltip("Projectile lifetime in seconds.")]
        [Range(0.1f, 8f)] public float projectileLifetime = 1.4f;
        [Tooltip("Sprite for the projectile. Must point along +X.")]
        public Sprite projectileSprite;
        [Tooltip("How many projectiles are fired at once.")]
        [Range(1, 9)] public int projectileCount = 1;
        [Tooltip("Half angle of the spread when firing more than one projectile (degrees).")]
        [Range(0f, 45f)] public float spread = 8f;
        [Tooltip("Scale of the projectile sprite.")]
        [Range(0.1f, 5f)] public float projectileScale = 1f;

        [Header("Held pose")]
        [Tooltip("Distance from the character centre at which the weapon sprite sits.")]
        [Range(0f, 2f)] public float holdDistance = 0.45f;
        [Tooltip("Extra rotation of the held sprite, in degrees.")]
        [Range(-180f, 180f)] public float holdAngle = 0f;
        [Tooltip("Scale of the held sprite.")]
        [Range(0.1f, 5f)] public float holdScale = 1f;

        public float Cooldown => Mathf.Max(0.02f, cooldown);
    }
}
