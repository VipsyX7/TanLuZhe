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
    /// Builds the playable demo level from scratch: camera rig, 2D global light, parallax
    /// background, level geometry, one-way platforms, a moving platform, enemies, pickups,
    /// checkpoints, hazards, HUD and the fully wired player (controller + grapple).
    /// </summary>
    public static class DemoSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/PlatformerDemo.unity";
        public const string ArtDir = "Assets/Art/Generated/";
        private const string MaterialsDir = "Assets/Settings/Materials/";

        private const int OrderSky = -300;
        private const int OrderHillsFar = -280;
        private const int OrderHillsNear = -260;
        private const int OrderClouds = -240;
        private const int OrderSolid = 0;
        private const int OrderActor = 10;
        private const int OrderRope = 30;
        private const int OrderHook = 40;
        private const int OrderFx = 60;

        private static Material _spriteMaterial;
        private static PhysicsMaterial2D _playerMaterial;
        private static PhysicsMaterial2D _enemyMaterial;
        private static PhysicsMaterial2D _worldMaterial;
        private static Font _font;

        [MenuItem("TanLuZhe/2. Build Demo Scene", false, 2)]
        public static void Build()
        {
            BuildInternal(true);
        }

        /// <summary>Entry point used by <c>-executeMethod</c> in batch mode.</summary>
        public static void BuildFromCommandLine()
        {
            ProjectSetup.Run();
            BuildInternal(false);
        }

        public static void BuildInternal(bool interactive)
        {
            ProjectSetup.Run();
            ResolveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            PlayerParts player = BuildPlayer(new Vector2(3f, 1.6f));
            BuildLevel(player);
            BuildHud(player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TanLuZhe] Demo scene built at {ScenePath}");
        }

        // ==================================================================== assets
        private static void ResolveAssets()
        {
            _spriteMaterial = FindMaterial("Sprite-Lit-Default");
            if (_spriteMaterial == null)
            {
                Debug.LogWarning("[TanLuZhe] Sprite-Lit-Default not found, sprites will use their default unlit material.");
            }

            _playerMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(ProjectSetup.PhysicsDir + "PlayerFrictionless.physicsMaterial2D");
            _enemyMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(ProjectSetup.PhysicsDir + "EnemyFrictionless.physicsMaterial2D");
            _worldMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(ProjectSetup.PhysicsDir + "WorldBounceless.physicsMaterial2D");

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Microsoft YaHei", "Segoe UI" }, 24);
        }

        private static Material FindMaterial(string name)
        {
            string[] guids = AssetDatabase.FindAssets(name + " t:Material");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".mat")) continue;
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && m.name == name) return m;
            }
            return null;
        }

        private static Sprite S(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
            if (sprite == null)
            {
                Debug.LogError($"[TanLuZhe] Missing sprite '{name}'. Run 'TanLuZhe/1. Generate Art Assets' first.");
            }
            return sprite;
        }

        private static Material RopeMaterial()
        {
            ProjectSetup.EnsureFolder(MaterialsDir);
            string path = MaterialsDir + "RopeLine.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            Material material = new Material(shader) { name = "RopeLine" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void RegisterScene(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ==================================================================== helpers
        private static GameObject NewObject(string name, Vector2 position, int layer)
        {
            GameObject go = new GameObject(name);
            go.layer = layer;
            go.transform.position = new Vector3(position.x, position.y, 0f);
            return go;
        }

        private static SpriteRenderer AddSprite(GameObject go, string spriteName, Vector2 size, int order,
            SpriteDrawMode drawMode = SpriteDrawMode.Simple, Color? tint = null)
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(spriteName);
            sr.drawMode = drawMode;
            sr.sortingOrder = order;
            if (tint.HasValue) sr.color = tint.Value;
            if (drawMode != SpriteDrawMode.Simple) sr.size = size;
            if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
            return sr;
        }

        private static void SetField(Object target, string fieldName, object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[TanLuZhe] Serialized field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            Assign(property, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedProperty property, object value)
        {
            switch (value)
            {
                case float f: property.floatValue = f; break;
                case int i: property.intValue = i; break;
                case bool b: property.boolValue = b; break;
                case string s: property.stringValue = s; break;
                case Color c: property.colorValue = c; break;
                case Vector2 v2: property.vector2Value = v2; break;
                case Vector3 v3: property.vector3Value = v3; break;
                case System.Enum e: property.intValue = System.Convert.ToInt32(e); break;
                case Object o: property.objectReferenceValue = o; break;
                case null: property.objectReferenceValue = null; break;
                default:
                    Debug.LogError($"[TanLuZhe] Unsupported field value type {value.GetType().Name}");
                    break;
            }
        }

        private static GameObject Block(string name, Vector2 center, Vector2 size, string spriteName,
            int layer, PhysicsMaterial2D material, int order)
        {
            GameObject go = NewObject(name, center, layer);
            AddSprite(go, spriteName, size, order, SpriteDrawMode.Tiled);
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            if (material != null) collider.sharedMaterial = material;
            return go;
        }

        private static GameObject OneWayPlatform(string name, Vector2 center, Vector2 size, int layer)
        {
            GameObject go = NewObject(name, center, layer);
            AddSprite(go, "platform_tile", size, OrderSolid, SpriteDrawMode.Tiled);
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.usedByEffector = true;
            PlatformEffector2D effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 150f;
            effector.rotationalOffset = 0f;
            return go;
        }

        private static void SpikeStrip(string name, float x0, float x1, float topY)
        {
            GameObject root = NewObject(name, new Vector2((x0 + x1) * 0.5f, topY - 0.25f), GameLayers.Hazard);
            for (float x = x0; x < x1 - 0.01f; x += 1f)
            {
                GameObject spike = NewObject("Spike", new Vector2(x + 0.5f, topY - 0.25f), GameLayers.Hazard);
                spike.transform.SetParent(root.transform, true);
                AddSprite(spike, "spike", new Vector2(1f, 0.5f), OrderSolid + 1);
            }

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(x1 - x0, 0.5f);
            Hazard2D hazard = root.AddComponent<Hazard2D>();
            SetField(hazard, "_instantKill", true);
            SetField(hazard, "_damage", 999f);
        }

        private static GameObject Coin(Vector2 position)
        {
            GameObject go = NewObject("Coin", position, GameLayers.Collectible);
            AddSprite(go, "coin", new Vector2(0.5f, 0.5f), OrderActor + 1);
            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.24f;
            collider.isTrigger = true;
            go.AddComponent<Collectible2D>();
            return go;
        }

        // ==================================================================== environment
        private static void BuildEnvironment()
        {
            // ---- camera
            GameObject cameraGO = NewObject("Main Camera", new Vector2(3f, 3f), 0);
            cameraGO.transform.position = new Vector3(3f, 3f, -10f);
            cameraGO.tag = "MainCamera";
            Camera camera = cameraGO.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.045f, 0.13f, 1f);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 500f;
            cameraGO.AddComponent<AudioListener>();
            CameraRig2D rig = cameraGO.AddComponent<CameraRig2D>();
            SetField(rig, "_offset", new Vector2(0f, 1.4f));
            SetField(rig, "_deadZone", new Vector2(1.2f, 1.1f));
            SetField(rig, "_lookAhead", new Vector2(3.4f, 1.6f));
            SetField(rig, "_smoothTime", 0.16f);
            SetField(rig, "_useBounds", true);
            SetField(rig, "_minBounds", new Vector2(-14f, -10f));
            SetField(rig, "_maxBounds", new Vector2(118f, 30f));

            // ---- global 2D light
            GameObject lightGO = new GameObject("Global Light 2D");
            Light2D light2D = lightGO.AddComponent<Light2D>();
            SerializedObject lightSO = new SerializedObject(light2D);
            SerializedProperty typeProperty = lightSO.FindProperty("m_LightType");
            if (typeProperty != null)
            {
                typeProperty.intValue = 4; // Global
                SerializedProperty intensity = lightSO.FindProperty("m_Intensity");
                if (intensity != null) intensity.floatValue = 1f;
                SerializedProperty color = lightSO.FindProperty("m_Color");
                if (color != null) color.colorValue = new Color(1f, 0.98f, 0.95f, 1f);
                lightSO.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[TanLuZhe] Could not configure Light2D type; add a Global Light 2D manually if sprites render black.");
            }

            // ---- services
            GameObject services = new GameObject("Services");
            services.AddComponent<PhysicsBootstrap2D>();
            services.AddComponent<GameManager>();
            FxManager fx = services.AddComponent<FxManager>();
            SetField(fx, "_sparkSprite", S("spark"));
            SetField(fx, "_dustSprite", S("dust"));
            SetField(fx, "_slashSprite", S("slash"));
            SetField(fx, "_slashAngleOffset", 180f);
            SetField(fx, "_poolSize", 220);

            // ---- parallax background
            GameObject sky = NewObject("BG Sky", new Vector2(3f, 2f), 0);
            SpriteRenderer skyRenderer = AddSprite(sky, "bg_gradient", new Vector2(1f, 1f), OrderSky);
            sky.transform.localScale = new Vector3(170f, 46f, 1f);
            ParallaxLayer2D skyParallax = sky.AddComponent<ParallaxLayer2D>();
            SetField(skyParallax, "_horizontalFactor", 1f);
            SetField(skyParallax, "_verticalFactor", 1f);
            skyRenderer.color = new Color(1f, 1f, 1f, 0.9f);

            CreateHillRow("BG Hills Far", new Vector2(-24f, 3.5f), 12f, 10, new Vector2(3.2f, 1.6f),
                0.12f, 0.05f, OrderHillsFar, new Color(0.22f, 0.2f, 0.42f, 0.75f));
            CreateHillRow("BG Hills Near", new Vector2(-28f, 1.2f), 9f, 14, new Vector2(2.6f, 1.35f),
                0.28f, 0.1f, OrderHillsNear, new Color(0.16f, 0.15f, 0.32f, 0.9f));

            for (int i = 0; i < 14; i++)
            {
                float x = -6f + i * 9.5f;
                float y = 9f + ((i * 37) % 5) * 1.1f;
                GameObject cloud = NewObject("BG Cloud", new Vector2(x, y), 0);
                AddSprite(cloud, "bg_cloud", new Vector2(1f, 1f), OrderClouds);
                cloud.transform.localScale = new Vector3(4.5f + (i % 3), 2.6f + (i % 2) * 0.4f, 1f);
                ParallaxLayer2D p = cloud.AddComponent<ParallaxLayer2D>();
                SetField(p, "_horizontalFactor", 0.45f);
                SetField(p, "_verticalFactor", 0.12f);
            }
        }

        private static void CreateHillRow(string name, Vector2 start, float spacing, int count,
            Vector2 scale, float hFactor, float vFactor, int order, Color tint)
        {
            GameObject root = NewObject(name, start, 0);
            ParallaxLayer2D parallax = root.AddComponent<ParallaxLayer2D>();
            SetField(parallax, "_horizontalFactor", hFactor);
            SetField(parallax, "_verticalFactor", vFactor);

            for (int i = 0; i < count; i++)
            {
                GameObject hill = new GameObject("Hill");
                hill.transform.SetParent(root.transform, false);
                hill.transform.localPosition = new Vector3(i * spacing, 0f, 0f);
                hill.transform.localScale = new Vector3(scale.x, scale.y, 1f);
                SpriteRenderer sr = AddSprite(hill, "bg_hill", new Vector2(1f, 1f), order);
                sr.color = tint;
            }
        }

        // ==================================================================== player
        private struct PlayerParts
        {
            public GameObject Root;
            public PlayerController2D Controller;
            public PlayerHealth Health;
            public GrappleHook2D Grapple;
            public PlayerWeapons Weapons;
            public SpriteRenderer Visual;
        }

        private static PlayerParts BuildPlayer(Vector2 position)
        {
            PlayerParts parts = new PlayerParts();

            GameObject root = NewObject("Player", position, GameLayers.Player);
            root.tag = "Player";
            parts.Root = root;

            Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.mass = 1f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.72f, 1.5f);
            capsule.offset = new Vector2(0f, 0.05f);
            capsule.sharedMaterial = _playerMaterial;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            SpriteRenderer sr = AddSprite(visual, "player", new Vector2(1f, 1f), OrderActor);
            parts.Visual = sr;

            root.AddComponent<PlayerInputSource>();

            PlayerController2D controller = root.AddComponent<PlayerController2D>();
            parts.Controller = controller;
            SetField(controller, "_groundMask", GameLayers.SolidMask);

            PlayerHealth health = root.AddComponent<PlayerHealth>();
            parts.Health = health;
            SetField(health, "_visual", sr);
            SetField(health, "_voidY", -40f);

            // ---- grapple
            GameObject grappleGO = new GameObject("Grapple");
            grappleGO.transform.SetParent(root.transform, false);
            grappleGO.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            GrappleHook2D grapple = grappleGO.AddComponent<GrappleHook2D>();
            parts.Grapple = grapple;

            GrappleRopeRenderer rope = grappleGO.AddComponent<GrappleRopeRenderer>();
            SetField(rope, "_material", RopeMaterial());
            SetField(rope, "_segments", 16);
            SetField(rope, "_width", 0.085f);

            SetField(grapple, "_hookSprite", S("hook_head"));
            SetField(grapple, "_sparkSprite", S("spark"));
            SetField(grapple, "_hitMask", GameLayers.GrappleMask);
            SetField(grapple, "_origin", grappleGO.transform);
            SetField(grapple, "_maxRange", 16f);
            SetField(grapple, "_hookSpeed", 48f);
            SetField(grapple, "_retractSpeed", 70f);
            SetField(grapple, "_pullAcceleration", 140f);
            SetField(grapple, "_maxPullSpeed", 30f);
            SetField(grapple, "_releaseDistance", 1f);
            SetField(grapple, "_contactReleaseDistance", 0.08f);
            SetField(grapple, "_ropeGrip", 1f);
            SetField(grapple, "_positionCorrection", 9f);
            SetField(grapple, "_attachDamage", 16f);
            SetField(grapple, "_drawDebug", false);

            // ---- weapons
            GameObject handPivot = new GameObject("HandPivot");
            handPivot.transform.SetParent(root.transform, false);
            handPivot.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            GameObject mainHand = new GameObject("Main Hand");
            mainHand.transform.SetParent(root.transform, false);
            SpriteRenderer mainRenderer = AddSprite(mainHand, "white", new Vector2(1f, 1f), OrderActor + 2);

            GameObject offHand = new GameObject("Off Hand");
            offHand.transform.SetParent(root.transform, false);
            SpriteRenderer offRenderer = AddSprite(offHand, "white", new Vector2(1f, 1f), OrderActor + 1);

            WeaponHandVisual handVisual = root.AddComponent<WeaponHandVisual>();
            SetField(handVisual, "_mainRenderer", mainRenderer);
            SetField(handVisual, "_offRenderer", offRenderer);
            SetField(handVisual, "_sortingOrder", OrderActor + 1);
            SetField(handVisual, "_swingArc", 130f);
            SetField(handVisual, "_swingTime", 0.16f);

            PlayerWeapons weapons = root.AddComponent<PlayerWeapons>();
            parts.Weapons = weapons;
            SetField(weapons, "_handOrigin", handPivot.transform);
            SetField(weapons, "_handVisual", handVisual);
            SetField(weapons, "_hitMask", 1 << GameLayers.Enemy);
            SetField(weapons, "_backpackCapacity", 8);
            SetField(weapons, "_startingMainHand", WeaponLibraryGenerator.Load("Sword"));
            SetField(weapons, "_startingOffHand", WeaponLibraryGenerator.Load("Bow"));

            return parts;
        }

        // ==================================================================== level
        private static void BuildLevel(PlayerParts player)
        {
            GameObject level = new GameObject("Level");

            // ---------------------------------------------------------- ground
            Block("Ground A", new Vector2(9f, -1.5f), new Vector2(18f, 3f), "ground_tile", GameLayers.Ground, _worldMaterial, OrderSolid).transform.SetParent(level.transform, true);
            Block("Ground B", new Vector2(29f, -1.5f), new Vector2(14f, 3f), "ground_tile", GameLayers.Ground, _worldMaterial, OrderSolid).transform.SetParent(level.transform, true);
            Block("Ground C", new Vector2(41.5f, -1.5f), new Vector2(5f, 3f), "ground_tile", GameLayers.Ground, _worldMaterial, OrderSolid).transform.SetParent(level.transform, true);
            Block("Ground D", new Vector2(70f, -1.5f), new Vector2(24f, 3f), "ground_tile", GameLayers.Ground, _worldMaterial, OrderSolid).transform.SetParent(level.transform, true);
            Block("Ground E", new Vector2(99f, -1.5f), new Vector2(18f, 3f), "ground_tile", GameLayers.Ground, _worldMaterial, OrderSolid).transform.SetParent(level.transform, true);

            // ---------------------------------------------------------- grapple wall
            Block("Grapple Wall", new Vector2(37.5f, 3f), new Vector2(3f, 6f), "ground_tile", GameLayers.Ground, _worldMaterial, OrderSolid).transform.SetParent(level.transform, true);

            // ---------------------------------------------------------- floating platforms
            Vector3[] platforms =
            {
                new Vector3(20f, 3.2f, 3f),      // x, y, width
                new Vector3(41.5f, 6.6f, 4f),
                new Vector3(53f, 3.5f, 3f),
                new Vector3(63f, 4.6f, 3.5f),
                new Vector3(74f, 6.6f, 4f),
                new Vector3(78f, 9f, 3f),
                new Vector3(94f, 4f, 3.5f),
                new Vector3(99f, 6.6f, 3.5f),
            };
            for (int i = 0; i < platforms.Length; i++)
            {
                Vector3 p = platforms[i];
                GameObject go = Block($"Platform {i}", new Vector2(p.x, p.y), new Vector2(p.z, 0.6f),
                    "platform_tile", GameLayers.Ground, _worldMaterial, OrderSolid);
                go.transform.SetParent(level.transform, true);
            }

            // The chasm pillar is the intended grapple target: latch onto its face, winch up,
            // then jump onto its top and hop the rest of the gap.
            GameObject anchorBlock = Block("Chasm Pillar", new Vector2(50f, 3.5f), new Vector2(3f, 7f),
                "ground_tile", GameLayers.Ground, _worldMaterial, OrderSolid - 1);
            anchorBlock.transform.SetParent(level.transform, true);

            // ---------------------------------------------------------- one way platforms
            OneWayPlatform("OneWay 1", new Vector2(25f, 3.6f), new Vector2(4f, 0.4f), GameLayers.OneWay).transform.SetParent(level.transform, true);
            OneWayPlatform("OneWay 2", new Vector2(30f, 6.2f), new Vector2(4f, 0.4f), GameLayers.OneWay).transform.SetParent(level.transform, true);
            OneWayPlatform("OneWay 3", new Vector2(70f, 3.2f), new Vector2(4f, 0.4f), GameLayers.OneWay).transform.SetParent(level.transform, true);

            // ---------------------------------------------------------- moving platform
            GameObject mover = NewObject("Moving Platform", new Vector2(83f, 2.4f), GameLayers.OneWay);
            mover.transform.SetParent(level.transform, true);
            AddSprite(mover, "platform_tile", new Vector2(3f, 0.5f), OrderSolid, SpriteDrawMode.Tiled);
            Rigidbody2D moverBody = mover.AddComponent<Rigidbody2D>();
            moverBody.bodyType = RigidbodyType2D.Kinematic;
            BoxCollider2D moverCollider = mover.AddComponent<BoxCollider2D>();
            moverCollider.size = new Vector2(3f, 0.5f);
            moverCollider.sharedMaterial = _worldMaterial;
            MovingPlatform2D platform = mover.AddComponent<MovingPlatform2D>();
            SetField(platform, "_travel", new Vector2(5f, 0f));
            SetField(platform, "_speed", 2.4f);

            // ---------------------------------------------------------- hazards
            SpikeStrip("Spikes Pit A", 18f, 22f, -3.4f);
            SpikeStrip("Spikes Chasm", 44f, 58f, -5.5f);
            SpikeStrip("Spikes Pit B", 82f, 90f, -3.4f);

            // ---------------------------------------------------------- enemies
            CreateEnemy(new Vector2(12f, 0.8f), 1.6f, 1);
            CreateEnemy(new Vector2(28f, 0.8f), 1.6f, 1);
            CreateEnemy(new Vector2(68f, 0.9f), 3.6f, 1);   // heavy: the hook pulls you more than it
            CreateEnemy(new Vector2(76f, 7.6f), 1.6f, 1);
            CreateEnemy(new Vector2(96f, 0.8f), 1.6f, 1);

            // ---------------------------------------------------------- coins
            Vector2[] coins =
            {
                new Vector2(6f, 1.2f), new Vector2(8f, 1.2f), new Vector2(10f, 1.2f),
                new Vector2(19f, 4.6f),
                new Vector2(25f, 4.7f), new Vector2(26.5f, 4.7f),
                new Vector2(31f, 7.3f),
                new Vector2(35.2f, 2.6f), new Vector2(35.2f, 4.8f),
                new Vector2(41.5f, 7.8f),
                new Vector2(47f, 6f), new Vector2(50f, 8.2f), new Vector2(53f, 4.8f), new Vector2(56f, 2.6f),
                new Vector2(63f, 5.8f),
                new Vector2(67f, 1.2f), new Vector2(70f, 4.3f),
                new Vector2(74f, 7.8f), new Vector2(78f, 10.2f),
                new Vector2(84f, 4.2f), new Vector2(86f, 4.2f),
                new Vector2(94f, 5.2f), new Vector2(99f, 7.8f),
                new Vector2(106f, 2f),
            };
            for (int i = 0; i < coins.Length; i++) Coin(coins[i]);

            // ---------------------------------------------------------- checkpoints
            CreateCheckpoint(new Vector2(40.5f, 1f));
            CreateCheckpoint(new Vector2(60f, 1f));
            CreateCheckpoint(new Vector2(92f, 1f));

            // ---------------------------------------------------------- weapon pickups
            CreateWeaponPickup(new Vector2(14f, 1.2f), "Spear");
            CreateWeaponPickup(new Vector2(33f, 1.2f), "Hammer");
            CreateWeaponPickup(new Vector2(66f, 1.2f), "Wand");
            CreateWeaponPickup(new Vector2(96f, 1.2f), "Sword");

            // ---------------------------------------------------------- goal
            GameObject goal = NewObject("Level Goal", new Vector2(104f, 2.2f), 0);
            AddSprite(goal, "goal", new Vector2(1f, 1f), OrderActor);
            goal.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            CircleCollider2D goalCollider = goal.AddComponent<CircleCollider2D>();
            goalCollider.radius = 0.75f;
            goalCollider.isTrigger = true;
            goal.AddComponent<LevelGoal2D>();

            // ---------------------------------------------------------- wire the camera
            CameraRig2D rig = Object.FindFirstObjectByType<CameraRig2D>();
            if (rig != null) SetField(rig, "_target", player.Root.transform);
        }

        private static void CreateEnemy(Vector2 position, float mass, int facing)
        {
            GameObject go = NewObject("Enemy", position, GameLayers.Enemy);

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.mass = mass;
            rb.gravityScale = 1f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.78f, 0.64f);
            collider.sharedMaterial = _enemyMaterial;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            SpriteRenderer sr = AddSprite(visual, "enemy", new Vector2(1f, 1f), OrderActor);
            sr.transform.localScale = new Vector3(Mathf.Sign(facing), 1f, 1f);

            EnemyController2D enemy = go.AddComponent<EnemyController2D>();
            SetField(enemy, "_visual", sr);
            SetField(enemy, "_idleSprite", S("enemy"));
            SetField(enemy, "_hurtSprite", S("enemy_hurt"));
            SetField(enemy, "_groundMask", GameLayers.EnemyGroundMask);
            SetField(enemy, "_maxHealth", mass > 3f ? 90f : 45f);
            SetField(enemy, "_patrolSpeed", mass > 3f ? 2.2f : 3f);
            SetField(enemy, "_chaseSpeed", mass > 3f ? 3.4f : 4.8f);
            SetField(enemy, "_hurtSound", ProjectSetup.HitImpactSound);
            SetField(enemy, "_hurtVolume", 0.85f);
            SetField(enemy, "_hurtPitchJitter", 0.12f);
            SetField(enemy, "_hurtSpatialBlend", 0.25f);
        }

        private static void CreateCheckpoint(Vector2 position)
        {
            GameObject go = NewObject("Checkpoint", position, 0);
            SpriteRenderer sr = AddSprite(go, "checkpoint", new Vector2(1f, 1f), OrderActor - 1);
            sr.color = new Color(0.55f, 0.6f, 0.7f, 1f);
            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.6f, 2.4f);
            collider.isTrigger = true;
            Checkpoint2D checkpoint = go.AddComponent<Checkpoint2D>();
            SetField(checkpoint, "_flagRenderer", sr);
            SetField(checkpoint, "_respawnOffset", new Vector2(0f, 1.2f));
        }

        private static void CreateWeaponPickup(Vector2 position, string weaponName)
        {
            WeaponDefinition weapon = WeaponLibraryGenerator.Load(weaponName);
            if (weapon == null)
            {
                Debug.LogWarning($"[TanLuZhe] Weapon '{weaponName}' not found, skipping pickup.");
                return;
            }

            GameObject go = NewObject($"Weapon Pickup ({weaponName})", position, GameLayers.Collectible);
            SpriteRenderer sr = AddSprite(go, "white", new Vector2(1f, 1f), OrderActor + 3);
            sr.sprite = weapon.icon != null ? weapon.icon : weapon.worldSprite;
            sr.color = weapon.tint;

            CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.85f;
            collider.isTrigger = true;

            WeaponPickup2D pickup = go.AddComponent<WeaponPickup2D>();
            SetField(pickup, "_weapon", weapon);
            SetField(pickup, "_renderer", sr);
        }

        // ==================================================================== hud
        private static void BuildHud(PlayerParts player)
        {
            GameObject canvasGO = new GameObject("HUD Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            HUDController hud = canvasGO.AddComponent<HUDController>();

            // health bar
            Image barBack = MakePanel(canvasGO.transform, "Health Bar BG", new Vector2(40f, -40f),
                new Vector2(360f, 34f), new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, 1f));
            Image barFill = MakePanel(barBack.transform, "Health Fill", Vector2.zero,
                new Vector2(-8f, -8f), new Color(0.4f, 1f, 0.6f, 1f), new Vector2(0f, 1f));
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = 1f;
            RectTransform fillRect = barFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);

            Text healthText = MakeText(canvasGO.transform, "Health Text", "100 / 100",
                new Vector2(414f, -40f), new Vector2(220f, 34f), TextAnchor.MiddleLeft, 26,
                Color.white, new Vector2(0f, 1f));

            Text scoreText = MakeText(canvasGO.transform, "Score Text", "COINS 00    DEATHS 00    CHECKPOINTS 0",
                new Vector2(40f, -84f), new Vector2(700f, 34f), TextAnchor.MiddleLeft, 24,
                new Color(0.85f, 0.9f, 1f), new Vector2(0f, 1f));

            Text grappleText = MakeText(canvasGO.transform, "Grapple Text", "HOOK READY",
                new Vector2(40f, -124f), new Vector2(760f, 34f), TextAnchor.MiddleLeft, 24,
                new Color(0.55f, 1f, 0.9f), new Vector2(0f, 1f));

            Text hintText = MakeText(canvasGO.transform, "Hint Text", string.Empty,
                new Vector2(40f, 44f), new Vector2(900f, 150f), TextAnchor.LowerLeft, 22,
                new Color(0.8f, 0.85f, 1f, 0.9f), new Vector2(0f, 0f));

            Text weaponText = MakeText(canvasGO.transform, "Weapon Text", "MAIN [LMB]  Sword      OFF [RMB]  Bow",
                new Vector2(40f, -164f), new Vector2(1000f, 34f), TextAnchor.MiddleLeft, 24,
                new Color(1f, 0.92f, 0.7f), new Vector2(0f, 1f));

            Text pickupText = MakeText(canvasGO.transform, "Pickup Prompt", string.Empty,
                new Vector2(0f, -150f), new Vector2(900f, 44f), TextAnchor.MiddleCenter, 30,
                new Color(1f, 1f, 0.6f), new Vector2(0.5f, 0f));
            RectTransform pickupRect = pickupText.rectTransform;
            pickupRect.anchorMin = new Vector2(0.5f, 0.5f);
            pickupRect.anchorMax = new Vector2(0.5f, 0.5f);
            pickupRect.pivot = new Vector2(0.5f, 0.5f);
            pickupRect.anchoredPosition = new Vector2(0f, -220f);

            // win banner
            GameObject winPanel = new GameObject("Win Panel");
            winPanel.transform.SetParent(canvasGO.transform, false);
            RectTransform winRect = winPanel.AddComponent<RectTransform>();
            winRect.anchorMin = Vector2.zero;
            winRect.anchorMax = Vector2.one;
            winRect.offsetMin = Vector2.zero;
            winRect.offsetMax = Vector2.zero;
            Image winBack = winPanel.AddComponent<Image>();
            winBack.color = new Color(0.03f, 0.02f, 0.08f, 0.78f);
            Text winText = MakeText(winPanel.transform, "Win Text", "LEVEL CLEAR!",
                new Vector2(0f, 0f), new Vector2(1200f, 400f), TextAnchor.MiddleCenter, 54,
                new Color(0.7f, 1f, 0.9f), new Vector2(0.5f, 0.5f));
            RectTransform winTextRect = winText.rectTransform;
            winTextRect.anchoredPosition = Vector2.zero;
            winPanel.SetActive(false);

            // backpack UI (builds its own panel at runtime)
            WeaponInventoryUI inventory = canvasGO.AddComponent<WeaponInventoryUI>();
            SetField(inventory, "_weapons", player.Weapons);
            SetField(inventory, "_panelSprite", S("white"));
            SetField(inventory, "_slotSprite", S("white"));
            SetField(inventory, "_font", _font);

            SetField(hud, "_healthFill", barFill);
            SetField(hud, "_healthText", healthText);
            SetField(hud, "_scoreText", scoreText);
            SetField(hud, "_grappleText", grappleText);
            SetField(hud, "_hintText", hintText);
            SetField(hud, "_weaponText", weaponText);
            SetField(hud, "_pickupText", pickupText);
            SetField(hud, "_winPanel", winPanel);
            SetField(hud, "_winText", winText);
        }

        private static Image MakePanel(Transform parent, string name, Vector2 anchoredPosition,
            Vector2 size, Color color, Vector2 anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.sprite = S("white");
            image.color = color;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return image;
        }

        private static Text MakeText(Transform parent, string name, string content, Vector2 anchoredPosition,
            Vector2 size, TextAnchor alignment, int fontSize, Color color, Vector2 anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = _font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }
    }
}
