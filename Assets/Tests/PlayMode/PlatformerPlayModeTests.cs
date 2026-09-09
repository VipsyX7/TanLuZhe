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
        private static PhysicsMaterial2D _frictionless;

        // ---------------------------------------------------------------- harness
        private sealed class ScriptedInputSource : IInputSource
        {
            // Edge flags self-clear on the first read so a press is consumed exactly once, no
            // matter how many Update calls the test host squeezes into one frame.
            private bool _jumpDown;
            private bool _grappleDown;
            private bool _interactDown;
            private bool _inventoryDown;
            private bool _attackMainDown;
            private bool _attackOffDown;

            public float Horizontal { get; set; }
            public bool JumpDown { get => Consume(ref _jumpDown); set => _jumpDown = value; }
            public bool JumpHeld { get; set; }
            public bool GrappleDown { get => Consume(ref _grappleDown); set => _grappleDown = value; }
            public bool ReleaseHeld { get; set; }
            public bool DownHeld { get; set; }
            public bool InteractDown { get => Consume(ref _interactDown); set => _interactDown = value; }
            public bool InventoryDown { get => Consume(ref _inventoryDown); set => _inventoryDown = value; }
            public bool AttackMainDown { get => Consume(ref _attackMainDown); set => _attackMainDown = value; }
            public bool AttackMainHeld { get; set; }
            public bool AttackOffDown { get => Consume(ref _attackOffDown); set => _attackOffDown = value; }
            public bool AttackOffHeld { get; set; }
            public Vector2 PointerScreen { get; set; }

            private static bool Consume(ref bool flag)
            {
                if (!flag) return false;
                flag = false;
                return true;
            }

            public void ClearEdges()
            {
                _jumpDown = false;
                _grappleDown = false;
                _interactDown = false;
                _inventoryDown = false;
                _attackMainDown = false;
                _attackOffDown = false;
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
            collider.sharedMaterial = Frictionless();
            return go;
        }

        /// <summary>The game uses frictionless physics materials; mirror that so tests measure the rope, not friction.</summary>
        private static PhysicsMaterial2D Frictionless()
        {
            if (_frictionless == null)
                _frictionless = new PhysicsMaterial2D("TestFrictionless") { friction = 0f, bounciness = 0f };
            return _frictionless;
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
            rig.Collider.sharedMaterial = Frictionless();

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
            collider.sharedMaterial = Frictionless();

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
            Rig player = MakePlayer(new Vector2(0.02f, 8f));

            _input.Horizontal = 1f;
            yield return StepFixed(30);

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
            yield return StepFixed(12);

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "Hook should latch onto the wall.");
            Assert.AreEqual(GrappleAnchorType.Wall, player.Grapple.AnchorType);

            Vector2 anchor = player.Grapple.CurrentAnchorWorld;
            float distanceBefore = Vector2.Distance(player.Body.position, anchor);
            float xBefore = player.Body.position.x;

            yield return StepFixed(4);

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "The pull should still be running.");
            Assert.IsTrue(player.Controller.IsBeingPulled, "The player should be flagged as being pulled.");
            Assert.That(Vector2.Distance(player.Body.position, anchor), Is.LessThan(distanceBefore - 0.3f),
                "The continuous pull must drag the player toward the hook's landing point.");
            Assert.That(player.Body.position.x, Is.GreaterThan(xBefore + 0.15f), "The player must travel toward the wall.");

            // keep flying in until the player arrives; the hook must let go by itself.
            int guard = 0;
            while (player.Grapple.State == GrappleState.Attached && guard++ < 150)
                yield return new WaitForFixedUpdate();

            Assert.AreNotEqual(GrappleState.Attached, player.Grapple.State,
                "Arriving at the landing point must release the hook automatically.");
            Assert.IsFalse(player.Controller.IsBeingPulled);
            Assert.That(player.Body.position.x, Is.GreaterThan(xBefore + 1f), "The player must reach the wall.");

            // The hook must never tunnel through the wall.
            Assert.That(anchor.x, Is.LessThan(6f), "Impact point must be on the near face of the wall.");
            Assert.That(wall.GetComponent<BoxCollider2D>().bounds.min.x, Is.EqualTo(5.5f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Grapple_Wall_AppliesContinuousPullAcceleration()
        {
            MakeBox("Wall", new Vector2(14f, 4f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(14f, 4f)));
            yield return StepFixed(20);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);

            // The pull is an acceleration, so the closing speed has to keep climbing (it is not
            // pinned to a fixed winch speed like before).
            float maxPull = 0f;
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForFixedUpdate();
                maxPull = Mathf.Max(maxPull, player.Grapple.CurrentPullSpeed);
            }

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);
            Assert.That(maxPull, Is.GreaterThan(8f),
                "The hook must keep feeding acceleration into the player instead of holding a fixed speed.");
            Assert.That(maxPull, Is.LessThanOrEqualTo(30.01f), "The pull speed must respect the configured cap.");
        }

        [UnityTest]
        public IEnumerator Grapple_Wall_AutoReleasesOnArrival()
        {
            MakeBox("Wall", new Vector2(6f, 4f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 4f)));
            yield return StepFixed(15);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);
            Assert.IsTrue(player.Controller.IsBeingPulled);

            Vector2 anchor = player.Grapple.CurrentAnchorWorld;
            float closest = float.MaxValue;

            for (int i = 0; i < 150; i++)
            {
                yield return new WaitForFixedUpdate();
                if (player.Grapple.State != GrappleState.Attached) break;
                closest = Mathf.Min(closest, Vector2.Distance(player.Body.position, anchor));
            }

            Assert.AreNotEqual(GrappleState.Attached, player.Grapple.State,
                "The hook must detach by itself once the player reaches the landing point.");
            Assert.IsFalse(player.Controller.IsBeingPulled, "The pull flag must be cleared by the auto release.");
            Assert.That(closest, Is.LessThanOrEqualTo(1.8f),
                "It must actually arrive at the landing point first (within one physics step of the release radius).");
        }

        [UnityTest]
        public IEnumerator Grapple_Attached_IgnoresPlayerInput()
        {
            MakeBox("Wall", new Vector2(14f, 4f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(14f, 4f)));
            yield return StepFixed(20);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);
            Assert.IsTrue(player.Controller.ControlLocked, "Control must be locked while the rope is attached.");

            // Hammer every action key while hooked. None of it may reach the character.
            float maxUpwardSpeed = float.MinValue;
            float maxHorizontalSpeed = 0f;

            for (int i = 0; i < 12; i++)
            {
                _input.Horizontal = i % 2 == 0 ? -1f : 1f;  // steer away, then into the rope
                _input.JumpDown = true;                      // jump
                _input.JumpHeld = true;
                _input.DownHeld = true;                      // drop through one-way platforms

                yield return new WaitForFixedUpdate();

                maxUpwardSpeed = Mathf.Max(maxUpwardSpeed, player.Body.linearVelocity.y);
                maxHorizontalSpeed = Mathf.Max(maxHorizontalSpeed, Mathf.Abs(player.Body.linearVelocity.x));
            }

            _input.JumpDown = false;
            _input.JumpHeld = false;
            _input.DownHeld = false;
            _input.Horizontal = 0f;

            Assert.That(maxUpwardSpeed, Is.LessThan(8f),
                "Jump input must be ignored while the rope is attached (a jump would reach ~16 m/s).");
            Assert.That(maxHorizontalSpeed, Is.GreaterThan(1f),
                "The rope, not the input, should be moving the player.");
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State,
                "The action keys must not be able to cancel or fight the rope.");
        }

        [UnityTest]
        public IEnumerator Grapple_Attached_SuspendsGravity()
        {
            // Horizontal rope and a freshly zeroed velocity: the only thing that could move the
            // player down is gravity, so any sink proves gravity is still active.
            MakeBox("Wall", new Vector2(10f, 2f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(10f, 2f)));
            yield return StepFixed(15);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);
            Assert.IsTrue(player.Controller.ControlLocked);

            player.Body.linearVelocity = Vector2.zero;
            float yBefore = player.Body.position.y;
            yield return StepFixed(6);

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "Keep the rope attached for this window.");
            Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(-0.5f),
                "Gravity must be suspended while the rope is attached (the player must not be falling).");
            Assert.That(player.Body.position.y, Is.GreaterThan(yBefore - 0.1f),
                "A weightless player must not sink while the rope is attached (gravity would drop ~0.5 here).");
            Assert.That(player.Body.linearVelocity.x, Is.GreaterThan(2f), "The rope must still be pulling sideways.");
        }

        [UnityTest]
        public IEnumerator Grapple_Extending_LeavesPlayerInControl()
        {
            MakeBox("Ground", new Vector2(0f, -0.5f), new Vector2(40f, 1f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 0.76f));
            yield return StepFixed(30);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(15f, 1.5f)));  // long flight, nothing to hit
            Assert.AreEqual(GrappleState.Extending, player.Grapple.State);
            Assert.IsFalse(player.Controller.ControlLocked, "A hook in flight must not lock the player.");

            // steering still works
            float xBefore = player.Body.position.x;
            _input.Horizontal = 1f;
            yield return StepFixed(4);
            Assert.AreEqual(GrappleState.Extending, player.Grapple.State, "Keep the hook in flight.");
            Assert.That(player.Body.position.x, Is.GreaterThan(xBefore + 0.15f),
                "Air steering must still work while the hook is flying.");

            // and jumping still works
            _input.JumpDown = true;
            _input.JumpHeld = true;
            yield return new WaitForFixedUpdate();
            _input.JumpDown = false;

            Assert.AreEqual(GrappleState.Extending, player.Grapple.State, "Keep the hook in flight.");
            Assert.That(player.Body.linearVelocity.y, Is.GreaterThan(5f),
                "Jumping must still work while the hook is flying.");

            _input.JumpHeld = false;
            _input.Horizontal = 0f;
        }

        [UnityTest]
        public IEnumerator Grapple_Wall_PreservesTangentialMomentum()
        {
            MakeBox("Wall", new Vector2(8f, 2f), new Vector2(1f, 8f), GameLayers.Ground);
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            // Fire horizontally into the wall while moving straight up: the rope direction and
            // the velocity are perpendicular, so 100% of the motion is tangential. The pull may
            // only change the radial part, so the upward swing has to survive.
            player.Body.linearVelocity = new Vector2(0f, 20f);
            Assert.IsTrue(player.Grapple.FireAt(new Vector2(8f, 2f)));
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

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 2f)));

            int guard = 0;
            while (player.Grapple.State == GrappleState.Extending && guard++ < 60)
                yield return new WaitForFixedUpdate();

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "Hook should latch onto the enemy.");
            Assert.AreEqual(GrappleAnchorType.Enemy, player.Grapple.AnchorType);
            Assert.IsTrue(player.Grapple.IsAttachedToEnemy);

            float playerXBefore = player.Body.position.x;
            float enemyXBefore = enemy.Body.position.x;
            float playerXAtContact = playerXBefore;
            float enemyXAtContact = enemyXBefore;
            float closest = float.MaxValue;

            // Both bodies are dragged toward each other until they actually meet, which is when
            // the hook releases by itself. Sample the last attached frame, before the collision
            // bounce can push them apart again.
            for (int i = 0; i < 150; i++)
            {
                yield return new WaitForFixedUpdate();
                if (player.Grapple.State != GrappleState.Attached) break;

                playerXAtContact = player.Body.position.x;
                enemyXAtContact = enemy.Body.position.x;
                closest = Mathf.Min(closest, Vector2.Distance(player.Body.position, enemy.Body.position));
            }

            Assert.That(enemyXAtContact - enemyXBefore, Is.LessThan(-0.2f),
                "The enemy must be dragged toward the player.");
            Assert.That(playerXAtContact - playerXBefore, Is.GreaterThan(0.1f),
                "The player must be dragged toward the enemy.");
            Assert.That(closest, Is.LessThan(1.6f), "The two bodies must actually meet.");
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

            yield return StepFixed(4);

            // Only the horizontal axis is compared, and only for a few steps: gravity acts
            // vertically, the rope here is horizontal, and stopping before contact keeps the
            // measurement purely about the rope's impulses.
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
            int guard = 0;
            while (player.Grapple.State == GrappleState.Extending && guard++ < 60)
                yield return new WaitForFixedUpdate();
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);
            Assert.IsTrue(player.Controller.ControlLocked, "Control must be locked while hooked.");

            // Let the pull build up real speed toward the anchor.
            yield return StepFixed(4);
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "The rope should still be attached.");
            Assert.That(player.Body.linearVelocity.x, Is.GreaterThan(1f), "The pull should have built up speed.");

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

            // If the rope were still attached, the continuous pull would immediately add another
            // ~1.2 m/s toward the anchor every step; a coasting body proves the pull is gone.
            yield return StepFixed(20);
            Assert.AreEqual(GrappleState.Idle, player.Grapple.State, "The cut rope must retract and go idle.");
            Assert.IsFalse(player.Controller.IsBeingPulled, "Nothing may keep pulling after the release.");
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
            int guard = 0;
            while (player.Grapple.State == GrappleState.Extending && guard++ < 60)
                yield return new WaitForFixedUpdate();
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);

            Object.Destroy(enemy.Root);
            yield return StepFixed(5);

            Assert.AreNotEqual(GrappleState.Attached, player.Grapple.State,
                "The hook must let go when its target is destroyed.");
            Assert.IsFalse(player.Controller.IsBeingPulled);
        }

        [UnityTest]
        public IEnumerator Grapple_Enemy_HeadStaysPinnedToTarget()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            Rig enemy = MakeEnemy(new Vector2(6f, 2f), 1.5f);
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(6f, 2f)));
            yield return StepFixed(20);

            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);
            Assert.IsTrue(player.Grapple.IsAttachedToEnemy);

            // Drag the enemy around by hand: the hook head must stay glued onto it.
            for (int i = 0; i < 6; i++)
            {
                enemy.Body.position += new Vector2(0.3f, 0.18f);
                yield return null; // a frame, so the visual pinning runs

                Assert.AreEqual(GrappleState.Attached, player.Grapple.State, "The hook must stay attached.");
                Assert.That(Vector2.Distance(player.Grapple.HeadPosition, player.Grapple.CurrentAnchorWorld),
                    Is.LessThan(0.05f), "The hook head must sit exactly on the hooked enemy.");
            }
        }

        [UnityTest]
        public IEnumerator Grapple_Enemy_ContactReleasesHook()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            Rig enemy = MakeEnemy(new Vector2(4f, 2f), 1f);
            yield return StepFixed(2);

            Assert.IsTrue(player.Grapple.FireAt(new Vector2(4f, 2f)));
            int guard = 0;
            while (player.Grapple.State == GrappleState.Extending && guard++ < 60)
                yield return new WaitForFixedUpdate();
            Assert.AreEqual(GrappleState.Attached, player.Grapple.State);
            Assert.IsTrue(player.Grapple.IsAttachedToEnemy);

            for (int i = 0; i < 150 && player.Grapple.State == GrappleState.Attached; i++)
                yield return new WaitForFixedUpdate();

            Assert.AreNotEqual(GrappleState.Attached, player.Grapple.State,
                "Touching the hooked enemy must cut the rope automatically.");
            Assert.IsFalse(player.Controller.IsBeingPulled, "The pull flag must be cleared.");
            Assert.That(Vector2.Distance(player.Body.position, enemy.Body.position), Is.LessThan(1.6f),
                "The hook must only let go once the player has actually reached the enemy.");
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
