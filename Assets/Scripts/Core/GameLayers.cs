using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Central definition of the project's physics layers plus the collision rules that are
    /// applied at runtime. Rules are applied in code instead of the Project Settings matrix so
    /// they are explicit, version-controllable and unit-testable.
    /// </summary>
    public static class GameLayers
    {
        public const int Ground = 8;
        public const int Player = 9;
        public const int Enemy = 10;
        public const int OneWay = 11;
        public const int Hazard = 12;
        public const int Collectible = 13;

        public static readonly string[] Names =
        {
            "Ground", "Player", "Enemy", "OneWay", "Hazard", "Collectible"
        };

        /// <summary>Everything the player can stand on, slide along or wall jump from.</summary>
        public static int SolidMask => (1 << Ground) | (1 << OneWay) | (1 << Enemy);

        /// <summary>Layers the grapple hook is allowed to latch onto.</summary>
        public static int GrappleMask => (1 << Ground) | (1 << OneWay) | (1 << Enemy);

        /// <summary>Enemy navigation probes.</summary>
        public static int EnemyGroundMask => (1 << Ground) | (1 << OneWay);

        private static bool _applied;

        /// <summary>
        /// Applies the layer collision matrix. Safe to call repeatedly; the engine only records
        /// the change once.
        /// </summary>
        public static void ApplyCollisionRules()
        {
            if (_applied) return;
            _applied = true;

            // Enemies should never shove each other around: it destroys their pathing.
            Physics2D.IgnoreLayerCollision(Enemy, Enemy, true);

            // Pickups and hazards are trigger volumes; they must not push bodies.
            Physics2D.IgnoreLayerCollision(Collectible, Collectible, true);
            Physics2D.IgnoreLayerCollision(Collectible, Enemy, true);
            Physics2D.IgnoreLayerCollision(Collectible, Hazard, true);
            Physics2D.IgnoreLayerCollision(Hazard, Hazard, true);
        }
    }
}
