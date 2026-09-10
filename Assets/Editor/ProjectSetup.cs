using UnityEditor;
using UnityEngine;

namespace TanLuZhe.EditorTools
{
    /// <summary>
    /// One click project configuration: physics layers, frictionless physics materials used by
    /// the character controller, and 2D physics settings that suit a platformer.
    /// </summary>
    public static class ProjectSetup
    {
        public const string PhysicsDir = "Assets/Settings/Physics/";
        public const string AudioDir = "Assets/Audio/";

        public const string HitImpactFileName = "660770__madpancake__hit-impact.ogg";
        public const string CoinFileName = "336936__the-sacha-rush__coin9.wav";
        public const string PullFileName =
            "810141__designerschoice__paprhndl-blue-snowball-microphone-cu_tissue-pull-out_nicholas-judy_tdc.wav";

        public static readonly string[] AudioFileNames = { HitImpactFileName, CoinFileName, PullFileName };

        [MenuItem("TanLuZhe/0. Setup Project (layers + physics)", false, 0)]
        public static void Run()
        {
            EnsureLayers();
            EnsurePhysicsMaterials();
            EnsurePhysicsSettings();
            ConfigureAudioImport();
            AssetDatabase.SaveAssets();
            Debug.Log("[TanLuZhe] Project setup complete: layers, physics materials, 2D physics settings, audio import.");
        }

        /// <summary>Impact clip played by every monster (and by the player) when it takes damage.</summary>
        public static AudioClip HitImpactSound => LoadAudio(HitImpactFileName);

        /// <summary>Pickup jingle played when the player collects a coin.</summary>
        public static AudioClip CoinSound => LoadAudio(CoinFileName);

        /// <summary>The yank played when the grapple hook bites and starts pulling the player in.</summary>
        public static AudioClip PullSound => LoadAudio(PullFileName);

        public static AudioClip LoadAudio(string fileName)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + fileName);
            if (clip == null)
                Debug.LogWarning($"[TanLuZhe] Audio clip not found: {AudioDir + fileName}");
            return clip;
        }

        /// <summary>
        /// Imports the game's short sound effects fully preloaded so the first trigger never hitches.
        /// </summary>
        public static void ConfigureAudioImport()
        {
            for (int i = 0; i < AudioFileNames.Length; i++)
            {
                string path = AudioDir + AudioFileNames[i];
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[TanLuZhe] Sound effect not found at {path}. " +
                                     "Drop the file into Assets/Audio/ and re-run setup.");
                    continue;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                settings.preloadAudioData = true;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = false;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
        }

        public static void EnsureLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[TanLuZhe] Could not open ProjectSettings/TagManager.asset");
                return;
            }

            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");
            if (layers == null)
            {
                Debug.LogError("[TanLuZhe] TagManager has no 'layers' property");
                return;
            }

            for (int i = 0; i < GameLayers.Names.Length; i++)
            {
                int index = GameLayers.Ground + i;
                if (index >= layers.arraySize) continue;

                SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                string wanted = GameLayers.Names[i];
                if (slot.stringValue == wanted) continue;

                if (!string.IsNullOrEmpty(slot.stringValue) && slot.stringValue != wanted)
                    Debug.LogWarning($"[TanLuZhe] Layer {index} was '{slot.stringValue}', overwriting with '{wanted}'.");

                slot.stringValue = wanted;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsurePhysicsMaterials()
        {
            EnsureFolder(PhysicsDir);

            CreateMaterial("PlayerFrictionless", 0f, 0f);
            CreateMaterial("EnemyFrictionless", 0f, 0f);
            CreateMaterial("WorldBounceless", 0f, 0f);
        }

        private static void CreateMaterial(string name, float friction, float bounciness)
        {
            string path = PhysicsDir + name + ".physicsMaterial2D";
            PhysicsMaterial2D existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (existing != null)
            {
                existing.friction = friction;
                existing.bounciness = bounciness;
                EditorUtility.SetDirty(existing);
                return;
            }

            PhysicsMaterial2D material = new PhysicsMaterial2D(name)
            {
                friction = friction,
                bounciness = bounciness,
            };
            AssetDatabase.CreateAsset(material, path);
        }

        public static void EnsurePhysicsSettings()
        {
            // A platformer wants gravity that is stable at high vertical speeds.
            Physics2D.gravity = new Vector2(0f, -9.81f);
            Physics2D.velocityIterations = 8;
            Physics2D.positionIterations = 3;
            Physics2D.queriesHitTriggers = false;

            GameLayers.ApplyCollisionRules();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path.TrimEnd('/'))) return;

            string[] parts = path.TrimEnd('/').Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
