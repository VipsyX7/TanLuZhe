using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TanLuZhe.EditorTools
{
    /// <summary>
    /// Static verification of the generated demo scene. Runs head-less in batch mode and exits
    /// with a non-zero code if anything is missing, which makes the scene build part of CI
    /// instead of something a human has to eyeball.
    /// </summary>
    public static class SceneValidator
    {
        private static int _errors;
        private static int _checks;

        [MenuItem("TanLuZhe/3. Validate Demo Scene", false, 3)]
        public static void Validate()
        {
            EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath, OpenSceneMode.Single);
            ValidateLoadedScene();
        }

        public static void ValidateFromCommandLine()
        {
            EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath, OpenSceneMode.Single);
            ValidateLoadedScene();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(_errors == 0 ? 0 : 1);
            }
        }

        private static void ValidateLoadedScene()
        {
            _errors = 0;
            _checks = 0;

            CheckNoMissingScripts();

            // ---- camera
            Camera camera = Object.FindFirstObjectByType<Camera>();
            Check(camera != null, "Main Camera exists");
            if (camera != null)
            {
                Check(camera.orthographic, "Camera is orthographic");
                Check(camera.orthographicSize > 1f, "Camera orthographic size is sane");
                Check(camera.transform.position.z < -1f, "Camera sits in front of the sprite plane");
                CameraRig2D rig = camera.GetComponent<CameraRig2D>();
                Check(rig != null, "Camera has a CameraRig2D");
                if (rig != null) Check(GetRef(rig, "_target") != null, "CameraRig2D has a follow target");
            }

            Check(Object.FindFirstObjectByType<Light2D>() != null, "Global Light 2D exists (lit sprites need it)");
            Check(Object.FindFirstObjectByType<PhysicsBootstrap2D>() != null, "PhysicsBootstrap2D exists");
            Check(Object.FindFirstObjectByType<GameManager>() != null, "GameManager exists");

            FxManager fx = Object.FindFirstObjectByType<FxManager>();
            Check(fx != null, "FxManager exists");
            if (fx != null)
            {
                Check(GetRef(fx, "_sparkSprite") != null, "FxManager spark sprite assigned");
                Check(GetRef(fx, "_dustSprite") != null, "FxManager dust sprite assigned");
            }

            // ---- player
            PlayerController2D controller = Object.FindFirstObjectByType<PlayerController2D>();
            Check(controller != null, "Player exists");
            if (controller != null)
            {
                Check(controller.GetComponent<Rigidbody2D>() != null, "Player has a Rigidbody2D");
                Check(controller.GetComponent<CapsuleCollider2D>() != null, "Player has a CapsuleCollider2D");
                Check(controller.GetComponent<PlayerInputSource>() != null, "Player has a PlayerInputSource");
                Check(GetInt(controller, "_groundMask") != 0, "Player ground mask is configured");
                Check(controller.GetComponent<Rigidbody2D>().collisionDetectionMode == CollisionDetectionMode2D.Continuous,
                    "Player uses continuous collision detection (fast rope yanks)");

                PlayerHealth health = controller.GetComponent<PlayerHealth>();
                Check(health != null, "Player has health");
                if (health != null) Check(GetRef(health, "_visual") != null, "Player health has a visual to flash");
            }

            GrappleHook2D grapple = Object.FindFirstObjectByType<GrappleHook2D>();
            Check(grapple != null, "Grapple hook exists");
            if (grapple != null)
            {
                Check(GetRef(grapple, "_hookSprite") != null, "Hook head sprite assigned");
                Check(GetRef(grapple, "_origin") != null, "Hook muzzle transform assigned");
                Check(GetInt(grapple, "_hitMask") != 0, "Hook hit mask is configured");
                Check(GetFloat(grapple, "_maxRange") > 4f, "Hook range is usable");
                Check(GetFloat(grapple, "_pullAcceleration") > 5f, "Hook pull acceleration is usable");
                Check(GetFloat(grapple, "_releaseDistance") > 0f, "Hook auto-release distance configured");
                Check(GetRef(grapple, "_pullSound") != null, "Hook has the pull sound assigned");

                GrappleRopeRenderer rope = grapple.GetComponent<GrappleRopeRenderer>();
                Check(rope != null, "Hook has a rope renderer");
                if (rope != null) Check(GetRef(rope, "_material") != null, "Rope material assigned");
            }

            // ---- weapons
            PlayerWeapons weapons = Object.FindFirstObjectByType<PlayerWeapons>();
            Check(weapons != null, "Player weapons component exists");
            if (weapons != null)
            {
                Check(GetRef(weapons, "_handOrigin") != null, "Weapon hand origin assigned");
                Check(GetRef(weapons, "_handVisual") != null, "Weapon hand visual assigned");
                Check(GetInt(weapons, "_hitMask") != 0, "Weapon hit mask configured");
                Check(GetRef(weapons, "_startingMainHand") != null, "Starting main hand weapon assigned");
                Check(GetRef(weapons, "_startingOffHand") != null, "Starting off hand weapon assigned");
            }

            WeaponHandVisual handVisual = Object.FindFirstObjectByType<WeaponHandVisual>();
            Check(handVisual != null, "Weapon hand visual exists");
            if (handVisual != null)
            {
                Check(GetRef(handVisual, "_mainRenderer") != null, "Main hand renderer assigned");
                Check(GetRef(handVisual, "_offRenderer") != null, "Off hand renderer assigned");
            }

            WeaponInventoryUI inventory = Object.FindFirstObjectByType<WeaponInventoryUI>();
            Check(inventory != null, "Weapon inventory UI exists");
            if (inventory != null)
            {
                Check(GetRef(inventory, "_weapons") != null, "Inventory UI is wired to the player weapons");
                Check(GetRef(inventory, "_font") != null, "Inventory UI has a font");
            }

            WeaponPickup2D[] pickups = Object.FindObjectsByType<WeaponPickup2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(pickups.Length >= 3, $"Weapon pickups placed in the level ({pickups.Length})");
            int wiredPickups = 0;
            for (int i = 0; i < pickups.Length; i++)
            {
                if (GetRef(pickups[i], "_weapon") != null) wiredPickups++;
            }
            Check(pickups.Length > 0 && wiredPickups == pickups.Length, "Every weapon pickup has a weapon asset");

            // ---- level content
            Check(Count<EnemyController2D>() >= 4, $"Enemies present ({Count<EnemyController2D>()})");

            int enemiesWithSound = 0;
            EnemyController2D[] allEnemies = Object.FindObjectsByType<EnemyController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allEnemies.Length; i++)
            {
                if (GetRef(allEnemies[i], "_hurtSound") != null) enemiesWithSound++;
            }
            Check(allEnemies.Length > 0 && enemiesWithSound == allEnemies.Length,
                $"Every enemy plays the hit impact sound ({enemiesWithSound}/{allEnemies.Length})");

            Check(Object.FindFirstObjectByType<SfxPlayer>() != null, "Shared SfxPlayer exists for one-shot sounds");

            int coinsWithSound = 0;
            Collectible2D[] allCoins = Object.FindObjectsByType<Collectible2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allCoins.Length; i++)
            {
                if (GetRef(allCoins[i], "_pickupSound") != null) coinsWithSound++;
            }
            Check(allCoins.Length > 0 && coinsWithSound == allCoins.Length,
                $"Every coin plays the pickup sound ({coinsWithSound}/{allCoins.Length})");

            PlayerHealth playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
            Check(playerHealth != null && GetRef(playerHealth, "_hurtSound") != null,
                "The player plays the same hit impact sound as the monsters");

            Check(Count<Collectible2D>() >= 15, $"Coins present ({Count<Collectible2D>()})");
            Check(Count<Checkpoint2D>() >= 3, $"Checkpoints present ({Count<Checkpoint2D>()})");
            Check(Count<LevelGoal2D>() == 1, "Exactly one level goal");
            Check(Count<Hazard2D>() >= 3, $"Hazards present ({Count<Hazard2D>()})");
            Check(Count<MovingPlatform2D>() >= 1, "Moving platform present");
            Check(Count<PlatformEffector2D>() >= 3, $"One-way platforms present ({Count<PlatformEffector2D>()})");
            Check(Count<ParallaxLayer2D>() >= 4, $"Parallax layers present ({Count<ParallaxLayer2D>()})");

            int solidColliders = 0;
            foreach (Collider2D collider in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (collider.isTrigger) continue;
                if (collider.GetComponentInParent<PlayerController2D>() != null) continue;
                if (collider.GetComponentInParent<EnemyController2D>() != null) continue;
                if (collider.GetComponentInParent<Hazard2D>() != null) continue;
                if (collider.GetComponentInParent<MovingPlatform2D>() != null) continue;
                solidColliders++;
            }
            Check(solidColliders >= 15, $"Static level geometry present ({solidColliders} colliders)");

            // ---- sprites / rendering
            int sprites = 0;
            int brokenSprites = 0;
            foreach (SpriteRenderer renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sprites++;
                if (renderer.sprite == null) brokenSprites++;
                if (renderer.sharedMaterial == null) brokenSprites++;
            }
            Check(sprites >= 60, $"Sprite renderers present ({sprites})");
            Check(brokenSprites == 0, $"Every sprite renderer has a sprite and material (broken: {brokenSprites})");

            // ---- hud
            HUDController hud = Object.FindFirstObjectByType<HUDController>();
            Check(hud != null, "HUD exists");
            if (hud != null)
            {
                Check(GetRef(hud, "_healthFill") != null, "HUD health fill assigned");
                Check(GetRef(hud, "_healthText") != null, "HUD health text assigned");
                Check(GetRef(hud, "_scoreText") != null, "HUD score text assigned");
                Check(GetRef(hud, "_hintText") != null, "HUD control hints assigned");
                Check(GetRef(hud, "_grappleText") != null, "HUD grapple readout assigned");
                Check(GetRef(hud, "_weaponText") != null, "HUD weapon readout assigned");
                Check(GetRef(hud, "_pickupText") != null, "HUD pickup prompt assigned");
                Check(GetRef(hud, "_winPanel") != null, "HUD win panel assigned");
            }

            if (_errors == 0)
            {
                Debug.Log($"[TanLuZhe] Scene validation passed: {_checks} checks, 0 problems.");
            }
            else
            {
                Debug.LogError($"[TanLuZhe] Scene validation FAILED: {_errors} problem(s) out of {_checks} checks.");
            }
        }

        private static void CheckNoMissingScripts()
        {
            int missing = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Component[] components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] == null)
                    {
                        missing++;
                        Debug.LogError($"[TanLuZhe] Missing script on '{go.name}'");
                    }
                }
            }
            Check(missing == 0, "No missing scripts in the scene");
        }

        private static int Count<T>() where T : Component
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        private static Object GetRef(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[TanLuZhe] Validator: field '{field}' not found on {target.GetType().Name}");
                return null;
            }
            return property.objectReferenceValue;
        }

        private static int GetInt(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            return property != null ? property.intValue : 0;
        }

        private static float GetFloat(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            return property != null ? property.floatValue : 0f;
        }

        private static void Check(bool condition, string description)
        {
            _checks++;
            if (condition) return;
            _errors++;
            Debug.LogError($"[TanLuZhe] Scene check FAILED: {description}");
        }
    }
}
