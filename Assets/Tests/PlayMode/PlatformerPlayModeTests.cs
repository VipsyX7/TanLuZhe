using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TanLuZhe.Tests
{
    /// <summary>
    /// Play mode tests for the character physics and - most importantly - for the three grapple
    /// rules that were specified:
    ///   1. hit a wall  -> keep the inertia, get pulled toward the impact point,
    ///   2. hit an enemy-> both bodies get dragged toward each other, mass weighted,
    ///   3. Left Ctrl   -> the pull stops, the momentum is kept exactly.
    /// </summary>
    public sealed class PlatformerPlayModeTests
    {
        private const float Dt = 0.02f;

        private GameObject _root;
        private ScriptedInputSource _input;

        // ---------------------------------------------------------------- harness
        private sealed class ScriptedInputSource : IInputSource
        {
            public float Horizontal { get; set; }
            public bool JumpDown { get; set; }
            public bool JumpHeld { get; set; }
            public bool GrappleDown { get; set; }
            public bool ReleaseHeld { get; set; }
            public bool DownHeld { get; set; }
            public Vector2 PointerScreen { get; set; }

            public void ClearEdges()
            {
                JumpDown = false;
                GrappleDown = false;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
            _input = new ScriptedInputSource();
            GameLayers.ApplyCollisionRules();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
        }

        private GameObject MakeBox(string name, Vector2 center, Vector2 size, int layer)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.layer = layer;
            go.transform.position = center;
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            return go;
        }

        private struct Rig
        {
            public GameObject Root;
            public PlayerController2D Controller;
            public PlayerHealth Health;
            public GrappleHook2D Grapple;
            public Rigidbody2D Body;
            public CapsuleCollider2D Collider;
        }

        private Rig MakePlayer(Vector2 position)
        {
            Rig rig = new Rig();
            GameObject go = new GameObject("Player");
            go.transform.SetParent(_root.transform, false);
            go.layer = GameLayers.Player;
            go.transform.position = position;
            rig.Root = go;

            rig.Body = go.AddComponent<Rigidbody2D>();
            rig.Body.gravityScale = 0f;
            rig.Body.mass = 1f;
            rig.Body.freezeRotation = true;

            rig.Collider = go.AddComponent<CapsuleCollider2D>();
            rig.Collider.direction = CapsuleDirection2D.Vertical;
            rig.Collider.size = new Vector2(0.72f, 1.5f);
            rig.Collider.offset = new Vector2(0f, 0.05f);

            rig.Controller = go.AddComponent<PlayerController2D>();
            rig.Controller.SetInputSource(_input);

            rig.Health = go.AddComponent<PlayerHealth>();

            GameObject grappleGO = new GameObject("Grapple");
            grappleGO.transform.SetParent(go.transform, false);
            grappleGO.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            rig.Grapple = grappleGO.AddComponent<GrappleHook2D>();
            rig.Grapple.SetInputSource(_input);

            return rig;
        }

        private Rig MakeEnemy(Vector2 position, float mass)
        {
            Rig rig = new Rig();
            GameObject go = new GameObject("Enemy");
            go.transform.SetParent(_root.transform, false);
            go.layer = GameLayers.Enemy;
            go.transform.position = position;
            rig.Root = go;

            rig.Body = go.AddComponent<Rigidbody2D>();
            rig.Body.mass = mass;
            rig.Body.gravityScale = 0f; // keep the test free of gravity so rope math is isolated
            rig.Body.freezeRotation = true;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 0.8f);

            EnemyController2D enemy = go.AddComponent<EnemyController2D>();
            enemy.enabled = false; // test the physics, not the AI

            return rig;
        }

        private static IEnumerator StepFixed(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static Vector2 AnchorDistance(Rig player, GrappleHook2D hook)
        {
            return hook.CurrentAnchorWorld - (Vector2)player.Body.position;
        }

        // ================================================================ physics
        [UnityTest]
        public IEnumerator Player_FallsAndRestsOnGround()
        {
            MakeBox("Ground", new Vector2(0f, -0.5f), new Vector2(60f, 1f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 3f));

            yield return StepFixed(90);

            Assert.IsTrue(player.Controller.IsGrounded, "Player should be grounded after falling onto the floor.");
            Assert.That(player.Body.position.y, Is.EqualTo(0.75f).Within(0.12f), "Player should rest on top of the ground.");
            Assert.That(Mathf.Abs(player.Body.linearVelocity.y), Is.LessThan(0.4f), "Player should not be bouncing.");
        }

        [UnityTest]
        public IEnumerator Player_JumpReachesConfiguredHeight()
        {
            MakeBox("Ground", new Vector2(0f, -0.5f), new Vector2(60f, 1f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 0.76f));

            yield return StepFixed(30);
            float startY = player.Body.position.y;

            _input.JumpDown = true;
            _input.JumpHeld = true;
            yield return new WaitForFixedUpdate();
            _input.ClearEdges();

            float peak = startY;
            for (int i = 0; i < 90; i++)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, player.Body.position.y);
            }

            float height = peak - startY;
            Assert.That(height, Is.EqualTo(3.1f).Within(0.6f), "Jump apex should be close to the configured 3.1 units.");
        }

        [UnityTest]
        public IEnumerator Player_KeepsAirMomentum_WithoutInput()
        {
            Rig player = MakePlayer(new Vector2(0f, 6f));
            yield return StepFixed(2);

            player.Body.linearVelocity = new Vector2(12f, 0f);
            yield return StepFixed(30);

            Assert.That(player.Body.linearVelocity.x, Is.GreaterThan(11f),
                "With no input the controller must not scrub off horizontal air momentum.");
        }

        [UnityTest]
        public IEnumerator Player_WallSlideLimitsFallSpeed()
        {
            MakeBox("Ground", new Vector2(0f, -0.5f), new Vector2(40f, 1f), GameLayers.Ground);
            MakeBox("Wall", new Vector2(0.9f, 5f), new Vector2(1f, 12f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0.2f, 6f));

            _input.Horizontal = 1f;
            yield return StepFixed(60);

            Assert.IsTrue(player.Controller.IsWallSliding, "Holding into a wall while falling should trigger a wall slide.");
            Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(-4.9f),
                "Wall slide must clamp the fall speed.");
        }

        // ================================================================ grapple: wall
        [UnityTest]
        public IEnumerator Grapple_Wall_PullsPlayerTowardImpactPoint()
        {
            MakeBox("Ground", new Vector2(0f, -0.5f), new Vector2(60f, 1f), GameLayers.Ground);
            GameObject wall = MakeBox("Wall", new Vector2(6f, 4f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 0.76f));

            yield return StepFixed(30);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 4f)), "Hook should launch.");
            yield return StepFixed(20);

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "Hook should latch onto the wall.");
            Assert.AreEqual(GrappleAnchorType.Wall, player.Grapple.AnchorType);

            Vector2 anchor = player.Grapple.CurrentAnchorWorld;
            float distanceBefore = Vector2.Distance(player.Body.position, anchor);
            float xBefore = player.Body.position.x;

            yield return StepFixed(70);

            float distanceAfter = Vector2.Distance(player.Body.position, anchor);
            Assert.That(distanceAfter, Is.LessThan(distanceBefore - 1f),
                "The winch must pull the player toward the hook's landing point.");
            Assert.That(player.Body.position.x, Is.GreaterThan(xBefore + 1f), "The player must travel toward the wall.");
            Assert.IsTrue(player.Controller.IsBeingPulled, "The player should be flagged as being pulled.");
            Assert.IsTrue(player.Grapple.IsRopeTaut, "The rope should be taut while winching.");

            // The hook must never tunnel through the wall.
            Assert.That(anchor.x, Is.LessThan(6f), "Impact point must be on the near face of the wall.");
            Assert.That(wall.GetComponent<BoxCollider2D>().bounds.min.x, Is.EqualTo(5.5f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Grapple_Wall_PreservesTangentialMomentum()
        {
            MakeBox("Wall", new Vector2(4f, 2f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            // Fire horizontally into the wall while moving straight up: the rope direction and
            // the velocity are perpendicular, so 100% of the motion is tangential. A rigid rope
            // may only cancel the radial part, so the upward swing has to survive.
            player.Body.linearVelocity = new Vector2(0f, 20f);
            Assert.IsTrue(player.Grapple.FireAt(new Vector2(4f, 2f)));
            yield return StepFixed(8);

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "Hook should latch onto the wall.");

            Vector2 anchor = player.Grapple.CurrentAnchorWorld;
            Vector2 dirBefore = (anchor - (Vector2)player.Body.position).normalized;
            Vector2 vBefore = player.Body.linearVelocity;
            float radialBefore = Vector2.Dot(vBefore, dirBefore);
            float tangentialBefore = (vBefore - radialBefore * dirBefore).magnitude;

            Assert.That(tangentialBefore, Is.GreaterThan(6f), "Test setup: the motion must be mostly tangential.");

            yield return StepFixed(6);

            Vector2 dirAfter = (player.Grapple.CurrentAnchorWorld - (Vector2)player.Body.position).normalized;
            Vector2 vAfter = player.Body.linearVelocity;
            float radialAfter = Vector2.Dot(vAfter, dirAfter);
            float tangentialAfter = (vAfter - radialAfter * dirAfter).magnitude;

            Assert.That(player.Grapple.IsAttached, Is.True, "The rope must still be attached.");
            Assert.That(tangentialAfter, Is.GreaterThan(tangentialBefore * 0.55f),
                "The rope must only remove the radial velocity; the tangential swing (the inertia) has to survive.");
            Assert.That(radialAfter, Is.Not.EqualTo(radialBefore).Within(0.01f),
                "The radial component is the part the rope is allowed to change.");
        }

        // ================================================================ grapple: enemy
        [UnityTest]
        public IEnumerator Grapple_Enemy_PullsBothBodiesTogether()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            Rig enemy = MakeEnemy(new Vector2(6f, 2f), 4f);
            yield return StepFixed(2);

            float playerXBefore = player.Body.position.x;
            float enemyXBefore = enemy.Body.position.x;

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 2f)));
            yield return StepFixed(20);

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "Hook should latch onto the enemy.");
            Assert.AreEqual(GrappleAnchorType.Enemy, player.Grapple.AnchorType);
            Assert.IsTrue(player.Grapple.IsAttachedToEnemy);

            yield return StepFixed(40);

            float playerDelta = player.Body.position.x - playerXBefore;
            float enemyDelta = enemy.Body.position.x - enemyXBefore;

            Assert.That(enemyDelta, Is.LessThan(-0.2f), "The enemy must be dragged toward the player.");
            Assert.That(playerDelta, Is.GreaterThan(0.1f), "The player must be dragged toward the enemy.");
            Assert.That(Vector2.Distance(player.Body.position, enemy.Body.position), Is.LessThan(6f),
                "The two bodies must close the distance between them.");
        }

        [UnityTest]
        public IEnumerator Grapple_Enemy_PullIsMassWeighted()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            Rig heavy = MakeEnemy(new Vector2(6f, 2f), 4f);
            yield return StepFixed(2);

            Vector2 playerVelocityBefore = player.Body.linearVelocity;
            Vector2 enemyVelocityBefore = heavy.Body.linearVelocity;

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 2f)));
            yield return StepFixed(20);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);

            yield return StepFixed(15);

            // Only the horizontal axis is compared: gravity acts vertically and the rope here is
            // horizontal, so every horizontal velocity change comes from the rope's impulses.
            float playerSpeedGain = Mathf.Abs(player.Body.linearVelocity.x - playerVelocityBefore.x);
            float enemySpeedGain = Mathf.Abs(heavy.Body.linearVelocity.x - enemyVelocityBefore.x);

            Assert.That(enemySpeedGain, Is.GreaterThan(0.05f), "The enemy must actually be moved by the rope.");
            Assert.That(playerSpeedGain, Is.GreaterThan(enemySpeedGain * 2f),
                "A 4x heavier enemy must be moved ~4x less than the player (equal and opposite impulses).");
        }

        // ================================================================ grapple: release
        [UnityTest]
        public IEnumerator Grapple_LeftCtrl_ReleasesAndKeepsInertia()
        {
            // Airborne on purpose: on the ground the controller is allowed to brake, in the air
            // it must not touch the momentum, so the rope release is the only variable here.
            MakeBox("Wall", new Vector2(6f, 4f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 2f));

            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 4f)));
            yield return StepFixed(12);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);

            // Let the winch build up real speed toward the anchor.
            yield return StepFixed(12);
            Assert.That(player.Body.linearVelocity.x, Is.GreaterThan(1f), "The winch should have built up speed.");

            // Press Left Ctrl exactly like the player would.
            _input.ReleaseHeld = true;
            yield return null; // Update() sees the key and cuts the rope
            _input.ReleaseHeld = false;

            Vector2 velocityAtRelease = player.Body.linearVelocity;

            Assert.AreNotEqual(GrappleState.Attached, player.Grapple.State, "Left Ctrl must detach the hook.");
            Assert.IsFalse(player.Controller.IsBeingPulled, "The pull flag must be cleared.");
            Assert.AreEqual(GrappleAnchorType.None, player.Grapple.AnchorType);

            yield return StepFixed(3);

            Assert.That(player.Body.linearVelocity.x, Is.EqualTo(velocityAtRelease.x).Within(0.3f),
                "Horizontal inertia must survive the release untouched.");

            // If the rope were still attached, the winch would immediately pin the closing speed
            // back to its top value; a coasting body proves the pull is really gone.
            yield return StepFixed(20);
            Assert.AreEqual(GrappleState.Idle, player.Grapple.State, "The cut rope must retract and go idle.");
            Assert.That(player.Body.linearVelocity.x, Is.EqualTo(velocityAtRelease.x).Within(0.5f),
                "Nothing may keep accelerating the player toward the old anchor after the release.");
        }

        // ================================================================ grapple: misc
        [UnityTest]
        public IEnumerator Grapple_Miss_RetractsToIdle()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(15f, 2f)));
            Assert.AreEqual(GrappleState.Extending, player.Grapple.State);

            yield return StepFixed(60);

            Assert.AreEqual(GrappleState.Idle, player.Grapple.State, "A missed hook must retract and return to idle.");
            Assert.AreEqual(GrappleAnchorType.None, player.Grapple.AnchorType);
        }

        [UnityTest]
        public IEnumerator Grapple_EnemyDeath_DetachesHook()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            Rig enemy = MakeEnemy(new Vector2(4f, 2f), 1f);
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(4f, 2f)));
            yield return StepFixed(15);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);

            Object.Destroy(enemy.Root);
            yield return StepFixed(5);

            Assert.AreNotEqual(GrappleState.Attached, player.Grapple.State,
                "The hook must let go when its target is destroyed.");
            Assert.IsFalse(player.Controller.IsBeingPulled);
        }

        [UnityTest]
        public IEnumerator Grapple_ReelSpeedRampsUp()
        {
            MakeBox("Wall", new Vector2(6f, 4f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 4f));
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 4f)));
            yield return StepFixed(12);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);

            float early = player.Grapple.CurrentReelSpeed;
            yield return StepFixed(20);
            float later = player.Grapple.CurrentReelSpeed;

            Assert.That(later, Is.GreaterThan(early), "The winch must accelerate instead of snapping to full speed.");
            Assert.That(later, Is.LessThanOrEqualTo(9.51f), "Winch speed must respect the configured maximum.");
        }

        [UnityTest]
        public IEnumerator Enemy_MassHeavier_MeansLessAcceleration()
        {
            Rig light = MakeEnemy(new Vector2(0f, 2f), 1f);
            yield return StepFixed(2);
            light.Body.AddForce(Vector2.right * 10f, ForceMode2D.Impulse);
            yield return StepFixed(1);
            float lightSpeed = light.Body.linearVelocity.x;

            Rig heavy = MakeEnemy(new Vector2(0f, 2f), 4f);
            yield return StepFixed(2);
            heavy.Body.AddForce(Vector2.right * 10f, ForceMode2D.Impulse);
            yield return StepFixed(1);
            float heavySpeed = heavy.Body.linearVelocity.x;

            Assert.That(lightSpeed, Is.GreaterThan(heavySpeed * 2f), "Mass must matter for impulses.");
            Assert.That(Dt, Is.GreaterThan(0f));
        }
    }
}
