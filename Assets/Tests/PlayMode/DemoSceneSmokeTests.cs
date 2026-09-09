using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TanLuZhe.Tests
{
    /// <summary>
    /// End to end smoke test: loads the real generated demo scene, injects deterministic input
    /// and drives a short run. Any exception, missing reference or error log in the scene fails
    /// the test automatically, so this covers the whole wiring (camera rig, HUD, FX pool, enemy
    /// AI, moving platform, grapple) rather than a single component in isolation.
    /// </summary>
    public sealed class DemoSceneSmokeTests
    {
        private const string SceneName = "PlatformerDemo";

        private sealed class ScriptedInputSource : IInputSource
        {
            public float Horizontal { get; set; }
            public bool JumpDown { get; set; }
            public bool JumpHeld { get; set; }
            public bool GrappleDown { get; set; }
            public bool ReleaseHeld { get; set; }
            public bool DownHeld { get; set; }
            public Vector2 PointerScreen { get; set; } = new Vector2(1600f, 700f);
        }

        private static IEnumerator StepFixed(int steps)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        /// <summary>
        /// The demo scene must not leak into the other tests in this play session, so it is
        /// unloaded again as soon as each smoke test finishes.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene demo = SceneManager.GetSceneByName(SceneName);
            if (demo.IsValid() && demo.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("SmokeTestCleanup");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(demo);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DemoScene_RunsEndToEnd()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            PlayerController2D controller = Object.FindFirstObjectByType<PlayerController2D>();
            GrappleHook2D grapple = Object.FindFirstObjectByType<GrappleHook2D>();
            CameraRig2D rig = Object.FindFirstObjectByType<CameraRig2D>();
            HUDController hud = Object.FindFirstObjectByType<HUDController>();

            Assert.IsNotNull(controller, "Demo scene must contain the player controller.");
            Assert.IsNotNull(grapple, "Demo scene must contain the grapple hook.");
            Assert.IsNotNull(rig, "Demo scene must contain the camera rig.");
            Assert.IsNotNull(hud, "Demo scene must contain the HUD.");

            ScriptedInputSource input = new ScriptedInputSource();
            controller.SetInputSource(input);
            grapple.SetInputSource(input);

            float cameraStartX = rig.transform.position.x;

            // ---- the player settles on the floor
            yield return StepFixed(45);
            Assert.IsTrue(controller.IsGrounded, "The player should land on the starting ground.");

            // ---- run right
            float startX = controller.transform.position.x;
            input.Horizontal = 1f;
            yield return StepFixed(45);
            Assert.That(controller.transform.position.x, Is.GreaterThan(startX + 1f), "The player should run right.");

            // ---- jump
            float groundY = controller.transform.position.y;
            input.JumpDown = true;
            input.JumpHeld = true;
            yield return new WaitForFixedUpdate();
            input.JumpDown = false;

            float peak = groundY;
            for (int i = 0; i < 25; i++)
            {
                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, controller.transform.position.y);
            }
            input.JumpHeld = false;
            input.Horizontal = 0f;
            Assert.That(peak, Is.GreaterThan(groundY + 1.2f), "The player should leave the ground when jumping.");

            yield return StepFixed(45);

            // ---- grapple the tall wall at x = 36..39 and winch up its right face.
            // Approaching from ground C (39..44) keeps the test clear of the patrolling enemy
            // that owns ground B, so the result depends only on the rope.
            controller.Teleport(new Vector2(42f, 1f));
            yield return StepFixed(30);
            Assert.IsTrue(controller.IsGrounded,
                $"Teleport target should be on the ground (pos={controller.transform.position}, vel={controller.Body.linearVelocity}).");

            Assert.IsTrue(grapple.FireAt(new Vector2(39f, 5f)), "The hook should launch toward the wall.");
            yield return StepFixed(15);
            Assert.AreEqual(GrappleState.Attached, grapple.State, "The hook should latch onto the level geometry.");
            Assert.AreEqual(GrappleAnchorType.Wall, grapple.AnchorType);

            float yBeforePull = controller.transform.position.y;
            yield return StepFixed(60);
            Assert.That(controller.transform.position.y, Is.GreaterThan(yBeforePull + 1f),
                "Winching toward the impact point should lift the player up the wall.");

            // ---- cut the rope with Left Ctrl
            Vector2 velocityAtRelease = controller.Body.linearVelocity;
            input.ReleaseHeld = true;
            yield return null;
            input.ReleaseHeld = false;
            yield return StepFixed(2);

            Assert.AreNotEqual(GrappleState.Attached, grapple.State, "Left Ctrl should cut the rope.");
            Assert.IsFalse(controller.IsBeingPulled, "The pull flag should be cleared after the release.");
            Assert.That(controller.Body.linearVelocity.x, Is.EqualTo(velocityAtRelease.x).Within(0.4f),
                "Releasing must not destroy the horizontal momentum.");

            yield return StepFixed(25);
            Assert.AreEqual(GrappleState.Idle, grapple.State, "The cut rope should retract back to the player.");

            // ---- grapple an enemy. Pick the patroller on the opening flat ground so the line
            // from the player to the enemy is clear of platforms (the hook would otherwise
            // legitimately latch onto the geometry in between).
            EnemyController2D enemy = null;
            float bestDistance = float.MaxValue;
            foreach (EnemyController2D candidate in Object.FindObjectsByType<EnemyController2D>(FindObjectsSortMode.None))
            {
                float d = Mathf.Abs(candidate.transform.position.x - 12f);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    enemy = candidate;
                }
            }
            Assert.IsNotNull(enemy, "Demo scene must contain enemies.");

            controller.Teleport(new Vector2(enemy.transform.position.x - 5f, 1.5f));
            yield return StepFixed(30);

            Assert.IsTrue(grapple.FireAt(enemy.transform.position));
            yield return StepFixed(25);
            Assert.AreEqual(GrappleState.Attached, grapple.State, "The hook should latch onto the enemy.");
            Assert.AreEqual(GrappleAnchorType.Enemy, grapple.AnchorType);
            Assert.IsTrue(controller.IsBeingPulled, "The player should be flagged as pulled by the enemy rope.");

            yield return StepFixed(30);

            input.ReleaseHeld = true;
            yield return null;
            input.ReleaseHeld = false;
            Assert.IsFalse(controller.IsBeingPulled, "Cutting the enemy rope should clear the pull flag.");

            // ---- the world keeps running: the camera followed, no errors were logged
            yield return StepFixed(20);
            Assert.That(rig.transform.position.x, Is.GreaterThan(cameraStartX + 2f),
                "The camera should have followed the player across the level.");
        }

        [UnityTest]
        public IEnumerator DemoScene_EnemiesPatrolAndPickupsExist()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            EnemyController2D[] enemies = Object.FindObjectsByType<EnemyController2D>(FindObjectsSortMode.None);
            Collectible2D[] coins = Object.FindObjectsByType<Collectible2D>(FindObjectsSortMode.None);
            Checkpoint2D[] checkpoints = Object.FindObjectsByType<Checkpoint2D>(FindObjectsSortMode.None);
            LevelGoal2D[] goals = Object.FindObjectsByType<LevelGoal2D>(FindObjectsSortMode.None);

            Assert.That(enemies.Length, Is.GreaterThanOrEqualTo(4), "The level should have enemies to grapple.");
            Assert.That(coins.Length, Is.GreaterThanOrEqualTo(15), "The level should have coins.");
            Assert.That(checkpoints.Length, Is.GreaterThanOrEqualTo(3), "The level should have checkpoints.");
            Assert.AreEqual(1, goals.Length, "The level should have exactly one goal.");

            Vector3 enemyStart = enemies[0].transform.position;
            yield return StepFixed(60);

            Assert.That(Vector3.Distance(enemies[0].transform.position, enemyStart), Is.GreaterThan(0.1f),
                "Enemies should actually patrol instead of standing still.");
        }
    }
}
