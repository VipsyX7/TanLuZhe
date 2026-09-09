using UnityEngine;

namespace TanLuZhe
{
    /// <summary>
    /// Installs the physics layer collision rules before the first simulation step of the scene.
    /// Put one of these in every gameplay scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [AddComponentMenu("TanLuZhe/Physics Bootstrap 2D")]
    public sealed class PhysicsBootstrap2D : MonoBehaviour
    {
        private void Awake() => GameLayers.ApplyCollisionRules();
    }
}
